# Auditoria e plano de evolução do backend

> Projeto auditado: `5644aea` (23/08/2026)  
> Escopo: `IdealCreative.Api` — API ASP.NET Core 10, PostgreSQL, MinIO/R2, PayPal e Mercado Pago.

## Resumo executivo

O backend já atende bem a um **MVP funcional em VPS**: não depende do Firebase, usa PostgreSQL, possui autenticação JWT com papéis, catálogo, carrinho, cupons, pedidos, comentários moderáveis, armazenamento S3 compatível com MinIO/R2, links temporários para arquivos digitais e uma rotina de expiração de reservas de estoque.

O padrão atual é um **monólito MVC em camadas leves**, com `Controllers -> EF Core DbContext -> PostgreSQL`. Há alguns serviços pontuais (`TokenService` e `OrderReservationCleanupService`), mas as regras principais de negócio ainda vivem nos controllers. Isso é adequado para começar, porém não deve continuar crescendo dessa forma: `CommerceController` já concentra carrinho, cupom, pedido, estoque, frete, compra manual, administração e indicadores.

Não é recomendável inserir uma arquitetura excessiva (por exemplo, repositório genérico sobre EF Core). O caminho recomendado é evoluir para um **monólito modular por funcionalidade**, com controllers finos e casos de uso explícitos. O `DbContext` já exerce, na prática, os papéis de Unit of Work e Repository; duplicá-los apenas aumentaria o código sem ganho real.

## O que existe hoje

```text
Blazor WebAssembly
        |
        v  JWT + REST
ASP.NET Core API
  Controllers (rotas, parte das regras e respostas)
        |
        +--> EF Core / AppDbContext --> PostgreSQL
        +--> ASP.NET Identity --> usuários e papéis
        +--> MinIO client --> MinIO local / Cloudflare R2
        +--> HttpClient --> PayPal / Mercado Pago
        +--> BackgroundService --> liberação de estoque expirado
```

### Padrão arquitetural atual

| Aspecto | Situação atual | Avaliação |
|---|---|---|
| Entrada HTTP | ASP.NET Core MVC com controllers REST | Adequado |
| Persistência | EF Core + Npgsql, acesso direto ao `AppDbContext` | Adequado em módulos pequenos |
| Identidade | ASP.NET Identity + JWT + papéis `Admin` e `Customer` | Boa base |
| Regras de negócio | Controllers, especialmente `CommerceController` e `PaymentsController` | Precisa separar |
| Integrações externas | Uso direto de `IMinioClient` e `IHttpClientFactory` nos controllers | Precisa de adaptadores |
| Trabalho assíncrono | Um `BackgroundService` periódico | Bom começo, mas limitado |
| Banco | Entidades EF, alguns dados serializados em JSON | Funciona, precisa evoluir |
| Erros | `AddProblemDetails` e `UseExceptionHandler` | Base correta, falta padronização |

Em outras palavras: é um **MVC com Transaction Script**. Cada endpoint executa uma sequência de consultas e alterações necessárias para concluir uma operação. Esse estilo é simples e rápido para o MVP, mas perde legibilidade e segurança quando a operação passa a ter muitos efeitos colaterais.

## Pontos positivos que devem ser preservados

- PostgreSQL substitui totalmente Firestore/Realtime Database para este sistema e é a escolha correta na VPS ARM64.
- Preço, cupom e estoque são recalculados no servidor na criação do pedido; o navegador não decide o total final.
- A baixa de estoque usa `ExecuteUpdateAsync` com condição de saldo, o que reduz o risco de vender acima do estoque em requisições concorrentes.
- Há transações nas operações de pedido, cancelamento e compra manual.
- O estado do pedido possui uma máquina de transição básica: pendente, aguardando pagamento, pago, produção, enviado, concluído e cancelado.
- Produtos digitais só geram download após uma compra confirmada; o link expira em 15 minutos.
- Avaliações exigem compra paga e começam ocultas para moderação.
- A API restringe as rotas administrativas por papel e limita tamanho de upload.
- A composição de produção já prevê API não exposta publicamente, backup diário PostgreSQL e R2 como armazenamento S3.

## Achados e melhorias necessárias

As prioridades abaixo indicam impacto, não significam que o sistema esteja inutilizável hoje.

### P0 — fazer antes de uma operação comercial real

| Área | Problema observado | Risco | Como corrigir |
|---|---|---|---|
| Migrações | `DatabaseBootstrap` usa `EnsureCreatedAsync` e uma grande sequência de SQL condicional. | Atualização e rollback de banco não são rastreáveis nem confiáveis em produção. | Criar migrations EF Core versionadas, remover `EnsureCreatedAsync` e aplicar `Database.MigrateAsync()` no processo de deploy, com backup antes da migration. |
| Webhooks de pagamento | A assinatura atual verifica somente um cabeçalho próprio (`x-idealcreative-webhook-secret`); os comentários do próprio código indicam que a verificação oficial ainda não foi implementada. | Um endpoint exposto pode aceitar confirmação indevida se o segredo for vazado ou a integração estiver configurada incorretamente. | Implementar os verificadores oficiais: PayPal `verify-webhook-signature` com headers oficiais; Mercado Pago com `x-signature`/manifest e consulta da transação na API do provedor. Nunca confiar apenas no payload recebido. |
| Cupom concorrente | A disponibilidade do cupom é checada antes do pagamento; a contagem de uso é incrementada posteriormente sem condição atômica de limite. | Dois pagamentos simultâneos podem ultrapassar `MaxUsesGlobal` ou o limite por cliente. | No momento de confirmar o pagamento, executar update condicional/transacional do cupom (`CurrentUsesGlobal < MaxUsesGlobal`) e registrar o uso por pedido/usuário em tabela própria. |
| Testes | Não há projeto de testes nem pipeline de CI identificado. | Regras de estoque, pagamento e download podem regredir sem aviso. | Criar testes unitários e de integração em Docker; bloquear merge/deploy quando eles falharem. |
| Segredos e backup | O compose de desenvolvimento traz credenciais de exemplo no repositório, e o backup de produção permanece apenas em volume local por 14 dias. | Vazamento acidental ou perda total da VPS. | Usar `.env` fora do Git ou secret manager; copiar backup criptografado e versionado para um bucket R2 distinto; testar restauração mensalmente. |

### P1 — implementar na sequência

| Área | Problema observado | Como deve ficar |
|---|---|---|
| Pedidos e carrinhos | `ItemsJson` é uma lista JSON dentro de `Orders` e `Carts`. | Criar `OrderItem` normalizado para preservar produto, nome, preço, quantidade, arquivo digital e frete como *snapshot*. Manter JSON apenas onde houver motivo real. |
| Tags | `TagsJson` em `Products` exige `ILIKE` e reescrita de texto ao editar uma tag. | Criar `ProductTags(ProductId, TagId)` com índice e chave única. Filtros e renomeações tornam-se consistentes e rápidos. |
| Dinheiro | O banco usa centavos (`long`), mas carrinho/frete e DTOs usam `double`. | Usar somente `long` em centavos internamente ou `decimal` nos limites HTTP. Não calcular valores financeiros com `double`. |
| Concurrency | Produtos e pedidos não têm token de concorrência. Há operações atômicas de estoque, mas alterações simultâneas de cadastro/status podem se sobrescrever. | Adicionar `xmin` do PostgreSQL ou coluna de versão como token de concorrência; devolver `409 Conflict` quando houver edição desatualizada. |
| Pagamentos | `PaymentsController` cria clientes HTTP sem políticas explícitas e centraliza PayPal/Mercado Pago. | Criar `IPaymentGateway`, `PayPalGateway` e `MercadoPagoGateway`, com clientes nomeados, timeout, retry limitado para leitura e idempotency key nas chamadas que criam cobrança. |
| Upload | Validação de imagem depende de `ContentType` enviado pelo cliente; o download/imagem é carregado inteiro em memória antes da resposta. | Validar assinatura/binário e extensão permitida, sanitizar metadata, fazer streaming de leitura e enviar arquivos grandes por URL pré-assinada com política de tamanho/tipo. Avaliar antivírus assíncrono para arquivos digitais. |
| Downloads | O token de download usa a mesma chave JWT e o endpoint público é um proxy manual. | Usar uma chave exclusiva `Storage:DownloadSigningKey`, incluir propósito/audiência no token e preferir URL pré-assinada R2 quando possível. |
| Proteção da API | Não há rate limit, cabeçalhos de segurança, tratamento claro de origem encaminhada ou política de timeout global. | Configurar rate limiter para login, cadastro, avaliações, busca e webhooks; `UseForwardedHeaders`, HTTPS no proxy, cabeçalhos de segurança no proxy e limites de requisição globais. |
| Observabilidade | Há health checks básicos para banco, mas não há métricas, rastreamento ou log estruturado de operação. | Adicionar logs estruturados com `OrderId`, `PaymentId` e `UserId`, OpenTelemetry, health checks de banco/R2 e alertas mínimos. |
| Dados pessoais | Perfil armazena endereço e documento em texto puro. | Minimizar dados, restringir consultas de admin, aplicar retenção e criptografar backups. Se CPF/documento não for indispensável, não coletar. |

### P2 — melhorias de escala e manutenção

- Paginar avaliações e downloads corretamente; hoje há trechos que carregam pedidos/avaliações para filtrar em memória e consultas N+1 de usuários nas avaliações.
- Criar índices para consultas reais: produto publicado/data/preço, pedidos por usuário/status, categorias/tags normalizadas e transações por pedido.
- Mover a fila futura de impressão Arduino/máquina para uma tabela `OutboxMessages` e worker dedicado. O pedido aprovado grava a mensagem na mesma transação; um worker reprocessa falhas sem duplicar impressão.
- Adicionar e-mail transacional por fila: confirmação de pedido, pagamento aprovado e entrega de arquivo digital.
- Separar relatórios do fluxo transacional se o volume crescer; inicialmente as consultas SQL com projeção são suficientes.
- Definir retenção de `RawPayload` de pagamentos, pois pode conter informações sensíveis do provedor.
- Remover ou limitar o endpoint público que incrementa visualizações; ele pode ser inflado artificialmente. Registrar visualização no servidor com rate limit ou tratar como métrica aproximada.

## Design recomendado: monólito modular por funcionalidade

Não é necessário dividir o projeto em microserviços. Uma única API e um único banco são mais simples para a VPS, para backup e para deploy. A separação recomendada é interna:

```text
IdealCreative.Api/
  Features/
    Catalog/
      CatalogController.cs
      CatalogService.cs
      Contracts/
      Validators/
    Checkout/
      CheckoutController.cs
      CreateOrderService.cs
      OrderStatusService.cs
      Contracts/
    Payments/
      PaymentsController.cs
      IPaymentGateway.cs
      PayPalGateway.cs
      MercadoPagoGateway.cs
      WebhookProcessor.cs
    Storage/
      StorageController.cs
      IObjectStorage.cs
      S3ObjectStorage.cs
    Identity/
    Reviews/
  Data/
    AppDbContext.cs
    Migrations/
  Infrastructure/
    Observability/
    BackgroundJobs/
```

### Regras de responsabilidade

| Camada | Faz | Não faz |
|---|---|---|
| Controller | Autentica/autoriza, lê request, chama um caso de uso e devolve HTTP. | Não calcula pedido, não acessa múltiplas tabelas diretamente, não chama PayPal/R2. |
| Caso de uso/Service | Implementa uma ação de negócio completa: criar pedido, confirmar pagamento, criar produto. | Não conhece detalhes de HTML, Blazor ou HTTP externo. |
| Entidades/Modelos | Representam estado e invariantes simples. | Não formatam resposta da API. |
| EF Core | Persiste e consulta dados. | Não define a regra de checkout. |
| Gateway/Adapter | Traduz a API externa para uma interface interna. | Não decide se um pedido pode ser pago. |
| Worker | Consome tarefas persistidas e reprocessa falhas. | Não depende de uma requisição web aberta. |

### O que não adicionar

- Não criar `GenericRepository<T>`: o EF Core já fornece consultas, tracking e transações. Um wrapper genérico esconderia recursos úteis e aumentaria manutenção.
- Não criar microserviços para catálogo, pagamento e impressão nesta fase. Eles introduziriam fila, autenticação interserviço, observabilidade distribuída e mais pontos de falha.
- Não usar CQRS/MediatR somente por moda. Casos de uso organizados por feature já resolvem o problema atual; adotar um mediator depois só se houver muitos comandos e handlers independentes.

## Modelo de dados alvo

O objetivo é preservar o histórico de uma venda mesmo que o produto seja editado ou excluído.

```text
Product ----< ProductTag >---- Tag
   |
   +---- Category

Order ----< OrderItem ---- Product (referência opcional)
  |
  +----< PaymentTransaction
  +----< CouponRedemption >---- Coupon
  +----< OutboxMessage

Cart ----< CartItem ---- Product
```

`OrderItem` deve conter um snapshot imutável: `ProductId` opcional, título, imagem, preço em centavos, quantidade, se é digital, caminho do arquivo no momento da venda e valores de frete. Assim, apagar ou editar um produto não altera o pedido já concluído.

## Fluxo correto de checkout e pagamento

1. O cliente altera o carrinho; a API guarda somente produto e quantidade, sem confiar em preço enviado pelo navegador.
2. `CreateOrderService` lê produtos, verifica publicação/estoque, calcula preço, frete e cupom no servidor.
3. Em uma transação, reserva estoque e cria `Order`/`OrderItem` em `Pending`.
4. `IPaymentGateway.CreateAsync` cria a intenção externa com chave de idempotência. O pedido vira `AwaitingPayment`.
5. O webhook oficial é validado, consulta o provedor quando necessário e persiste o evento bruto com chave única do evento externo.
6. Em uma transação idempotente, o sistema confirma o pagamento, aplica o uso do cupom com limite atômico, contabiliza vendas e grava `OutboxMessage` para e-mail/impressão.
7. O worker lê a outbox, envia e-mail/ordem de impressão e marca a mensagem como concluída. Falhas têm tentativa, intervalo e registro de erro.
8. Se o pagamento expirar, o worker cancela o pedido uma única vez e devolve o estoque.

## Plano de implementação seguro

### Fase 1 — confiabilidade essencial

1. Criar projeto `IdealCreative.Api.Tests` e testes de integração com PostgreSQL/MinIO em Docker.
2. Introduzir migrations EF Core e transformar o bootstrap atual em seed idempotente de papéis/admin, sem DDL manual.
3. Criar `OrderItem`, migrar dados existentes de `ItemsJson` e manter leitura retrocompatível somente durante a transição.
4. Extrair `CreateOrderService`, `OrderStatusService` e `CouponService` do `CommerceController`.
5. Tornar aplicação de cupom e confirmação de pagamento atômicas.

### Fase 2 — integrações de produção

1. Criar gateways de PayPal e Mercado Pago e implementar assinatura oficial de webhook.
2. Adicionar idempotência em criação/captura de pagamento e em eventos recebidos.
3. Criar `IObjectStorage` e concentrar MinIO/R2 num adaptador; validar conteúdo de upload e separar a chave de links de download.
4. Enviar backups criptografados para R2 e documentar uma restauração testada.

### Fase 3 — operação e futuro Arduino

1. Adicionar rate limiting, logs estruturados, correlação, métricas e health checks completos.
2. Criar outbox e worker para e-mail e impressão de pedidos aceitos.
3. Criar uma aplicação/agente local para a impressora e a máquina. Ela deve buscar tarefas autenticadas e confirmar execução; nunca expor a máquina da oficina diretamente à internet.

## Cenários mínimos de teste

| Cenário | Resultado esperado |
|---|---|
| Dois clientes compram o último item ao mesmo tempo | Apenas um pedido reserva/compra; o outro recebe erro de estoque. |
| Dois pagamentos confirmam o mesmo cupom de uso único | Apenas um é aprovado com cupom; o outro é tratado sem desconto ou encaminhado para análise, conforme regra definida. |
| Mesmo webhook recebido três vezes | Uma única transação e uma única contabilização de venda/cupom. |
| Pagamento aprovado após expiração | Pedido não volta para pago automaticamente; evento fica como aprovação tardia para análise. |
| Produto editado após venda | Pedido e download mantêm o snapshot adquirido. |
| Cliente sem pedido pago tenta avaliar/baixar | API retorna `403`/`404`, sem entregar o conteúdo. |
| Arquivo com MIME falso enviado como imagem | Upload é rejeitado. |
| Backup restaurado em banco vazio | Aplicação sobe, consulta dados e autenticação funcionam. |

## Critério para considerar o backend pronto para produção

- Migrations aplicadas por versão e testadas em cópia do banco.
- Credenciais fora do Git e rotacionáveis.
- Webhooks oficiais e idempotentes para os dois provedores.
- Testes de checkout, estoque, cupom, download e webhook rodando no CI.
- Backup diário fora da VPS, criptografado e restauração comprovada.
- Rate limiting, logs correlacionados e alertas básicos ativos.
- Todas as operações financeiras usam `long` em centavos ou `decimal`, sem `double`.
- Worker/outbox preparado antes de integrar Arduino ou máquina de produção.

## Conclusão

A base atual é boa para continuar porque já elimina Firebase, roda em Docker, mantém a loja simples e usa componentes compatíveis com VPS ARM64. A principal melhoria não é trocar a linguagem ou introduzir microserviços: é **organizar as regras críticas em casos de uso, versionar o banco e endurecer os fluxos de pagamento/estoque**. Isso mantém a IdealCreative leve para operar e segura para crescer.

## Privacidade e exclusão de conta

A conta não deve ser apagada fisicamente quando houver um pedido com obrigação operacional, financeira ou de defesa do consumidor. O comportamento recomendado é encerrar o acesso de imediato e, quando possível, anonimizar os dados pessoais sem apagar o histórico necessário do pedido. A especificação técnica, as retenções e o fluxo completo estão em [ACCOUNT-DELETION-AND-RETENTION.md](ACCOUNT-DELETION-AND-RETENTION.md).

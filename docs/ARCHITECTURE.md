# Arquitetura da IdealCreative VPS

## Camadas

- `frontend/BlazorClient`: Blazor WebAssembly/MudBlazor responsivo, com menu superior em telas grandes e navegação móvel inferior.
- `IdealCreative.Api`: ASP.NET Core 10 com controllers REST, autenticação JWT e autorização por papel.
- PostgreSQL: Identity, produtos, carrinhos, cupons, pedidos, categorias, tags, avaliações e configurações.
- MinIO/R2: imagens e arquivos digitais. O código usa API S3, portanto a troca para Cloudflare R2 não exige alterar o front-end.

## Regras de segurança

- preço e estoque são sempre lidos do PostgreSQL no servidor;
- criação de pedido decrementa estoque dentro de uma transação; cancelamento ou expiração devolve o estoque uma única vez;
- cupom, frete e total são recalculados no servidor e nunca aceitos diretamente do navegador;
- rotas administrativas exigem o papel `Admin`;
- arquivos digitais não devem ser públicos: em produção, gerar URL assinada com expiração;
- avaliações só podem ser criadas por compradores com pedido pago e começam ocultas até moderação;
- chaves JWT, banco, R2 e pagamentos devem vir de secrets/variáveis de ambiente;
- webhooks PayPal/Mercado Pago devem aceitar o mesmo evento mais de uma vez sem duplicar pedido.

## Fluxo principal

`Blazor -> JWT -> API -> PostgreSQL` para identidade, catálogo, carrinho e pedidos.

`Blazor -> API -> MinIO/R2` para upload administrativo. O cliente recebe somente URL pública de imagem ou URL assinada de download.

`Gateway de pagamento -> webhook API -> pedido` para confirmar pagamento; o navegador nunca deve ser a fonte final de confirmação.

Os adaptadores PayPal e Mercado Pago usam credenciais opcionais. Sem credenciais, o ambiente local usa o provedor `Manual/local`, permitindo testar o ciclo completo sem depender da internet.

## Produtos sazonais

A terceira seção da página inicial busca produtos publicados com a tag `sazonal` ou com a tag do mês atual em português, por exemplo `agosto`. Se não houver correspondência, a API usa os produtos publicados mais recentes para a seção não ficar vazia.

## Armazenamento

- Desenvolvimento: MinIO, com imagens públicas servidas pelo proxy autenticado da própria API.
- Produção: Cloudflare R2. `R2_PUBLIC_BASE_URL` deve apontar para o domínio público de imagens e `FRONTEND_PUBLIC_URL` para a URL HTTPS da loja.
- Arquivos digitais usam URL assinada e exigem compra confirmada; o administrador também pode inspecionar, baixar e excluir arquivos não vinculados pelo menu **Mídia**.

## Backup PostgreSQL

O serviço `backup` da composição de produção executa `pg_dump --format=custom` uma vez por dia, grava no volume Docker `idealcreative-backups` e remove arquivos com mais de 14 dias.

Exemplo de restauração, executado na VPS e substituindo o nome do arquivo:

```bash
docker compose -f docker-compose.production.yml cp \
  backup:/backups/idealcreative-AAAAMMDD-HHMMSS.dump ./restore.dump
docker compose -f docker-compose.production.yml cp \
  ./restore.dump postgres:/tmp/restore.dump
docker compose -f docker-compose.production.yml exec postgres \
  pg_restore --clean --if-exists --no-owner -U idealcreative -d idealcreative \
  /tmp/restore.dump
```

Antes de restaurar, faça um backup adicional do banco atual. Teste restaurações periodicamente; possuir arquivos sem testar a recuperação não garante continuidade.

## Limites intencionais

Não existem módulos de GeoIP, nota fiscal, assinatura, criptomoeda, disputa PayPal ou rastreamento Melhor Envio. E-mail e integração Arduino/máquina devem entrar futuramente por uma fila persistente, sem bloquear a criação do pedido.

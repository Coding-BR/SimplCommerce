# Exclusão de conta, anonimização e retenção de registros

> Projeto: IdealCreative VPS  
> Status: MVP implementado; revisão jurídica e expurgo por prazo ainda obrigatórios  
> Jurisdição considerada: Brasil

## Decisão recomendada

A loja deve oferecer **“Excluir conta e dados pessoais”** dentro de **Minha conta → Privacidade**, mas a operação não deve significar apagar cegamente todas as linhas do banco.

O MVP desta especificação já está implementado na API e no front-end. O expurgo futuro de registros retidos deve ser ativado somente depois de a tabela de temporalidade ser validada pelo responsável jurídico/contábil.

O comportamento correto é:

1. encerrar o acesso do cliente imediatamente;
2. bloquear a exclusão final se houver pedido em andamento;
3. anonimizar ou eliminar os dados que não precisam mais ser mantidos;
4. reter de forma restrita o mínimo necessário dos registros de pedido, pagamento, fiscal e defesa da loja enquanto existir obrigação legal ou prazo de defesa;
5. aplicar a mesma política aos backups, sem reativar a conta quando houver uma restauração.

Portanto, a opção visível ao cliente deve ser chamada de **“Excluir conta”**, mas a confirmação deve explicar em linguagem simples: “Seu acesso será encerrado. Dados pessoais que não precisem ser mantidos por obrigação legal serão apagados ou anonimizados; registros de compra podem ser preservados pelo prazo aplicável.”

## Base legal e limite desta especificação

Este documento orienta a engenharia do produto e **não substitui parecer de advogado, contador ou encarregado de dados (DPO)**. Antes de publicar a política de privacidade, a loja deve confirmar os prazos aplicáveis ao seu regime tributário, à UF, aos documentos fiscais emitidos e ao tipo de operação.

- A LGPD assegura ao titular direitos de acesso, correção, anonimização, bloqueio e eliminação; a eliminação dos dados tratados com consentimento tem exceções. [Art. 18 da LGPD](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709compilado.htm)
- Ao término do tratamento, a LGPD determina eliminação dentro dos limites técnicos, mas autoriza conservação para cumprir obrigação legal/regulatória, entre outras hipóteses. [Art. 16 da LGPD](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709compilado.htm)
- A própria ANPD esclarece que um pedido de eliminação não elimina automaticamente dados quando houver hipótese legal de conservação. [Orientação da ANPD](https://www.gov.br/anpd/pt-br/assuntos/titular-de-dados-1/direito-dos-titulares)
- O prazo de **cinco anos não é uma “lei universal de backup”**. Ele aparece, por exemplo, para pretensão de reparação por fato do produto ou serviço no CDC e em regras de decadência/prescrição tributária. [Art. 27 do CDC](https://www.planalto.gov.br/ccivil_03/leis/l8078compilado.htm) e [arts. 173 e 174 do CTN](https://www.planalto.gov.br/ccivil_03/leis/l5172compilado.htm)

Na prática, a política deve documentar cada categoria de dado, a finalidade, a base legal, o prazo e a ação ao final do prazo. “Guardar o banco inteiro por cinco anos” é excessivo; “apagar todos os registros de uma compra” pode ser incompatível com obrigações fiscais, defesa do consumidor e prevenção de fraude.

## Regra funcional para o cliente

| Situação da conta | Ação ao solicitar exclusão | Motivo |
|---|---|---|
| Sem pedidos, ou somente carrinho/favoritos | Encerrar acesso e anonimizar/excluir no mesmo fluxo. | Não há obrigação operacional de pedido. |
| Pedido `Pending` ou `AwaitingPayment` | Solicitar que o cliente cancele o pedido ou aguarde a expiração da reserva. A exclusão final fica pendente. | Evita deixar estoque reservado ou pagamento sem responsável. |
| Pedido `Processing` (em produção) | Bloquear exclusão final e informar o número do pedido. Permitir somente solicitar o encerramento, que será concluído após a finalização/cancelamento. | A loja precisa concluir ou tratar a produção e a entrega. |
| Pedido `Shipped` (enviado) | Bloquear exclusão final até entrega, devolução ou solução do atendimento. | Endereço e contato podem ser necessários para a entrega. |
| Pedido `Paid`, `Delivered`, `Cancelled` | Encerrar acesso imediatamente; anonimizar os dados de perfil que não precisam ser conservados. Reter dados mínimos da transação conforme política de retenção. | Há histórico financeiro/consumidor, mas o login não deve continuar ativo. |
| Disputa, chargeback, devolução, fraude ou ordem judicial | Bloquear anonimização definitiva dos dados estritamente necessários até o encerramento da obrigação. | Defesa de direitos e cumprimento de obrigação. |

**Importante:** a mensagem deve apresentar o bloqueio como temporário e específico, nunca como recusa genérica. Exemplo: “Não é possível concluir a exclusão enquanto o pedido #ABC está em produção. Você já pode encerrar o acesso; a remoção dos dados elegíveis será concluída após a finalização do pedido.”

## O que excluir, anonimizar ou reter

| Dado/categoria | Ação no pedido de exclusão | Retenção sugerida | Observação |
|---|---|---|---|
| Senha, sessões, token de atualização | Revogar imediatamente e inutilizar. | Nenhuma após processamento. | JWT atual é válido por até 8h; será necessária revogação no servidor. |
| Carrinho e preferências não contratuais | Excluir. | Imediata. | Não há razão para conservar. |
| Perfil: nome, telefone, endereço, nascimento, documento | Anonimizar ou excluir após checagem de obrigações. | Até a finalidade/obrigação aplicável. | Nunca manter CPF/documento “por padrão”. |
| E-mail/login | Substituir por identificador interno não recuperável, por exemplo `deleted-<id>@invalid.local`. | Enquanto a linha técnica existir. | Preserva unicidade e impede novo login. |
| Avaliação e comentário | Excluir, salvo fundamento documentado para retenção; se mantida para estatística, anonimizar autor. | Imediata, em regra. | Não é necessário manter o nome do cliente. |
| Pedido e itens comprados | Reter o mínimo necessário; remover ou pseudonimizar dados pessoais que não forem exigidos. | Prazo definido com contador/jurídico; normalmente avaliar ao menos 5 anos conforme o contexto. | Produto, valor, data, status e identificadores técnicos são importantes para auditoria. |
| Pagamento e webhook | Reter referência, valor, provedor, status e evidência mínima. | Conforme obrigação fiscal/contratual/antifraude. | Reduzir ou expurgar payload bruto sensível quando não for mais necessário. |
| Endereço de entrega em pedido concluído | Reter apenas se for indispensável à obrigação/defesa; depois anonimizar. | Não usar “5 anos” automaticamente. | É dado pessoal; deve ter prazo e motivo explícitos. |
| Backup | Não editar backup histórico individualmente; impedir restauração que reative conta excluída. | Mesmo prazo definido no plano de backup e necessidade de recuperação. | Backups devem ser criptografados, isolados e com acesso limitado. |

## Modelo técnico proposto

Não usar `DELETE FROM AspNetUsers` como ação padrão. O sistema atual possui `Orders.UserId`, `Reviews.UserId` e dados históricos sem chaves estrangeiras formais; a exclusão física quebraria a rastreabilidade e pode deixar dados identificáveis duplicados no pedido.

### Novos campos no usuário

```text
ApplicationUser
  AccountState              Active | DeletionRequested | Deactivated | Anonymized
  DeletionRequestedAt       timestamptz?
  DeactivatedAt             timestamptz?
  AnonymizedAt              timestamptz?
  TokenVersion              integer
  RetentionUntil            timestamptz?
```

### Tabelas novas

```text
PrivacyRequests
  Id, UserId, Type, Status, RequestedAt, ProcessedAt,
  BlockingReason, LegalBasis, RetentionUntil, OperatorId, Notes

DataRetentionSchedule
  Category, LegalBasis, RetentionPeriod, StartEvent, AnonymizationAction

AccountDeletionAudit
  Id, SubjectReferenceHash, RequestedAt, CompletedAt,
  Result, BlockingReason, RetentionRuleVersion
```

`AccountDeletionAudit` deve guardar o mínimo possível. Um hash de referência técnica é preferível a manter e-mail, CPF ou endereço no log de auditoria. A tabela prova que o pedido foi atendido sem recriar um cadastro pessoal paralelo.

## Fluxo de implementação

```text
Cliente pede exclusão
        |
        v
Confirma identidade + senha atual
        |
        v
Checagem de pedidos, disputa e retenções
   |                         |
sem bloqueio              há bloqueio operacional
   |                         |
   v                         v
Revoga sessões             Cria PrivacyRequest pendente
Desativa login             Explica pedido que bloqueia
Exclui carrinho            Desativa marketing imediatamente
Anonimiza dados elegíveis  Reavalia ao mudar status do pedido
   |
   v
Registra auditoria mínima e agenda expurgo no fim da retenção
```

### Endpoint e tela sugeridos

```text
GET  /api/users/privacy/deletion-preview
POST /api/users/privacy/delete-account
GET  /api/users/privacy/deletion-status
POST /api/users/privacy/cancel-deletion   (somente antes da anonimização)
```

`deletion-preview` informa, antes da confirmação:

- se há pedido que bloqueia a conclusão e qual é o status;
- quais dados serão removidos agora;
- quais categorias serão retidas, por qual fundamento e até quando;
- que downloads digitais e histórico da conta deixarão de estar acessíveis após a desativação;
- um link para exportar os dados antes de encerrar a conta.

A requisição de exclusão precisa pedir a senha atual ou uma segunda confirmação autenticada. Para conta com login social, usar a reautenticação do provedor. Não solicitar CPF, cartão ou outros dados adicionais para confirmar essa ação.

## Revogação de sessão: alteração necessária no backend atual

Hoje o JWT é emitido com validade de oito horas e não há armazenamento de sessão. Só marcar a conta como deletada no banco não invalida automaticamente um token já emitido.

Escolha recomendada:

1. adicionar `TokenVersion` ao usuário e como claim no JWT;
2. validar a versão e `AccountState == Active` no evento `OnTokenValidated`;
3. incrementar `TokenVersion` ao desativar, trocar senha ou promover/rebaixar permissões;
4. reduzir o access token para 15–30 minutos e usar refresh token rotativo armazenado em tabela, se a experiência exigir sessão longa.

Para um volume pequeno, a consulta de versão/estado por requisição autenticada é aceitável. Se crescer, usar cache de curta duração com invalidação explícita.

## Anonimização prática

Ao concluir uma exclusão elegível:

```text
DisplayName       -> "Cliente removido"
Email/UserName    -> "deleted-<guid>@invalid.local"
PasswordHash      -> hash aleatório sem senha conhecida
SecurityStamp     -> novo valor
PhoneNumber       -> null
BirthDate         -> null
Street/Number/... -> null
CustomerDocument  -> null
Cart              -> removido
Reviews           -> removidas ou autor anonimizado
```

Nos pedidos, eliminar ou substituir os campos duplicados de identificação pessoal que não tiverem fundamento de retenção. O ideal, numa próxima migration, é separar **snapshot comercial** (produto, total, status, data, provedor) de **dados de entrega** com prazo específico de expurgo.

Não sobrescrever o `UserId` do pedido com um valor genérico: manter um identificador técnico pseudonimizado é útil para consistência, mas ele não deve permitir recuperar a pessoa após a anonimização.

## Backups e restauração

Backups existem para continuidade operacional e não precisam ser reescritos arquivo a arquivo a cada exclusão. Porém, devem obedecer a controles:

- criptografia em repouso e em trânsito;
- bucket R2 de backup separado do bucket de mídia, sem acesso público;
- acesso administrativo mínimo e registro de restauração;
- prazo de retenção documentado para cada backup;
- restauração somente em ambiente isolado quando possível;
- após restaurar um backup anterior a uma exclusão, reaplicar a fila/registro de solicitações concluídas antes de liberar o ambiente para produção.

A restauração não pode fazer uma conta anonimizada voltar a existir. O procedimento operacional deve incluir uma verificação de solicitações de privacidade processadas depois da data do backup.

## Eventos que concluem uma exclusão pendente

O worker de retenção deve reavaliar pedidos `DeletionRequested` quando ocorrer:

- cancelamento de pedido pendente;
- conclusão de produção;
- confirmação de entrega;
- encerramento de devolução, disputa ou chargeback;
- término de uma retenção previamente registrada.

Isso combina naturalmente com a futura `OutboxMessages`: a alteração do status do pedido grava um evento; o worker de privacidade lê o evento e decide se a solicitação pendente já pode ser concluída.

## Testes obrigatórios

| Cenário | Resultado esperado |
|---|---|
| Cliente sem pedidos exclui a conta | Não consegue mais autenticar; carrinho e perfil são removidos/anonimizados. |
| Cliente com pedido em produção pede exclusão | Acesso é encerrado, mas a solicitação informa o bloqueio e não remove dados necessários. |
| Pedido é concluído depois do pedido de exclusão | Worker conclui anonimização automaticamente conforme política. |
| Token emitido antes da exclusão | É recusado após revogação. |
| Backup anterior à exclusão é restaurado | Processo de pós-restauração reaplica a anonimização; a conta não volta a autenticar. |
| Administrador consulta pedido de conta anonimizda | Vê dados comerciais necessários, não endereço/CPF/contato além do prazo permitido. |
| Cliente tenta baixar produto digital após encerrar a conta | Download é inacessível, pois o login foi encerrado; aviso prévio já foi exibido. |

## Checklist para publicar a função

- [ ] Política de privacidade e termos atualizados com categorias, finalidade, base legal e prazos.
- [ ] Prazo tributário/fiscal confirmado para o regime e a UF da loja.
- [ ] Tela de prévia e confirmação de exclusão implementada.
- [ ] Bloqueios de pedido em produção, envio, disputa e devolução implementados.
- [ ] Revogação real de token/sessão implementada.
- [ ] Anonimização de perfil, carrinho, avaliações e cópias de PII em pedidos implementada.
- [ ] Registro mínimo de solicitação e processamento criado.
- [ ] Backups externos criptografados e processo de restauração revisado.
- [ ] Testes automatizados dos cenários desta especificação.
- [ ] Revisão jurídica/contábil concluída antes da ativação em produção.

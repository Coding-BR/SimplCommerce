# Senhas, recuperação de acesso e credenciais de integrações

## Decisão adotada

Senhas de clientes e administradores **não são criptografadas**. Elas são transformadas por hash irreversível com o `PasswordHasher` versionado do ASP.NET Core Identity. A configuração atual usa PBKDF2-HMAC-SHA512, salt aleatório individual e 220.000 iterações. Quando o custo configurado aumenta, o Identity atualiza hashes antigos após um login válido.

Argon2id é uma excelente opção, mas exigiria um componente externo e uma migração personalizada. Para este projeto .NET, PBKDF2 nativo e versionado reduz dependências e segue a recomendação vigente do OWASP para PBKDF2-HMAC-SHA512. O custo deve ser medido novamente na VPS ARM64 para que uma verificação permaneça abaixo de aproximadamente um segundo.

Política aplicada:

- mínimo de 15 e máximo de 128 caracteres;
- frases-senha e todos os caracteres Unicode aceitos, sem regras artificiais de maiúscula/símbolo;
- bloqueio de senhas comuns conhecidas pelo validador local;
- limite de tentativas por IP e bloqueio temporário da conta após cinco falhas;
- nunca registrar senha, token de recuperação ou segredo de integração em logs.

Antes de grande escala, ampliar a lista local de senhas comprometidas com um arquivo offline confiável ou serviço k-anonymous. A indisponibilidade desse serviço não deve bloquear todos os cadastros.

## Recuperação de senha

O fluxo usa token do ASP.NET Identity protegido por Data Protection, ligado ao usuário, com validade de uma hora. A resposta de solicitação é sempre genérica e tem tempo mínimo, reduzindo enumeração de contas. Há rate limit, confirmação dupla no frontend, notificação por SMTP após a troca e revogação dos JWTs anteriores por `TokenVersion`. O usuário não é autenticado automaticamente depois da troca.

O link é construído exclusivamente a partir de `Frontend:PublicUrl`; nunca a partir do cabeçalho `Host` enviado pelo visitante. Produção exige HTTPS. A página define política `no-referrer` para evitar vazamento do token por navegação externa.

## Credenciais do painel administrativo

Tokens de PayPal, Mercado Pago, SMTP, frete e R2 precisam ser recuperáveis para chamar as APIs, portanto não podem usar hash de senha. Eles são protegidos com ASP.NET Data Protection antes de entrar no PostgreSQL.

- GET administrativo devolve somente `...Configured: true/false`, nunca o segredo;
- campo secreto vazio preserva o valor atual;
- remoção exige checkbox explícito;
- variáveis de ambiente continuam como fallback de implantação;
- pagamentos, SMTP, frete e armazenamento leem a configuração em tempo real;
- a chave do JWT e a chave mestra do Data Protection não aparecem no painel.

O volume `idealcreative-dataprotection` torna o chaveiro persistente. Na VPS, configure também um certificado PKCS#12 para cifrar o chaveiro em repouso:

```text
Security__DataProtectionCertificate__Path=/run/secrets/idealcreative-dp.pfx
Security__DataProtectionCertificate__Password=senha-fornecida-pelo-gerenciador-de-segredos
```

O certificado e sua senha devem vir de Docker Secrets, systemd credentials ou cofre externo, nunca do Git. Faça backup conjunto do PostgreSQL e do chaveiro/certificado: sem a chave, as credenciais protegidas não podem ser recuperadas.

## Limitações que permanecem explícitas

- O adaptador de cotação Melhor Envio ainda não foi implementado; selecionar esse provedor devolve uma resposta clara em vez de inventar uma cotação.
- Webhooks de pagamento ainda usam o segredo compartilhado do projeto. Antes de produção com dinheiro real, implementar a validação oficial de assinatura e replay de cada provedor.
- O bloqueio local de senhas comuns é pequeno e deve receber uma lista comprometida mais ampla antes de alto volume.
- `ForwardedHeaders` só confia em proxies conhecidos. Configure a rede/IP do proxy reverso na VPS; não aceite cabeçalhos encaminhados de qualquer origem.

## Referências técnicas

- NIST SP 800-63B: https://pages.nist.gov/800-63-4/sp800-63b.html
- OWASP Password Storage: https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html
- OWASP Forgot Password: https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html
- OWASP Secrets Management: https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html
- Microsoft Data Protection: https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview

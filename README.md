# IdealCreative VPS

Nova base da IdealCreative em C#/.NET 10, separada do projeto Firebase original. O front-end Blazor WebAssembly conserva a navegação útil da loja, mas foi simplificado e ligado a uma API própria em ASP.NET Core, PostgreSQL e armazenamento S3 compatível (MinIO local ou Cloudflare R2 em produção). Firebase, Redis, GeoIP, assinaturas, cripto e nota fiscal não fazem parte desta versão enxuta.

## Executar somente via Docker

```powershell
cd C:\Users\adria\Desktop\forum\idealcreative-vps
docker compose up -d --build
```

- Loja: http://localhost:5289
- API: http://localhost:5288
- Health: http://localhost:5288/health/ready
- PostgreSQL: localhost:55432
- MinIO API: http://localhost:59100
- MinIO Console: http://localhost:59101
- Caixa de e-mails de desenvolvimento (Mailpit): http://localhost:59102

Login administrativo local (apenas desenvolvimento): `admin@idealcreative.local` / `IdealCreative#2026`. Em VPS, substituir todos os segredos por variáveis de ambiente e não usar estas credenciais.

## Já funcional

- login interno e cadastro com ASP.NET Identity + JWT;
- recuperação de senha por e-mail SMTP, com link de uso único válido por uma hora;
- catálogo público, detalhe, filtros de busca/preço e produtos em PostgreSQL;
- criação, edição, publicação e exclusão de produtos para administrador, incluindo produtos digitais;
- categorias e tags com CRUD administrativo;
- carrinho persistente por usuário;
- cupom de desconto e cálculo de subtotal/total;
- criação, consulta paginada e transições controladas de status dos pedidos;
- reserva atômica de estoque e liberação automática após duas horas sem pagamento;
- upload de imagem e arquivo digital via API para MinIO (mesma interface S3 do R2), com download autorizado e URL temporária;
- checkout local e adaptadores PayPal/Mercado Pago ativados quando suas credenciais são configuradas;
- avaliações de compradores confirmados, uma por cliente/produto, com moderação administrativa;
- perfil do cliente, lista de clientes e promoção/rebaixamento de administradores;
- dashboard administrativo com dados reais, health checks e composição Docker compatível com ARM64.

## Produção em VPS

Use a composição separada, que não publica PostgreSQL nem MinIO na internet e espera todos os segredos por variáveis de ambiente:

```bash
docker compose -f docker-compose.production.yml up -d --build
```

Copie `.env.production.example` para um arquivo seguro fora do repositório e preencha todos os valores. Na produção, use Cloudflare R2 (endpoint S3 compatível) e um proxy reverso/Cloudflare Tunnel na frente do front-end. O Nginx do contêiner encaminha `/api` internamente, portanto o navegador não precisa acessar a porta privada da API.

Os webhooks de pagamento exigem os segredos configurados; a captura PayPal valida o status `COMPLETED` antes de marcar o pedido como pago. O serviço `backup` cria diariamente um `pg_dump` no volume `idealcreative-backups` e retém 14 dias. Consulte [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) para regras e restauração.

Para recuperação de senha na VPS, preencha as variáveis `SMTP_*` no arquivo seguro de ambiente. Utilize a credencial SMTP ou *app password* do provedor de e-mail; nunca use a senha da conta principal. Em desenvolvimento, o serviço Mailpit recebe os e-mails localmente e permite conferir o link sem enviar mensagens externas.

## Pendências antes da abertura da loja

1. Adicionar migrações EF Core versionadas para substituir o bootstrap SQL incremental.
2. Validar PayPal e Mercado Pago com contas reais e webhooks do domínio definitivo.
3. Adicionar fila persistente para e-mail, impressão Arduino e integração futura com a máquina.

O projeto Firebase original não é alterado e não é carregado pelo novo front-end.

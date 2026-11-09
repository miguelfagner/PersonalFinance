# Web na versão de compatibilidade

`PersonalFinance.Api` ainda hospeda `PersonalFinance.Client` neste repositório. Sua publicação mantém as rotas `/api/*`, o Blazor na mesma origem e o protocolo de sincronização. A API separada no novo repositório já remove esse hosting; esta versão conjunta existe para a transição.

## Desenvolvimento local

Abra `PersonalFinance.slnx` na raiz desta pasta e escolha `PersonalFinance.Api` como projeto inicial. Configure o SQL Server LocalDB e os valores `ConnectionStrings:Finance`, `Authentication:JwtKey`, `BootstrapUser:Email` e `BootstrapUser:Password` com User Secrets. O perfil `https` não fornece credenciais. Execute:

```powershell
dotnet run --project Web/PersonalFinance.Api --launch-profile https
```

A API utiliza conta única configurada, sem cadastro público. O login mantém `Token` e `ExpiresAt` e acrescenta `RefreshToken` e `RefreshExpiresAt`; `POST /api/auth/refresh` rotaciona o token, e `POST /api/auth/logout` revoga a sessão. O access token dura 15 minutos e o refresh token, 30 dias. Configure `AllowedOrigins` para a origem publicada do Blazor durante a separação.

## Publicação

A versão conjunta deve ser publicada antes dos clientes separados somente após backup e validação do banco e do SQLite. Configure os segredos como variáveis de ambiente no MonsterASP. Não use o antigo perfil `site94880-WebDeploy`; ele não faz parte dos novos repositórios. Não copie o `appsettings.json` sensível anterior. Consulte [DEPLOYMENT.md](DEPLOYMENT.md) para o roteiro.

O Mobile mantém a fila SQLite e IDs de operação idempotentes. A carga inicial continua em `/api/sync/from-device`; comandos e feed seguem `/api/commands` e `/api/sync/*`. Antes do corte, verifique que nenhum comando permaneça pendente e que contagens e totais financeiros coincidam com o backup.

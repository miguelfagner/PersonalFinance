# PersonalFinance — versão de compatibilidade

Este repositório preserva a aplicação conjunta durante a migração. A API ainda hospeda o Blazor nesta versão; o Android mantém o banco SQLite e a sincronização. O corte para três soluções independentes está preparado em repositórios privados:

- [PersonalFinance.Api](https://github.com/miguelfagner/PersonalFinance.Api)
- [PersonalFinance.Mobile](https://github.com/miguelfagner/PersonalFinance.Mobile)
- [PersonalFinance.Blazor](https://github.com/miguelfagner/PersonalFinance.Blazor)

O Blazor continuará em `https://miguelfagner.runasp.net/`. A nova URL pública da API será definida antes da publicação dos clientes separados. Este repositório só será arquivado depois de validar API, Blazor e Mobile em produção, incluindo ausência de pendências de sincronização.

## Estrutura atual

- `Mobile/`: aplicativo Android `net9.0-android`, SQLite, widget e fila offline.
- `Shared/`: contratos e cálculos usados pela versão conjunta.
- `Web/`: API ASP.NET Core e cliente Blazor WebAssembly hospedado por ela.
- `Tests/`: testes de regressão e importação.

Abra `PersonalFinance.slnx` nesta pasta. Para configurar a API localmente, use User Secrets; na hospedagem, use variáveis de ambiente para `ConnectionStrings__Finance`, `Authentication__JwtKey`, `BootstrapUser__Email` e `BootstrapUser__Password`. Nunca grave credenciais em arquivos versionados. O perfil `https` usa apenas o ambiente de desenvolvimento.

```powershell
dotnet build PersonalFinance.slnx -c Debug
dotnet run --project Web/PersonalFinance.Api --launch-profile https
dotnet run --project Tests/PersonalFinance.Mobile.RegressionTests
dotnet run --project Tests/PersonalFinance.ImportTests -- --calculations-only
```

O teste completo de integração exige SQL Server Express LocalDB. Veja [Web/DEPLOYMENT.md](Web/DEPLOYMENT.md) para a ordem segura do corte. Antes de publicar, faça backups do SQL Server e do SQLite do dispositivo e registre contagens e totais financeiros. A migração de sessões adiciona `RefreshSessions` sem recriar as tabelas financeiras.

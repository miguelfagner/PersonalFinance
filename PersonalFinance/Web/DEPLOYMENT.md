# Corte para API, Mobile e Blazor independentes

Estado atual: este repositório contém a versão conjunta de compatibilidade. O Blazor permanecerá em `https://miguelfagner.runasp.net/`; a nova URL HTTPS da API ainda precisa ser definida. Não publique o Blazor separado com `apiBaseUrl` vazio e não aponte o Mobile para uma URL presumida.

## Antes de qualquer publicação

1. Faça backup verificável do SQL Server atual e do SQLite do dispositivo. Registre contagens e totais financeiros de receitas, despesas, transações e saldos; o script de referência está em `PersonalFinance.Api/ops/financial-baseline.sql` no novo repositório.
2. Registre a quantidade de comandos pendentes no Mobile e no Blazor. Preserve o banco do aplicativo ao instalar uma atualização; preserve também a origem atual do Blazor para que a migração da fila IndexedDB encontre as pendências.
3. Configure `ConnectionStrings__Finance`, `Authentication__JwtKey`, `BootstrapUser__Email` e `BootstrapUser__Password` no ambiente do MonsterASP. Não copie os antigos `appsettings.json` nem o perfil WebDeploy. Rotacione a senha SQL, chave JWT e senha bootstrap antes do corte.
4. Configure `AllowedOrigins` com a origem HTTPS exata do Blazor. Mantenha a conta única; não habilite cadastro público.

## Sequência

1. Publique esta versão conjunta de compatibilidade e valide login, refresh, logout, rotas financeiras e sincronização no endereço atual. A migração EF adiciona somente `RefreshSessions`; verifique contagens e totais após a aplicação.
2. Defina e provisiona a nova URL HTTPS da API. Publique a API independente nesse site e valide `/api/health`, `/openapi/v1.json`, CORS, login e sincronização com os dados preservados.
3. Configure `apiBaseUrl` absoluto no Blazor independente e publique seus arquivos estáticos na origem atual. O `web.config` fornece fallback para `index.html`. Valide login cross-origin, CRUD, fila offline, atualização da PWA e rotas internas.
4. Configure a URL da API no Mobile independente, instale a atualização sobre a instalação existente e valide banco SQLite, fila, carga inicial, reconexão, widget e ausência de duplicação.
5. Compare novamente contagens e totais com o baseline, confirme que não há comandos pendentes e observe estabilidade antes de arquivar este repositório.

A ordem e os comandos detalhados da nova API estão em `ops/CUTOVER.md` no repositório `PersonalFinance.Api`. O rollback requer os backups e a restauração coordenada do código, banco e clientes; não descarte os backups nem o repositório legado durante a observação.

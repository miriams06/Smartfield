# Sysprime SmartField

Sysprime SmartField é uma aplicação para controlo de assiduidade e gestão de equipas no terreno.

A solução separa a utilização móvel dos funcionários da área de backoffice e mantém as regras de negócio, validações e isolamento por empresa no servidor.

> Registar no terreno, validar no backoffice e preparar a integração com o PRIMAVERA.

## Perfis de utilizador

A aplicação utiliza três perfis:

- `Admin`: acesso administrativo ao backoffice e às operações de gestão;
- `Manager`: acesso operacional ao backoffice;
- `Employee`: acesso à área móvel de assiduidade.

A policy `Backoffice` permite acesso a `Admin` e `Manager`.

O administrador de desenvolvimento é criado através do seed da aplicação. Na página de gestão de funcionários, depois de o funcionário estar criado e enquanto ainda não tiver uma conta associada, um `Admin` pode abrir a secção **Conta de utilizador**, definir email e password e escolher o perfil da conta entre `Employee` e `Manager`.

Uma conta criada como `Employee` utiliza a área móvel de assiduidade. Uma conta criada como `Manager` tem acesso ao backoffice. A atribuição de `Manager` fica reservada a utilizadores `Admin`; este fluxo não permite criar novas contas `Admin`.

## Área móvel do funcionário

O utilizador `Employee` pode:

- consultar o estado atual da jornada;
- selecionar o local de trabalho onde se encontra;
- usar automaticamente o local habitual quando existe um `DefaultWorkSiteId`;
- receber aviso quando o local habitual é aplicado por defeito;
- registar entrada, início de pausa, fim de pausa e saída;
- enviar a localização atual no momento da picagem;
- receber feedback da validação de geofence;
- consultar o histórico diário;
- consultar entrada, saída, pausas e tempo trabalhado;
- consultar os locais de trabalho utilizados em cada dia e em cada picagem.

A aplicação não faz tracking contínuo. A localização é pedida apenas quando a operação necessita dela.

## Assiduidade

A assiduidade é baseada em eventos (`AttendanceEvent`) e preserva os registos originais.

Eventos suportados:

- `ClockIn`;
- `BreakStart`;
- `BreakEnd`;
- `ClockOut`.

A aplicação valida a sequência das picagens e calcula entrada, saída, pausas, minutos trabalhados e estado atual do funcionário.

Cada evento pode guardar timestamp do servidor e do cliente, latitude, longitude, precisão, local de trabalho, projeto quando aplicável, resultado da geofence e distância ao local.

### Resumo diário no ClockOut

Na PWA, **Registar saída** abre o ecrã **Resumo do dia**. A saída só é enviada depois
de selecionar **TERMINAR DIA**, com um resumo de pelo menos 10 caracteres após
`Trim()` e no máximo 4000 caracteres no total. Cancelar não regista uma picagem.
Espaços e quebras de linha do conteúdo válido são preservados. Se o pedido falhar,
o formulário mantém o texto e reutiliza o `ClientEventId` numa nova tentativa.

O request `POST /api/attendance/punch` aceita `dailySummary`. É obrigatório para
novos ClockOut; ClockIn, BreakStart e BreakEnd não o exigem. A validação é feita
também em Application, independentemente do browser.

O resumo fica em `DailyWorkReport`, separado de `AttendanceEvent`, com unicidade
por `CompanyId + EmployeeId + WorkDate`. `WorkDate` é a data do timestamp do servidor
do ClockOut na timezone da empresa; numa jornada que atravessa a meia-noite, fica
associado ao dia da saída, tal como o evento no histórico atual.

É permitido reabrir a jornada no mesmo dia. O próximo ClockOut exige novamente
um resumo e atualiza o mesmo relatório, incluindo a ligação ao novo ClockOut e
`SubmittedAtUtc`/`UpdatedAtUtc`. A auditoria guarda o texto e a ligação anteriores
e novos. Repetir um `ClientEventId` já registado não altera o relatório.

Picagem, relatório, AuditLog e Outbox usam a mesma transação. O resumo não é incluído
nos logs de diagnóstico nem no payload de integração da picagem. O histórico Employee
e o detalhe diário do backoffice apresentam o resumo em consulta. Não existe endpoint
de edição para Manager/Admin; corrigir a hora de uma picagem não altera silenciosamente
o texto nem a data do relatório. Dias antigos sem relatório mostram “Sem resumo submetido.”

Antes de usar esta versão, aplicar a migration `AddDailyWorkReports` com o comando
`dotnet tool run dotnet-ef -- database update` indicado abaixo. A migration não cria resumos para eventos antigos.

## Geolocalização e geofence

Os locais de trabalho (`WorkSite`) podem ter código, nome, morada, latitude, longitude, raio de geofence, estado ativo/inativo e código de centro de custo ERP.

A distância entre o funcionário e o local é calculada no servidor através da fórmula de Haversine.

Modos disponíveis:

- `Disabled`: não bloqueia;
- `Warning`: aceita a picagem e assinala incumprimento;
- `Block`: rejeita quando a regra de geofence não é cumprida.

O raio definido no próprio `WorkSite` tem precedência sobre o raio por defeito da empresa. Locais inativos não podem ser usados em novas picagens.

## Backoffice

Os perfis `Admin` e `Manager` podem aceder às áreas protegidas pela policy `Backoffice`.

### Assiduidade

Permite:

- filtrar por data, funcionário e local;
- consultar entrada, saída, total diário, pausas e estado;
- identificar eventos fora da geofence;
- abrir detalhe diário por funcionário;
- consultar eventos originais e respetivo local;
- corrigir eventos sem apagar o registo original;
- exportar assiduidade para CSV.

A exportação CSV inclui `Date`, `EmployeeNumber`, `EmployeeName`, `ClockIn`, `ClockOut`, `BreakMinutes`, `WorkedMinutes`, `WorkSite`, `ProjectCode` e `GeofenceStatus`.

### Funcionários

Permite:

- listar, pesquisar, criar e editar funcionários;
- ativar e desativar;
- definir local habitual;
- associar utilizadores;
- criar uma conta de login para um funcionário que ainda não tenha conta associada;
- escolher `Employee` ou `Manager` ao criar a conta de login;
- reservar a criação de contas `Manager` a utilizadores `Admin`;
- guardar o código de funcionário do ERP.

### Locais de trabalho

Permite listar, pesquisar, criar, editar, ativar/desativar e configurar coordenadas, raio de geofence e código de centro de custo ERP.

Também permite configurar regras gerais de geolocalização da empresa, incluindo obrigatoriedade de localização, modo de geofence e raio por defeito.

### Projetos

Permite listar, pesquisar, criar e editar projetos, definir tipo e estado, associar cliente e local de trabalho, definir datas e guardar referências para integração ERP.

Projetos e locais de trabalho são conceitos distintos. A geofence é validada sobre o `WorkSite` usado na picagem.

## Correções e auditoria

Uma correção administrativa não elimina o `AttendanceEvent` original. A aplicação guarda a correção separadamente, incluindo novo tipo, novo timestamp, motivo, utilizador e data da correção.

A aplicação mantém `AuditLog` para operações relevantes, incluindo:

- login administrativo;
- criação e alteração de funcionários;
- criação e alteração de locais de trabalho;
- criação de projetos;
- criação de eventos de assiduidade;
- correções de assiduidade.

Consulta administrativa:

```text
GET /api/admin/audit
```

## Integração e Outbox

A solução contém uma `IntegrationOutbox` para desacoplar eventos destinados a sistemas externos.

Existem, entre outros, os eventos:

- `AttendanceCreated`;
- `AttendanceCorrected`;
- `EmployeeCreated`;
- `EmployeeUpdated`;
- `ProjectCreated`.

A integração com PRIMAVERA está isolada em `SmartField.Integrations.Primavera`.

Estão definidos contratos e DTOs para testar ligação, obter funcionários, projetos e centros de custo e enviar assiduidade. A implementação atualmente registada é `NotConfiguredPrimaveraClient`, pelo que ainda não existe comunicação real com o ERP.

## Logging e tratamento de erros

A API utiliza Serilog para consola e ficheiros diários em `logs/`.

Existe `CorrelationId` por pedido e middleware global de exceções. Erros inesperados são devolvidos em `ProblemDetails` sem expor detalhes internos, incluindo o identificador de correlação para pesquisa nos logs.

## Multiempresa e segurança

A aplicação utiliza ASP.NET Core Identity e JWT Bearer.

O `CompanyId` é obtido da identidade autenticada e não é confiado a partir do browser. Os dados de negócio são restringidos à empresa autenticada.

## Arquitetura

```text
Smartfield.sln
├── SmartField.Domain
├── SmartField.Application
├── SmartField.Infrastructure
├── SmartField.Integrations.Primavera
├── SmartField.Api
├── SmartField.Client
├── SmartField.Domain.Tests
├── SmartField.Application.Tests
├── SmartField.Infrastructure.Tests
├── SmartField.Api.Tests
└── SmartField.Integrations.Primavera.Tests
```

- `SmartField.Domain`: entidades, enums e regras de domínio.
- `SmartField.Application`: casos de uso, contratos e regras de aplicação.
- `SmartField.Infrastructure`: EF Core, SQL Server, Identity, stores, auditoria, outbox e migrations.
- `SmartField.Integrations.Primavera`: contratos e implementações relacionados com PRIMAVERA.
- `SmartField.Api`: controllers, autenticação, autorização, Swagger, CORS, health checks, logging e composição de dependências.
- `SmartField.Client`: Blazor WebAssembly PWA com área móvel e backoffice.

O Client comunica apenas com a API. O frontend não comunica diretamente com PRIMAVERA.

## Stack

- .NET 8
- ASP.NET Core Web API
- Blazor WebAssembly PWA
- Entity Framework Core 8
- SQL Server
- ASP.NET Core Identity
- JWT Bearer
- Serilog
- Swagger / OpenAPI
- xUnit

## Pré-requisitos

- [.NET SDK 8.0.424](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), na arquitetura da máquina
- SQL Server ou SQL Server Express
- `dotnet-ef` 8.0.23, restaurado como ferramenta local do repositório

```powershell
dotnet tool restore
```

O `global.json` exige exatamente o SDK `8.0.424` (`rollForward: disable`, sem versões
preview). Instalar esse SDK antes de executar comandos no repositório; ter apenas o
runtime .NET 8 ou um SDK 9/10 não é suficiente. Pode coexistir com outros SDKs.
O `global.json` seleciona o SDK, mas não o instala automaticamente.

Executar os comandos a partir da raiz do repositório. O manifest
`.config/dotnet-tools.json` fixa `dotnet-ef` em `8.0.23`, alinhado com os packages EF
Core da solução. Não é necessária instalação global. Para invocar explicitamente
a ferramenta local, usar `dotnet tool run dotnet-ef -- <argumentos>`.

## Configuração local

### Clonar

```powershell
git clone https://github.com/miriams06/Smartfield.git
cd Smartfield
```

### Base de dados

Configuração de desenvolvimento por omissão:

```text
Server=.\SQLEXPRESS;Database=SmartFieldDb;Trusted_Connection=True;TrustServerCertificate=True
```

Para outra ligação, usar preferencialmente `user-secrets`:

```powershell
dotnet user-secrets set "ConnectionStrings:SmartField" "Server=.\SQLEXPRESS;Database=SmartFieldDb;Trusted_Connection=True;TrustServerCertificate=True" --project .\SmartField.Api
```

### Utilizadores e dados de demonstração (Development)

```powershell
dotnet user-secrets set "Seed:AdminPassword" "<password-local-segura>" --project .\SmartField.Api
dotnet user-secrets set "Seed:ManagerPassword" "<password-local-segura>" --project .\SmartField.Api
dotnet user-secrets set "Seed:EmployeePassword" "<password-local-segura>" --project .\SmartField.Api
```

Substituir os placeholders por passwords locais que cumpram a política do Identity.
Podem ser iguais em Development. Nunca guardar passwords reais no repositório.
Também é possível configurar `Seed__AdminPassword`, `Seed__ManagerPassword` e
`Seed__EmployeePassword` no ambiente.

Após aplicar as migrations, iniciar a API em `Development`. O `DevelopmentDataSeeder`
é executado apenas neste ambiente, nunca em Production ou Staging. Sem qualquer
password configurada não executa o seed; uma password de perfil em falta produz um
aviso e impede a criação das novas contas desse perfil. Configurar as três para obter
o conjunto completo. Contas existentes mantêm as passwords e o estado ativo/inativo.

| Empresa | Email | Perfil | Funcionário |
| --- | --- | --- | --- |
| SYS-DEMO | admin@smartfield.local | Admin | Mantém a associação existente, se houver |
| SYS-DEMO | manager@smartfield.local | Manager | MGR001 — Marta Ferreira |
| SYS-DEMO | joao.silva@smartfield.local | Employee | FUNC001 — João Silva |
| SYS-DEMO | maria.costa@smartfield.local | Employee | FUNC002 — Maria Costa |
| AVAC-DEMO | manager.avac@smartfield.local | Manager | MGR001 — Carlos Sousa |
| AVAC-DEMO | tecnico.avac@smartfield.local | Employee | TEC001 — Técnico AVAC |

São criadas `SYS-DEMO` (Sysprime Demo) e `AVAC-DEMO` (AVAC Demo), com timezone
`Europe/Lisbon`. Os funcionários novos usam a sede da respetiva empresa como
`DefaultWorkSite`. Os managers têm registo de funcionário para guardar o nome e a associação.

- SYS-DEMO: locais `SYS-SEDE` (Sede), `SYS-ARM` (Armazém) e `OBR-001` (Obra Porto).
  Projetos `OBR-001` (Construção Porto, Construction, local Obra Porto) e `MAN-001`
  (Manutenção AVAC, Maintenance, local Sede).
- AVAC-DEMO: local `AVAC-SEDE` (Sede AVAC) e projeto `MAN-AVAC-001`
  (Contrato Manutenção Cliente Demo, Maintenance, local Sede AVAC).

Os projetos novos ficam Active. As configurações novas permitem pausas e deixam a
geofence Disabled, sem exigir geolocalização; não são inventadas coordenadas.
Configurações e dados já editados não são repostos em cada arranque.

O seed reutiliza códigos por empresa e emails de utilizadores, sem duplicar dados.
Um conflito de associação ou perfil aborta a transação com erro explícito. Não aplica
migrations automaticamente nem altera os seeds históricos das migrations: estes já
incluem SYS-DEMO/FUNC001, independentemente do ambiente, mas não contas de login.

Compatibilidade com o seed antigo: se o Admin estiver ligado a FUNC001, esse funcionário
passa a `ADMIN-DEMO`; se `employee@smartfield.local` estiver ligado a FUNC002, passa a
`MOBILE-DEMO`. Mantêm-se IDs, contas, passwords e histórico, criando João/Maria nos
números pedidos. A conta antiga de Employee continua disponível, mas já não é criada
em bases novas. O registo genérico FUNC001 sem conta é aproveitado para João.

Validação manual: entrar com cada perfil, verificar backoffice/PWA, locais habituais
e listagens; comparar SYS-DEMO com AVAC-DEMO e tentar consultar um ID da outra empresa.
Reiniciar a API e confirmar que os registos não duplicam. Os testes de seed e isolamento
com SQL real usam `SMARTFIELD_TEST_CONNECTION_STRING` e `Category=Integration`.

### Restaurar e compilar

```powershell
dotnet tool restore
dotnet restore
dotnet build
```

O restauro utiliza o `NuGet.Config` versionado. Confirmar as versões selecionadas:

```powershell
dotnet --version                  # 8.0.424
dotnet tool run dotnet-ef -- --version # 8.0.23
```

Depois de alterar a versão no manifest, repetir `dotnet tool restore`. Alterações
do SDK devem atualizar `global.json` e esta documentação em conjunto.

### Aplicar migrations

Depois do restauro e build, verificar as migrations e a correspondência do modelo
com o snapshot sem abrir uma ligação SQL:

```powershell
dotnet tool run dotnet-ef -- migrations list --no-connect --project .\SmartField.Infrastructure --startup-project .\SmartField.Api --no-build -- --environment Development
dotnet tool run dotnet-ef -- migrations has-pending-model-changes --project .\SmartField.Infrastructure --startup-project .\SmartField.Api --no-build -- --environment Development
```

Para aplicar as migrations à base configurada:

```powershell
dotnet tool run dotnet-ef -- database update `
  --project .\SmartField.Infrastructure `
  --startup-project .\SmartField.Api `
  --context SmartFieldDbContext
```

## Executar localmente

API:

```powershell
dotnet run --project .\SmartField.Api --launch-profile https
```

```text
API:     https://localhost:7088
Swagger: https://localhost:7088/swagger
Health:  https://localhost:7088/health
```

Client:

```powershell
dotnet run --project .\SmartField.Client --launch-profile https
```

```text
Client: https://localhost:7084
```

## Endpoints principais

### Autenticação

```text
POST /api/auth/login
GET  /api/auth/me
```

### Funcionários

```text
GET  /api/employees?search=<texto>
GET  /api/employees/options?employeeId=<guid-opcional>
GET  /api/employees/{id}
POST /api/employees
PUT  /api/employees/{id}
POST /api/employees/{id}/user
```

### Locais de trabalho

```text
GET  /api/worksites?search=<texto>
GET  /api/worksites/{id}
POST /api/worksites
PUT  /api/worksites/{id}
GET  /api/attendance/worksites
```

### Projetos

```text
GET  /api/projects?search=<texto>
GET  /api/projects/{id}
POST /api/projects
PUT  /api/projects/{id}
```

### Geolocalização

```text
POST /api/geolocation/validate
GET  /api/geofence-settings
PUT  /api/geofence-settings
```

### Assiduidade

```text
GET  /api/attendance/state
GET  /api/attendance/today
GET  /api/attendance/history
GET  /api/attendance/day/{date}
POST /api/attendance/punch
GET  /api/attendance/admin/day
GET  /api/attendance/admin/day/{date}/employees/{employeeId}
POST /api/attendance/admin/events/{attendanceEventId}/corrections
GET  /api/attendance/admin/export.csv
```

### Auditoria

```text
GET /api/admin/audit
```

## PWA

O Client inclui manifest, ícones, service worker de desenvolvimento e publicação, layout móvel e layout de backoffice.

A sincronização offline de eventos de negócio ainda não está implementada.

## Testes

Os testes normais não necessitam de uma instância SQL Server:

```powershell
dotnet test .\Smartfield.sln --no-build --no-restore
```

Os testes que abrem ligações reais estão marcados com `Category=Integration`. Quando
`SMARTFIELD_TEST_CONNECTION_STRING` não está definida, são ignorados com uma mensagem
clara e a suite normal continua a executar.

Esta variável substitui `SMARTFIELD_TEST_SQLSERVER`, anteriormente utilizada pelos
testes de concorrência e persistência. Sem configuração não é escolhida uma instância
SQL automaticamente. Uma ligação configurada mas inválida faz os testes falharem.

Para executar também os testes de integração SQL, definir uma ligação para uma base de
testes acessível. Os testes de Infrastructure criam e removem bases descartáveis com
nomes únicos, pelo que a ligação deve permitir essas operações.

Exemplo com LocalDB em desenvolvimento:

```powershell
$env:SMARTFIELD_TEST_CONNECTION_STRING = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
dotnet test .\Smartfield.sln --no-build --no-restore
```

Pode ser utilizada outra instância SQL Server através da mesma variável. Não guardar
credenciais reais no repositório.

Para executar apenas os testes de integração configurados:

```powershell
dotnet test .\Smartfield.sln --filter "Category=Integration" --no-build --no-restore
```

Para voltar a executar sem SQL e consultar as mensagens dos testes ignorados:

```powershell
Remove-Item Env:SMARTFIELD_TEST_CONNECTION_STRING -ErrorAction SilentlyContinue
dotnet test .\Smartfield.sln --no-build --no-restore --logger "console;verbosity=normal"
```

Ou por projeto:

```powershell
dotnet test .\SmartField.Domain.Tests
dotnet test .\SmartField.Application.Tests
dotnet test .\SmartField.Infrastructure.Tests
dotnet test .\SmartField.Api.Tests
dotnet test .\SmartField.Integrations.Primavera.Tests
```

## Migrations

As migrations ficam em:

```text
SmartField.Infrastructure/Persistence/Migrations
```

Criar:

```powershell
dotnet tool run dotnet-ef -- migrations add <NomeDaMigration> `
  --project .\SmartField.Infrastructure `
  --startup-project .\SmartField.Api `
  --context SmartFieldDbContext `
  --output-dir Persistence\Migrations
```

Aplicar:

```powershell
dotnet tool run dotnet-ef -- database update `
  --project .\SmartField.Infrastructure `
  --startup-project .\SmartField.Api `
  --context SmartFieldDbContext
```

## Segurança de configuração

Não incluir no repositório passwords, tokens, API keys, segredos JWT, connection strings com credenciais reais, certificados privados ou dados reais sem necessidade.

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

O administrador de desenvolvimento é criado através do seed da aplicação. Na gestão de funcionários, um `Admin` pode criar a conta de login associada ao funcionário e escolher entre os perfis `Employee` e `Manager`. A criação de contas `Manager` fica reservada a utilizadores `Admin`.

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
- criar uma conta de login para o funcionário;
- escolher `Employee` ou `Manager` ao criar a conta;
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

- .NET SDK 8
- SQL Server ou SQL Server Express
- `dotnet-ef` 8 para gerir migrations

```powershell
dotnet tool install --global dotnet-ef --version 8.0.23
```

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

### Administrador de desenvolvimento

```powershell
dotnet user-secrets set "Seed:AdminPassword" "<password-local-segura>" --project .\SmartField.Api
```

Utilizador de demonstração:

```text
Email: admin@smartfield.local
Role: Admin
```

### Restaurar e compilar

```powershell
dotnet restore .\Smartfield.sln --configfile .\NuGet.Config
dotnet build .\Smartfield.sln --no-restore
```

### Aplicar migrations

```powershell
dotnet ef database update `
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

```powershell
dotnet test .\Smartfield.sln --no-build --no-restore
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
dotnet ef migrations add <NomeDaMigration> `
  --project .\SmartField.Infrastructure `
  --startup-project .\SmartField.Api `
  --context SmartFieldDbContext `
  --output-dir Persistence\Migrations
```

Aplicar:

```powershell
dotnet ef database update `
  --project .\SmartField.Infrastructure `
  --startup-project .\SmartField.Api `
  --context SmartFieldDbContext
```

## Segurança de configuração

Não incluir no repositório passwords, tokens, API keys, segredos JWT, connection strings com credenciais reais, certificados privados ou dados reais sem necessidade.

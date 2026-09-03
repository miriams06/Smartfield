# Sysprime SmartField

Sysprime SmartField é uma aplicação para controlo de assiduidade e gestão de equipas no terreno.

A solução separa a utilização móvel dos funcionários da área de backoffice e mantém a lógica de negócio e validações no servidor.

> Registar no terreno, validar no backoffice e preparar a integração com o PRIMAVERA.

## Funcionalidades atuais

### Área móvel do funcionário

O funcionário autenticado pode:

- consultar o estado atual da jornada;
- selecionar o local de trabalho onde se encontra;
- usar automaticamente o local habitual quando existe um `DefaultWorkSiteId`;
- receber um aviso quando o local habitual é aplicado por defeito;
- registar entrada, início de pausa, fim de pausa e saída;
- enviar a localização atual no momento da picagem;
- receber feedback quando a localização está fora da geofence;
- consultar o histórico diário de assiduidade;
- consultar entrada, saída, pausas e tempo trabalhado;
- consultar os locais de trabalho utilizados em cada dia;
- consultar o local associado a cada picagem;
- ver avisos de picagens fora da geofence.

A aplicação não faz tracking contínuo. A localização é pedida apenas quando uma operação necessita dela.

### Assiduidade

A assiduidade é baseada em eventos (`AttendanceEvent`) e preserva os registos originais.

Os eventos suportados são:

- `ClockIn`;
- `BreakStart`;
- `BreakEnd`;
- `ClockOut`.

A aplicação valida a sequência das picagens e calcula:

- hora de entrada;
- hora de saída;
- duração das pausas;
- número de pausas;
- minutos trabalhados;
- estado atual do funcionário.

Cada evento pode guardar:

- timestamp do servidor;
- timestamp enviado pelo cliente;
- latitude;
- longitude;
- precisão da localização;
- local de trabalho;
- projeto, quando aplicável;
- resultado da geofence;
- distância ao local de trabalho.

### Geolocalização e geofence

Os locais de trabalho (`WorkSite`) podem ter:

- código;
- nome;
- morada;
- latitude;
- longitude;
- raio de geofence;
- estado ativo/inativo;
- código de centro de custo ERP.

A distância entre a localização do funcionário e o local de trabalho é calculada no servidor através da fórmula de Haversine.

Existem três modos de geofence:

- `Disabled`: a geofence não bloqueia a operação;
- `Warning`: a picagem é aceite, mas fica assinalada quando está fora do raio;
- `Block`: a picagem é rejeitada quando não cumpre as regras configuradas.

O raio definido no próprio `WorkSite` tem precedência sobre o raio por defeito da empresa.

Os locais inativos não podem ser utilizados para validar novas picagens.

### Backoffice de assiduidade

Utilizadores `Admin` e `Manager` podem consultar a assiduidade diária das equipas.

A área de backoffice permite:

- filtrar por data;
- filtrar por funcionário;
- filtrar por local de trabalho;
- consultar entrada e saída;
- consultar total diário;
- consultar pausas;
- consultar estado atual;
- identificar dias com eventos fora da geofence;
- abrir o detalhe de um funcionário;
- consultar os eventos originais;
- consultar o local associado a cada evento;
- corrigir eventos de assiduidade sem apagar o evento original.

### Correções de assiduidade

Uma correção administrativa não altera nem elimina o `AttendanceEvent` original.

A aplicação guarda uma correção separada com:

- evento original;
- tipo original;
- timestamp original;
- novo tipo;
- novo timestamp;
- motivo;
- utilizador que efetuou a correção;
- data da correção.

Os cálculos e exportações podem aplicar a correção mais recente sem destruir o histórico original.

### Exportação CSV

O backoffice permite exportar assiduidade para CSV por período, com filtros opcionais por funcionário e local de trabalho.

O ficheiro inclui:

- `Date`;
- `EmployeeNumber`;
- `EmployeeName`;
- `ClockIn`;
- `ClockOut`;
- `BreakMinutes`;
- `WorkedMinutes`;
- `WorkSite`;
- `ProjectCode`;
- `GeofenceStatus`.

A exportação aplica as correções de assiduidade existentes.

### Gestão de funcionários

O backoffice permite:

- listar funcionários;
- pesquisar;
- criar;
- editar;
- ativar e desativar;
- definir o local de trabalho habitual;
- associar um utilizador existente;
- criar uma conta de login para um funcionário;
- atribuir automaticamente a role `Employee` à conta criada;
- guardar o código de funcionário do ERP.

### Gestão de locais de trabalho

O backoffice permite:

- listar locais;
- pesquisar;
- criar;
- editar;
- ativar e desativar;
- configurar coordenadas;
- configurar raio de geofence;
- configurar o código de centro de custo do ERP.

Também é possível configurar as regras gerais de geolocalização da empresa, incluindo:

- obrigatoriedade de localização;
- modo de geofence;
- raio por defeito.

### Gestão de projetos

O backoffice permite:

- listar projetos;
- pesquisar;
- criar;
- editar;
- definir tipo e estado;
- associar cliente;
- associar um local de trabalho;
- definir datas;
- guardar referências para integração ERP.

Os projetos e os locais de trabalho são conceitos distintos. A validação de geofence é feita sobre o `WorkSite` selecionado na picagem.

### Auditoria

A aplicação mantém registos de auditoria para operações relevantes.

Um `AuditLog` pode guardar:

- empresa;
- utilizador;
- tipo de entidade;
- identificador da entidade;
- ação;
- valores anteriores;
- valores novos;
- timestamp UTC.

São auditadas, entre outras, operações como:

- login administrativo;
- criação e alteração de funcionários;
- criação e alteração de locais de trabalho;
- criação de projetos;
- criação de eventos de assiduidade;
- correções de assiduidade.

A consulta administrativa está disponível através de:

```text
GET /api/admin/audit
```

### Integração e Outbox

A solução contém uma `IntegrationOutbox` para registar eventos destinados a sistemas externos sem tornar o funcionamento normal da aplicação dependente da disponibilidade desses sistemas.

Existem tipos de evento como:

- `AttendanceCreated`;
- `AttendanceCorrected`;
- `EmployeeCreated`;
- `EmployeeUpdated`;
- `ProjectCreated`.

A integração com PRIMAVERA está isolada no projeto `SmartField.Integrations.Primavera`.

Estão definidos contratos e DTOs para operações como:

- testar ligação;
- obter funcionários;
- obter um funcionário;
- obter projetos;
- obter centros de custo;
- enviar assiduidade.

A implementação atualmente registada é `NotConfiguredPrimaveraClient`. Isto significa que a arquitetura está preparada para a integração, mas não existe ainda comunicação real com um ERP PRIMAVERA.

### Logging e tratamento de erros

A API utiliza Serilog para logging em:

- consola;
- ficheiros diários em `logs/`.

Os ficheiros de log têm retenção limitada.

Cada pedido pode transportar um `CorrelationId`, utilizado também no logging e nas respostas de erro inesperado.

Existe middleware global para exceções não tratadas. Em vez de expor detalhes internos ao cliente, a API devolve `ProblemDetails` com um identificador de correlação que pode ser usado para localizar o erro nos logs.

### Multiempresa e segurança

A aplicação suporta isolamento por empresa através de `CompanyId`.

O `CompanyId` é obtido a partir da identidade autenticada. O browser não decide a empresa sobre a qual um pedido é executado.

A solução utiliza:

- ASP.NET Core Identity;
- autenticação JWT Bearer;
- roles `Admin`, `Manager` e `Employee`;
- policy `Backoffice` para `Admin` e `Manager`.

O acesso aos dados de negócio é restringido à empresa autenticada.

## Arquitetura

A solução está dividida em projetos com responsabilidades distintas:

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

### SmartField.Domain

Contém entidades, enums e regras de domínio. Não depende de EF Core, Infrastructure, Blazor ou PRIMAVERA.

### SmartField.Application

Contém serviços de aplicação, contratos, regras de negócio e abstrações de persistência para áreas como assiduidade, geolocalização, auditoria, Integration Outbox, funcionários, projetos e locais de trabalho.

### SmartField.Infrastructure

Contém as implementações técnicas, incluindo Entity Framework Core, SQL Server, ASP.NET Core Identity, stores, persistência de auditoria, persistência de outbox e migrations.

### SmartField.Integrations.Primavera

Isola os contratos e implementações relacionados com PRIMAVERA. O Client e o Domain não dependem de DLLs do PRIMAVERA.

### SmartField.Api

Contém controllers, autenticação e autorização, configuração de JWT, Swagger, CORS, health checks, logging, tratamento global de erros e composição de dependências.

### SmartField.Client

É uma aplicação Blazor WebAssembly PWA com área móvel para `Employee` e área de backoffice para `Admin` e `Manager`.

O Client comunica apenas com a API.

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

## Regras arquiteturais importantes

- O Client comunica apenas com a API.
- A validação de regras de negócio é feita no servidor.
- O `CompanyId` não é confiado a partir do browser.
- O frontend não comunica diretamente com PRIMAVERA.
- Uma falha do PRIMAVERA não deve impedir o funcionamento normal do SmartField.
- Integrações externas devem ser desacopladas através da `IntegrationOutbox` quando aplicável.
- Datas persistidas usam UTC, salvo necessidade explícita em contrário.
- `AttendanceEvent` é baseado em eventos e preserva o histórico original.
- Correções de assiduidade não apagam silenciosamente eventos existentes.
- A geolocalização é recolhida apenas no momento da operação necessária.
- Não existe tracking contínuo ou em background.

## Pré-requisitos

- .NET SDK 8
- SQL Server ou SQL Server Express
- `dotnet-ef` 8 para gerir migrations

Instalação do EF CLI:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.23
```

## Configuração local

### Clonar o repositório

```powershell
git clone https://github.com/miriams06/Smartfield.git
cd Smartfield
```

### Base de dados

Em desenvolvimento, a configuração por omissão usa:

```text
Server=.\SQLEXPRESS;Database=SmartFieldDb;Trusted_Connection=True;TrustServerCertificate=True
```

Para usar outra ligação, prefere `user-secrets`:

```powershell
dotnet user-secrets set "ConnectionStrings:SmartField" "Server=.\SQLEXPRESS;Database=SmartFieldDb;Trusted_Connection=True;TrustServerCertificate=True" --project .\SmartField.Api
```

### Administrador de desenvolvimento

O administrador de desenvolvimento só é criado quando `Seed:AdminPassword` está definido e o ambiente é `Development`.

```powershell
dotnet user-secrets set "Seed:AdminPassword" "<password-local-segura>" --project .\SmartField.Api
```

Utilizador de demonstração:

```text
Email: admin@smartfield.local
Role: Admin
```

A password não deve ser adicionada ao repositório.

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

### API

```powershell
dotnet run --project .\SmartField.Api --launch-profile https
```

```text
API:     https://localhost:7088
Swagger: https://localhost:7088/swagger
Health:  https://localhost:7088/health
```

### Client

```powershell
dotnet run --project .\SmartField.Client --launch-profile https
```

```text
Client: https://localhost:7084
```

O Client usa `SmartField.Client/wwwroot/appsettings.Development.json` para localizar a API.

As origens permitidas são configuradas em `Cors:AllowedOrigins` na API.

## Endpoints principais

### Autenticação

```text
POST /api/auth/login
GET  /api/auth/me
```

### Health check

```text
GET /health
```

### Funcionários

Policy `Backoffice`:

```text
GET  /api/employees?search=<texto>
GET  /api/employees/options?employeeId=<guid-opcional>
GET  /api/employees/{id}
POST /api/employees
PUT  /api/employees/{id}
POST /api/employees/{id}/user
```

### Locais de trabalho

Policy `Backoffice`:

```text
GET  /api/worksites?search=<texto>
GET  /api/worksites/{id}
POST /api/worksites
PUT  /api/worksites/{id}
```

Para o fluxo móvel de assiduidade:

```text
GET /api/attendance/worksites
```

### Projetos

Policy `Backoffice`:

```text
GET  /api/projects?search=<texto>
GET  /api/projects/{id}
POST /api/projects
PUT  /api/projects/{id}
```

### Geolocalização

```text
POST /api/geolocation/validate
```

### Configuração de geofence

Policy `Backoffice`:

```text
GET /api/geofence-settings
PUT /api/geofence-settings
```

### Assiduidade do funcionário

```text
GET  /api/attendance/state
GET  /api/attendance/today
GET  /api/attendance/history
GET  /api/attendance/day/{date}
POST /api/attendance/punch
```

### Assiduidade de backoffice

Policy `Backoffice`:

```text
GET  /api/attendance/admin/day
GET  /api/attendance/admin/day/{date}/employees/{employeeId}
POST /api/attendance/admin/events/{attendanceEventId}/corrections
GET  /api/attendance/admin/export.csv
```

### Auditoria

Policy `Backoffice`:

```text
GET /api/admin/audit
```

## Integração PRIMAVERA

A configuração prevista encontra-se na secção:

```json
{
  "Primavera": {
    "BaseUrl": "",
    "Company": "",
    "Username": "",
    "Password": "",
    "ApiKey": ""
  }
}
```

A integração é considerada configurada quando existe `BaseUrl`, `Company` e uma das seguintes formas de autenticação:

- `ApiKey`; ou
- `Username` + `Password`.

Credenciais reais devem ser fornecidas por configuração segura e nunca adicionadas ao repositório.

A implementação atual não efetua comunicação real com o ERP; quando a integração não está configurada, `NotConfiguredPrimaveraClient` devolve resultados controlados sem interromper o SmartField.

## PWA

O Client inclui:

- `manifest.webmanifest`;
- ícones;
- service worker de desenvolvimento;
- service worker de publicação;
- layout móvel;
- layout de backoffice.

O service worker publicado gere os assets estáticos da aplicação.

A sincronização offline de eventos de negócio ainda não está implementada.

## Testes

Executar todos os testes:

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

Criar uma migration:

```powershell
dotnet ef migrations add <NomeDaMigration> `
  --project .\SmartField.Infrastructure `
  --startup-project .\SmartField.Api `
  --context SmartFieldDbContext `
  --output-dir Persistence\Migrations
```

Aplicar migrations:

```powershell
dotnet ef database update `
  --project .\SmartField.Infrastructure `
  --startup-project .\SmartField.Api `
  --context SmartFieldDbContext
```

Listar migrations:

```powershell
dotnet ef migrations list `
  --project .\SmartField.Infrastructure `
  --startup-project .\SmartField.Api `
  --context SmartFieldDbContext
```

Não editar migrations que já tenham sido aplicadas em ambientes partilhados. Para alterações posteriores ao modelo, criar uma nova migration.

## Segurança de configuração

Não incluir no repositório:

- passwords;
- tokens;
- API keys;
- segredos JWT;
- connection strings com credenciais reais;
- certificados privados;
- dados reais de clientes ou funcionários sem necessidade.

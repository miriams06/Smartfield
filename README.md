# Sysprime SmartField

Sysprime SmartField é uma aplicação para controlo de assiduidade e gestão de equipas no terreno.

O princípio do produto é:

> Registar no terreno, validar no backoffice e integrar com o PRIMAVERA.

A aplicação funciona primeiro em modo standalone, mas a arquitetura está preparada para evoluir para gestão de obras, intervenções, ordens de trabalho, equipas, centros de custo, tempos por obra, materiais, despesas, deslocações e integração com PRIMAVERA ERP.

## Estado funcional atual

A solução já inclui:

- arquitetura por camadas;
- ASP.NET Core Web API;
- Blazor WebAssembly PWA;
- Entity Framework Core com SQL Server;
- migrations e seed de dados de demonstração;
- ASP.NET Core Identity;
- autenticação JWT;
- roles `Admin`, `Manager` e `Employee`;
- isolamento multiempresa por `CompanyId`;
- área móvel e área de backoffice;
- health check da API e do SQL Server;
- gestão de funcionários no backoffice:
  - listagem;
  - pesquisa;
  - criação;
  - edição;
  - ativação e desativação;
  - associação a utilizador;
  - associação a local habitual;
  - código de funcionário no ERP;
- gestão de locais de trabalho no backoffice:
  - listagem;
  - pesquisa;
  - criação;
  - edição;
  - ativação e desativação;
  - coordenadas de latitude e longitude;
  - raio de geofence;
  - código de centro de custo ERP;
- serviço de geolocalização:
  - integração com a Browser Geolocation API;
  - recolha pontual de latitude, longitude e precisão;
  - validação de permissões e indisponibilidade de localização;
  - cálculo de distância ao local de trabalho através de Haversine;
  - modos de geofence `Disabled`, `Warning` e `Block`;
  - validação sempre no servidor e no contexto da empresa autenticada.

Ainda não devem ser consideradas implementadas funcionalidades como:

- registo real de picagens;
- pausas e retomas;
- correções de assiduidade pelo backoffice;
- gestão de obras e intervenções;
- integração efetiva com PRIMAVERA;
- sincronização offline de eventos de negócio.

As entidades e abstrações existentes para estas áreas representam a fundação do produto, não necessariamente fluxos funcionais completos.

## Stack

- .NET 8
- ASP.NET Core Web API
- Blazor WebAssembly PWA
- Entity Framework Core 8
- SQL Server
- ASP.NET Core Identity
- JWT
- xUnit

## Estrutura da solução

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
└── SmartField.Api.Tests
```

### Responsabilidades dos projetos

- `SmartField.Domain`: entidades, enums e regras de domínio. Não depende de EF Core, Infrastructure, Blazor ou PRIMAVERA.
- `SmartField.Application`: casos de uso, contratos e regras de aplicação. Depende apenas de `SmartField.Domain`.
- `SmartField.Infrastructure`: persistência, EF Core, Identity, auditoria e outbox. Implementa contratos da Application.
- `SmartField.Integrations.Primavera`: abstrações e implementação futura da integração PRIMAVERA.
- `SmartField.Api`: composição da aplicação, autenticação, autorização, controllers e health checks.
- `SmartField.Client`: Blazor WebAssembly PWA. Comunica apenas com `SmartField.Api`.
- `*.Tests`: testes separados por camada.

## Regras arquiteturais essenciais

- O Client comunica apenas com a API.
- O frontend nunca comunica diretamente com PRIMAVERA.
- Não devem ser usadas DLLs do PRIMAVERA no Client.
- Lógica específica do PRIMAVERA não deve ser colocada diretamente nos Controllers.
- Uma indisponibilidade do PRIMAVERA não pode impedir o funcionamento normal do SmartField.
- Eventos destinados a sistemas externos devem utilizar `IntegrationOutbox`.
- As entidades de negócio relevantes pertencem a uma `Company`.
- O `CompanyId` é derivado da identidade autenticada e nunca deve ser confiado a partir do browser.
- Datas persistidas usam UTC, salvo justificação explícita.
- `AttendanceEvent` é um modelo baseado em eventos e preserva o histórico original.
- Correções não devem apagar silenciosamente eventos de assiduidade.
- Geolocalização só deve ser recolhida no momento da operação que a exige; não existe tracking contínuo ou em background.

## Multiempresa e segurança

O JWT inclui a empresa do utilizador autenticado. A API expõe essa informação através de `ICurrentCompanyProvider`, e o `SmartFieldDbContext` aplica filtros globais às entidades de negócio com `CompanyId`.

Mesmo com filtros globais, os casos de uso e queries relevantes devem continuar a restringir explicitamente os dados à empresa autenticada.

Nunca incluir no repositório:

- passwords;
- tokens;
- API keys;
- segredos JWT;
- connection strings com credenciais reais;
- certificados privados;
- dados reais de clientes ou funcionários sem necessidade.

## Pré-requisitos

- .NET SDK 8
- SQL Server ou SQL Server Express
- ferramenta `dotnet-ef` 8, caso ainda não esteja instalada

Instalação opcional do EF CLI:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.23
```

## Configuração local

### 1. Clonar o repositório

```powershell
git clone https://github.com/miriams06/Smartfield.git
cd Smartfield
```

### 2. Configurar o SQL Server

A configuração de desenvolvimento usa, por omissão:

```text
Server=.\SQLEXPRESS;Database=SmartFieldDb;Trusted_Connection=True;TrustServerCertificate=True
```

Quando a instância local for diferente, definir a connection string através de user-secrets em vez de colocar credenciais no ficheiro de configuração:

```powershell
dotnet user-secrets set "ConnectionStrings:SmartField" "Server=.\SQLEXPRESS;Database=SmartFieldDb;Trusted_Connection=True;TrustServerCertificate=True" --project .\SmartField.Api
```

### 3. Configurar o administrador de desenvolvimento

O seed de Identity só cria o administrador quando `Seed:AdminPassword` estiver definido e o ambiente for `Development`.

```powershell
dotnet user-secrets set "Seed:AdminPassword" "<password-local-segura>" --project .\SmartField.Api
```

Utilizador de demonstração:

```text
Email: admin@smartfield.local
Role: Admin
```

A password nunca deve ser adicionada ao repositório.

### 4. Restaurar e compilar

```powershell
dotnet restore .\Smartfield.sln --configfile .\NuGet.Config
dotnet build .\Smartfield.sln --no-restore
```

### 5. Aplicar as migrations

```powershell
dotnet ef database update `
  --project .\SmartField.Infrastructure `
  --startup-project .\SmartField.Api `
  --context SmartFieldDbContext
```

A base criada por omissão chama-se:

```text
SmartFieldDb
```

Dados de demonstração incluídos:

```text
Company: SYS-DEMO — SmartField Demo
Employee: FUNC001 — Funcionário Demo
```

O administrador de desenvolvimento é criado pela API depois de as migrations estarem aplicadas e de `Seed:AdminPassword` estar configurado.

## Executar localmente

Abrir dois terminais.

### API

```powershell
dotnet run --project .\SmartField.Api --launch-profile https
```

Endereços de desenvolvimento:

```text
API:     https://localhost:7088
Swagger: https://localhost:7088/swagger
Health:  https://localhost:7088/health
```

### Client

```powershell
dotnet run --project .\SmartField.Client --launch-profile https
```

Endereço de desenvolvimento:

```text
Client: https://localhost:7084
```

O Client usa `SmartField.Client/wwwroot/appsettings.Development.json` para localizar a API. As origens permitidas são configuradas na secção `Cors:AllowedOrigins` da API.

## Autenticação e autorização

Roles existentes:

- `Admin`: acesso administrativo ao backoffice;
- `Manager`: acesso operacional ao backoffice;
- `Employee`: acesso à área móvel de terreno.

A policy `Backoffice` permite acesso a `Admin` e `Manager`.

Em desenvolvimento, quando não existe `Jwt:SigningKey`, a API gera uma chave aleatória ao arrancar. Consequentemente, tokens emitidos antes de um reinício da API deixam de ser válidos. Em produção, `Jwt:SigningKey` é obrigatório e deve ser fornecido por configuração segura.

## Endpoints disponíveis

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

Protegidos pela policy `Backoffice`:

```text
GET  /api/employees?search=<texto>
GET  /api/employees/options?employeeId=<guid-opcional>
GET  /api/employees/{id}
POST /api/employees
PUT  /api/employees/{id}
```

### Locais de trabalho

Protegidos pela policy `Backoffice`:

```text
GET  /api/worksites?search=<texto>
GET  /api/worksites/{id}
POST /api/worksites
PUT  /api/worksites/{id}
```

### Geolocalização

Requer utilizador autenticado:

```text
POST /api/geolocation/validate
```

Pedido:

```json
{
  "latitude": 38.722252,
  "longitude": -9.139337,
  "accuracyMeters": 10,
  "workSiteId": "00000000-0000-0000-0000-000000000000"
}
```

O `workSiteId` deve ser o GUID real de um local de trabalho da empresa autenticada.

A resposta indica se a operação pode prosseguir, se a localização está dentro do raio e qual a distância calculada:

```json
{
  "isAccepted": true,
  "isInsideGeofence": true,
  "distanceFromWorkSiteMeters": 12.34,
  "geofenceMode": 1,
  "resultCode": "InsideGeofence",
  "message": "A localização está dentro do raio permitido."
}
```

A API nunca recebe `CompanyId` do Client para decidir a empresa. O contexto da empresa é obtido a partir do utilizador autenticado.

## Geolocalização e geofence

O Client usa `navigator.geolocation.getCurrentPosition`, através de um wrapper JavaScript em:

```text
SmartField.Client/wwwroot/js/smartfield-geolocation.js
```

O acesso à localização é pontual. Não existe `watchPosition`, tracking contínuo nem recolha em background.

O serviço trata os estados:

```text
success
permission-denied
position-unavailable
timeout
unsupported
unknown-error
```

A decisão de geofence é sempre feita no servidor. Os modos atuais são:

- `Disabled`: não bloqueia a operação;
- `Warning`: aceita, mas assinala localização fora do raio ou localização indisponível;
- `Block`: rejeita quando a localização não cumpre a regra definida.

A distância ao local de trabalho é calculada na Application através da fórmula de Haversine. O raio específico do `WorkSite`, quando definido, tem precedência sobre `CompanySettings.DefaultGeofenceRadiusMeters`.

A persistência de `Latitude`, `Longitude`, `LocationAccuracyMeters`, `DistanceFromWorkSiteMeters` e `IsInsideGeofence` pertence ao momento em que o respetivo `AttendanceEvent` for criado. O endpoint isolado de validação não cria eventos nem registos paralelos de geolocalização.

## PWA

O Client contém:

- `manifest.webmanifest`;
- ícones da aplicação;
- service worker de desenvolvimento;
- service worker de publicação;
- layout móvel;
- layout de backoffice.

O service worker publicado gere os assets estáticos da aplicação. A sincronização offline de eventos de negócio ainda não está implementada.

## Testes

Executar todos os testes:

```powershell
dotnet test .\Smartfield.sln --no-build --no-restore
```

Ou executar por projeto:

```powershell
dotnet test .\SmartField.Domain.Tests
dotnet test .\SmartField.Application.Tests
dotnet test .\SmartField.Infrastructure.Tests
dotnet test .\SmartField.Api.Tests
```

Antes de integrar alterações:

```powershell
dotnet restore .\Smartfield.sln --configfile .\NuGet.Config
dotnet build .\Smartfield.sln --no-restore
dotnet test .\Smartfield.sln --no-build --no-restore
git diff --check
git status
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

Listar migrations:

```powershell
dotnet ef migrations list `
  --project .\SmartField.Infrastructure `
  --startup-project .\SmartField.Api `
  --context SmartFieldDbContext
```

Não editar migrations já aplicadas em ambientes partilhados. Criar uma migration nova para alterações posteriores ao modelo.

## Fluxo de desenvolvimento

- Trabalhar um card de cada vez.
- Criar uma branch pequena e focada.
- Ler o card, descrição, checklist, dependências e critérios de conclusão antes de alterar código.
- Inspecionar primeiro o estado real do repositório.
- Não assumir que um card está implementado apenas porque existe no Planner.
- Não assumir que uma funcionalidade não existe sem verificar o código.
- Não fazer refactoring fora do scope do card.
- Preservar alterações locais não relacionadas.
- Executar build e testes antes de abrir ou concluir o pull request.
- Registar melhorias fora de scope no backlog.

Exemplo:

```powershell
git switch master
git pull --ff-only
git switch -c <nome-da-branch>
```

Depois da validação:

```powershell
git status
git diff --check
git push -u origin <nome-da-branch>
```

A integração em `master` deve ser feita através de pull request revisto.

## Fonte de verdade

O Microsoft Planner é a fonte oficial para:

- cards;
- backlog;
- prioridades;
- descrições;
- checklists;
- dependências;
- critérios de conclusão.

O repositório é a fonte oficial para:

- estado real da implementação;
- arquitetura existente;
- nomes de ficheiros, classes e namespaces;
- packages;
- migrations;
- contratos da API;
- testes.

Quando Planner e código divergirem, a diferença deve ser identificada sem destruir código funcional.

## Checklist de passagem a outro programador

Antes da passagem do projeto:

1. confirmar que `master` contém apenas alterações validadas;
2. executar restore, build e testes;
3. confirmar as migrations aplicadas na base de desenvolvimento;
4. partilhar os valores de configuração por um canal seguro, nunca pelo Git;
5. confirmar os URLs locais da API e do Client;
6. validar login, health check e funcionalidades já implementadas;
7. explicar as regras de isolamento por empresa;
8. explicar que a localização é recolhida apenas sob pedido e validada no servidor;
9. entregar o Planner juntamente com o acesso ao repositório;
10. listar decisões pendentes e dívida técnica fora do README, no backlog ou documentação apropriada.

## Resolução de problemas frequentes

### `Unable to locate a Local Database Runtime installation`

A connection string está a apontar para LocalDB, mas o runtime não está instalado. Usar a instância real disponível no SQL Server, por exemplo `.\SQLEXPRESS`, através de user-secrets.

### Erros `NU1100` ou `NU1603` durante o restore

Usar o `NuGet.Config` existente na raiz:

```powershell
dotnet restore .\Smartfield.sln --configfile .\NuGet.Config --force --no-cache
```

Quando necessário, limpar o cache:

```powershell
dotnet nuget locals all --clear
```

### O administrador de desenvolvimento não existe

Confirmar que:

- as migrations foram aplicadas;
- a API está em `Development`;
- `Seed:AdminPassword` está configurado em user-secrets;
- a API foi iniciada depois dessa configuração.

### O Client não comunica com a API

Confirmar:

- URL em `SmartField.Client/wwwroot/appsettings.Development.json`;
- API em execução;
- certificado HTTPS de desenvolvimento aceite;
- origem do Client incluída em `Cors:AllowedOrigins`.

### A geolocalização devolve `permission-denied`

Confirmar que o browser tem permissão de localização para a origem do Client e que a aplicação está a correr em HTTPS.

### `POST /api/geolocation/validate` devolve `400`

Confirmar que latitude e longitude são válidas e enviadas em conjunto, que `accuracyMeters` não é negativo e que `workSiteId`, quando enviado, é um GUID válido.

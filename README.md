# Sysprime SmartField

Sysprime SmartField é uma aplicação para controlo de assiduidade e gestão de equipas no terreno.

O princípio do produto é:

> Registar no terreno, validar no backoffice e integrar com o PRIMAVERA.

A primeira versão funciona em modo standalone, mas a solução nasce preparada para evoluir para integração com PRIMAVERA ERP, gestão de obras, intervenções, ordens de trabalho, equipas, centros de custo, tempos por obra, materiais, despesas e deslocações.

## Estado Atual

Este repositório contém a solução base do SmartField, criada no âmbito do card `S1.01 - Criar solução SmartField`.

O código atual é uma fundação técnica. Ainda não deve ser assumido que existem funcionalidades de negócio completas de assiduidade, autenticação, multiempresa, integração PRIMAVERA ou modelo de dados final.

## Stack

- .NET 8
- ASP.NET Core Web API
- Blazor WebAssembly PWA
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- JWT

## Estrutura da Solução

```text
Smartfield.sln
SmartField.Domain
SmartField.Application
SmartField.Infrastructure
SmartField.Integrations.Primavera
SmartField.Api
SmartField.Client
SmartField.Domain.Tests
SmartField.Application.Tests
```

### Projetos

- `SmartField.Domain`: regras e entidades de domínio. Não deve depender de Infrastructure, EF Core, Blazor ou PRIMAVERA.
- `SmartField.Application`: casos de uso, contratos e regras de aplicação. Depende de `SmartField.Domain`.
- `SmartField.Infrastructure`: persistência, Identity, auditoria e outbox. Depende de `SmartField.Application` e `SmartField.Domain`.
- `SmartField.Integrations.Primavera`: integração futura com PRIMAVERA através de abstrações/serviços dedicados. Depende de `SmartField.Application` e `SmartField.Domain`.
- `SmartField.Api`: ASP.NET Core Web API. É o ponto de entrada para o cliente e orquestra Application, Infrastructure e Integrations.
- `SmartField.Client`: Blazor WebAssembly PWA. Deve comunicar apenas com `SmartField.Api`.
- `SmartField.Domain.Tests`: testes do domínio.
- `SmartField.Application.Tests`: testes da camada de aplicação.

## Regras Arquiteturais Base

- O frontend nunca comunica diretamente com PRIMAVERA.
- O client não deve conter DLLs, SDKs ou lógica específica do PRIMAVERA.
- Controllers não devem conter lógica específica de integração PRIMAVERA.
- Uma indisponibilidade do PRIMAVERA não pode impedir o funcionamento normal do SmartField.
- Eventos destinados a sistemas externos devem passar por `IntegrationOutbox`.
- Todas as entidades de negocio relevantes devem pertencer a uma `Company`.
- O `CompanyId` deve ser derivado da identidade autenticada, nunca confiado a partir do browser.
- Datas persistidas devem usar UTC, salvo justificação explícita.
- `AttendanceEvent` deve ser um modelo baseado em eventos e preservar o histórico original.
- Correções a picagens devem ser auditadas e não apagar silenciosamente eventos originais.
- Geolocalização deve ser recolhida apenas no momento da picagem; não há tracking contínuo ou em background.

## Configuração

Existem ficheiros de configuração para ambientes de desenvolvimento e produção:

- `SmartField.Api/appsettings.Development.json`
- `SmartField.Api/appsettings.Production.json`
- `SmartField.Client/wwwroot/appsettings.Development.json`
- `SmartField.Client/wwwroot/appsettings.Production.json`

Não devem ser colocados no repositório passwords, tokens, API keys, segredos JWT, certificados privados ou connection strings com credenciais reais.

Em produção, a connection string e restantes segredos devem ser fornecidos por configuração segura do ambiente.

## Executar Localmente

Restaurar dependências:

```bash
dotnet restore Smartfield.sln
```

Compilar a solução:

```bash
dotnet build Smartfield.sln
```

Executar a API:

```bash
dotnet run --project SmartField.Api
```

Perfis atuais da API:

- HTTP: `http://localhost:5273`
- HTTPS: `https://localhost:7088`
- Swagger em desenvolvimento: `/swagger`

Executar o Client:

```bash
dotnet run --project SmartField.Client
```

Perfis atuais do Client:

- HTTP: `http://localhost:5046`
- HTTPS: `https://localhost:7084`

## Testes

Executar todos os testes:

```bash
dotnet test Smartfield.sln
```

Projetos de teste existentes:

- `SmartField.Domain.Tests`
- `SmartField.Application.Tests`

## Notas de Desenvolvimento

- Trabalhar um card de cada vez.
- O Microsoft Planner define backlog, prioridades, sprints, descrições, checklists, dependências e critérios de conclusão.
- O repositório define o estado real da implementação.
- Quando Planner e código divergirem, a divergência deve ser identificada sem destruir código funcional.
- Melhorias fora do scope do card atual devem ser registadas para backlog, não implementadas no mesmo card.

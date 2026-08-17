# BFA Platform

Fundação técnica da plataforma da BFA — Brazilian Footvolley Academy, uma rede de franquias de futevôlei. O backend começa como um monólito modular em .NET 10, com MVC e Razor Views no mesmo host que futuramente exporá a API para o aplicativo do aluno.

Esta etapa contém somente a fundação técnica. Ainda não há módulos de negócio, autenticação, tabelas ou conexão com banco de dados.

## Estrutura do monorepo

```text
backend/              Solution, projetos de aplicação e testes
brand/                Referências e ativos de identidade visual
database/             Migrations SQL versionadas e seeds controlados
docs/                 Arquitetura e decisões arquiteturais
infra/                Infraestrutura de implantação futura
mobile/student-app/   Aplicativo do aluno futuro
```

A solution `backend/BFA.sln` contém:

```text
src/BFA.Web
src/BFA.Application
src/BFA.Domain
src/BFA.Infrastructure
tests/BFA.UnitTests
tests/BFA.IntegrationTests
```

`BFA.Web` é o único projeto executável. Ele hospeda a interface MVC/Razor e os endpoints sob `/api/v1`.

## Pré-requisitos

- SDK .NET 10
- PostgreSQL para fases futuras; nenhum banco é necessário para executar esta fundação

## Executar a aplicação

Na raiz do repositório:

```powershell
dotnet restore backend/BFA.sln
dotnet run --project backend/src/BFA.Web/BFA.Web.csproj
```

Use a URL indicada no terminal. O diagnóstico da API está disponível em:

```text
GET /api/v1/health
```

## Build e testes

```powershell
dotnet build backend/BFA.sln
dotnet test backend/BFA.sln
```

## Configuração

A aplicação reconhece os ambientes `Development`, `Staging` e `Production` pelos arquivos `appsettings.{Environment}.json`. Configurações sensíveis devem vir de variáveis de ambiente ou .NET User Secrets; por exemplo, a futura conexão PostgreSQL usará a chave:

```text
ConnectionStrings__BfaDatabase
```

Nenhuma credencial real deve ser versionada.

## PostgreSQL e evolução de schema

Entity Framework Core com Npgsql será usado apenas para persistência em runtime. O schema de PostgreSQL será controlado por scripts SQL imutáveis e versionados em `database/migrations`.

A aplicação nunca executará `EnsureCreated`, `EnsureDeleted` ou `Database.Migrate` na inicialização. O deploy da aplicação e o deploy do schema são operações separadas.

Leia `AGENTS.md` e `docs/ARCHITECTURE.md` antes de implementar novas funcionalidades.

# BFA Platform

Plataforma operacional da BFA — Brazilian Footvolley Academy, uma rede de franquias de futevôlei. O backend é um monólito modular em .NET 10, com MVC e Razor Views no mesmo host que também expõe a API sob `/api/v1`.

O código é a fonte de verdade. As docs acompanham o estado real do repositório e precisam ser mantidas em sincronia com ele.

Hoje o sistema já cobre franqueadora, operação de unidade, área do professor e área do aluno. Professor e Aluno ainda estão em transição de escopo/UX, mas já existem como áreas funcionais.

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
- PostgreSQL quando um fluxo utilizar persistência; build e testes básicos não acessam um banco real

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

A aplicação reconhece os ambientes `Development`, `Staging` e `Production` pelos arquivos `appsettings.{Environment}.json`. Configurações sensíveis devem vir de variáveis de ambiente ou .NET User Secrets. A conexão PostgreSQL usa a chave:

```text
ConnectionStrings__BfaDatabase
```

Nenhuma credencial real deve ser versionada.

Em Development, configure `ConnectionStrings:BfaDatabase` com .NET User Secrets. Staging e Production usam a variável de ambiente `ConnectionStrings__BfaDatabase`. Consulte `docs/ENVIRONMENTS.md` para o procedimento completo.

## PostgreSQL e evolução de schema

Entity Framework Core com Npgsql é usado apenas para persistência em runtime dentro de `BFA.Infrastructure`. O schema é versionado por SQL manual em `database/migrations/`; a aplicação nunca executa `EnsureCreated`, `EnsureDeleted` ou `Database.Migrate` na inicialização.

O deploy da aplicação e o deploy do schema são operações separadas.

## Estado atual

- Foco principal: operação da unidade.
- Área do aluno: implementada, mas ainda em transição de escopo.
- Área do professor: implementada, ainda em transição.
- UI administrativa: padronizada por `docs/UI-ADMIN-STANDARDS.md`.

Leia `AGENTS.md` e `docs/ARCHITECTURE.md` antes de implementar novas funcionalidades.

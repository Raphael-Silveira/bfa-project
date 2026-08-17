# AGENTS.md — BFA Platform

## 1. Purpose

This repository contains the BFA — Brazilian Footvolley Academy platform.

This file is the operational constitution for Codex and other coding agents working in this repository.

Before changing code:

1. Read this file.
2. Read `docs/ARCHITECTURE.md`.
3. Inspect the existing code related to the task.
4. Make the smallest change that satisfies the task.

If a requested change conflicts with these rules, do not silently bypass the architecture.

---

## 2. Product Context

BFA is a footvolley franchise network.

The same platform supports:

- Public institutional website
- Franchise sales
- Unit discovery
- Championships and registrations
- Uniform/product storefront
- Franchisor administration
- Franchise/unit administration
- Teachers and staff
- Students and guardians
- Classes and schedules
- Enrollments
- Attendance
- Billing and payments
- Reports
- Future student mobile application

The web experience is built with ASP.NET Core MVC + Razor Views.

The future mobile application consumes API endpoints exposed by the same backend.

---

## 3. Repository Strategy

Use one monorepo.

Target structure:

```text
/
├── AGENTS.md
├── README.md
│
├── backend/
│   ├── BFA.sln
│   ├── src/
│   │   ├── BFA.Web/
│   │   ├── BFA.Application/
│   │   ├── BFA.Domain/
│   │   └── BFA.Infrastructure/
│   └── tests/
│       ├── BFA.UnitTests/
│       └── BFA.IntegrationTests/
│
├── database/
│   ├── migrations/
│   ├── seeds/
│   └── README.md
│
├── mobile/
│   └── student-app/        # future
│
├── docs/
│   ├── ARCHITECTURE.md
│   └── adr/
│
└── infra/
```

Do not create separate backend solutions for students, franchisees, championships, billing, or the public website.

Do not create microservices unless an accepted ADR explicitly authorizes it.

---

## 4. Technology Baseline

Backend/web baseline:

- .NET 10 LTS
- ASP.NET Core MVC
- Razor Views
- ASP.NET Core Web API controllers/endpoints in the same web host
- C#
- PostgreSQL
- Entity Framework Core 10 for runtime persistence
- Npgsql.EntityFrameworkCore.PostgreSQL 10
- OpenAPI for API endpoints

Production target:

- Linux VPS
- Ubuntu 24.04 LTS preferred
- ASP.NET Core/Kestrel
- Nginx reverse proxy
- systemd process management
- PostgreSQL

Do not introduce a SPA framework for the main web product by default.

JavaScript may be used for progressive enhancement where necessary, but server-rendered MVC/Razor is the default.

---

## 5. Architectural Style

The backend is a modular monolith with one deployable ASP.NET Core web host.

Dependency direction:

```text
BFA.Web
   |
   v
BFA.Application
   |
   v
BFA.Domain

BFA.Infrastructure
   |
   +------> BFA.Application
   |
   +------> BFA.Domain
```

Rules:

- `BFA.Domain` must not reference ASP.NET Core, EF Core, PostgreSQL, MVC, Razor, HTTP, or Infrastructure.
- `BFA.Application` must not depend on `BFA.Infrastructure`.
- `BFA.Web` must not contain business rules.
- MVC controllers must not query `DbContext` directly.
- API controllers/endpoints must not query `DbContext` directly.
- Razor Views must contain presentation logic only.
- Razor Views must never query the database.
- `BFA.Infrastructure` owns EF Core and external technical integrations.
- Domain entities are not ViewModels.
- Domain entities are not API DTOs.
- EF entities/domain entities must not be returned directly from API endpoints.

---

## 6. BFA.Web Structure

`BFA.Web` is the only executable web project initially.

Expected structure:

```text
BFA.Web/
├── Areas/
│   ├── Franqueadora/
│   │   ├── Controllers/
│   │   └── Views/
│   │
│   ├── Unidade/
│   │   ├── Controllers/
│   │   └── Views/
│   │
│   └── Aluno/
│       ├── Controllers/
│       └── Views/
│
├── Api/
│   └── V1/
│       └── Controllers/
│
├── Controllers/
├── Views/
│   ├── Home/
│   ├── Franquias/
│   ├── Unidades/
│   ├── Campeonatos/
│   ├── Loja/
│   └── Shared/
│
├── ViewModels/
├── ViewComponents/
├── TagHelpers/
├── wwwroot/
├── Program.cs
└── appsettings.json
```

### Public MVC

Public routes may include:

```text
/
 /quem-somos
 /franquias
 /unidades
 /campeonatos
 /loja
```

These routes are public presentation/entry points.

Transactional operations must call Application use cases.

### Area Franqueadora

Technical name and route prefix: `Franqueadora`.

Expected namespaces:

```text
BFA.Web.Areas.Franqueadora.Controllers
BFA.Web.Areas.Franqueadora.ViewModels
```

Audience:

- Network owners
- Franchisor administrators
- Network operations staff

### Area Unidade

Technical name and route prefix: `Unidade`.

Expected namespaces:

```text
BFA.Web.Areas.Unidade.Controllers
BFA.Web.Areas.Unidade.ViewModels
```

Audience:

- Franchise/unit owners
- Unit administrators
- Authorized staff
- Teachers where applicable

### Area Aluno

Technical name and route prefix: `Aluno`.

Expected namespaces:

```text
BFA.Web.Areas.Aluno.Controllers
BFA.Web.Areas.Aluno.ViewModels
```

Audience:

- Students
- Guardians/responsible users

### API

Future mobile clients use:

```text
/api/v1/...
```

The API lives in the same process initially but is logically separated from MVC routes.

---

## 7. Business Modules

Within Domain and Application, organize by business capability.

Initial capabilities:

```text
Identidade
Organizacoes
Unidades
Alunos
Responsaveis
Professores
Turmas
Matriculas
Presencas
Cobrancas
Campeonatos
Comercio
Relatorios
```

Do not create every module on day one.

Create a module when the corresponding feature is implemented.

Avoid global buckets such as:

```text
Helpers
Utils
Managers
GenericServices
```

unless there is a truly cross-cutting, clearly named concern.

### Technical and business naming

Standard .NET technical names remain in English. This includes:

```text
Domain
Application
Infrastructure
Web
Controllers
Views
ViewModels
```

Names that represent BFA business concepts must be written in Portuguese. C# identifiers and namespaces use Portuguese without diacritics when necessary and follow normal PascalCase conventions.

Required examples:

```text
Students      -> Alunos
Teachers      -> Professores
Classes       -> Turmas
Enrollments   -> Matriculas
Attendance    -> Presencas
Billing       -> Cobrancas
Championships -> Campeonatos
Reports       -> Relatorios
```

Technical suffixes stay in English when combined with a business name. Examples:

```text
AlunoController
AlunoViewModel
IAlunoRepository
BFA.Domain.Alunos
BFA.Application.Matriculas
```

The MVC Areas are named `Franqueadora`, `Unidade`, and `Aluno`. Controllers in these Areas must use the matching `[Area("...")]` value, namespace, folder, and route prefix.

---

## 8. MVC and Razor Standards

Use ASP.NET Core MVC with Razor Views as the default web UI architecture.

Rules:

- Controllers are thin.
- Controllers invoke Application use cases.
- Controllers map Application results to ViewModels.
- Views receive ViewModels designed for presentation.
- Views must not receive DbContext.
- Views must not perform business decisions.
- Partial Views are for reusable markup.
- View Components are preferred when reusable UI requires server-side data/work.
- Tag Helpers may be used for reusable HTML behavior.
- Avoid large JavaScript client-side business flows when normal MVC post/redirect/get solves the problem.
- Use POST/Redirect/GET for normal form submissions.
- Validate both server-side and client-side where appropriate, but server-side validation is authoritative.
- Use antiforgery protection for state-changing MVC forms.
- Use Areas to separate audience/workflow, not to duplicate business logic.

Do not build separate MVC applications for Franqueadora, Unidade, and Aluno unless a future operational requirement justifies it.

---

## 9. Domain Rules

Business rules belong in Domain or Application.

Examples:

- A unit administrator cannot access another unit without permission.
- A student enrollment must belong to an authorized unit.
- Payment state is decided by backend rules, never Razor or JavaScript.
- Championship registration rules are enforced by Application/Domain.
- Authorization is enforced by the server even if a navigation item is hidden.

Prefer explicit domain language.

Avoid premature generic repositories/base services.

---

## 10. Multi-Tenancy

The platform is multi-tenant from the first version.

Core hierarchy:

```text
Organizacao
└── Unidade
```

BFA example:

```text
Organizacao: BFA
├── Unidade: BFA Tietê
├── Unidade: BFA Sorocaba
└── Unidade: ...
```

Users may have multiple memberships:

```text
Usuario
└── VinculoUsuario
    ├── OrganizacaoId
    ├── UnidadeId (nullable for organization-wide access)
    └── Papel/Permissoes
```

Rules:

- Never trust `OrganizacaoId` or `UnidadeId` posted by a browser/mobile client as authorization.
- Resolve authorized tenant context on the server.
- Every tenant-scoped query must enforce tenant authorization.
- Network administrators may have organization-wide access.
- Unit users have only explicitly authorized unit access.
- Tenant isolation must be covered by integration tests.

Do not add global EF tenancy filters without an explicit architectural decision and tests.

---

## 11. PostgreSQL and ORM Policy — Critical

PostgreSQL is the persisted business database.

EF Core is allowed for runtime:

- SELECT
- INSERT
- UPDATE
- DELETE
- Mapping
- Change tracking
- Transactions
- LINQ queries

Npgsql is the PostgreSQL provider.

EF Core is NOT the authority for production schema deployment.

### Forbidden at application startup

Never add:

```csharp
Database.EnsureCreated();
Database.EnsureDeleted();
Database.Migrate();
```

The web application must start without modifying schema.

### Schema source of truth

Database structure is evolved through immutable SQL scripts:

```text
/database/migrations
```

Naming:

```text
V001__initial_schema.sql
V002__create_organizacoes.sql
V003__create_unidades.sql
V004__create_usuarios.sql
```

Rules:

- Never modify an applied shared migration.
- Never delete an applied migration.
- Fixes are new migrations.
- Every schema change is explicit SQL.
- Test in Development before Staging.
- Staging receives the same migration intended for Production.
- Production application deployment never silently runs DDL.

Destructive changes require explicit review:

```text
DROP TABLE
DROP COLUMN
TRUNCATE
ALTER COLUMN TYPE
broad DELETE
```

Prefer expand/migrate/contract.

### Database roles

Production must have separate roles.

`bfa_app`:

- Runtime application role
- DML only as required
- No schema ownership
- No CREATE/DROP/ALTER privileges

`bfa_deploy`:

- Controlled schema deployment role
- DDL privileges as required
- Credentials never available to the running web application

---

## 12. EF Core Mapping Standards

EF Core lives only in:

```text
BFA.Infrastructure/Persistence
```

Expected layout:

```text
Persistence/
├── BfaDbContext.cs
├── Configurations/
├── Repositories/
└── Transactions/
```

Rules:

- One primary `BfaDbContext` initially.
- Configure mappings with Fluent API.
- Avoid persistence attributes in Domain when Fluent API can express the mapping.
- Define string lengths intentionally.
- Define decimal precision intentionally.
- Define delete behavior intentionally.
- Avoid lazy loading.
- Avoid implicit cascade delete unless explicitly intended and tested.
- Avoid a generic repository abstraction over every entity merely to wrap EF.
- Application code must not reference EF Core types.

The SQL schema and EF mapping must remain synchronized through integration tests.

---

## 13. Environment Policy

First-class environments:

```text
Development
Staging
Production
```

Each has separate:

- PostgreSQL database
- PostgreSQL credentials
- Application secrets
- External service settings

Rules:

- Development never points to Production.
- Staging never shares the Production database.
- No production secrets in Git.
- Local secrets use environment variables or .NET User Secrets.
- Never log passwords, tokens, connection strings, payment secrets, or sensitive personal data.

---

## 14. Production Hosting

Preferred production baseline:

```text
Internet
   |
   v
Nginx :80/:443
   |
   v
ASP.NET Core / Kestrel
   |
   v
BFA.Web
   |
   v
PostgreSQL
```

`BFA.Web` runs as a systemd service.

Nginx handles the public HTTP/HTTPS edge and reverse proxies to Kestrel.

The application should be published with:

```bash
dotnet publish -c Release
```

Do not compile application code directly on Production as the normal deployment method.

Production deployment and database deployment remain separate controlled operations.

---

## 15. API Standards

API base route:

```text
/api/v1
```

Rules:

- Resource-oriented endpoints.
- Consistent Problem Details errors.
- Server-side authorization.
- Async I/O.
- CancellationToken where appropriate.
- No stack traces/internal exception details returned publicly.
- DTOs are separate from Domain entities and MVC ViewModels.

The future app must reuse backend use cases; do not duplicate aluno rules inside mobile code.

---

## 16. Application Layer

Application coordinates use cases.

Examples:

```text
CriarUnidade
CriarAluno
MatricularAluno
CriarTurma
RegistrarPresenca
GerarCobranca
RegistrarInscricaoCampeonato
CriarPedido
```

Rules:

- One clear purpose per use case.
- Interfaces for infrastructure capabilities live here when needed.
- No EF Core reference.
- No HttpContext dependency in business use cases.
- No static service locator.
- Do not introduce MediatR by default.
- Do not introduce AutoMapper by default.
- Prefer explicit mappings.

---

## 17. Domain Layer

Domain contains business concepts and invariants.

Rules:

- No database code.
- No MVC/Razor code.
- No HTTP code.
- No EF attributes by default.
- `decimal` for money.
- Persist timestamps in UTC.
- Model local timezone semantics explicitly for schedules.
- Use domain-specific names.

---

## 18. Authentication and Authorization

Authentication proves identity.
Authorization controls capabilities and tenant access.

The architecture must support:

- Administradores da franqueadora
- Administradores de unidade/franquia
- Staff
- Professores
- Alunos
- Responsaveis

Web MVC and future mobile API may use different authentication mechanisms over the same identity/user model.

Do not finalize token strategy until the Identity phase.

Authorization is always enforced server-side.

---

## 19. Testing

Projects:

```text
BFA.UnitTests
BFA.IntegrationTests
```

Unit tests cover:

- Domain rules
- Application rules
- Pure business behavior

Integration tests cover:

- Real PostgreSQL behavior
- EF mappings
- SQL/schema assumptions
- Tenant isolation
- Authorization
- Transactions
- Critical MVC/API flows

Do not use an in-memory provider as proof of PostgreSQL behavior.

Before reporting backend work complete:

```bash
dotnet build
dotnet test
```

Never claim they pass unless actually executed.

---

## 20. Coding Standards

- Nullable reference types enabled.
- Async database/network/file I/O.
- No `.Result` or `.Wait()` in application code.
- `Guid` identifiers unless an ADR changes the policy.
- C# names use normal .NET PascalCase conventions.
- .NET technical names remain in English; BFA business names use Portuguese without diacritics in identifiers and namespaces.
- PostgreSQL objects use `snake_case`.
- Prefer small cohesive classes.
- No unrelated refactors during feature work.
- No dead/commented implementation.
- No secrets in source.

---

## 21. Dependencies

Do not add packages casually.

Approved initial external persistence package:

```text
Npgsql.EntityFrameworkCore.PostgreSQL
```

Keep EF Core/Npgsql major versions aligned with .NET/EF 10.

Any material new dependency must have a clear reason.

Do not introduce a second ORM without an ADR.

---

## 22. Git Discipline

Keep changes small and reviewable.

Suggested commits:

```text
chore: create solution foundation
feat: add organization module
feat: add unit management
db: create organization and unit tables
fix: enforce unit authorization
test: cover cross-unit access
docs: record architecture decision
```

Do not commit/push unless explicitly requested.

Database changes and corresponding application mapping changes should be reviewable together.

---

## 23. ADRs

Material architecture changes require an ADR in:

```text
/docs/adr
```

Required for decisions such as:

- Replace EF Core
- Introduce another ORM
- Create another database
- Split into microservices
- Create another web backend
- Change tenancy strategy
- Change schema deployment strategy
- Introduce a message broker

ADR format:

```text
Status
Context
Decision
Consequences
```

---

## 24. Codex Working Protocol

Before coding:

1. Read `AGENTS.md`.
2. Read `docs/ARCHITECTURE.md`.
3. Inspect relevant code.
4. Keep scope narrow.

While coding:

- Do not put business rules in MVC controllers or Razor views.
- Do not access DbContext from controllers.
- Do not change schema without SQL migration.
- Do not run schema migration at application startup.
- Do not bypass tenant authorization.
- Add/update tests for important behavior.
- Avoid unnecessary dependencies.

After coding:

1. Run `dotnet build`.
2. Run `dotnet test`.
3. Report files changed.
4. Report tests actually executed.
5. Report database migrations created.
6. Report destructive/backward-incompatible changes explicitly.

---

## 25. Development Order

### Phase 0 — Foundation

```text
1. Monorepo folders
2. BFA.sln
3. BFA.Web MVC project
4. BFA.Application
5. BFA.Domain
6. BFA.Infrastructure
7. Unit/integration test projects
8. Project references
9. Basic MVC/Razor page
10. API health endpoint
11. PostgreSQL connectivity baseline
12. Database migration convention
13. Development/Staging/Production configuration baseline
```

No product modules yet.

### Phase 1 — Identity + tenancy

```text
Identidade
Organizacao
Unidade
Vinculo de usuario
Authorization
Tenant isolation
```

### Phase 2 — Core operation

```text
Alunos
Responsaveis
Professores
Turmas
Matriculas
Presencas
```

### Phase 3 — Finance

```text
Taxa de matricula
Cobrancas
Pagamentos
Inadimplencia
Relatorios
```

### Phase 4 — Network products

```text
Campeonatos
Inscricoes
Loja/uniformes
```

### Phase 5 — Mobile

```text
Aplicativo do aluno consumindo /api/v1
```

---

## 26. Definition of Done

Applicable work is Done only when:

- Architecture rules are respected.
- MVC/Razor concerns remain presentation-only.
- Tenant authorization is considered.
- Database changes are explicit/versioned in SQL.
- EF mapping matches PostgreSQL schema.
- Tests are updated.
- `dotnet build` passes.
- `dotnet test` passes.
- No secrets were introduced.
- Architectural/operational changes are documented.

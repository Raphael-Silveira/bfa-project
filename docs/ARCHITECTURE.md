# BFA Platform — Architecture v2

**Status:** Initial architecture  
**Repository:** Single monorepo  
**Backend:** Single .NET solution  
**Web:** ASP.NET Core MVC + Razor Views  
**API:** Same ASP.NET Core host under `/api/v1`  
**Target:** .NET 10 LTS  
**Database:** PostgreSQL  
**Runtime ORM:** Entity Framework Core + Npgsql  
**Schema management:** Versioned SQL scripts

---

## 1. Main Decision

BFA starts as one modular monolith and one deployable web process.

```text
                         BFA Platform
                              |
            +-----------------+------------------+
            |                 |                  |
            v                 v                  v
       Public Site       Admin Areas         Area Aluno
        MVC/Razor         MVC/Razor          MVC/Razor
            |                 |                  |
            +-----------------+------------------+
                              |
                              v
                         BFA.Web Host
                              |
                    +---------+----------+
                    |                    |
                    v                    v
                MVC Routes           /api/v1
                                         |
                                         v
                                  Future Mobile App

                              |
                              v
                       Application Layer
                              |
                              v
                           Domain
                              ^
                              |
                       Infrastructure
                              |
                     EF Core + Npgsql
                              |
                              v
                         PostgreSQL
```

The web app and future API share the same business core.

---

## 2. Solution

```text
backend/
└── BFA.sln
    ├── src/
    │   ├── BFA.Web
    │   ├── BFA.Application
    │   ├── BFA.Domain
    │   └── BFA.Infrastructure
    │
    └── tests/
        ├── BFA.UnitTests
        └── BFA.IntegrationTests
```

Only `BFA.Web` is executable initially.

---

## 3. BFA.Web

`BFA.Web` is responsible for HTTP delivery and UI composition.

```text
BFA.Web/
├── Areas/
│   ├── Franqueadora/
│   ├── Unidade/
│   └── Aluno/
├── Api/
│   └── V1/
├── Controllers/
├── Views/
├── ViewModels/
├── ViewComponents/
├── TagHelpers/
├── wwwroot/
└── Program.cs
```

### Root MVC site

Public brand experience:

- Home
- About BFA
- Franchise opportunity
- Units
- Campeonatos
- Store/uniforms
- Contact/login entry points

### Area: Franqueadora

Network-wide management.

Route prefix: `/Franqueadora`.

Controller namespace: `BFA.Web.Areas.Franqueadora.Controllers`.

### Area: Unidade

Franchise/unit operation.

Route prefix: `/Unidade`.

Controller namespace: `BFA.Web.Areas.Unidade.Controllers`.

### Area: Aluno

Student/guardian self-service.

Route prefix: `/Aluno`.

Controller namespace: `BFA.Web.Areas.Aluno.Controllers`.

### `/api/v1`

API contract for the future mobile application and future external integrations.

---

## 4. Why One Web Host?

The first version does not need four separately deployed web systems.

One host provides:

- One authentication boundary
- One deployment
- One configuration model
- Shared MVC infrastructure
- Shared Application/Domain layer
- Simple Contabo operation
- API readiness for mobile

Logical boundaries remain explicit through MVC Areas and Application modules.

If operational needs change later, a boundary can be extracted through an ADR.

### Naming standard

Standard .NET technical names remain in English:

```text
Domain
Application
Infrastructure
Web
Controllers
Views
ViewModels
```

BFA business names use Portuguese. C# namespaces and identifiers omit diacritics when necessary:

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

Technical suffixes remain in English when combined with a business concept, for example `AlunoController`, `AlunoViewModel`, and `IAlunoRepository`.

---

## 5. Project Responsibilities

### BFA.Domain

Business concepts and invariants only.

No EF Core.
No MVC.
No Razor.
No HTTP.

### BFA.Application

Business use cases and orchestration.

No EF Core.
No Razor.
No DbContext.

### BFA.Infrastructure

- EF Core
- Npgsql
- PostgreSQL persistence
- Authentication infrastructure
- Payments
- Email
- Storage
- External services

### BFA.Web

- MVC controllers
- Razor Views
- ViewModels
- API controllers
- Authentication/authorization middleware
- OpenAPI
- Dependency injection composition

No business rules in controllers/views.

---

## 6. MVC Request Flow

```text
Browser
   |
   v
MVC Controller
   |
   v
Application Use Case
   |
   +------> Domain
   |
   +------> Infrastructure port
                |
                v
              EF Core
                |
                v
           PostgreSQL
   |
   v
ViewModel
   |
   v
Razor View
   |
   v
HTML
```

Razor is a rendering layer, never a persistence/business layer.

---

## 7. Future Mobile Request Flow

```text
Mobile App
   |
   v
/api/v1
   |
   v
API Controller
   |
   v
Same Application Use Case
   |
   v
Same Domain / Infrastructure / PostgreSQL
```

No duplicate student backend is created.

---

## 8. Multi-Tenancy

```text
Organizacao
└── Unidade
```

User authorization uses memberships rather than a single hard-coded unit.

```text
Usuario
└── VinculoUsuario
    ├── OrganizacaoId
    ├── UnidadeId?
    └── Papel/Permissoes
```

Tenant authorization is server-side and mandatory.

---

## 9. PostgreSQL

One PostgreSQL database initially.

No database-per-franchise.

Tenant data remains logically isolated through keys + authorization.

Guidelines:

- UUID identifiers
- `snake_case`
- explicit foreign keys
- explicit indexes
- explicit nullability
- explicit delete behavior
- `numeric`/C# `decimal` for money
- UTC persisted timestamps

---

## 10. EF Core + Npgsql

EF Core is used for runtime persistence.

Provider:

```text
Npgsql.EntityFrameworkCore.PostgreSQL
```

EF Core may execute DML but does not automatically deploy schema.

`BFA.Web` composes persistence with a single `AddInfrastructure(builder.Configuration)` call. `BFA.Infrastructure` reads `ConnectionStrings:BfaDatabase` and registers `BfaDbContext` with `UseNpgsql`. The context initially has no `DbSet`.

```text
BFA.Web
   |
   v
Application
   |
   v
Infrastructure
   |
   v
EF Core + Npgsql
   |
   v
PostgreSQL
```

Forbidden application-startup behavior:

```csharp
Database.EnsureCreated();
Database.EnsureDeleted();
Database.Migrate();
```

---

## 11. Schema Deployment

Schema changes live in:

```text
database/migrations/
```

Example:

```text
V001__initial_schema.sql
V002__create_organization.sql
V003__create_unit.sql
V004__create_user_identity.sql
```

Process:

```text
Write reviewed SQL
       |
       v
Development
       |
       v
Integration tests
       |
       v
Staging
       |
       v
Production approval
       |
       v
Production
```

Runtime API credentials cannot perform DDL.

---

## 12. Production Layout — Contabo

Preferred host baseline:

```text
Contabo Linux VPS
└── Ubuntu 24.04 LTS
    ├── Nginx
    │   └── HTTPS / reverse proxy
    ├── BFA.Web
    │   └── ASP.NET Core 10 / Kestrel
    ├── systemd
    │   └── bfa-web.service
    └── PostgreSQL
```

PostgreSQL may later move to a separate host/service without changing the Application/Domain architecture.

---

## 13. Environments

```text
Development
Staging
Production
```

Each environment has its own database and credentials.

Conceptually:

```text
bfa_dev
bfa_staging
bfa_prod
```

Never share databases across environments.

---

## 14. Initial Modules

Modules are created only when needed.

Planned sequence:

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

---

## 15. First Codex Task

Create only the technical skeleton:

```text
backend/BFA.sln

backend/src/BFA.Web
backend/src/BFA.Application
backend/src/BFA.Domain
backend/src/BFA.Infrastructure

backend/tests/BFA.UnitTests
backend/tests/BFA.IntegrationTests

database/migrations
database/seeds
docs/adr
infra
mobile/student-app
```

Configure project references:

```text
BFA.Web
  -> BFA.Application
  -> BFA.Infrastructure

BFA.Application
  -> BFA.Domain

BFA.Infrastructure
  -> BFA.Application
  -> BFA.Domain

BFA.UnitTests
  -> BFA.Domain
  -> BFA.Application

BFA.IntegrationTests
  -> BFA.Web
  -> BFA.Infrastructure
```

Create:

- Basic MVC Home controller + Razor View
- Basic `/api/v1/health` endpoint
- No business entities
- No database tables
- No authentication yet
- No EF automatic migrations

Then prove:

```bash
dotnet build
dotnet test
```

Only after the foundation passes do we implement Identity and tenancy.

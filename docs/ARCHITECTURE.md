# BFA Platform — Architecture

**Status:** Initial architecture  
**Architecture style:** Modular monolith  
**Repository:** Monorepo  
**Backend:** Single .NET solution  
**Web:** ASP.NET Core MVC + Razor Views  
**Target framework:** .NET 10 LTS  
**Database:** PostgreSQL  
**Runtime ORM:** Entity Framework Core  
**Schema management:** Versioned SQL, independent of application startup

---

## 1. Objective

BFA — Brazilian Footvolley Academy is not only a software product.
It is the digital platform that supports a footvolley franchise network.

The architecture must support the business without forcing early microservice complexity.

The first version must be:

- Safe to deploy
- Easy to understand
- Friendly to incremental development with Codex
- Multi-tenant from day one
- Ready for different client applications
- Explicit about database changes
- Testable
- Capable of evolving without rewriting the entire platform

---

## 2. Core Architectural Decision

BFA starts as a modular monolith.

There is one backend application boundary and one primary relational database.

```text
                          ┌──────────────────┐
                          │   Public Site    │
                          └──────────────────┘

┌──────────────────┐      ┌──────────────────┐      ┌──────────────────┐
│   Admin Areas    │      │    Area Aluno    │      │   App do Aluno   │
│                  │      │                  │      │      Future      │
└────────┬─────────┘      └────────┬─────────┘      └────────┬─────────┘
         │                         │                         │
         └─────────────────────────┼─────────────────────────┘
                                   │ HTTPS / JSON
                                   ▼
                         ┌─────────────────────┐
                         │       BFA.Web       │
                         │                     │
                         │  Modular Monolith   │
                         └──────────┬──────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │     PostgreSQL      │
                         └─────────────────────┘
```

Separate user experiences do not require separate business backends.

The mobile application must consume the same API rather than reimplement business rules.

---

## 3. Why Not Microservices Initially?

The domain is still being discovered.

Splitting services now would introduce:

- Distributed transactions
- Multiple deployments
- Service-to-service authentication
- More observability requirements
- Contract/version management between services
- More infrastructure
- More complex local development
- Higher cost when changing domain boundaries

The modular monolith gives us clear internal boundaries without distributed-system overhead.

A module may become a separate service later only when there is a concrete operational or organizational reason.

---

## 4. Monorepo Layout

Target repository:

```text
bfa-platform/
│
├── AGENTS.md
├── README.md
│
├── backend/
│   ├── BFA.sln
│   │
│   ├── src/
│   │   ├── BFA.Web/
│   │   ├── BFA.Application/
│   │   ├── BFA.Domain/
│   │   └── BFA.Infrastructure/
│   │
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
│   └── student-app/
│
├── docs/
│   ├── ARCHITECTURE.md
│   └── adr/
│
└── infra/
```

The repository may contain empty placeholders for future clients, but code should only be created when needed.

---

## 5. Backend Solution

`backend/BFA.sln` contains the backend projects.

```text
BFA.sln
│
├── BFA.Web
├── BFA.Application
├── BFA.Domain
├── BFA.Infrastructure
├── BFA.UnitTests
└── BFA.IntegrationTests
```

The database scripts are deliberately outside the solution's runtime projects.

This reinforces the rule that application startup does not own production schema mutation.

---

## 6. Project Responsibilities

### BFA.Domain

Contains business concepts and invariants.

Examples:

```text
Organizacao
Unidade
Aluno
Professor
Turma
Matricula
Presenca
Cobranca
Campeonato
Pedido
```

Domain must not know about:

```text
ASP.NET Core
Entity Framework Core
PostgreSQL
HTTP
JWT
Payment SDKs
Email providers
UI
```

---

### BFA.Application

Contains application use cases and interfaces to external capabilities.

Examples:

```text
CriarUnidade
CriarAluno
MatricularAluno
AgendarTurma
RegistrarPresenca
GerarCobranca
RegistrarInscricaoCampeonato
```

Application coordinates the domain.

It can define ports/interfaces that Infrastructure implements.

Examples:

```text
IAlunoRepository
IUnidadeRepository
IPaymentGateway
IEmailSender
IClock
ICurrentUser
ITenantContext
```

Avoid interfaces that exist only to wrap one line of code.

---

### BFA.Infrastructure

Contains technical implementations.

Initial areas:

```text
Persistence/
Authentication/
Payments/
Email/
Storage/
```

EF Core lives here.

Example:

```text
BFA.Infrastructure/
└── Persistence/
    ├── BfaDbContext.cs
    ├── Configurations/
    ├── Repositories/
    └── Transactions/
```

Infrastructure references Application/Domain abstractions, never the opposite.

---

### BFA.Web

Contains the single HTTP host, MVC/Razor presentation, and API endpoints.

MVC Areas:

```text
BFA.Web/
└── Areas/
    ├── Franqueadora/
    │   ├── Controllers/
    │   └── Views/
    ├── Unidade/
    │   ├── Controllers/
    │   └── Views/
    └── Aluno/
        ├── Controllers/
        └── Views/
```

Area names, `[Area("...")]` values, namespaces, and route prefixes must match:

```text
Franqueadora -> BFA.Web.Areas.Franqueadora.Controllers -> /Franqueadora
Unidade      -> BFA.Web.Areas.Unidade.Controllers      -> /Unidade
Aluno        -> BFA.Web.Areas.Aluno.Controllers        -> /Aluno
```

Responsibilities:

```text
Routing
Authentication middleware
Authorization policies
Request validation boundary
Serialization
Problem Details
OpenAPI
Dependency injection composition
```

API endpoints/controllers must stay thin.

Forbidden pattern:

```text
Controller
  └── DbContext
       └── business rule
```

Expected pattern:

```text
Endpoint
  └── Application use case
       ├── Domain
       └── Infrastructure through interfaces
```

### Technical and business naming standard

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

BFA business names use Portuguese. C# identifiers and namespaces omit diacritics when necessary and use PascalCase:

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

Technical suffixes stay in English when combined with a business concept, for example `AlunoController`, `AlunoViewModel`, and `IAlunoRepository`.

---

## 7. Internal Modularization

The backend is layered by projects and modularized by business capability.

Example:

```text
BFA.Domain/
├── Organizacoes/
├── Unidades/
├── Alunos/
├── Professores/
├── Turmas/
└── Matriculas/

BFA.Application/
├── Organizacoes/
├── Unidades/
├── Alunos/
├── Professores/
├── Turmas/
└── Matriculas/
```

A feature should live close to its business capability.

Avoid a giant global folder structure such as:

```text
Services/
Repositories/
Models/
Helpers/
Utils/
```

where unrelated domains become mixed.

---

## 8. Initial Domain Boundaries

These boundaries are provisional and may evolve through ADRs.

### Identidade

Authentication identity and account lifecycle.

### Organizacoes

Represents the franchise network/legal operational umbrella.

Initial BFA instance:

```text
BFA — Brazilian Footvolley Academy
```

### Unidades

Represents physical/operational franchise locations.

Examples:

```text
BFA Tietê
BFA Sorocaba
BFA Campinas
```

### Alunos

Student profile and lifecycle.

### Professores

Teachers, coaches, and teaching relationships.

### Turmas

Class definitions, schedules, capacity, and instructors.

### Matriculas

Relationship between students and classes/units, including enrollment state.

### Presencas

Class attendance and participation records.

### Cobrancas

Enrollment fees, monthly charges, payments, status, and financial rules.

### Campeonatos

Events, categories, registrations, participants, and competition-related workflows.

### Comercio

Uniform/product catalog, orders, and fulfillment.

### Relatorios

Read-oriented reporting over authorized organization/unit data.

Reporting must not bypass tenant authorization.

---

## 9. Multi-Tenant Model

BFA is multi-tenant by organization and unit.

Base hierarchy:

```text
Organizacao
└── Unidade
```

A user is not modeled as permanently belonging to exactly one unit.

Instead:

```text
Usuario
└── VinculoUsuario
    ├── OrganizacaoId
    ├── UnidadeId?
    ├── Papel
    └── Permissoes
```

Examples:

Franchisor administrator:

```text
OrganizacaoId = BFA
UnidadeId = null
Papel = AdministradorFranqueadora
```

Unit administrator:

```text
OrganizacaoId = BFA
UnidadeId = BFA Tietê
Papel = AdministradorUnidade
```

Teacher:

```text
OrganizacaoId = BFA
UnidadeId = BFA Tietê
Papel = Professor
```

This allows future scenarios where one user works with more than one unit.

---

## 10. Tenant Isolation

Tenant isolation is mandatory at the API/Application boundary.

Every tenant-scoped use case must know the authorized tenant context.

A client-provided `UnidadeId` is input, not authorization.

Conceptually:

```text
Authenticated User
        │
        ▼
Memberships / Permissions
        │
        ▼
Authorized Tenant Context
        │
        ▼
Application Use Case
        │
        ▼
Tenant-scoped persistence query
```

The system must have integration tests proving that a user from Unit A cannot read or mutate Unit B data.

This is a release-blocking security requirement.

---

## 11. Database Strategy

PostgreSQL is the relational database.

There is one primary database initially.

Do not create a database per unit.

Tenant separation is logical through organization/unit identifiers and authorization.

The schema should use:

```text
uuid identifiers
snake_case database names
explicit foreign keys
explicit indexes
explicit nullability
explicit numeric precision
explicit delete behavior
```

Money values use PostgreSQL `numeric` mapped to C# `decimal`.

Timestamps are persisted in UTC.
For business schedules, local timezone semantics must be explicit.

---

## 12. EF Core Decision

EF Core remains the runtime ORM.

Reasons:

- Productive LINQ querying
- Change tracking for transactional use cases
- Mature PostgreSQL integration
- Explicit Fluent API mappings
- Familiar .NET ecosystem integration

However, EF Core is not allowed to mutate production schema automatically.

The concern being solved is not "ORM versus no ORM".
It is separating runtime persistence from schema deployment.

```text
Runtime:
BFA API
   │
   ▼
EF Core
   │
   ▼
SELECT / INSERT / UPDATE / DELETE
   │
   ▼
PostgreSQL


Schema deployment:
Versioned SQL
   │
   ▼
Controlled DB deployment
   │
   ▼
CREATE / ALTER / DROP
   │
   ▼
PostgreSQL
```

---

## 13. Schema Migration Strategy

Schema source of truth:

```text
/database/migrations
```

Example:

```text
V001__initial_schema.sql
V002__create_organizacao.sql
V003__create_unidade.sql
V004__create_identidade.sql
V005__create_aluno.sql
```

Scripts are append-only after reaching a shared environment.

Never change history to make the current model look cleaner.

Correct approach:

```text
V010__add_documento_aluno.sql
V011__correct_indice_documento_aluno.sql
```

Incorrect approach:

```text
edit V010 after Staging/Production received it
```

---

## 14. Safe Database Evolution

Prefer additive evolution.

Example:

Instead of immediately renaming/removing a production column:

```text
1. Add new column
2. Deploy compatible application code
3. Backfill/migrate data
4. Verify
5. Stop using old column
6. Remove old column in a later reviewed migration
```

This is the expand/migrate/contract pattern.

Destructive operations must be visible and intentional.

---

## 15. Database Credentials

Production separation:

```text
bfa_app
```

Used by the API.
DML permissions only as required.

```text
bfa_deploy
```

Used by the database deployment process.
DDL privileges.

The API deployment must not have access to `bfa_deploy` credentials.

This reduces the blast radius of an application or coding mistake.

---

## 16. Environments

Three first-class environments:

```text
Development
Staging
Production
```

Each has its own:

```text
Database
Credentials
Secrets
Configuration
External service settings
```

No environment shares a database with Production.

Deployment progression:

```text
Development
    │
    ▼
Staging
    │
    ▼
Production
```

Database scripts must follow the same progression.

---

## 17. API Contract

Initial API version:

```text
/api/v1
```

Example resources:

```text
/api/v1/unidades
/api/v1/alunos
/api/v1/turmas
/api/v1/matriculas
/api/v1/cobrancas
/api/v1/campeonatos
```

The route does not grant access.
Authorization is always evaluated in the backend.

Errors use a consistent Problem Details format.

---

## 18. Authentication Strategy

The exact identity implementation will be finalized before the Identity module is implemented.

Architectural requirements are already fixed:

- API-based authentication
- Supports web and future mobile clients
- Supports refresh/session lifecycle appropriate to clients
- Server-side authorization
- Organization/unit membership
- Role/permission policies
- Revocation capability
- No authorization based only on frontend state

Authentication implementation must not force the mobile app and web application to have different business identities.

---

## 19. Frontend Architecture

### Area Franqueadora

Audience:

```text
Administradores da franqueadora
Equipe de operacoes da rede
```

### Area Unidade

Audience:

```text
Administradores de unidade/franquia
Equipe operacional
Professores quando aplicavel
```

### Area Aluno

Audience:

```text
Alunos
Responsaveis
```

### Aplicativo do aluno

Future mobile application.

The mobile application should reuse API capabilities already exposed for the aluno experience whenever the interaction model allows it.

No frontend directly accesses PostgreSQL.

---

## 20. Public Website

The public BFA site is a logically separate presentation surface in `BFA.Web` from the operational Areas.

Expected public capabilities may include:

```text
Brand/institutional pages
Units
Franchise sales
Championship discovery
Championship registration entry points
Uniform/store entry points
Login entry points
```

Where public actions become transactional, they must use BFA API endpoints rather than own independent business logic.

---

## 21. Testing Architecture

### Unit Tests

Focus:

```text
Domain invariants
Application rules
Permission decisions that do not require DB integration
Pure calculations
```

### Integration Tests

Focus:

```text
PostgreSQL behavior
EF Core mappings
Foreign keys
Indexes relevant to behavior
Transactions
API authentication/authorization
Tenant isolation
Critical endpoint flows
```

A fake/in-memory provider must not be treated as proof that PostgreSQL-specific persistence behavior works.

---

## 22. Coding Philosophy

The project optimizes for clarity and controlled evolution.

Prefer:

```text
Explicit code
Small changes
Clear names
Few dependencies
Tests around critical boundaries
Immutable database history
Documented decisions
```

Avoid:

```text
Framework magic
Premature microservices
Generic abstractions without a use case
Large unrelated refactors
Automatic production schema changes
Business rules in frontend/controllers
```

---

## 23. Architecture Decision Records

Important architecture changes are documented in `docs/adr`.

Initial ADR candidates:

```text
0001-use-modular-monolith.md
0002-use-ef-core-for-runtime-persistence.md
0003-manage-schema-with-versioned-sql.md
0004-use-organization-unit-multitenancy.md
```

An ADR is required before materially reversing these choices.

---

## 24. Deployment Principle

Application deployment and database schema deployment are related but separate operations.

A future pipeline should conceptually support:

```text
Build
  │
  ▼
Automated Tests
  │
  ▼
Deploy application candidate to Staging
  │
  ▼
Apply reviewed DB migration to Staging when required
  │
  ▼
Validate
  │
  ▼
Controlled Production DB migration
  │
  ▼
Production application deployment
```

Exact ordering can vary for backward-compatible expand/contract deployments.

The API must not run migrations at startup.

---

## 25. Initial Delivery Plan

### Phase 0 — Foundation

Deliver:

```text
Monorepo
BFA.sln
Project references
AGENTS.md
ARCHITECTURE.md
Build baseline
Test baseline
Configuration baseline
Database folder/migration convention
```

No aluno, turma, cobranca, or campeonato feature yet.

### Phase 1 — Tenant Foundation

Deliver:

```text
Identidade foundation
Organizacao
Unidade
Vinculo de usuario
Tenant context
Authorization
Tenant isolation integration tests
```

### Phase 2 — Academic Operation

Deliver:

```text
Aluno
Professor
Turma
Matricula
Presenca
```

### Phase 3 — Financial Operation

Deliver:

```text
Taxa de matricula
Cobrancas
Pagamentos
Inadimplencia
Views/relatorios financeiros
```

### Phase 4 — Network Experiences

Deliver:

```text
Campeonatos
Inscricoes
Comercio/uniformes
Experiencia do aluno
```

### Phase 5 — Mobile

Deliver the aplicativo do aluno without duplicating backend business logic.

---

## 26. First Technical Task

The first Codex implementation task should create only the repository/backend skeleton.

It should not create domain features yet.

Expected output:

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
```

It should also configure valid project references and prove the baseline with:

```bash
dotnet build
dotnet test
```

Only after this baseline is clean should the first business capability be introduced.

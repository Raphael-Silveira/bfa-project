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

Global administrative experience for an organization.

Main route: `/franqueadora`.

Controller namespace: `BFA.Web.Areas.Franqueadora.Controllers`.

Access requires the `AdministradorRede` policy backed by an active organization-wide `VinculoAcesso`. `AdministradorUnidade` and the operational profiles do not receive access to this global area. The initial dashboard resolves the organization from the authenticated user's links and shows only real counts for units and active administrative links.

When a user administers more than one organization, the area returns a controlled selection-pending state instead of choosing a context implicitly. Organization selection is not implemented yet.

`GET /conta/admin-rede` remains a temporary authorization diagnostic endpoint and is not the Franqueadora experience.

An `AdministradorRede` manages Units only inside the Organization resolved from that user's active organization-wide access link. Unit listing, lookup, editing, activation, and deactivation are always scoped by `OrganizacaoId`; resource operations combine `OrganizacaoId` and the Unit identifier so another Organization's Unit is reported as not found. `OrganizacaoId` is never accepted from an MVC form as authorization context.

The initial Unit management routes live under `/franqueadora/unidades`. Units are created active and may be activated or deactivated, but are never physically deleted in this phase. The `(organizacao_id, slug)` database constraint remains the definitive uniqueness protection, with an application pre-check for a friendly validation response.

An `AdministradorRede` may assign an existing `UsuarioIdentity` as `AdministradorUnidade` in one or more Units owned by the current Organization. Access management always scopes queries and mutations by both `OrganizacaoId` and `UnidadeId`, uses the existing `VinculoAcesso`, and activates or deactivates links without physical deletion. An inactive equivalent link is reactivated instead of duplicated. The Unit access screen continues to require an existing user and never provisions one implicitly.

Franchisor user management lives at `GET /franqueadora/usuarios`, `GET /franqueadora/usuarios/novo`, `POST /franqueadora/usuarios/novo`, `GET /franqueadora/usuarios/{usuarioId}/editar`, and `POST /franqueadora/usuarios/{usuarioId}/editar`. All routes require `AdministradorRede`; the active organization-wide access link supplies the Organization context, and no `OrganizacaoId` is accepted from the browser. The listing combines organization-scoped `VinculoAcesso` records with `FranqueadoUsuario` relationships to identify people and their functions, removes duplicates produced by multiple links, and falls back to the Identity email for bootstrap administrators that do not yet have a `PerfilUsuario`. Its "Acesso às unidades" column is derived exclusively from active `VinculoAcesso` records; `FranqueadoUnidade` never grants or implies authorization in that listing.

User editing changes only the global name, login email, and contact phone. Application and Infrastructure require an active relationship between the target user and the current Organization, through `VinculoAcesso` and/or an active `FranqueadoUsuario`/`Franqueado`; an identifier from another tenant is reported as not found. A user with active relationships in more than one Organization receives a controlled conflict because `UsuarioIdentity` and `PerfilUsuario` are global. Infrastructure revalidates this scope inside the same explicit transaction used by `UserManager<UsuarioIdentity>` and `BfaDbContext`, keeps `Email` and `UserName` synchronized through Identity, and creates `PerfilUsuario` only on a valid POST when a bootstrap user does not yet have one. Editing does not mutate `VinculoAcesso`, `FranqueadoUsuario`, or `FranqueadoUnidade`.

`TipoCadastroUsuario` is an Application-only orchestration choice, not a persisted user classification. The current choices are `AdministradorRede` and `Franqueado`. Application validates the tenant context and selected Units, creates the Domain graph, and sends one aggregate to Infrastructure. Infrastructure uses the shared `BfaDbContext` and `UserManager<UsuarioIdentity>` inside one explicit database transaction, so Identity, `PerfilUsuario`, commercial relationships, Unit relationships, and access links either commit together or roll back together.

The mandatory administrative UI standard is documented in `docs/UI-ADMIN-STANDARDS.md`; `docs/ADMIN-VISUAL.md` remains its concise implementation companion. Area-specific styling extends that shared contract without placing administrative rules in the global public-site stylesheet.

### Area: Unidade

Franchise/unit operation.

Context route: `/unidade/{unidadeId}`.

Controller namespace: `BFA.Web.Areas.Unidade.Controllers`.

The first version is available to an active `AdministradorUnidade` only for the exact Organization/Unit pair in its active `VinculoAcesso`. An `AdministradorRede` keeps transversal superaccess to active Units inside its own Organization, but never to another Organization. The route identifier is not authorization: Web resolves the active Unit and Organization from the current persistence source, creates `ContextoUnidade`, and invokes the existing resource-based `AcessoUnidadePorPerfilRequirement` on every request.

The initial dashboard identifies the current Unit and renders only data backed by implemented modules. It reuses the shared Admin Shell, drawer, logout, responsive behavior, and visual tokens. Its navigation contains `Visão Geral` and the read-only `Contrato`; operational UI and use cases for Professores, Alunos, Turmas, Matriculas, Presencas, and finance are not created by this first version.

The Unit dashboard now includes a read-only summary of the active franchise contract, and `GET /unidade/{unidadeId}/contrato` exposes its current terms and the documents from the vigente version. Authorized document visualization and download are streamed through private storage routes under that same Unit context; storage keys and physical paths are never exposed. The server revalidates the authenticated user's active Unit access, Organization, active `FranqueadoUnidade`, active `ContratoFranquia`, and vigente version. This experience offers no contract creation, editing, upload, versioning, formalization, cancellation, or closing actions.

Users with one active `AdministradorUnidade` context are sent directly to `/unidade/{unidadeId}` after login. Users with more than one active Unit are sent to `GET /selecionar-unidade`; `POST /selecionar-unidade` requires antiforgery and revalidates the selected Unit against the authenticated user's current active links. The browser never supplies an `OrganizacaoId` as authorization context. `Trocar unidade` is shown only when more than one active administered Unit is available.

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

User authorization uses contextual access links rather than a single hard-coded unit.

```text
UsuarioIdentity
└── VinculoAcesso
    ├── OrganizacaoId
    ├── UnidadeId?
    └── Perfil
```

Tenant authorization is server-side and mandatory.

The initial access profiles are `AdministradorRede`, `AdministradorUnidade`, `Professor`, `Aluno`, and `Responsavel`. A user may have multiple links, including links to different units and links with different profiles.

Initial cases:

- **Administrador da rede:** organization-wide access; `UnidadeId` is null.
- **Administrador de Unidade:** access to one or more units according to their active links.
- **Professor:** future access to operational features of the linked units.
- **Aluno:** future access to the student experience in the linked units.

`Responsavel` is reserved from the initial model and currently also requires a unit-scoped link.

### Authentication and authorization boundary

ASP.NET Core Identity is infrastructure for authentication only. The technical user is `UsuarioIdentity`, keyed by `Guid`, and contains no organization, unit, profile, or other business data.

The Identity model uses `IdentityUserContext<UsuarioIdentity, Guid>` without global Identity Roles. BFA authorization context is represented by `VinculoAcesso`, associated with `Organizacao` and optionally `Unidade`. A single user may have multiple links and profiles.

The responsibility boundary is:

```text
Identity          = who the user is (authentication)
VinculoAcesso     = which contexts and profiles the user has
Policies/Handlers = authorization decision
```

The post-login functional destination is selected by `IDestinoPosLogin` in Application from the user's active access links and active Unit contexts. Application returns a typed `DestinoPosLoginResultado` and does not know MVC URLs. `AdministradorRede` has priority and Web maps it to `/franqueadora`; one `AdministradorUnidade` context maps to `/unidade/{unidadeId}`; multiple contexts map to `/selecionar-unidade`; no valid administrative context maps to `/acesso-negado`. A local `ReturnUrl`, validated with `Url.IsLocalUrl`, always has priority over this normal landing decision, and external return URLs are never followed.

`GET /acessar` is the central authenticated entry point exposed by the public navigation. Anonymous users are sent to `/login`; authenticated users are routed through `IUsuarioAtual`, `IDestinoPosLogin`, and the Web URL mapper. An authenticated user who requests `GET /login` follows the same destination mechanism instead of seeing the login form again. The public Home only chooses between the `Login` and `Acessar sistema` calls to action based on authentication state and contains no profile rule.

The `/acessar` mechanism now supports `AdministradorRede` and `AdministradorUnidade`. It may later be expanded for `Professor`, `Aluno`, and `Responsavel`, with possible experiences under `/professor`, `/aluno`, and `/responsavel`; those destinations and routes do not exist yet. Priority among different future operational profiles remains intentionally undefined. The current Unit selection resolves only multiple `AdministradorUnidade` contexts and does not choose between different product experiences.

Only active access links associated with an active Unit and active Organization participate in the Unit destination and context selection. Profiles, `OrganizacaoId`, `UnidadeId`, and the list of Units are not copied into the authentication cookie; handlers and context queries consult the persistent source for every decision, without an authorization cache at this stage. Deactivating a link or Unit therefore takes effect on the next request of an existing authenticated session.

The initial policies provide generic entry checks for `AdministradorRede`, administration (`AdministradorRede` or `AdministradorUnidade`), `Professor`, `Aluno`, and `Responsavel`. Access to a specific unit is resource-based: Web supplies a persistence-independent `ContextoUnidade` containing `OrganizacaoId` and `UnidadeId`, and the handler compares both identifiers with the user's active links.

Context rules:

- `AdministradorRede` has transversal access to all units in the organization of its active organization-wide link, but no implicit access to another organization.
- `AdministradorUnidade` has access only to the exact organization/unit pairs in its active links.
- `Professor`, `Aluno`, and `Responsavel` have access only to the contexts in their active links.
- A user with several links is authorized independently in each matching context; one unit link never grants access to another unit.

For operations that also require a profile in a unit, `AcessoUnidadePorPerfilRequirement` combines the exact resource context with the allowed profiles. `AdministradorRede` remains a superaccess profile only inside its own organization.

`VinculoAcesso` belongs to Domain and references the technical user only through `UsuarioId` as a `Guid`; Domain does not reference ASP.NET Core Identity. Database integrity ensures that a unit-scoped link uses a unit from the same organization through the composite foreign key `(organizacao_id, unidade_id)`.

Current Identity schema version is explicitly v2 and contains:

```text
usuarios
usuario_claims
usuario_logins
usuario_tokens
```

Passkey schema v3 is not enabled in this phase. No role, user-role, or role-claim table is part of the model. This decision is recorded in `docs/adr/0005-identity-sem-roles-globais.md`.

The access-link schema is introduced separately by `V003__criar_vinculos_acesso.sql`. It is not an Identity Role schema and does not add global roles.

### Complementary user and franchisee registration

Authentication, registration data, authorization, and commercial relationships remain separate:

```text
UsuarioIdentity     = authentication and credentials
PerfilUsuario       = complementary registration data for the person using the system
VinculoAcesso       = contextual authorization
Franqueado          = commercial and contractual entity of an Organizacao
FranqueadoUsuario   = association between a Franqueado and one or more technical users
FranqueadoUnidade   = current or historical commercial association with a Unidade
```

`PerfilUsuario` has a one-to-one relationship with `UsuarioIdentity` and does not contain password, Identity normalization fields, access profiles, `OrganizacaoId`, or `UnidadeId`. There is no immutable `TipoUsuario`: the same technical user may accumulate business relationships and active access links over time.

`Franqueado` belongs to one `Organizacao`, while `FranqueadoUsuario` allows both a Franqueado to have several system users and a user to be associated with more than one Franqueado. `FranqueadoUnidade` carries `OrganizacaoId` explicitly and uses composite foreign keys so the related Franqueado and Unidade must belong to that same organization. One Franqueado may operate several Units.

Commercial Unit relationships are not physically replaced. The unique `(organizacao_id, franqueado_id, unidade_id)` relationship is reactivated when needed instead of being duplicated, while its inactive state preserves history. A PostgreSQL partial unique index on `(organizacao_id, unidade_id) WHERE ativo = true` permits only one current active Franqueado per Unit. The full `(organizacao_id, unidade_id, ativo)` index supports both current and historical lookups; the unique relationship index also covers the organization/franchisee prefix required by the tenant-integrity foreign key. The independent `franqueado_id` index remains useful for queries across the Franqueado's Units without an `OrganizacaoId` predicate.

The Franqueadora management flow keeps this relationship separate from the Unit's own lifecycle. Both initial Franqueado registration and later Unit linking use the same Application rule: for every selected Unit, the operation creates or reactivates one `FranqueadoUnidade` and the exact `AdministradorUnidade` access of the active principal `FranqueadoUsuario`. Both records are persisted atomically in one explicit transaction; an equivalent inactive record is reactivated and an equivalent active record is not duplicated. The Franqueado detail page derives its Unit list exclusively from `FranqueadoUnidade`.

Unlinking performs a soft deactivation of that commercial relationship and of that principal user's corresponding access; it never deactivates the `Unidade` and never changes access links held by other Unit administrators. Managers, secretaries, other administrators, and future operational users may have an explicit `VinculoAcesso` without being a `FranqueadoUsuario` and without having a `FranqueadoUnidade`. Therefore no generic synchronization, inference, or background process may create a commercial relationship from an access link, or an access link from a commercial relationship. The paired rule is invoked only by the explicit Franqueado registration and Unit-linking use cases.

Development offers the explicit, read-only command `--diagnosticar-vinculos-franqueados`. It reports active principal `AdministradorUnidade` accesses without a corresponding active commercial relationship and the inverse condition. It never changes data or runs automatically.

A Franqueado may have several associated system users, but a partial unique index on `franqueado_id WHERE principal = true AND ativo = true` permits at most one active principal user. Non-principal users and inactive former principal users may coexist.

The manually reviewed `V004__criar_usuarios_e_franqueados.sql` defines `perfis_usuario`, `franqueados`, `franqueados_usuarios`, and `franqueados_unidades`. It is never executed automatically by EF Core or application startup.

For a `PessoaFisica` Franqueado, Application derives `nome_razao_social`, commercial email, and commercial phone from the associated user's name, login email, and profile phone; company-only fields are discarded even if a client posts them manually. For a `PessoaJuridica`, the company fields remain explicit. CPF is stored as 11 digits. CNPJ is stored as 14 uppercase characters: the first 12 are ASCII letters or digits and the last 2 are digits. Numeric legacy CNPJs remain valid. `V005__adequar_cnpj_alfanumerico.sql` changes only this database check constraint; it does not change the `varchar(14)` column.

The implemented creation flows coordinate the concepts without merging them:

```text
New AdministradorRede
├── UsuarioIdentity
├── PerfilUsuario
└── VinculoAcesso: AdministradorRede

New Franqueado user
├── UsuarioIdentity
├── PerfilUsuario
├── Franqueado
├── FranqueadoUsuario
├── FranqueadoUnidade (one or more)
└── VinculoAcesso: AdministradorUnidade for each selected Unit
```

An `AdministradorRede` does not create a password for a new user. `UsuarioIdentity` is created with email as username and without a password. ASP.NET Core Identity generates its standard password-reset token, which Web transports with URL-safe Base64 encoding in a one-time displayed link. The token and link are not persisted in custom tables, stored in `usuario_tokens`, logged, or displayed in the user listing.

The public `GET /definir-senha` and `POST /definir-senha` endpoints validate the Identity token and apply the current Identity password policy. The POST requires antiforgery, never authenticates the user automatically, and redirects to `/login` with a generic success message. Invalid, expired, or already-used links return a controlled response without internal details. Email delivery and invitation persistence remain deferred.

### Professors, Unit relationships, and remuneration history

The professional model remains separate from technical authentication and authorization:

```text
Professor
└── ProfessorUnidade
    └── ProfessorRemuneracao

UsuarioIdentity
└── VinculoAcesso: Professor
```

`Professor` is an organization-scoped business entity and may exist without a
`UsuarioIdentity`. `ProfessorUnidade` records the professional relationship with an exact
Unit in the same Organization; it does not grant system access. Access is granted only by an
explicit `VinculoAcesso` with `PerfilAcesso.Professor`, created by a future approved use case.
Therefore, professional relationship and authorization are not interchangeable and no
automatic synchronization exists between them.

After insertion, a Professor's `Id`, `OrganizacaoId`, and creation timestamp are immutable;
its optional user association and registration data may evolve through controlled Domain
operations. A `ProfessorUnidade` is a historical identity whose `Id`, tenant, Professor,
Unit, and creation timestamp never change. Reactivation changes only its active state and
update timestamp, so remuneration history can never be reinterpreted by moving a relationship
to another person or Unit.

Remuneration belongs to `ProfessorUnidade`, not directly to `Professor`. Its initial
modalities are `Mensal`, `PorAula`, and `PorHora`, persisted as strings. Historical terms are
append-only: a change closes the current effective period once and inserts a new record in the
same future transaction. PostgreSQL locks the corresponding professional relationship while
validating a write, permits only one open remuneration, rejects overlapping date ranges, and
prevents changes to historical value, modality, initial date, observation, tenant, identity,
and creation audit. Runtime grants contain no physical `DELETE` permission for these tables.

The active-state hierarchy is also enforced in PostgreSQL. A `ProfessorUnidade` cannot be
inactivated while it has an open remuneration, and a `Professor` cannot be inactivated while
it has an active professional relationship. The future use case must close the current
remuneration, inactivate the relationship, and only then inactivate the Professor, in that
order and in one transaction where applicable. Reactivation reuses the unique existing
`ProfessorUnidade` and never creates remuneration automatically; a new remuneration is an
explicit operation. Parent-row locking and reverse active-state validation prevent concurrent
writes from reopening the inconsistent state.

`UsuarioIdentity` already stores username independently from email, but the current `/login`
flow remains email-based. A future explicitly approved change may accept "E-mail ou usuario"
and allow a Professor to authenticate with a username without an email; V008 does not change
login behavior, create users, or create access links.

The manually reviewed `V008__criar_professores_e_remuneracoes.sql` defines only
`professores`, `professores_unidades`, and `professores_remuneracoes`. It is deployed manually
and is never executed by EF Core or application startup.

### Versioned franchise contracts and private documents

The contractual aggregate begins from the commercial relationship and preserves every
formalized condition as a separate historical record:

```text
FranqueadoUnidade
└── ContratoFranquia
    └── ContratoFranquiaVersao
        └── DocumentoContratoFranquia
```

`ContratoFranquia` is the identity of the contract over time and does not carry commercial
values. `ContratoFranquiaVersao` records effective dates, royalties, fixed monthly fee,
optional adhesion fee, optional due day, change reason, and observations. Royalties are
stored as the percentage itself (`8.00` means 8%, not `0.08`); royalties and a fixed fee may
coexist. A partial unique index permits at most one `Ativo` contract for each
`FranqueadoUnidade`, and another permits at most one `Vigente` version for each contract.

After insertion, a contract's `Id`, `FranqueadoUnidadeId`, and creation timestamp are
immutable in both Domain and PostgreSQL; a historical contract can never be moved to another
commercial Unit relationship. Its number may be adjusted only while the previous status is
`Rascunho`, while `AtualizadoEmUtc` records every permitted administrative change. The
`proteger_contrato_franquia()` trigger function enforces the minimum transitions
`Rascunho -> Ativo|Cancelado` and `Ativo -> Encerrado|Cancelado`; `Encerrado` and `Cancelado`
are terminal. Keeping the same status remains valid.

Formalized conditions are never overwritten. A future contractual change must mark the
previous version as `Substituida` and insert the next version as `Vigente` in one database
transaction. The pair `(contrato_franquia_id, numero_versao)` is unique and provides the
per-contract sequence without a global version sequence. The Franqueadora flow now creates
the next number from the current contract history and treats a concurrent unique violation as
a controlled conflict; it does not use a global sequence.

The Domain exposes term editing only while a version is `Rascunho`. Identity and creation
audit (`Id`, parent contract, version number, creation timestamp, and creating user) never
change after insertion. V007 repeats this protection in PostgreSQL through the
`proteger_versao_contrato_formalizada()` trigger function: once the previous status is
`Vigente`, `Substituida`, or `Cancelada`, all commercial terms and effective dates are frozen.
The minimum status transitions are `Rascunho -> Vigente|Cancelada`,
`Vigente -> Substituida|Cancelada`; `Substituida` and `Cancelada` are terminal. Keeping the
same status is allowed, but does not bypass the frozen-term rule.

`DocumentoContratoFranquia` belongs to the exact contract version and stores only metadata:
original name, logical storage key, content type, size, optional lowercase hexadecimal
SHA-256, timestamps, and responsible user. The globally unique key follows a server-generated
logical form such as `contratos/{contratoId}/versoes/{versaoId}/{documentoId}.pdf`; it is not
an absolute filesystem path and never derives its physical name from the uploaded original
name. PostgreSQL does not store PDF bytes, Base64, BLOBs, or large objects.
Document evidence is append-only for the runtime role: `bfa_app_role` receives only `SELECT`
and `INSERT` on `documentos_contrato_franquia`. A correction or aditivo creates another
metadata row and another physical file; existing document metadata is not updated or deleted.

Application owns the storage port `IArmazenamentoDocumentosContrato`, including staged save,
confirmation, technical discard, read, and existence operations. Infrastructure provides
`ArmazenamentoLocalDocumentosContrato`, which
resolves validated relative keys beneath a configured private base directory and rejects
absolute paths, `..`, invalid segments, and any normalized path that escapes that base. No
contract document directory is placed under `wwwroot`, exposed by a direct public URL, or
served directly by Nginx. The Franqueadora endpoints authorize the complete
Organizacao/Franqueado/Unidade/Contrato/Versao/Documento chain and stream PDFs through the
application; storage keys and physical paths never become public URLs.

The implemented lifecycle is:

```text
Contrato Rascunho + Versao 1 Rascunho
        |
        +--> PDFs append-only
        |
        v
Contrato Ativo + Versao Vigente
        |
        +--> Nova Versao Rascunho + documento Contrato/Aditivo
        |
        v
Versao anterior Substituida + nova Versao Vigente
```

Activation requires a `Contrato` document. Formalizing a later version requires a `Contrato`
or `Aditivo`. Draft and active contracts may be canceled without physical deletion; ending an
active contract changes only its aggregate lifecycle to `Encerrado`. Its last version remains
`Vigente` to mean “last formalized set of terms”, not that the commercial operation remains
active.

In Contabo production, `Armazenamento__Documentos__DiretorioBase` configures the private root;
the planned value is `/var/lib/bfa/storage`, with contract files beneath its `contratos`
subtree. The Linux account running BFA.Web requires read/write access to this private
directory. The path is operational configuration and is not hardcoded in application code
or committed with a secret.

PostgreSQL and the filesystem do not share one ACID transaction. Upload therefore streams to
a private temporary file first while enforcing the configured size, checking `%PDF-`, and
calculating lowercase SHA-256. Only after validation does Infrastructure open the database
transaction, move the temporary file to its server-generated final key, insert metadata, and
commit. A failure before confirmation discards the temporary file; a database failure after
the move rolls back metadata and technically removes the unconfirmed final file. This cleanup
is compensation for an operation that never committed, not a document-delete use case.

Development configures `.storage/` outside `wwwroot` and Git ignores it. Production supplies
`Armazenamento__Documentos__DiretorioBase` (planned `/var/lib/bfa/storage`) and may override
`Armazenamento__Documentos__TamanhoMaximoBytes`. Malware scanning and a future metadata versus
filesystem reconciliation tool remain explicit future hardening; persisted documents are not
automatically deleted when a physical file is missing.

```text
BFA.Web
├── PostgreSQL       (contract data and document metadata)
└── private storage  (document content)
```

A complete BFA backup and restore must treat PostgreSQL and the private storage directory as
one recoverable set. The database contains each `ChaveArmazenamento`, so restoration must
preserve the pairing between that metadata and the corresponding physical file. V007 created
the three tables and their metadata constraints and is immutable after deployment; physical
storage remains provisioned independently per environment.

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

`BFA.Web` composes persistence with a single `AddInfrastructure(builder.Configuration)` call. `BFA.Infrastructure` reads `ConnectionStrings:BfaDatabase`, registers `BfaDbContext` with `UseNpgsql`, and registers Identity Core with its EF user stores. The context derives from `IdentityUserContext<UsuarioIdentity, Guid>` and exposes `Organizacoes`, `Unidades`, `VinculosAcesso`, `PerfisUsuario`, `Franqueados`, `FranqueadosUsuarios`, `FranqueadosUnidades`, `Estados`, `Municipios`, `ContratosFranquia`, `ContratosFranquiaVersoes`, `DocumentosContratoFranquia`, `Professores`, `ProfessoresUnidades`, and `ProfessoresRemuneracoes`. All custom mappings remain isolated in separate Fluent API configurations inside Infrastructure.

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

### Catálogo de Localidades

BFA maintains a shared local catalog of Brazilian States and Municipalities. The IBGE Localities API is the master source, but normal operational flows query only the BFA PostgreSQL database and therefore do not depend on IBGE availability to open forms or create business records.

Synchronization is an explicit command (`dotnet run -- --sincronizar-localidades-ibge`) and never runs during normal web startup. It downloads and validates the complete remote catalog before opening the database transaction. Only a complete valid batch is upserted by the official IBGE code; records absent from that complete batch are marked inactive and are never physically deleted.

The new Franqueado form loads active States and Municipalities only from this local catalog. The browser submits their official codes, Application validates that both records are active and that the Municipality belongs to the selected State, and then persists the official State abbreviation and Municipality name in the existing textual `franqueados.estado` and `franqueados.cidade` columns. Normal form use never calls the IBGE integration. The same catalog and searchable combobox may later support Unidades, Professores, Alunos, Responsaveis, and other platform addresses.

The dependent Municipality selector cancels its previous HTTP request with `AbortController` whenever the selected State changes. The endpoint keeps cooperative cancellation through `HttpContext.RequestAborted` and treats an `OperationCanceledException` as expected only when that HTTP request was actually aborted; unrelated cancellations remain visible for diagnosis. The browser also verifies the current request identity before changing the selector, so an older response cannot overwrite the latest State selection.

---

## 11. Schema Deployment

Schema changes live in:

```text
database/migrations/
```

Example:

```text
V001__criar_organizacoes_e_unidades.sql
V002__criar_identidade.sql
V003__criar_vinculos_acesso.sql
V004__criar_usuarios_e_franqueados.sql
V005__adequar_cnpj_alfanumerico.sql
V006__criar_catalogo_localidades.sql
V007__criar_contratos_franquia.sql
V008__criar_professores_e_remuneracoes.sql
```

`bfa_schema_history` records applied SQL versions. Reviewed scripts are executed manually by `bfa_*_deploy`; runtime application logins never deploy schema.

Runtime permissions are assigned to one portable PostgreSQL role without login. Environment-specific login users are members of that role:

```text
bfa_app_role (NOLOGIN)
├── bfa_dev_app (LOGIN)
├── bfa_staging_app (LOGIN)
└── bfa_prod_app (LOGIN)
```

Versioned migrations may reference `bfa_app_role`, but never environment-specific login names. Roles and memberships are provisioned separately from application schema migrations.

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
    ├── private storage
    │   └── /var/lib/bfa/storage (not served by Nginx)
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

### Controlled initial bootstrap

The first BFA organization and its first two network administrators are provisioned only through the explicit `--bootstrap-inicial` command in `Development`. Normal web startup never runs this operation. Credentials come from secure configuration, users are created through ASP.NET Core Identity `UserManager`, and the organization/users/access links are committed in one database transaction.

The bootstrap performs runtime DML only: it does not create schema, execute migrations, create units, or expose an HTTP endpoint. Operational instructions are documented in `docs/BOOTSTRAP-INICIAL.md`.

---

## 14. Initial Modules

Modules are created only when needed.

Planned sequence:

```text
Identidade
Usuarios
Organizacoes
Unidades
Acessos
Franqueados
Contratos
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

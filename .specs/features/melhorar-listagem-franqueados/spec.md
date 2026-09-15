# Melhorar listagem de Franqueados

**Status**: approved for implementation

## Problem Statement

A listagem de Franqueados permite consultar e editar entidades comerciais, mas não oferece entrada clara de cadastro, busca server-side, paginação nem estados vazios distintos. A tabela também apresenta densidade desnecessária no desktop.

## Objective

Melhorar somente `/franqueadora/franqueados`, mantendo o modelo comercial, autorização, rotas existentes de detalhe/edição/contratos e o fluxo de cadastro integrado a Novo Usuário.

## Out of Scope

- Alterar Detalhe, Editar, Novo Usuário ou a relação Usuário → Franqueado.
- Alterar Unidades vinculadas, Contratos, Domain ou policies.
- Criar rota ou cadastro duplicado de Franqueado.
- Adicionar telefone/email à listagem.
- Criar migration, alterar banco ou modificar histórico Git.
- Commit e push.

## Assumptions & Open Questions

| Assumption | Chosen default | Rationale |
|---|---|---|
| Destino de Novo Franqueado | `/franqueadora/usuarios/novo` sem pré-seleção | Não existe parâmetro seguro atual para abrir `TipoCadastro=Franqueado`; não criar comportamento improvisado. |
| Tamanho da página | 10 | É o padrão usado pelas listagens administrativas equivalentes. |
| Busca | Nome/Razão social, Nome fantasia e CPF/CNPJ | Atende identificação operacional sem filtros de baixo valor. |
| Comparação | `ToLower().Contains` no provider atual | Reutiliza o padrão existente no repositório e mantém tradução para PostgreSQL. |
| Paginação | Servidor, após filtro e ordenação determinística | Evita carregar toda a listagem na View. |

**Open questions: none**

## User Stories

### US-01 — Encontrar Franqueados

Como AdministradorRede, quero buscar Franqueados por identificação comercial ou documento para localizar rapidamente uma entidade da minha Organização.

**Acceptance Criteria**:

1. WHEN an authorized network administrator searches by name, business name, fantasy name, or CPF/CNPJ, the system SHALL filter on the server using a case-insensitive comparison scoped to the current Organization.
2. WHEN the search term changes, the system SHALL start at page 1 and preserve the term in the query string.
3. WHEN no result matches a search, the system SHALL show a search-specific empty state.

### US-02 — Navegar pela listagem

Como AdministradorRede, quero navegar por páginas de resultados sem perder o termo pesquisado.

**Acceptance Criteria**:

1. WHEN results exist, the system SHALL apply deterministic ordering, count, Skip/Take and render the existing pagination component.
2. WHEN a pagination link is selected, the system SHALL preserve the active search query.
3. WHEN the clear action is selected, the system SHALL navigate to `/franqueadora/franqueados` without search parameters.

### US-03 — Executar ações da listagem

Como AdministradorRede, quero iniciar cadastro, visualizar e editar Franqueados a partir da listagem.

**Acceptance Criteria**:

1. WHEN the list header is rendered, it SHALL expose `Novo Franqueado` linking to the existing `/franqueadora/usuarios/novo` flow.
2. WHEN a row/card is rendered, it SHALL preserve correct Visualizar and Editar links with keyboard-accessible labels and titles.
3. WHEN the list is rendered, it SHALL preserve status and active-unit count without adding new commercial fields.

## Requirement Traceability

| ID | Requirement | Evidence |
|---|---|---|
| SEARCH-01 | Busca server-side nos três grupos de identificação | Integration tests + repository implementation |
| PAGE-01 | Ordenação, count, Skip/Take e paginação | Integration tests + `PaginaResultado` |
| EMPTY-01 | Estado vazio inicial distinto do estado filtrado | Integration tests |
| NAV-01 | Novo Franqueado reutiliza rota existente | Integration test |
| ACTION-01 | Visualizar/Editar preservados com acessibilidade | Integration test + partial |
| TENANT-01 | Isolamento por Organização preservado | Existing integration test + regression |
| UI-01 | Desktop table/mobile cards e sem novos campos | Integration test + Razor inspection |
| DB-01 | Nenhuma migration/banco alterado | Migration diff gate |

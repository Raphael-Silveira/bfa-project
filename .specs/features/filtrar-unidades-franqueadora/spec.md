# Filtrar Unidades da Franqueadora

**Status**: approved-for-implementation

## Problem Statement

A listagem `/franqueadora/unidades` exibe todas as unidades autorizadas, mas não permite localizar uma unidade por nome nem separar unidades franqueadas das unidades próprias da Rede. A página já recebe `FranqueadoIdAtivo`, calculado a partir de `FranqueadoUnidade` ativo, e a ação contextual de contrato depende desse mesmo dado.

## Scope

- Adicionar busca server-side pelo nome da Unidade.
- Adicionar filtro de tipo com `Todos`, `Franqueadas` e `Rede`.
- Preservar os parâmetros na URL e voltar à listagem sem filtros ao limpar.
- Exibir empty state específico quando filtros não encontrarem resultados.
- Preservar a ação `Gerenciar contrato` somente para unidades com vínculo ativo.
- Preservar autorização e isolamento por Organização.

## Out of Scope

- Alterar regras de negócio ou autorização.
- Criar paginação, pois a tela atual não possui paginação.
- Criar ou alterar migrations, schema, banco ou dependências.
- Alterar as rotas ou módulos de Alunos e Planos.
- Alterar o domínio sem necessidade.
- Criar commit ou push.

## Assumptions & Open Questions

| Assumption | Chosen default | Rationale |
|---|---|---|
| Paginação | Não adicionar | A tela atual não possui paginação e o pedido determina preservar esse limite. |
| Tipo ausente | `Todos` | É o comportamento default solicitado. |
| Tipo inválido | `Todos` | Mantém a consulta segura sem inventar uma nova regra de autorização. |
| Busca vazia | Sem restrição por nome | Campo vazio equivale a ausência de filtro. |

**Open questions: none**

## User Stories

### US-01 — Buscar e filtrar unidades

Como AdministradorRede, quero filtrar a listagem de Unidades por nome e tipo para encontrar rapidamente as unidades relevantes.

**Acceptance Criteria**:

1. WHEN an authorized AdministradorRede opens `/franqueadora/unidades` without filters, the system SHALL return every Unit from the selected Organization.
2. WHEN `busca` is provided, the system SHALL apply a server-side case-insensitive name filter and SHALL return only matching Units from the selected Organization.
3. WHEN `tipo=franqueadas` is provided, the system SHALL return only Units with an active `FranqueadoUnidade`.
4. WHEN `tipo=rede` is provided, the system SHALL return only Units without an active `FranqueadoUnidade`.
5. WHEN `tipo=todos` or no type is provided, the system SHALL add no type restriction.
6. WHEN `busca` and `tipo` are both provided, the system SHALL apply both restrictions together.
7. WHEN no Unit matches the filters, the page SHALL show `Nenhuma unidade encontrada com os filtros informados.` and SHALL distinguish this from an unfiltered empty organization.

### US-02 — Preservar tenant e contrato

Como responsável pela segurança e pela operação da Rede, quero que os filtros não ampliem acesso nem alterem as ações existentes.

**Acceptance Criteria**:

1. WHEN filtering is requested, the system SHALL never return a Unit from another Organization.
2. WHEN a filtered result has an active `FranqueadoUnidade`, the page SHALL keep the existing `Gerenciar contrato` action and its existing route.
3. WHEN a filtered result has no active `FranqueadoUnidade`, the page SHALL not render `Gerenciar contrato`.
4. WHEN `Limpar` is selected, the browser SHALL navigate to `/franqueadora/unidades` without filter parameters.

### US-03 — Preservar estado e UI responsiva

Como administrador, quero os filtros selecionados visíveis e utilizáveis em desktop e mobile.

**Acceptance Criteria**:

1. WHEN the page is rendered after a filtered request, the search value and type selection SHALL remain represented in the form and querystring.
2. WHEN the page is rendered on desktop, the compact filter controls SHALL occupy one row with the search control receiving the larger share of space.
3. WHEN the page is rendered on mobile, the controls SHALL fit the available width without horizontal overflow.

## Requirement Traceability

| ID | Requirement | Verification |
|---|---|---|
| FILTRO-01 | Busca server-side por nome | Integration HTTP tests |
| FILTRO-02 | Tipo Franqueadas usa vínculo ativo | Integration HTTP tests |
| FILTRO-03 | Tipo Rede usa ausência de vínculo ativo | Integration HTTP tests |
| FILTRO-04 | Todos/sem filtro retorna unidades autorizadas | Integration HTTP tests |
| FILTRO-05 | Busca e tipo combinados | Integration HTTP tests |
| TENANT-01 | Outra organização permanece oculta | Integration HTTP tests |
| CONTRATO-01 | Ação de contrato permanece condicional | Existing and new integration tests |
| UI-01 | Labels, estado selecionado, limpar e empty state | HTML assertions + browser QA |
| UI-02 | Layout responsivo | Browser QA at requested viewports |

## Constraints

- O predicado de tipo deve usar o vínculo ativo, não o texto visual do badge.
- Razor não deve duplicar a regra de tipo nem consultar dados.
- A consulta deve aplicar filtros, ordenação determinística e materialização. Não adicionar paginação.

# Tasks — Filtrar Unidades da Franqueadora

## Test Coverage Matrix

| Requirement | Unit | Integration / HTTP | Manual QA |
|---|---|---|---|
| FILTRO-01 | N/A | Nome existente e inexistente | Desktop/mobile |
| FILTRO-02 | N/A | Vínculo ativo | Ação filtrada |
| FILTRO-03 | N/A | Sem vínculo ativo | Ação filtrada |
| FILTRO-04 | N/A | Sem filtro/Todos | Desktop/mobile |
| FILTRO-05 | N/A | Consulta combinada | Desktop/mobile |
| TENANT-01 | N/A | Outra organização excluída | N/A |
| CONTRATO-01 | N/A | Rota condicional existente | Desktop/mobile |
| UI-01 | N/A | Labels, seleção, limpar, empty | Browser |
| UI-02 | N/A | N/A | 1440/1024/768/390/360 |

## Gate Check Commands

```powershell
python C:\Users\rapha\.codex\skills\tlc-spec-driven\scripts\validate_spec.py .specs/features/filtrar-unidades-franqueadora/spec.md --strict
python C:\Users\rapha\.codex\skills\tlc-spec-driven\scripts\validate_tasks.py .specs/features/filtrar-unidades-franqueadora/tasks.md --strict
dotnet test backend/BFA.sln --filter FullyQualifiedName~UnidadesFranqueadoraEndpointTests
dotnet build backend/BFA.sln
dotnet test backend/BFA.sln
git diff --check
git diff -- database/migrations
```

## Execution Plan

```text
T1 -> T2 -> T3 -> T4 -> T5
```

### T1: Formalizar filtro e consulta server-side

**Depends on**: none
**Where**: Application contracts/service and Infrastructure repository
**Change**: Adicionar o contrato normalizado e aplicar predicados de tenant, nome e vínculo ativo antes da ordenação determinística e materialização.
**Tests**: Testes de endpoint exercitam a consulta pelo caminho HTTP autorizado.
**Gate**: Testes direcionados de Unidades passam.

### T2: Propagar filtros no endpoint e ViewModel

**Depends on**: T1
**Where**: `UnidadesController.cs`, `UnidadesFranqueadoraIndexViewModel.cs`
**Change**: Bind de `busca` e `tipo`, preservação do estado submetido, default Todos e mapeamento sem alterar autorização ou propagação do ID de contrato.
**Tests**: Asserções de querystring e seleção.
**Gate**: Testes direcionados de Unidades passam.

### T3: Implementar filtro compacto e estados da View

**Depends on**: T2
**Where**: `Views/Unidades/Index.cshtml`
**Change**: Adicionar controles GET compactos, link Limpar, empty state filtrado e layout responsivo, preservando markup da lista e partial de contrato.
**Tests**: Asserções HTML de textos, opções, empty state e ação.
**Gate**: Testes direcionados e diff check passam.

### T4: Completar regressões de comportamento

**Depends on**: T3
**Where**: `UnidadesFranqueadoraEndpointTests.cs`
**Change**: Cobrir sem filtro, nome, ausência, cada tipo, Todos, combinação, tenant, estado da querystring, limpar e ação condicional.
**Tests**: Todos os critérios e casos listados.
**Gate**: Build e suíte completa passam.

### T5: Validar artefatos e UI

**Depends on**: T4
**Where**: artefato de validação da feature
**Change**: Registrar evidências dos validadores, gates, migrations e viewports. Nenhuma mudança de produto.
**Tests**: QA nos viewports solicitados e combinações de filtros.
**Gate**: `validate_state.py --strict` passa; sem commit ou push.

## Task Breakdown

- [x] T1 — contrato e consulta server-side.
- [x] T2 — parâmetros no controller.
- [x] T3 — estado de filtros no ViewModel.
- [x] T4 — formulário e estados na View.
- [x] T5 — regressões de endpoint.
- [x] T6 — validação final e QA.

T2 e T3 são mantidos separados no registro, embora sejam executados em sequência antes da View, para que cada alteração de camada permaneça verificável.

## Restrictions

- No migration or database change.
- No authorization or business-rule change.
- No pagination.
- No commit or push.

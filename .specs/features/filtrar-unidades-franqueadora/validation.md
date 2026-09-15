# Filtrar Unidades da Franqueadora — Validation

**Date**: 2026-09-15
**Spec**: `.specs/features/filtrar-unidades-franqueadora/spec.md`
**Diff range**: working tree against `f34cd19` (no commit by user request)
**Verifier**: standalone fresh-eyes fallback

## Validation: PASS

## Task Completion

| Task | Status | Evidence |
|---|---|---|
| T1 | ✅ Done | Filter contract and repository predicates implemented. |
| T2 | ✅ Done | Controller binds and normalizes `busca`/`tipo`. |
| T3 | ✅ Done | ViewModel preserves filter state and filtered-empty state. |
| T4 | ✅ Done | Compact responsive GET form and existing unit list preserved. |
| T5 | ✅ Done | Six new HTTP regressions pass; existing contract-action tests remain green. |
| T6 | ✅ Done | Build/test gates, migration checks and scratch sensor completed. |

## Spec-Anchored Acceptance Criteria

| Criterion | Spec-defined outcome | `file:line` + assertion / evidence | Result |
|---|---|---|---|
| Sem filtro retorna todas as unidades autorizadas | Todas as unidades da Organização selecionada | `backend/tests/BFA.IntegrationTests/UnidadesFranqueadoraEndpointTests.cs:42` — `Assert.Contains` para unidade própria e `Assert.DoesNotContain` para unidade externa | ✅ PASS |
| Busca por nome é case-insensitive e server-side | Somente correspondências da Organização | `backend/tests/BFA.IntegrationTests/UnidadesFranqueadoraEndpointTests.cs:136` — `Assert.Contains("BFA Cerquilho", html, ...)`; `:137-138` — exclusões | ✅ PASS |
| Busca inexistente exibe estado filtrado | Mensagem exata de filtros e não “cadastrada” | `backend/tests/BFA.IntegrationTests/UnidadesFranqueadoraEndpointTests.cs:156-161` — asserções das duas mensagens/ausência | ✅ PASS |
| Tipo Franqueadas usa vínculo ativo | Somente unidades com `FranqueadoUnidade` ativo | `backend/tests/BFA.IntegrationTests/UnidadesFranqueadoraEndpointTests.cs:179-182` — unidade franqueada presente, Rede ausente e ação presente | ✅ PASS |
| Tipo Rede usa ausência de vínculo ativo | Somente unidades sem vínculo ativo | `backend/tests/BFA.IntegrationTests/UnidadesFranqueadoraEndpointTests.cs:200-203` — unidade Rede presente, franqueada/ação ausentes | ✅ PASS |
| Tipo Todos não restringe | Franqueadas e Rede permanecem presentes | `backend/tests/BFA.IntegrationTests/UnidadesFranqueadoraEndpointTests.cs:221-223` — ambas presentes e opção selecionada | ✅ PASS |
| Busca + tipo combinados | Interseção dos dois filtros | `backend/tests/BFA.IntegrationTests/UnidadesFranqueadoraEndpointTests.cs:246-251` — somente “Centro Franqueada” permanece | ✅ PASS |
| Tenant isolation | Unidade externa nunca aparece | `backend/tests/BFA.IntegrationTests/UnidadesFranqueadoraEndpointTests.cs:138` — `Assert.DoesNotContain("BFA Cerquilho Externa", ...)` | ✅ PASS |
| Gerenciar contrato permanece condicional | Presente só em franqueadas | `backend/tests/BFA.IntegrationTests/UnidadesFranqueadoraEndpointTests.cs:182` e `:203` — presença/ausência do título | ✅ PASS |
| Query state e Limpar | Busca/tipo selecionados; Limpar aponta para raiz | `backend/tests/BFA.IntegrationTests/UnidadesFranqueadoraEndpointTests.cs:139-141`, `:181`, `:202`, `:249-250` | ✅ PASS |

## Implementation Evidence

- `backend/src/BFA.Infrastructure/Franqueadora/UnidadesFranqueadoraRepositorio.cs:20-47` aplica tenant, nome case-insensitive, vínculo ativo e ordenação antes da materialização.
- `backend/src/BFA.Web/Areas/Franqueadora/Controllers/UnidadesController.cs:23-57` normaliza a querystring e preserva os valores no ViewModel.
- `backend/src/BFA.Web/Areas/Franqueadora/Views/Unidades/Index.cshtml:20-66` renderiza labels, opções, empty state e link Limpar.
- `backend/src/BFA.Web/Areas/Franqueadora/Views/Unidades/Index.cshtml:88-93` continua usando a partial existente, preservando `Gerenciar contrato`.

## Discrimination Sensor

| Mutation | Description | Result |
|---|---|---|
| 1 | No worktree temporário, trocado o ramo `TipoUnidadeFiltro.Franqueadas` para `TipoUnidadeFiltro.Rede` em `UnidadesFranqueadoraRepositorio.cs`. | ✅ Killed: 3 de 18 testes direcionados falharam. |

**Sensor depth**: lightweight
**Isolation**: worktree removido; status da árvore real permaneceu inalterado.

## Interactive UAT Results

| Viewport/scenario | Result | Details |
|---|---|---|
| Browser local autenticado, combinação de filtros e Limpar | ⏭️ Blocked | A aplicação iniciou em `127.0.0.1:5187` com `Hangfire__Enabled=false`, mas redirecionou para login; não havia credenciais seguras disponíveis. Nenhum bootstrap, dado ou migration foi executado. |
| Desktop/tablet/mobile | ⚠️ Pending manual confirmation | CSS foi revisado para uma linha no desktop e grid empilhado abaixo de `42.5rem`; confirmação visual autenticada permanece pendente. |

## Gate Check

- `validate_spec.py --strict`: 0 erros, 0 warnings.
- `validate_tasks.py --strict`: 0 erros, 1 warning de granularidade T2/T3 agrupadas no plano original.
- `dotnet test backend/BFA.sln --filter FullyQualifiedName~UnidadesFranqueadoraEndpointTests`: 18/18 aprovados.
- `dotnet build backend/BFA.sln`: 0 erros, 4 warnings NU1903 preexistentes.
- `dotnet test backend/BFA.sln`: 484 unitários + 706 integração = 1.190 aprovados; 0 falhas; 0 ignorados.
- `git diff --check`: aprovado.
- `git diff -- database/migrations`: vazio.
- Nenhuma migration criada; V001–V014 permanecem intactas.
- Nenhuma alteração de autorização, regra de negócio, banco ou dependência.
- Hangfire não foi executado; o único processo local iniciado para tentativa de QA usou `Hangfire__Enabled=false` e foi encerrado.

## Code Quality

| Check | Status |
|---|---|
| Escopo mínimo e cirúrgico | ✅ |
| Sem paginação fora do escopo | ✅ |
| Sem regra de negócio/autorização na View | ✅ |
| Predicado de tipo baseado em vínculo ativo | ✅ |
| Ação de contrato preservada | ✅ |
| Testes ancorados nos critérios | ✅ |
| Guidelines seguidas | ✅ `AGENTS.md`, `docs/ARCHITECTURE.md`, `brand/guide/brand-guide.md`, `docs/UI-ADMIN-STANDARDS.md` |

## Summary

**Overall**: ✅ Ready, com confirmação visual autenticada pendente por falta de credenciais locais seguras.

**Spec-anchored check**: 10/10 critérios cobertos.
**Sensor**: 1/1 mutação morta.
**Gate**: 1.190 testes aprovados.

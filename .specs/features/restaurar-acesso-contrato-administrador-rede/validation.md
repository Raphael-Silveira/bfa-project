# Restaurar acesso a Contrato para AdministradorRede — Validation

**Date**: 2026-09-15  
**Spec**: `.specs/features/restaurar-acesso-contrato-administrador-rede/spec.md`  
**Diff**: working tree against `5f1f9f0` (no commit by user request)  
**Verifier**: standalone fresh-eyes fallback; the delegated verifier hit the usage limit before producing a report.

## Task Completion

| Task | Status | Evidence |
|---|---|---|
| T1 | ✅ Done | Red regression for Rede + active franchise, then green. |
| T2 | ✅ Done | Red/green controlled-empty regression for Rede without commercial link. |
| T3 | ✅ Done | Dashboard View restored with read-only summary/empty state and GET link. |
| T4 | ✅ Done | Build, full tests, validators, diff and migration gates passed. |
| T5 | ✅ Done | Browser QA completed for AdminUnidade and responsive shell; HTTP coverage for remaining profiles. |

## Spec-Anchored Acceptance Criteria

| Criterion | Spec-defined outcome | Evidence | Result |
|---|---|---|---|
| US-01.1 Rede + active contract | Dashboard shows current summary and `/unidade/{id}/contrato` link | `AreaUnidadeEndpointTests.cs:471-475` asserts title, number, vigência, href and `Ver contrato`; View `Index.cshtml:96-116` renders them | ✅ PASS |
| US-01.2 direct contract read-only | 200 and no edit/upload/formalize/cancel/terminate | `AreaUnidadeEndpointTests.cs:472-481` asserts 200, detail text, storage-key absence and mutation absence | ✅ PASS |
| US-01.3 active `FranqueadoUnidade` remains visible | Contract remains visible for Rede | `AreaUnidadeEndpointTests.cs:447-481` creates active commercial chain and receives dashboard/detail | ✅ PASS |
| US-01.4 no active relationship | Dashboard/detail show controlled empty state without creation | `AreaUnidadeEndpointTests.cs:485-517`; View `Index.cshtml:126-129` | ✅ PASS |
| US-02.1 AdminUnidade dashboard | Same read-only summary/empty state | `AreaUnidadeEndpointTests.cs:391-415` | ✅ PASS |
| US-02.2 AdminUnidade URL | 200, no mutation actions | `AreaUnidadeEndpointTests.cs:413-440` | ✅ PASS |
| US-03.1 unauthorized profile | Denied access | `AreaUnidadeEndpointTests.cs:521-536` asserts Professor dashboard/contract redirect to access denied | ✅ PASS |
| US-03.2 cross-tenant | Denied without contract data | `AreaUnidadeEndpointTests.cs:546-582` existing cross-unit/cross-tenant contract/document assertions | ✅ PASS |
| US-03.3 tenant-scoped supplied result | View only branches over `Model.Contrato`; no query or authorization added | `Index.cshtml:96-129`; no controller/application/infrastructure diff | ✅ PASS |
| US-03.4 exactly one navigation link | Desktop/mobile shell keeps existing link | Browser AX snapshot showed one sidebar/drawer `Contrato`; no nav file changed | ✅ PASS |

## Discrimination Sensor

| Mutation | Scratch change | Result |
|---|---|---|
| 1 | Changed dashboard heading `Contrato da franquia` to `Contrato removido` in isolated worktree | ✅ Killed: active Rede regression failed at `AreaUnidadeEndpointTests.cs:471` |

**Sensor depth**: lightweight.  
**Isolation**: scratch worktree removed; real `git status --porcelain` matched the pre-sensor baseline.

## Interactive QA

| Scenario | Result |
|---|---|
| AdminUnidade active contract, direct GET, read-only | ✅ Pass |
| Desktop 1440/1024 and tablet 768 | ✅ Pass; no horizontal overflow |
| Mobile 390/360 and administrative drawer | ✅ Pass; card CTA full-width and `Contrato` remains in drawer |
| Rede with/without commercial link; Professor; cross-tenant | ✅ Covered by HTTP integration; manual browser execution not performed because no safe credentials/empty unit were available |
| Hangfire side effects | ✅ App QA ran with `Hangfire__Enabled=false` and was stopped afterward |

## Gate Results

- `validate_spec.py --strict`: 0 errors, 0 warnings.
- `validate_tasks.py --strict`: 0 errors, 0 warnings.
- `dotnet build backend/BFA.sln --no-restore`: 0 errors, 2 pre-existing NU1903 warnings.
- `dotnet test backend/BFA.sln --no-restore`: 484 unit + 697 integration passed; 0 failed, 0 skipped.
- `git diff --check`: passed.
- `git diff -- database/migrations`: empty.

## Code Quality

| Principle | Status |
|---|---|
| Minimum, surgical scope | ✅ |
| Existing ViewModel and route reused | ✅ |
| No new authorization/query/dependency/migration | ✅ |
| Current Admin Shell/design language and responsive behavior | ✅ |
| Tests assert values, routes, security and mutation absence | ✅ |
| Project guidelines followed (`AGENTS.md`, architecture/UI docs) | ✅ |

## Summary

**Overall**: ✅ Ready  
**Spec-anchored check**: 10/10 criteria matched expected outcomes  
**Sensor**: 1/1 mutation killed  
**Gate**: 1,181 tests passed, 0 failed, 0 skipped

The feature restores the missing dashboard discovery surface without changing authorization, persistence, contract management, Hangfire, or migrations.

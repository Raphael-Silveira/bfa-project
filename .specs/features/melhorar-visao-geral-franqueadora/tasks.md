# Tasks — Melhorar Visão Geral da Área Franqueadora

## Test Coverage Matrix

| Requirement | Unit | Integration / HTTP | Manual QA |
|---|---|---|---|
| UI-01 | N/A | Estrutura semântica do dashboard | Viewports definidos |
| DATA-01 | N/A | Oito labels e valores | Inspeção visual |
| MONEY-01 | N/A | Renderização HTML pt-BR | Valores legíveis |
| LIST-01 | N/A | Links e markup desktop/mobile | Mobile sem overflow |
| NAV-01 | N/A | Asserts existentes da sidebar | Drawer preservado |
| DB-01 | N/A | N/A | Diff de migrations vazio |

## Gate Check Commands

```powershell
python C:\Users\rapha\.codex\skills\tlc-spec-driven\scripts\validate_spec.py .specs/features/melhorar-visao-geral-franqueadora/spec.md --strict
python C:\Users\rapha\.codex\skills\tlc-spec-driven\scripts\validate_tasks.py .specs/features/melhorar-visao-geral-franqueadora --strict
dotnet build backend/BFA.sln
dotnet test backend/BFA.sln
git diff --check
git diff -- database/migrations
```

## Execution Plan

### Phase 1 — Implementação

```text
T1 -> T2 -> T3
```

### T1: Atualizar apresentação monetária

**Depends on**: none  
**Where**: `backend/src/BFA.Web/ViewModels/Franqueadora/FranqueadoraDashboardViewModel.cs`  
**Change**: Formatar os três valores monetários com cultura pt-BR.  
**Tests**: Teste de integração do dashboard.
**Gate**: Teste de integração confirma `R$` e separadores pt-BR.

### T2: Recompor dashboard e lista

**Depends on**: T1  
**Where**: `backend/src/BFA.Web/Areas/Franqueadora/Views/Inicio/Index.cshtml`  
**Change**: Reutilizar cards/ícones/listas existentes, separar operação/finanças e preservar links, estado vazio e sidebar.  
**Tests**: Teste HTTP do dashboard.
**Gate**: Teste HTTP confirma labels, classes, links e ausência dos itens removidos da sidebar.

### T3: Validar gates e registrar evidências

**Depends on**: T2  
**Where**: `.specs/features/melhorar-visao-geral-franqueadora/validation.md`  
**Change**: Executar validadores, sensor, build, testes e verificações de banco.  
**Tests**: Suítes completas e `git diff --check`.
**Gate**: Validadores sem erros, sensor mata mutação, build/teste sem erros e migrations sem diff.

## Task Breakdown

- [x] T1 — formatação monetária pt-BR.
- [x] T2 — composição visual e lista responsiva.
- [x] T3 — validação estrutural e gates.

## Restrições de execução

- Não criar commit nem push.
- Não modificar migrations, banco, autorização, rotas ou Hangfire.
- Preservar alterações não relacionadas já existentes no worktree.

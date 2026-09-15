# Tasks — Melhorar listagem de Franqueados

## Test Coverage Matrix

| Requirement | Unit | Integration / HTTP | Manual QA |
|---|---|---|---|
| SEARCH-01 | Serviço/repositório quando aplicável | Nome, fantasia, documento, tenant | Busca em todos os viewports |
| PAGE-01 | N/A | Paginação e querystring | Links e footer |
| EMPTY-01 | N/A | Sem dados vs sem resultado | Mensagem legível |
| NAV-01 | N/A | Link Novo Franqueado | Fluxo existente |
| ACTION-01 | N/A | Visualizar/Editar | Foco, title e aria-label |
| TENANT-01 | Existente | Cross-tenant | N/A |
| UI-01 | N/A | Markup desktop/mobile | 1920, 1440, 1024, 768, 390, 360, zoom 200% |
| DB-01 | N/A | N/A | Migrations sem diff |

## Gate Check Commands

```powershell
python C:\Users\rapha\.codex\skills\tlc-spec-driven\scripts\validate_spec.py .specs/features/melhorar-listagem-franqueados/spec.md --strict
python C:\Users\rapha\.codex\skills\tlc-spec-driven\scripts\validate_tasks.py .specs/features/melhorar-listagem-franqueados --strict
dotnet test backend/BFA.sln --filter "FullyQualifiedName~Franqueados"
dotnet build backend/BFA.sln
dotnet test backend/BFA.sln
git diff --check
git diff -- database/migrations
```

## Execution Plan

### Phase 1 — Contrato de consulta e paginação

```text
T1 -> T2
```

### T1: Estender consulta para busca paginada

**Depends on**: none  
**Where**: Application, Infrastructure, ViewModel e Controller da listagem  
**Change**: Adicionar parâmetros de busca/página, filtro, ordenação determinística, count e `PaginaResultado`.  
**Tests**: Testes unitários existentes atualizados somente por mudança de contrato e testes HTTP de busca/paginação.  
**Gate**: Testes direcionados verdes e tenant preservado.

### T2: Recompor a UI da listagem

**Depends on**: T1  
**Where**: `Franqueados/Index.cshtml` e `franqueadora.css` somente se necessário  
**Change**: Header com Novo Franqueado, busca, estados vazios, colunas agrupadas e paginação desktop/mobile.  
**Tests**: Testes HTTP de ações, estados, markup e querystring.  
**Gate**: Suíte direcionada, build e inspeção responsiva.

### Phase 2 — Verificação

```text
T3
```

### T3: Verificar gates e escopo

**Depends on**: T2  
**Where**: `validation.md`  
**Change**: Executar validadores, sensor discriminativo, build, testes e diff de migrations.  
**Tests**: Suíte completa.  
**Gate**: 0 erros, nenhum warning novo, migrations sem diff e nenhum commit/push.

## Task Breakdown

- [x] T1 — consulta server-side e paginação.
- [x] T2 — UI da listagem.
- [x] T3 — verificação final.

## Restrições

- Não alterar detalhe, edição, novo usuário, contratos, unidades vinculadas, domínio ou autorização.
- Não criar migration.
- Não criar commit nem push.

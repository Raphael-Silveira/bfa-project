# Tasks — Day Use V1

## Test Coverage Matrix

| Camada | Cobertura |
| --- | --- |
| Domain | invariantes Aluno/Avulso e valores |
| Application | autorização, configuração, registro, pagamento |
| PostgreSQL | constraints, índice único e isolamento |
| MVC | rotas, renderização e fluxos de cada perfil |

## Gate Check Commands

- Quick: `dotnet test backend/tests/BFA.UnitTests/BFA.UnitTests.csproj --no-restore`
- Full: `dotnet test backend/BFA.sln --no-restore`
- Build: `dotnet build backend/BFA.sln --no-restore`

## Execution Plan

T1 → T2 → T3 → T4 → T5 → T6

## Task Breakdown

### T1: Domínio e persistência estrutural

Implementar Unidade.DayUse e DayUse, mappings, DbSet e V025.

Tests: testes de invariantes e arquitetura/migration.
Gate: build.

### T2: Application Service e repositório

Implementar configuração, registro, histórico, pagamento operacional e isolamento.

Tests: unitários e PostgreSQL/integrados.
Gate: full.

### T3: Área Unidade

Adicionar controller, ViewModels, views, navegação e autorização de AdminUnidade.

Tests: endpoints e renderização.
Gate: full.

### T4: Área Professor

Adicionar controller, ViewModels, views, navegação e autorização de Professor.

Tests: endpoints e bloqueio de configuração.
Gate: full.

### T5: Gates finais

Executar build e testes completos, revisar diff e parar antes do commit.

Tests: solução completa.
Gate: build + test.

### T6: Busca por participante e exclusão operacional

Implementar filtro PostgreSQL case-insensitive em aluno/avulso; hard delete compartilhado com escopo AdminUnidade e restrição de criador do Professor; confirmação, antiforgery, PRG, preservação de filtros/página e nome de registrador. V025 permanece intacta; V026 concede apenas DELETE ao papel runtime, necessário para a operação.

Tests: Unit de autorização, PostgreSQL para busca/isolamento/exclusão/não impacto em Aluno e Matrícula, controller security e permissão da migration.
Gate: build + UnitTests + IntegrationTests. Status: gates automatizados aprovados; aguardando QA manual no navegador.

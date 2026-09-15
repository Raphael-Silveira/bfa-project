# Tasks — Idempotência Concorrente de Cobranças V1

## Test Coverage Matrix

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Migration/schema | integration PostgreSQL | validação preventiva, índices, avulso e status | `backend/tests/BFA.IntegrationTests/*Cobrancas*` | `dotnet test backend/tests/BFA.IntegrationTests/BFA.IntegrationTests.csproj` |
| Repository/job | unit + integration PostgreSQL | disputa real, resolução do vencedor e falhas não idempotentes | `backend/tests/BFA.UnitTests/Cobrancas/*` and `backend/tests/BFA.IntegrationTests/*Cobrancas*` | `dotnet test backend/BFA.sln` |
| Configuration | build/integration existing | Hangfire permanece desabilitado | existing Safety V1 tests | `dotnet test backend/BFA.sln` |

## Gate Check Commands

| Gate Level | Command |
| --- | --- |
| Quick | `dotnet test backend/tests/BFA.UnitTests/BFA.UnitTests.csproj --filter FullyQualifiedName~Cobrancas` |
| Full | `dotnet test backend/BFA.sln` |
| Build | `dotnet build backend/BFA.sln; dotnet test backend/BFA.sln; git diff --check; git diff -- database/migrations` |

## Execution Plan

```text
T1 -> T2 -> T3 -> T4 -> T5 -> T6
```

### T1: Criar V021 com validação preventiva e índices

**Depends on**: none  
**Where**: `database/migrations/V021__garantir_idempotencia_cobrancas_automaticas.sql`  
**Change**: Validar duplicidades antes dos índices e criar as duas unicidades sem excluir canceladas.  
**Tests**: migration PostgreSQL com base limpa, duplicidades, avulsas e cancelada+ativa.  
**Gate**: testes direcionados da migration verdes.

### T2: Adicionar operação idempotente de persistência automática

**Depends on**: T1  
**Where**: `ICobrancasRepositorio`, `CobrancasRepositorio` e contratos Application necessários.  
**Change**: Tratar somente `23505` dos índices financeiros e recuperar a cobrança vencedora.  
**Tests**: unitários para criada, existente e violação não financeira.  
**Gate**: Quick.

### T3: Usar a operação idempotente nos jobs

**Depends on**: T2  
**Where**: `GeracaoCobrancasJob.cs`.  
**Change**: Manter pré-consulta e fazer a criação automática idempotente para mensalidade e taxa.  
**Tests**: baseline Safety V1 atualizado para cancelada não recriar.  
**Gate**: Quick.

### T4: Cobrir concorrência real em PostgreSQL

**Depends on**: T3  
**Where**: `backend/tests/BFA.IntegrationTests/*Cobrancas*`.  
**Change**: Executar duas gerações/conexões concorrentes e cobrir tenants, competências, matrículas e avulsas.  
**Tests**: todos os cenários aprovados na spec.  
**Gate**: Full.

### T5: Documentar a invariância aprovada

**Depends on**: T4  
**Where**: `.specs/features/financeiro-idempotencia-cobrancas-v1/` e documentação financeira canônica, se necessária.  
**Change**: Registrar DataVencimento como competência e a dívida de reemissão explícita.  
**Tests**: inspeção de diff e validadores TLC.  
**Gate**: `validate_spec.py`, `validate_tasks.py` e `git diff --check`.

### T6: Verificação final sem Hangfire

**Depends on**: T5  
**Where**: sem alteração de produto.  
**Change**: Executar build, testes completos e confirmar V001–V020 intactas.  
**Tests**: suíte completa.  
**Gate**: Build.

## Task Breakdown

- [x] T1 — V021 com validação preventiva e índices.
- [x] T2 — Persistência idempotente para cobranças automáticas.
- [x] T3 — Integração da operação idempotente nos jobs.
- [x] T4 — Concorrência real em PostgreSQL.
- [x] T5 — Documentação e rastreabilidade.
- [x] T6 — Build, suíte completa e verificação final.

## Status

- [x] T1 — V021 e índices
- [x] T2 — persistência idempotente
- [x] T3 — jobs
- [x] T4 — concorrência PostgreSQL
- [x] T5 — documentação
- [x] T6 — gates finais

## Restrições

- Não alterar `.specs/features/governanca-acesso-usuarios-v1/`.
- Não habilitar Hangfire.
- Não executar migrations em banco de desenvolvimento.
- Não executar `INSERT`, `UPDATE` ou `DELETE` manual no banco de desenvolvimento.
- Não criar commit ou push nesta fase.

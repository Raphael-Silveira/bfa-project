# Tasks — Reconciliação Matrícula × Cobranças V1

## Task Breakdown

### T1: Atualizar especificação e decisão de pagamento integral

**Depends on**: none  
**Where**: `.specs/features/financeiro-reconciliacao-matricula-cobrancas-v1/`  
**Change**: Registrar regras de competência, pagamento total e ausência de V022.  
**Tests**: validadores TLC.  
**Gate**: `validate_spec.py` and `validate_tasks.py`.

### T2: Impedir pagamento parcial no Domínio

**Depends on**: T1  
**Where**: `backend/src/BFA.Domain/Cobrancas/Cobranca.cs`  
**Change**: Exigir valor exatamente igual ao saldo integral.  
**Tests**: testes unitários de pagamento.  
**Gate**: Quick unit tests.

### T3: Tornar pagamento consolidado integral e atômico

**Depends on**: T2  
**Where**: `backend/src/BFA.Infrastructure/Cobrancas/CobrancasRepositorio.cs`  
**Change**: Quitar cada cobrança inteira e não persistir pagamentos incompletos.  
**Tests**: PostgreSQL real.  
**Gate**: targeted integration tests.

### T4: Reconciliar cobranças na transação de finalização

**Depends on**: T3  
**Where**: Domínio Financeiro e `MatriculasRepositorio.FinalizarAsync`.  
**Change**: Cancelar somente mensalidades pendentes posteriores à competência final.  
**Tests**: integração PostgreSQL.  
**Gate**: reconciliation integration tests.

### T5: Cobrir status, competência, pagamentos, tenant e idempotência

**Depends on**: T4  
**Where**: testes unitários e de integração.  
**Change**: Cobrir todos os cenários aprovados.  
**Tests**: suíte direcionada.  
**Gate**: unit and integration tests.

### T6: Executar build, testes completos e verificar V001–V021/Hangfire

**Depends on**: T5  
**Where**: solução completa.  
**Change**: Executar gates sem banco compartilhado.  
**Tests**: build, unitários e integração.  
**Gate**: full build and test suite.

## Test Coverage Matrix

| Layer | Tests |
| --- | --- |
| Domain | pagamento integral, menor/maior, paga e cancelada |
| Application/Repository | consolidado sem parcial e sem alterações parciais |
| Matrícula/Financeiro | reconciliação na mesma transação e rollback |
| PostgreSQL | status, competência, taxa, avulsa, pagamentos e cross-tenant |
| Regression | job não gera após finalização; V001–V021; Hangfire disabled |

## Gate Check Commands

| Gate | Command |
| --- | --- |
| TLC | `validate_spec.py` and `validate_tasks.py` |
| Build | `dotnet build backend/BFA.sln` |
| Full tests | `dotnet test backend/BFA.sln` |

## Execution Plan

```text
T1 -> T2 -> T3 -> T4 -> T5 -> T6
```

## Restrictions

- Não alterar `.specs/features/governanca-acesso-usuarios-v1/`.
- Não criar V022 sem aprovação explícita após justificativa.
- Não executar migrations em banco compartilhado.
- Não habilitar Hangfire nem executar jobs reais no banco de desenvolvimento.
- Não corrigir automaticamente registros históricos parcialmente pagos.
- Não fazer commit ou push nesta fase.

# Financeiro — Idempotência Concorrente de Cobranças V1

## Problem Statement

As verificações atuais de existência são apenas aplicacionais. Duas execuções
concorrentes do job podem observar a mesma ausência e inserir cobranças
automáticas duplicadas. Esta fase coloca a invariância no PostgreSQL e trata a
disputa esperada sem transformar uma corrida idempotente em falha do job.

## Goals

- [x] Garantir uma única mensalidade automática por matrícula e competência.
- [x] Garantir uma única taxa automática por matrícula.
- [x] Preservar múltiplas cobranças avulsas.
- [x] Validar duplicidades antes de criar os índices da V021.
- [x] Tratar somente as violações de unicidade financeira conhecidas como
      disputa idempotente.

## Out of Scope

| Feature | Reason |
| --- | --- |
| Reemissão de cobrança cancelada | Será fluxo explícito futuro com autorização, auditoria e vínculo histórico. |
| Reconciliação de matrícula encerrada/cancelada | Fase posterior. |
| Desconto, juros, multa e estorno | Não fazem parte da invariância concorrente. |
| Gateway, pagador e relatórios novos | Não relacionados à proteção contra duplicidade. |
| Execução real do Hangfire em Development | Hangfire permanece desabilitado por padrão. |

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Identidade da mensalidade | `organizacao_id + unidade_id + matricula_id + tipo + ano/mês(data_vencimento)` | É a regra já usada pelo job e a competência técnica aprovada para esta fase. | yes |
| Identidade da taxa | `organizacao_id + unidade_id + matricula_id + tipo` | Uma matrícula possui no máximo uma taxa automática. | yes |
| Status da cobrança automática | Todos os status ocupam a identidade, inclusive `Cancelada`. | Cancelamento é decisão financeira explícita e não deve ser desfeito pelo job. | yes |
| Reemissão | Não automática; somente fluxo futuro explícito. | Evita recriação silenciosa. | yes |
| Representação de enum | Confirmar no schema/mapping antes da SQL final. | A migration deve usar os valores realmente persistidos. | yes |
| Dados existentes | A V021 valida e falha antes dos índices se encontrar duplicidades. | Não há acesso atual ao banco de desenvolvimento e nenhuma limpeza automática é permitida. | yes |

**Open questions:** none - all resolved or logged above.

## User Stories

### P1: Proteger obrigações automáticas ⭐ MVP

**User Story**: As the Financeiro system, I want the database to own automatic
charge identity so concurrent job executions cannot create duplicate obligations.

**Why P1**: A duplicidade financeira é uma falha de integridade de dados.

**Acceptance Criteria**:

1. The database SHALL enforce at most one non-avulsa mensalidade for each
   `organizacao_id`, `unidade_id`, `matricula_id`, persisted mensalidade type,
   and year/month of `data_vencimento`, regardless of status.
2. The database SHALL enforce at most one non-avulsa taxa de matrícula for each
   `organizacao_id`, `unidade_id`, `matricula_id`, and persisted matrícula type,
   regardless of status.
3. The database SHALL allow multiple `Avulso` charges for the same student or
   enrollment.
4. WHEN V021 detects an existing duplicate before index creation THEN the
   migration SHALL fail with a clear duplicate-sanitization message and SHALL
   not create either financial unique index.
5. WHEN two concurrent automatic inserts target the same approved identity THEN
   PostgreSQL SHALL persist exactly one charge and reject the other with
   unique-violation SQLSTATE `23505` on a known financial index.
6. IF an automatic insert receives `23505` from a financial unique index THEN
   the application SHALL resolve the existing winning charge and SHALL complete
   the job without propagating a technical failure.
7. IF an insert receives `23505` from an unrelated unique constraint THEN the
   application SHALL propagate the failure as an unexpected persistence error.
8. WHEN a cancelled automatic charge already occupies an approved identity
   THEN a subsequent automatic generation SHALL create zero replacement charges.

**Independent Test**: Run V021 and the concurrent PostgreSQL scenarios against
an isolated PostgreSQL instance, then assert row counts and SQLSTATE handling.

### P2: Preservar isolamento e competências

**User Story**: As a multi-tenant Financeiro system, I want the identity to
remain tenant-safe and competence-specific so valid charges remain independent.

**Why P2**: The uniqueness rule must not block legitimate operations.

**Acceptance Criteria**:

1. WHEN two different competencies are generated for the same matrícula THEN
   the database SHALL allow both monthly charges.
2. WHEN two different matrículas are generated THEN the database SHALL allow
   both monthly charges.
3. WHEN equal local identifiers are used in different tenant scopes in an
   isolated schema THEN the database SHALL allow the tenant-distinct charges.

**Independent Test**: Execute PostgreSQL integration cases with different
competencies, matrículas and tenant keys.

## Edge Cases

- Mensalidade cancelada plus mensalidade ativa na mesma competência is a
  duplicate and must block V021.
- Taxa cancelada plus taxa ativa na mesma matrícula is a duplicate and must
  block V021.
- Avulsa cancelada plus avulsa ativa remains allowed.
- A duplicate caused by the financial indexes is idempotent; another unique
  violation is not.
- Hangfire remains disabled during all automated tests.

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| FINID-01 | P1: Proteger obrigações automáticas | Execute | Verified |
| FINID-02 | P1: Proteger obrigações automáticas | Execute | Verified |
| FINID-03 | P1: Proteger obrigações automáticas | Execute | Verified |
| FINID-04 | P1: Proteger obrigações automáticas | Execute | Verified |
| FINID-05 | P1: Proteger obrigações automáticas | Execute | Verified |
| FINID-06 | P1: Proteger obrigações automáticas | Execute | Verified |
| FINID-07 | P1: Proteger obrigações automáticas | Execute | Verified |
| FINID-08 | P1: Proteger obrigações automáticas | Execute | Verified |
| FINID-09 | P2: Preservar isolamento e competências | Execute | Verified |
| FINID-10 | P2: Preservar isolamento e competências | Execute | Verified |
| FINID-11 | P2: Preservar isolamento e competências | Execute | Verified |

**Coverage:** 11 total, 11 verified.

## Task Breakdown

- [x] T1 — V021 com validação preventiva e índices.
- [x] T2 — Persistência idempotente para cobranças automáticas.
- [x] T3 — Integração da operação idempotente nos jobs.
- [x] T4 — Concorrência real em PostgreSQL.
- [x] T5 — Documentação e rastreabilidade.
- [x] T6 — Build, suíte completa e verificação final.

## Success Criteria

- [x] V021 falha preventivamente diante de duplicidades e não cria índices
      parcialmente.
- [x] Concorrência real persiste uma única cobrança automática.
- [x] Cobranças avulsas, competências diferentes e matrículas diferentes
      continuam permitidas.
- [x] O baseline da Safety V1 permanece verde.
- [x] V001–V020 permanecem intactas e Hangfire continua desabilitado.

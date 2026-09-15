# Financeiro — Reconciliação Matrícula × Cobranças V1

## Problem Statement

O encerramento ou cancelamento de uma Matrícula hoje atualiza a Matrícula e a
grade, mas não reconcilia cobranças futuras. O fluxo de pagamento também deve
garantir que uma cobrança seja quitada integralmente, sem pagamento parcial.

## Goals

- Reconciliar mensalidades futuras na mesma operação de encerramento/cancelamento.
- Preservar histórico, cobranças pagas, atrasadas, vencidas e taxas.
- Impedir novas mensalidades após o fim efetivo da Matrícula.
- Garantir pagamento integral no Domínio e no fluxo consolidado.
- Preservar V001–V021 e não criar V022 sem necessidade.

## Out of Scope

- Pró-rata, parcelamento, crédito, saldo em conta, estorno, juros ou multa.
- Gateway, Pix automático, boleto automático, split, chargeback ou conciliação.
- Novo job Hangfire.
- Saneamento automático de dados históricos parcialmente pagos.

## Assumptions & Open Questions

| Decision | Choice | Confirmed? |
| --- | --- | --- |
| Competência do encerramento | Mês de `DataFimReal` é preservado integralmente. | yes |
| Pagamento parcial | Não existe; o valor deve quitar a cobrança integralmente. | yes |
| Pagamentos históricos parciais | Não corrigir automaticamente nesta fase. | yes |
| Migration | Nenhuma V022; V001–V021 permanecem imutáveis. | yes |
| Execução | Sem novo job e sem Hangfire habilitado. | yes |

**Open questions:** none.

## User Stories

### P1: Reconciliar encerramento financeiro ⭐ MVP

**User Story**: As the Financeiro system, I want enrollment finalization to
reconcile future monthly charges atomically so historical obligations remain
consistent.

### P1: Garantir pagamento integral

**User Story**: As the Financeiro system, I want every payment to settle one
whole charge so partial financial states cannot be created.

## Approved Rules

| Contexto | Regra |
| --- | --- |
| Paga | Preservar sem alteração. |
| Cancelada | Preservar sem reabrir ou recriar. |
| Atrasada | Preservar como obrigação. |
| Pendente vencida | Preservar como obrigação. |
| Mensalidade pendente na competência do mês de `DataFimReal` | Preservar. |
| Mensalidade pendente em competência posterior | Cancelar por reconciliação. |
| Taxa de matrícula | Preservar, independentemente da competência. |
| Avulsa | Preservar; não participa da reconciliação automática. |
| Pagamento | Deve quitar exatamente o valor integral da cobrança. |

`DataFimReal` é inclusiva. A competência mensal é comparada por ano/mês,
preservando o mês do encerramento mesmo quando a data de vencimento cair após
o dia do encerramento. Não há pró-rata.

## EARS Acceptance Criteria

- WHEN a Matrícula is finalized THEN the system SHALL reconcile its monthly
  charges inside the same database transaction.
- WHEN a pending monthly charge belongs to a competence after `DataFimReal`
  THEN the system SHALL cancel it.
- WHEN a charge is paid, cancelled, overdue, pending overdue, a registration
  fee, or manual one-off THEN reconciliation SHALL preserve it.
- WHEN reconciliation is repeated THEN the final state SHALL be unchanged.
- WHEN a payment value differs from the outstanding full charge value THEN the
  Domain SHALL reject it.
- WHEN a consolidated payment is registered THEN every selected charge SHALL
  be fully paid or the whole operation SHALL produce no payments.
- WHEN existing data is partially paid THEN the system SHALL not auto-correct
  it in this phase.
- WHEN finalization runs for a tenant THEN reconciliation SHALL affect only
  charges from the same organization, unit, and enrollment.
- WHEN the enrollment is no longer active THEN future monthly generation SHALL
  not create a new charge.

## Requirement Traceability

| Requirement | Story | Verification | Status |
| --- | --- | --- | --- |
| RECON-01 | P1: Reconciliar encerramento financeiro | domain/integration | Verified |
| RECON-02 | P1: Reconciliar encerramento financeiro | domain/integration | Verified |
| RECON-03 | P1: Reconciliar encerramento financeiro | PostgreSQL integration | Verified |
| RECON-04 | P1: Garantir pagamento integral | unit | Verified |
| RECON-05 | P1: Garantir pagamento integral | PostgreSQL integration | Verified |
| RECON-06 | P1: Reconciliar encerramento financeiro | PostgreSQL integration | Verified |
| RECON-07 | both | inspection | Verified |

## Migration Decision

No schema change is required for the approved behavior. V001–V021 remain
immutable and V022 is not created. Reconciliation uses existing charge fields
and the existing transaction; no shared database is queried or modified during
implementation.

# Design — Reconciliação Matrícula × Cobranças V1

## Reconciliation Boundary

The existing `MatriculasRepositorio.FinalizarAsync` transaction remains the
transaction boundary. It loads charges for the exact organization, unit and
enrollment, applies the Domain reconciliation rule, then persists grades,
charges and enrollment status before commit.

No Hangfire job is added. The existing job continues to filter active
enrollments and the finalization transaction closes future monthly charges.

## Domain Rule

The Domain exposes a dedicated reconciliation cancellation operation. It is
available only for pending monthly charges whose year/month competence is after
the inclusive final date competence. It does not use free-text observations as
business state.

Registration fees and `Avulso` charges are excluded. Paid, cancelled, overdue
and already overdue-pending charges are never changed.

## Full Payment

`Cobranca.RegistrarPagamento` rejects any amount other than the exact remaining
full value. The consolidated repository flow selects concrete charges and
settles each entire balance; it does not accept an aggregate amount and cannot
partially settle a final charge. If a future caller supplies an insufficient
aggregate, the operation returns no payments and does not save changes.

Existing partially paid rows, if any, are observed only through tests or a
safe read-only database query. They are not automatically corrected.

## Schema and Operations

No V022 is planned. V001–V021 remain immutable. The implementation does not
start the application with Hangfire enabled and does not run migrations against
development, staging or production databases.

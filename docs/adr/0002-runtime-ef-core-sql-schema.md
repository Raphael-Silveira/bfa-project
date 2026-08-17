# ADR 0002 — Runtime EF Core, SQL-Managed Schema

**Status:** Accepted

## Context

The previous generation of the product experienced operational risk when development-time model/schema changes and automated ORM migrations were allowed to influence shared/production databases.

The new platform needs ORM productivity without giving application startup authority over production DDL.

## Decision

Use Entity Framework Core for runtime persistence and querying.

Manage database schema evolution using immutable, versioned SQL scripts stored under `/database/migrations`.

The production API runtime credential does not receive DDL privileges.

The API must never call `EnsureCreated`, `EnsureDeleted`, or `Database.Migrate` during startup.

## Consequences

Positive:

- Production DDL is explicit and reviewable
- Database history is stable
- Application mistakes have a smaller schema-level blast radius
- EF Core remains available for productive application development

Trade-offs:

- Developers must keep SQL schema changes and EF mappings synchronized
- Database deployment needs its own controlled process
- Integration tests become important to detect mapping/schema drift

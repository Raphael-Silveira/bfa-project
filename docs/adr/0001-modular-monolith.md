# ADR 0001 — Modular Monolith

**Status:** Accepted

## Context

BFA needs one backend serving multiple client experiences while the business domain is still evolving.
Splitting the platform into microservices now would introduce distributed-system complexity before stable service boundaries are known.

## Decision

Start with one backend solution implemented as a modular monolith.

Business capabilities are separated by module and project boundaries inside the solution.

Frontends remain independent clients of the API.

## Consequences

Positive:

- Simpler local development and deployment
- Easier cross-domain refactoring while boundaries evolve
- One transactional database boundary initially
- Lower operational complexity
- Clear path to extract a module later if justified

Trade-offs:

- Strong internal discipline is required to prevent a "big ball of mud"
- Modules cannot scale/deploy independently at first
- Architecture tests and code review must protect dependency boundaries

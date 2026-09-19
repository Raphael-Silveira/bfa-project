# Tasks

## Test Coverage Matrix

| Requirement | Unit | Integration | Architecture/UI |
|---|---|---|---|
| PGA-01/PGA-02 | AulaCancelamentoTests | — | V024 architecture test |
| PGA-03/PGA-05 | — | Professor call/cancel flows | — |
| PGA-04/PGA-09 | — | Professor confirmation render | Razor contract |
| PGA-06 | — | cross-unit/cross-turma access | controller contract |
| PGA-07/PGA-08 | — | Portal Aluno regressions | view contract |

## Gate Check Commands

- Quick: `dotnet test backend/tests/BFA.UnitTests/BFA.UnitTests.csproj --no-restore`
- Full: `dotnet test backend/tests/BFA.IntegrationTests/BFA.IntegrationTests.csproj --no-restore`
- Build: `dotnet build backend/BFA.sln --no-restore`

## Execution Plan

```text
T1 -> T2 -> T3 -> T4
```

## Task Breakdown

### T1: V024 e domínio

**Depends on:** none  
**Where:** `database/migrations/V024__adicionar_auditoria_cancelamento_aula.sql`, `Aula.cs`, `AulaConfiguration.cs`  
**Tests:** testes unitários de motivo, auditoria e repetição.  
**Gate:** Build  
**Done when:** V024 existe, o domínio persiste auditoria e não há V025.

### T2: Consulta e persistência do Professor

**Depends on:** T1  
**Where:** contratos Application e repositório Professor  
**Tests:** alunos elegíveis, presença existente, autorização, idempotência e cancelamento bloqueado.  
**Gate:** Full  
**Done when:** a consulta retorna participação/chamada e o caso de uso salva sem duplicidade.

### T3: Controller e Razor

**Depends on:** T2  
**Where:** Controller, ViewModels e Views da Área Professor  
**Tests:** renderização, POST válido, motivo inválido, aula cancelada e antiforgery.  
**Gate:** Full  
**Done when:** o Professor consulta, salva chamada e cancela com motivo; cancelada fica somente leitura.

### T4: Regressões Portal Aluno

**Depends on:** T3  
**Where:** Agenda, consulta Aluno e testes de regressão  
**Tests:** agenda mostra motivo, confirmação bloqueada, próxima aula ignora cancelada e frequência preservada.  
**Gate:** Build  
**Done when:** nenhuma regressão altera Financeiro, Matrícula, Login ou outras Áreas.

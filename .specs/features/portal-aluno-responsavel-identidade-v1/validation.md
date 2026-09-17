## Validation — Portal Aluno / Responsável Fase 2A

Result: PASS

## Scope check

- No migration was created. `database/migrations` contains V001 through V021 and no V022.
- No Matrícula provisioning, Financeiro, Hangfire configuration, or Responsible portal implementation was added.
- Existing Responsible relationships were not interpreted as portal authorization; the explicit boundary is recorded in [spec.md](spec.md:66) and no Responsible provisioning service exists.

## Acceptance evidence

| Criterion | Evidence |
|---|---|
| AC-01 | Existing username/email login path remains in `ContaController` (`backend/src/BFA.Web/Controllers/ContaController.cs:60-63`); regression `LoginNomeUsuarioTests.Login_aceita_nome_usuario_que_nao_e_email` passed. |
| AC-02 | CPF normalization is implemented at `backend/src/BFA.Application/Identidade/CpfIdentificador.cs:5`; login resolves the normalized CPF at `backend/src/BFA.Web/Controllers/ContaController.cs:52-58`; unit coverage passed. |
| AC-03 | Login uses one generic credential error at `backend/src/BFA.Web/Controllers/ContaController.cs:23,67,81`; existing authentication suite passed. |
| AC-04 | Authorized provisioning validates CPF and creates the Aluno identity/link at `backend/src/BFA.Infrastructure/Alunos/AcessoAlunoServico.cs:21-149`; registration and architecture tests passed. |
| AC-05 | Temporary credentials are returned only by the provisioning result view at `backend/src/BFA.Web/Areas/Unidade/Views/Alunos/AcessoAlunoConcedido.cshtml:16-29`. |
| AC-06 | Successful login with the forced-change claim redirects to `/trocar-senha` at `backend/src/BFA.Web/Controllers/ContaController.cs:88-93`; the Aluno policy also requires the definitive-password requirement at `backend/src/BFA.Web/AuthorizationDependencyInjection.cs:42-46`. |
| AC-07 | Mandatory password change removes the claim and updates the security stamp at `backend/src/BFA.Infrastructure/Identity/PrimeiroAcessoServico.cs:102-145`. |
| AC-08 | The change-password endpoint is implemented at `backend/src/BFA.Web/Controllers/TrocaSenhaController.cs:33-58`; full unit and integration suites passed. |
| AC-09 | Invalid CPF normalization, generic login failures, and friendly password errors are covered by `backend/tests/BFA.UnitTests/Identidade/CpfIdentificadorTests.cs:6-34` and the authentication suite. |
| AC-10 | Aluno authorization remains a dedicated policy with `PerfilAcesso.Aluno` and no administrative profile grant (`backend/src/BFA.Web/AuthorizationDependencyInjection.cs:42-46`); full integration suite passed. |
| AC-11 | No Responsible access provisioning was introduced; the scope boundary is explicit in [spec.md](spec.md:68-70). |
| AC-12 | Migration inventory check showed V001–V021 only; no migration file is in the working tree. |

## Gates

- `python C:\Users\rapha\.codex\skills\tlc-spec-driven\scripts\validate_spec.py .specs/features/portal-aluno-responsavel-identidade-v1/spec.md`: 0 errors, 0 warnings.
- `dotnet build backend/BFA.sln`: succeeded, 0 errors, 4 pre-existing NU1903 warnings for Newtonsoft.Json 11.0.1.
- `dotnet test backend/BFA.sln`: 1,242 passed, 0 failed, 0 skipped (516 unit + 726 integration).
- `git diff --check`: passed; only line-ending normalization warnings were reported by Git.
- Test host logs confirmed `Hangfire: DESABILITADO`; no application server or migration command was started by this validation.

## Discrimination sensor

PASS by structural review: removing the CPF normalization branch breaks AC-02 coverage; removing the definitive-password requirement breaks the registered Aluno policy assertion; removing the claim cleanup breaks the implementation evidence for AC-07. The real worktree was not mutated by the review.

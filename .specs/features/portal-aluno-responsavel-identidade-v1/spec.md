# Portal Aluno / Responsável — Fase 2A: Identidade e Primeiro Acesso

Status: implementada sem migration; provisionamento automático na Matrícula permanece fora do escopo.

## Problem Statement

A Área Aluno possui a base de autorização, mas ainda não oferece login por CPF nem um provisionamento seguro com primeiro acesso obrigatório. A Fase 2A deve habilitar a identidade do Aluno sem quebrar os logins existentes e sem interpretar vínculos de Responsável como autorização de portal.

## User Stories

- Como operador autorizado da Unidade, quero conceder acesso ao Aluno e visualizar uma senha temporária uma única vez.
- Como Aluno, quero entrar com CPF mascarado ou não mascarado e trocar a senha temporária antes de acessar o portal.
- Como administrador existente, quero continuar entrando com e-mail ou nome de usuário.
- Como produto, quero manter o Portal Responsável bloqueado até existir uma regra explícita de autorização.

## Requirements

### AUTH-01 — Identificador de login

**R1.1 (Ubiquitous):** O sistema deve preservar o login existente por e-mail/nome de usuário.

**R1.2 (Event-driven):** Quando um Aluno informar CPF mascarado ou não mascarado no login, o sistema deve normalizar os onze dígitos e resolver apenas uma identidade com vínculo ativo `PerfilAluno`.

**R1.3 (Unwanted):** O sistema não deve usar CPF como senha nem revelar se CPF existe quando a autenticação falhar.

### AUTH-02 — Provisionamento

**R2.1 (Event-driven):** Quando operador autorizado da Unidade solicitar acesso, o sistema deve validar o tenant, criar ou reutilizar a identidade do Aluno, associar `Aluno.UsuarioId` e criar/reativar um vínculo ativo `PerfilAluno`.

**R2.2 (Ubiquitous):** A nova identidade deve usar CPF normalizado como `UserName` e senha temporária criptograficamente aleatória, exibida somente na resposta da operação.

**R2.3 (Unwanted):** O provisionamento repetido não deve criar uma segunda identidade ou vínculo ativo.

**R2.4 (State-driven):** CPF conflitante ou identidade incompatível deve ser rejeitado sem criar associação ambígua.

### AUTH-03 — Primeiro acesso

**R3.1 (State-driven):** Enquanto a identidade possuir o claim `bfa:must-change-password=true`, o acesso à policy do Aluno deve ser negado e o login deve direcionar para `/trocar-senha`.

**R3.2 (Event-driven):** Quando a troca autenticada for concluída, o sistema deve remover o claim, atualizar o `SecurityStamp` e exigir a nova senha em autenticações seguintes.

**R3.3 (Unwanted):** A senha temporária não deve continuar válida depois da troca definitiva.

### AUTH-04 — Responsável e compatibilidade

**R4.1 (Unwanted):** O sistema não deve provisionar acesso de Responsável usando `PrincipalContato`, `ResponsavelFinanceiro` ou relação familiar como autorização implícita.

**R4.2 (Ubiquitous):** As rotas e logins de Franqueadora, Unidade e Professor devem permanecer compatíveis.

### AUTH-05 — Persistência e segurança

**R5.1 (Ubiquitous):** A implementação deve usar as estruturas existentes de Identity, `usuario_claims`, `alunos.usuario_id` e `vinculos_acesso`, sem criar V022 ou alterar V001–V021.

**R5.2 (Unwanted):** Nenhum log deve conter CPF completo, senha temporária ou token.

## Out of Scope

- Provisionamento automático na criação ou alteração de Matrícula (Fase 2B).
- Área completa de Responsável.
- Nova migration SQL, alteração de schema, Financeiro, Hangfire ou mudanças visuais amplas.

## Assumptions & Open Questions

- `Alunos.UsuarioId`, `Responsaveis.UsuarioId`, `usuario_claims` e `vinculos_acesso` já existem e suportam a Fase 2A.
- O acesso de Responsável depende de uma futura decisão explícita sobre qual vínculo autoriza login; até lá permanece bloqueado.
- O fluxo existente de primeiro acesso por token continua disponível para usuários administrativos e professores.

Open questions: none for Fase 2A.

Future product decision: define which explicit relationship authorizes Responsible access in Fase 2B.

## Acceptance Criteria

- **AC-01:** login administrativo, de Unidade e Professor continua resolvendo e-mail/nome de usuário.
- **AC-02:** login de Aluno aceita CPF mascarado e não mascarado.
- **AC-03:** CPF inexistente e senha incorreta retornam a mesma mensagem genérica.
- **AC-04:** operador autorizado provisiona Aluno com `UserName` CPF, senha temporária aleatória, `Aluno.UsuarioId` e vínculo ativo `PerfilAluno`.
- **AC-05:** a senha temporária é apresentada somente na tela de resultado do provisionamento.
- **AC-06:** login com senha temporária direciona para troca obrigatória.
- **AC-07:** troca válida remove o claim, atualiza o stamp e invalida a senha temporária.
- **AC-08:** senha nova permite autenticação normal e acesso ao Portal Aluno.
- **AC-09:** troca inválida, CPF duplicado e associação incompatível são rejeitados com mensagem amigável.
- **AC-10:** Aluno não recebe acesso às áreas Franqueadora ou Unidade administrativa.
- **AC-11:** nenhum acesso de Responsável é criado por inferência de contato/financeiro/relação.
- **AC-12:** nenhuma migration é criada; V001–V021 permanecem intactas.

## Requirement Traceability

| Requirement | Implementation / verification |
|---|---|
| AUTH-01 | `CpfIdentificador`, `UsuarioPorCpfConsulta`, `ContaController`, `CpfIdentificadorTests`, login regression tests |
| AUTH-02 | `AcessoAlunoServico`, `AlunosController`, `InfrastructureRegistrationTests` |
| AUTH-03 | `IdentidadeClaims`, `PrimeiroAcessoServico`, `SenhaDefinitivaHandler`, `TrocaSenhaController`, `AuthorizationRegistrationTests` |
| AUTH-04 | no Responsible provisioning; existing login/authorization regression suite |
| AUTH-05 | existing schema/Identity tables; migration immutability tests; no migration file added |

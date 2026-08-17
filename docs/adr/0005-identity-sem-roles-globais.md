# ADR 0005 — Identity sem roles globais

**Status:** Accepted

## Context

A BFA precisa autenticar usuários que podem atuar em diferentes organizações e unidades. Perfis como administrador da franqueadora, administrador de unidade, professor, aluno e responsável dependem do contexto multi-tenant e não representam papéis globais da plataforma.

Um mesmo usuário poderá possuir mais de um vínculo e exercer perfis diferentes conforme a organização ou unidade acessada. Modelar esses perfis como `IdentityRole` perderia esse contexto e misturaria autenticação técnica com autorização de negócio.

## Decision

ASP.NET Core Identity será responsável somente pela autenticação e pelo armazenamento técnico da conta. `UsuarioIdentity` herda de `IdentityUser<Guid>` e não contém dados de negócio.

O modelo utiliza `IdentityUserContext<UsuarioIdentity, Guid>`, sem `IdentityRole`, `RoleManager`, tabelas de roles ou vínculos globais usuário-role.

A autorização pertence à BFA e será implementada posteriormente por vínculos contextuais entre usuário, `Organizacao` e `Unidade`, com perfis e permissões próprios do domínio. Um usuário poderá possuir múltiplos vínculos e perfis.

## Consequences

Positive:

- A autenticação permanece isolada como preocupação técnica.
- Perfis e permissões preservam o contexto de organização e unidade.
- Um usuário pode atuar em mais de um tenant sem duplicar sua conta de autenticação.
- O schema Identity não cria tabelas globais de roles.

Trade-offs:

- A autorização multi-tenant exigirá um modelo próprio e testes de isolamento em etapa posterior.
- Recursos que esperam `IdentityRole` não poderão ser usados diretamente para representar perfis da BFA.
- Alterações futuras no mecanismo de autenticação, como habilitar o schema v3 de passkeys, exigirão decisão explícita e nova migration SQL.

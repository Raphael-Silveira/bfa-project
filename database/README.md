# Banco de dados

PostgreSQL é o banco de dados oficial e persistente da BFA Platform. O Entity Framework Core com Npgsql é usado somente para persistência em runtime dentro de `BFA.Infrastructure`.

## Ambientes

Cada ambiente possui banco e credenciais próprios:

| Ambiente | Banco |
| --- | --- |
| Development | `bfa_dev` |
| Staging | `bfa_staging` |
| Production | `bfa_prod` |

Development e Staging nunca podem apontar para `bfa_prod`. Consulte `docs/ENVIRONMENTS.md` para a origem das configurações e dos segredos.

## Migrations SQL

O schema é gerenciado por scripts SQL imutáveis e versionados em `database/migrations`. As migrations atuais são:

```text
V001__criar_organizacoes_e_unidades.sql
V002__criar_identidade.sql
V003__criar_vinculos_acesso.sql
```

V001 cria o histórico de schema e a fundação de multi-tenancy formada por `organizacoes` e `unidades`. V002 cria a persistência técnica de autenticação do ASP.NET Core Identity. V003 cria `vinculos_acesso`, o contexto multi-tenant de autorização associado a usuários, organizações, unidades e perfis. Depois de aplicada em qualquer ambiente compartilhado, uma migration nunca deve ser editada ou removida; correções são feitas por novos scripts versionados.

A tabela `bfa_schema_history` registra a versão aplicada, sua descrição, o instante UTC e o usuário de deploy responsável. Somente o processo de deploy controla esse histórico; `bfa_app_role` não recebe permissões nessa tabela.

O deploy do schema é uma operação controlada e separada do deploy da aplicação. O runtime nunca chama:

```csharp
Database.EnsureCreated();
Database.EnsureDeleted();
Database.Migrate();
```

Migrations do Entity Framework não são a fonte de verdade do schema. Nenhuma migration SQL ou EF é executada automaticamente pela aplicação.

## Autenticação e autorização

ASP.NET Core Identity é responsável somente por autenticação. O modelo utiliza `Guid` e não utiliza Identity Roles.

V002 corresponde ao schema v2 do Identity sem roles e cria apenas:

```text
usuarios
usuario_claims
usuario_logins
usuario_tokens
```

O schema v3 de passkeys é opt-in no .NET 10 e não está habilitado nesta etapa; portanto, V002 não cria `usuario_passkeys`. Também não existem tabelas de roles ou de vínculos usuário-role.

Os perfis da BFA são representados por `VinculoAcesso`, com contexto de `Organizacao` e, quando aplicável, `Unidade`. Eles não são roles globais do Identity. Policies e permissões serão implementadas em etapa posterior.

## Papéis PostgreSQL

### `bfa_app_role`

Role comum, sem login, que concentra as permissões de runtime da aplicação. Migrations podem referenciar esse role porque seu nome é igual em todos os ambientes.

Os usuários de login específicos de ambiente são membros dele:

```text
bfa_app_role (NOLOGIN)
├── bfa_dev_app (LOGIN)
├── bfa_staging_app (LOGIN)
└── bfa_prod_app (LOGIN)
```

`bfa_app_role` recebe apenas as permissões DML necessárias sobre as tabelas de negócio:

```text
SELECT
INSERT
UPDATE
DELETE
```

Não será proprietário do schema e não terá permissão para `CREATE`, `ALTER` ou `DROP`.

V002 concede ao role as permissões DML necessárias nas tabelas Identity e acesso à sequence usada pelo identificador de `usuario_claims`. V003 concede DML somente em `vinculos_acesso`. O role continua sem permissão para escrever em `bfa_schema_history`.

Migrations nunca devem referenciar diretamente `bfa_dev_app`, `bfa_staging_app` ou `bfa_prod_app`. A criação de `bfa_app_role`, dos logins e dos vínculos entre eles faz parte do provisionamento PostgreSQL de cada ambiente, não das migrations de schema da aplicação.

### `bfa_*_deploy`

Usuário separado de cada ambiente, como `bfa_dev_deploy`, usado para aplicar manualmente scripts SQL revisados e executar DDL de forma controlada. Suas credenciais nunca ficam disponíveis para `BFA.Web`.

O deploy de schema é sempre separado do deploy da aplicação.

## Seeds

Scripts futuros de seed controlado ficarão em `database/seeds`. Eles não devem conter segredos nem dados pessoais reais.

# Banco de dados

PostgreSQL é o banco de dados oficial e persistente da BFA Platform. O Entity Framework Core com Npgsql é usado somente para persistência em runtime dentro de `BFA.Infrastructure`.

Esta etapa não cria banco, tabela, entidade, seed ou migration inicial.

## Ambientes

Cada ambiente possui banco e credenciais próprios:

| Ambiente | Banco |
| --- | --- |
| Development | `bfa_dev` |
| Staging | `bfa_staging` |
| Production | `bfa_prod` |

Development e Staging nunca podem apontar para `bfa_prod`. Consulte `docs/ENVIRONMENTS.md` para a origem das configurações e dos segredos.

## Migrations SQL

O schema será gerenciado por scripts SQL imutáveis e versionados em `database/migrations`, usando nomes como:

```text
V001__initial_schema.sql
V002__create_organizacoes.sql
```

Ainda não existe `V001`. Uma migration aplicada em ambiente compartilhado nunca deve ser editada ou removida; correções são feitas por novos scripts.

O deploy do schema é uma operação controlada e separada do deploy da aplicação. O runtime nunca chama:

```csharp
Database.EnsureCreated();
Database.EnsureDeleted();
Database.Migrate();
```

Migrations do Entity Framework não são a fonte de verdade do schema e não são executadas automaticamente.

## Papéis PostgreSQL

### `bfa_app`

Usuário de runtime utilizado pela aplicação em Production. Receberá apenas as permissões DML necessárias:

```text
SELECT
INSERT
UPDATE
DELETE
```

Não será proprietário do schema e não terá permissão para `CREATE`, `ALTER` ou `DROP`.

### `bfa_deploy`

Usuário separado para aplicar scripts SQL revisados e executar DDL de forma controlada. Suas credenciais nunca ficam disponíveis para `BFA.Web`.

Nenhum usuário PostgreSQL real é criado nesta etapa.

## Seeds

Scripts futuros de seed controlado ficarão em `database/seeds`. Eles não devem conter segredos nem dados pessoais reais.

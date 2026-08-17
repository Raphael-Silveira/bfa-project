# Banco de dados

PostgreSQL será o banco de dados persistente da BFA Platform. Ainda não há tabelas, conexão real ou migration inicial nesta fase.

## Migrations

O schema é gerenciado por scripts SQL imutáveis em `database/migrations`, usando nomes como:

```text
V001__initial_schema.sql
V002__create_organizations.sql
```

Uma migration aplicada em ambiente compartilhado nunca deve ser editada ou removida. Correções são feitas por novos scripts. A primeira migration será criada somente na fase de Identidade, Organizacao e Unidade.

O runtime da aplicação não executa DDL e nunca chama `EnsureCreated`, `EnsureDeleted` ou `Database.Migrate`.

## Seeds

Scripts de seed controlados ficam em `database/seeds`. Eles não devem conter segredos nem dados pessoais reais.

## Produção

O papel `bfa_app` será usado pela aplicação com permissões DML estritamente necessárias. O papel separado `bfa_deploy` será usado pelo processo controlado de implantação do schema.

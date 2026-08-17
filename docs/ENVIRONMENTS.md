# Ambientes e configuração

A BFA Platform possui três ambientes de primeira classe e isolados:

| Ambiente | Banco PostgreSQL | Login de runtime | Origem principal da connection string |
| --- | --- | --- | --- |
| Development | `bfa_dev` | `bfa_dev_app` | .NET User Secrets |
| Staging | `bfa_staging` | `bfa_staging_app` | Variável de ambiente |
| Production | `bfa_prod` | `bfa_prod_app` | Variável de ambiente |

Cada ambiente deve possuir banco, usuário, senha e demais segredos próprios. Development e Staging nunca podem apontar para `bfa_prod`.

## Papéis PostgreSQL

As permissões de runtime são atribuídas ao role comum `bfa_app_role`, criado com `NOLOGIN`. Os logins específicos dos ambientes são membros desse role:

```text
bfa_app_role (NOLOGIN)
├── bfa_dev_app (LOGIN)
├── bfa_staging_app (LOGIN)
└── bfa_prod_app (LOGIN)
```

Isso permite aplicar os mesmos scripts SQL, sem alterações, em todos os ambientes. Migrations podem referenciar `bfa_app_role`, mas nunca os logins `bfa_dev_app`, `bfa_staging_app` ou `bfa_prod_app`.

A criação dos roles, dos logins e dos vínculos de membership pertence ao provisionamento do PostgreSQL e não às migrations de schema. Nenhum role é criado automaticamente pela aplicação.

## Chave de configuração

`BFA.Infrastructure` lê a connection string usando:

```text
ConnectionStrings:BfaDatabase
```

Em variáveis de ambiente, a mesma chave é escrita com dois sublinhados:

```text
ConnectionStrings__BfaDatabase
```

`BFA.Web` apenas chama `AddInfrastructure(builder.Configuration)`. O registro de `BfaDbContext` e `UseNpgsql` permanece em `BFA.Infrastructure`.

## Development

O projeto `BFA.Web` possui um `UserSecretsId`. A connection string local deve ser armazenada fora do repositório com .NET User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:BfaDatabase" "Host=<host>;Port=5432;Database=bfa_dev;Username=<usuario>;Password=<senha>" --project backend/src/BFA.Web/BFA.Web.csproj
```

User Secrets é uma conveniência de desenvolvimento e não deve ser usado em Staging ou Production. O arquivo de secrets fica no perfil local do usuário e não dentro do repositório.

## Staging

Defina:

```text
ASPNETCORE_ENVIRONMENT=Staging
ConnectionStrings__BfaDatabase=<connection string exclusiva para bfa_staging>
```

## Production

Defina:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__BfaDatabase=<connection string exclusiva para bfa_prod>
```

A connection string deve usar o login de runtime `bfa_prod_app`, membro de `bfa_app_role`. As permissões herdadas são limitadas às operações `SELECT`, `INSERT`, `UPDATE` e `DELETE` necessárias. O role não é proprietário do schema e não recebe permissões `CREATE`, `ALTER` ou `DROP`.

O usuário separado `bfa_prod_deploy` aplica os scripts SQL versionados. Development e Staging utilizam, respectivamente, `bfa_dev_deploy` e `bfa_staging_deploy`. Suas credenciais nunca são fornecidas ao processo `BFA.Web`.

## Arquivos appsettings

Os arquivos abaixo são versionados apenas com configurações não sensíveis, como níveis de logging:

```text
appsettings.json
appsettings.Development.json
appsettings.Staging.json
appsettings.Production.json
```

Usuários, senhas, tokens e connection strings reais não devem ser adicionados a esses arquivos ou a qualquer outro arquivo versionado.

## Schema

A inicialização da aplicação não cria banco ou tabelas e não executa migrations. São proibidas chamadas a `EnsureCreated`, `EnsureDeleted` e `Database.Migrate`. A evolução do schema é feita por scripts revisados em `database/migrations`.

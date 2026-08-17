# Ambientes e configuração

A BFA Platform possui três ambientes de primeira classe e isolados:

| Ambiente | Banco PostgreSQL | Origem principal da connection string |
| --- | --- | --- |
| Development | `bfa_dev` | .NET User Secrets |
| Staging | `bfa_staging` | Variável de ambiente |
| Production | `bfa_prod` | Variável de ambiente |

Cada ambiente deve possuir banco, usuário, senha e demais segredos próprios. Development e Staging nunca podem apontar para `bfa_prod`.

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

A connection string deve usar o usuário de runtime `bfa_app`, limitado às operações `SELECT`, `INSERT`, `UPDATE` e `DELETE` necessárias. Esse usuário não é proprietário do schema e não recebe permissões `CREATE`, `ALTER` ou `DROP`.

O usuário separado `bfa_deploy` aplica os scripts SQL versionados. Suas credenciais nunca são fornecidas ao processo `BFA.Web`.

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

A inicialização da aplicação não cria banco ou tabelas e não executa migrations. São proibidas chamadas a `EnsureCreated`, `EnsureDeleted` e `Database.Migrate`. A evolução do schema será feita futuramente por scripts revisados em `database/migrations`.

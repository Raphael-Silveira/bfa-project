using System.Text.RegularExpressions;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed partial class IdentityModelTests
{
    [Fact]
    public void UsuarioIdentity_utiliza_guid_e_nao_possui_campos_de_negocio()
    {
        Assert.True(typeof(IdentityUser<Guid>).IsAssignableFrom(typeof(UsuarioIdentity)));
        Assert.Equal(typeof(Guid), typeof(UsuarioIdentity).GetProperty(nameof(UsuarioIdentity.Id))!.PropertyType);

        Assert.Null(typeof(UsuarioIdentity).GetProperty("Nome"));
        Assert.Null(typeof(UsuarioIdentity).GetProperty("Cpf"));
        Assert.Null(typeof(UsuarioIdentity).GetProperty("OrganizacaoId"));
        Assert.Null(typeof(UsuarioIdentity).GetProperty("UnidadeId"));
        Assert.Null(typeof(UsuarioIdentity).GetProperty("Perfil"));
        Assert.Null(typeof(UsuarioIdentity).GetProperty("Role"));
    }

    [Fact]
    public void Modelo_utiliza_somente_as_tabelas_identity_definidas_pela_BFA()
    {
        using var context = CreateContext();
        var model = GetDesignTimeModel(context);

        var identityTables = model.GetEntityTypes()
            .Where(IsIdentityEntity)
            .Select(entityType => entityType.GetTableName()!)
            .OrderBy(tableName => tableName)
            .ToArray();

        Assert.Equal(
            ["usuario_claims", "usuario_logins", "usuario_tokens", "usuarios"],
            identityTables);
        Assert.DoesNotContain(
            model.GetEntityTypes(),
            entityType =>
                entityType.ClrType.Name.Contains("Role", StringComparison.Ordinal)
                || entityType.GetTableName()?.Contains("role", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Modelo_identity_possui_colunas_indices_e_relacionamentos_explicitos()
    {
        using var context = CreateContext();
        var model = GetDesignTimeModel(context);
        var usuario = model.FindEntityType(typeof(UsuarioIdentity))!;
        var claim = model.FindEntityType(typeof(IdentityUserClaim<Guid>))!;
        var login = model.FindEntityType(typeof(IdentityUserLogin<Guid>))!;
        var token = model.FindEntityType(typeof(IdentityUserToken<Guid>))!;

        Assert.Equal("usuarios", usuario.GetTableName());
        Assert.Equal("pk_usuarios", usuario.FindPrimaryKey()!.GetName());
        AssertColumn(usuario, nameof(UsuarioIdentity.Id), "id", "uuid", false);
        AssertColumn(usuario, nameof(UsuarioIdentity.UserName), "nome_usuario", "varchar(256)", true, 256);
        AssertColumn(
            usuario,
            nameof(UsuarioIdentity.NormalizedUserName),
            "nome_usuario_normalizado",
            "varchar(256)",
            true,
            256);
        AssertColumn(usuario, nameof(UsuarioIdentity.Email), "email", "varchar(256)", true, 256);
        AssertColumn(
            usuario,
            nameof(UsuarioIdentity.NormalizedEmail),
            "email_normalizado",
            "varchar(256)",
            true,
            256);
        AssertColumn(usuario, nameof(UsuarioIdentity.EmailConfirmed), "email_confirmado", "boolean", false);
        AssertColumn(usuario, nameof(UsuarioIdentity.PasswordHash), "hash_senha", "text", true);
        AssertColumn(usuario, nameof(UsuarioIdentity.SecurityStamp), "selo_seguranca", "text", true);
        AssertColumn(usuario, nameof(UsuarioIdentity.ConcurrencyStamp), "selo_concorrencia", "text", true);
        Assert.True(usuario.FindProperty(nameof(UsuarioIdentity.ConcurrencyStamp))!.IsConcurrencyToken);
        AssertColumn(usuario, nameof(UsuarioIdentity.PhoneNumber), "telefone", "varchar(256)", true, 256);
        AssertColumn(
            usuario,
            nameof(UsuarioIdentity.PhoneNumberConfirmed),
            "telefone_confirmado",
            "boolean",
            false);
        AssertColumn(
            usuario,
            nameof(UsuarioIdentity.TwoFactorEnabled),
            "dois_fatores_habilitado",
            "boolean",
            false);
        AssertColumn(
            usuario,
            nameof(UsuarioIdentity.LockoutEnd),
            "fim_bloqueio",
            "timestamp with time zone",
            true);
        AssertColumn(
            usuario,
            nameof(UsuarioIdentity.LockoutEnabled),
            "bloqueio_habilitado",
            "boolean",
            false);
        AssertColumn(
            usuario,
            nameof(UsuarioIdentity.AccessFailedCount),
            "contagem_falhas_acesso",
            "integer",
            false);

        Assert.Equal(
            ["ix_usuarios_email_normalizado", "ix_usuarios_nome_usuario_normalizado"],
            usuario.GetIndexes().Select(index => index.GetDatabaseName()).OrderBy(name => name));
        Assert.True(
            usuario.GetIndexes().Single(index =>
                index.GetDatabaseName() == "ix_usuarios_nome_usuario_normalizado").IsUnique);

        Assert.Equal("usuario_claims", claim.GetTableName());
        Assert.Equal("pk_usuario_claims", claim.FindPrimaryKey()!.GetName());
        AssertColumn(claim, nameof(IdentityUserClaim<Guid>.Id), "id", "integer", false);
        Assert.Equal(
            ValueGenerated.OnAdd,
            claim.FindProperty(nameof(IdentityUserClaim<Guid>.Id))!.ValueGenerated);
        AssertColumn(claim, nameof(IdentityUserClaim<Guid>.UserId), "usuario_id", "uuid", false);
        AssertColumn(claim, nameof(IdentityUserClaim<Guid>.ClaimType), "tipo", "text", true);
        AssertColumn(claim, nameof(IdentityUserClaim<Guid>.ClaimValue), "valor", "text", true);
        Assert.Equal(
            "ix_usuario_claims_usuario_id",
            Assert.Single(claim.GetIndexes()).GetDatabaseName());
        AssertForeignKey(claim, "fk_usuario_claims_usuarios_usuario_id");

        Assert.Equal("usuario_logins", login.GetTableName());
        Assert.Equal("pk_usuario_logins", login.FindPrimaryKey()!.GetName());
        AssertColumn(
            login,
            nameof(IdentityUserLogin<Guid>.LoginProvider),
            "provedor",
            "varchar(128)",
            false,
            128);
        AssertColumn(
            login,
            nameof(IdentityUserLogin<Guid>.ProviderKey),
            "chave_provedor",
            "varchar(128)",
            false,
            128);
        AssertColumn(
            login,
            nameof(IdentityUserLogin<Guid>.ProviderDisplayName),
            "nome_exibicao_provedor",
            "text",
            true);
        AssertColumn(login, nameof(IdentityUserLogin<Guid>.UserId), "usuario_id", "uuid", false);
        Assert.Equal(
            [nameof(IdentityUserLogin<Guid>.LoginProvider), nameof(IdentityUserLogin<Guid>.ProviderKey)],
            login.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(
            "ix_usuario_logins_usuario_id",
            Assert.Single(login.GetIndexes()).GetDatabaseName());
        AssertForeignKey(login, "fk_usuario_logins_usuarios_usuario_id");

        Assert.Equal("usuario_tokens", token.GetTableName());
        Assert.Equal("pk_usuario_tokens", token.FindPrimaryKey()!.GetName());
        AssertColumn(token, nameof(IdentityUserToken<Guid>.UserId), "usuario_id", "uuid", false);
        AssertColumn(
            token,
            nameof(IdentityUserToken<Guid>.LoginProvider),
            "provedor",
            "varchar(128)",
            false,
            128);
        AssertColumn(
            token,
            nameof(IdentityUserToken<Guid>.Name),
            "nome",
            "varchar(128)",
            false,
            128);
        AssertColumn(token, nameof(IdentityUserToken<Guid>.Value), "valor", "text", true);
        Assert.Equal(
            [
                nameof(IdentityUserToken<Guid>.UserId),
                nameof(IdentityUserToken<Guid>.LoginProvider),
                nameof(IdentityUserToken<Guid>.Name)
            ],
            token.FindPrimaryKey()!.Properties.Select(property => property.Name));
        AssertForeignKey(token, "fk_usuario_tokens_usuarios_usuario_id");
    }

    [Fact]
    public void V002_possui_colunas_identity_alinhadas_ao_modelo_EF()
    {
        var sql = ReadV002();

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "nome_usuario varchar(256) NULL",
                "nome_usuario_normalizado varchar(256) NULL",
                "email varchar(256) NULL",
                "email_normalizado varchar(256) NULL",
                "email_confirmado boolean NOT NULL",
                "hash_senha text NULL",
                "selo_seguranca text NULL",
                "selo_concorrencia text NULL",
                "telefone varchar(256) NULL",
                "telefone_confirmado boolean NOT NULL",
                "dois_fatores_habilitado boolean NOT NULL",
                "fim_bloqueio timestamptz NULL",
                "bloqueio_habilitado boolean NOT NULL",
                "contagem_falhas_acesso integer NOT NULL"
            ],
            GetSqlColumns(sql, "usuarios"));

        Assert.Equal(
            [
                "id integer GENERATED BY DEFAULT AS IDENTITY",
                "usuario_id uuid NOT NULL",
                "tipo text NULL",
                "valor text NULL"
            ],
            GetSqlColumns(sql, "usuario_claims"));

        Assert.Equal(
            [
                "provedor varchar(128) NOT NULL",
                "chave_provedor varchar(128) NOT NULL",
                "nome_exibicao_provedor text NULL",
                "usuario_id uuid NOT NULL"
            ],
            GetSqlColumns(sql, "usuario_logins"));

        Assert.Equal(
            [
                "usuario_id uuid NOT NULL",
                "provedor varchar(128) NOT NULL",
                "nome varchar(128) NOT NULL",
                "valor text NULL"
            ],
            GetSqlColumns(sql, "usuario_tokens"));

        Assert.Equal(
            ["usuario_claims", "usuario_logins", "usuario_tokens", "usuarios"],
            Regex.Matches(sql, @"CREATE TABLE (?<table>[a-z0-9_]+) \(", RegexOptions.CultureInvariant)
                .Select(match => match.Groups["table"].Value)
                .OrderBy(tableName => tableName));

        Assert.Contains("CREATE UNIQUE INDEX ix_usuarios_nome_usuario_normalizado", sql);
        Assert.Contains("CREATE INDEX ix_usuarios_email_normalizado", sql);
        Assert.Contains("CREATE INDEX ix_usuario_claims_usuario_id", sql);
        Assert.Contains("CREATE INDEX ix_usuario_logins_usuario_id", sql);
        Assert.Contains("CONSTRAINT pk_usuarios PRIMARY KEY (id)", sql);
        Assert.Contains("CONSTRAINT pk_usuario_claims PRIMARY KEY (id)", sql);
        Assert.Contains("CONSTRAINT fk_usuario_claims_usuarios_usuario_id", sql);
        Assert.Contains("CONSTRAINT pk_usuario_logins PRIMARY KEY (provedor, chave_provedor)", sql);
        Assert.Contains("CONSTRAINT fk_usuario_logins_usuarios_usuario_id", sql);
        Assert.Contains("CONSTRAINT pk_usuario_tokens PRIMARY KEY (usuario_id, provedor, nome)", sql);
        Assert.Contains("CONSTRAINT fk_usuario_tokens_usuarios_usuario_id", sql);
        Assert.Equal(
            3,
            Regex.Matches(sql, "ON DELETE CASCADE", RegexOptions.CultureInvariant).Count);
        Assert.DoesNotContain("AspNetRoles", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UserRoles", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RoleClaims", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Modelo_continua_contendo_organizacao_e_unidade_e_e_relacionalmente_valido()
    {
        using var context = CreateContext();
        var model = GetDesignTimeModel(context);
        var relationalModel = model.GetRelationalModel();

        Assert.NotNull(model.FindEntityType(typeof(Organizacao)));
        Assert.NotNull(model.FindEntityType(typeof(Unidade)));
        Assert.Contains(relationalModel.Tables, table => table.Name == "organizacoes");
        Assert.Contains(relationalModel.Tables, table => table.Name == "unidades");

        foreach (var table in relationalModel.Tables)
        {
            Assert.Matches(SnakeCaseName(), table.Name);

            foreach (var column in table.Columns)
            {
                Assert.Matches(SnakeCaseName(), column.Name);
            }
        }
    }

    private static BfaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql()
            .Options;

        return new BfaDbContext(options);
    }

    private static IModel GetDesignTimeModel(BfaDbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static bool IsIdentityEntity(IReadOnlyEntityType entityType)
    {
        return entityType.ClrType == typeof(UsuarioIdentity)
            || entityType.ClrType.Namespace == typeof(IdentityUser<Guid>).Namespace;
    }

    private static void AssertColumn(
        IEntityType entityType,
        string propertyName,
        string columnName,
        string columnType,
        bool isNullable,
        int? maxLength = null)
    {
        var property = entityType.FindProperty(propertyName)!;

        Assert.Equal(columnName, property.GetColumnName());
        Assert.Equal(columnType, property.GetColumnType());
        Assert.Equal(isNullable, property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    private static void AssertForeignKey(IEntityType entityType, string constraintName)
    {
        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(constraintName, foreignKey.GetConstraintName());
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal(typeof(UsuarioIdentity), foreignKey.PrincipalEntityType.ClrType);
    }

    private static string ReadV002()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "database",
            "migrations",
            "V002__criar_identidade.sql"));
    }

    private static string[] GetSqlColumns(string sql, string tableName)
    {
        var tableMatch = Regex.Match(
            sql,
            $@"CREATE TABLE {Regex.Escape(tableName)} \((?<body>.*?)\r?\n\);",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        Assert.True(tableMatch.Success, $"Tabela {tableName} nao encontrada na V002.");

        return tableMatch.Groups["body"].Value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimEnd(','))
            .TakeWhile(line => !line.StartsWith("CONSTRAINT ", StringComparison.Ordinal))
            .ToArray();
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SnakeCaseName();
}

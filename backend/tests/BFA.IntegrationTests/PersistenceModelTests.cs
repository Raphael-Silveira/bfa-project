using BFA.Domain.Acessos;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void Organizacao_possui_mapeamento_relacional_explicito()
    {
        using var context = CreateContext();
        var entityType = GetDesignTimeModel(context).FindEntityType(typeof(Organizacao));

        Assert.NotNull(entityType);
        Assert.Equal("organizacoes", entityType.GetTableName());
        Assert.Equal("pk_organizacoes", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(Organizacao.Id), "id", "uuid");
        AssertColumn(entityType, nameof(Organizacao.Nome), "nome", "varchar(150)", 150);
        AssertColumn(entityType, nameof(Organizacao.Slug), "slug", "varchar(100)", 100);
        AssertColumn(entityType, nameof(Organizacao.Ativa), "ativa", "boolean");
        AssertColumn(
            entityType,
            nameof(Organizacao.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(Organizacao.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");

        var alternateKey = entityType.GetKeys().Single(key => !key.IsPrimaryKey());
        Assert.Equal("uq_organizacoes_slug", alternateKey.GetName());
        Assert.Equal(
            [nameof(Organizacao.Slug)],
            alternateKey.Properties.Select(property => property.Name));

        Assert.Equal(
            [
                "ck_organizacoes_nome_nao_vazio",
                "ck_organizacoes_slug_nao_vazio",
                "ck_organizacoes_slug_normalizado"
            ],
            entityType.GetCheckConstraints()
                .Select(constraint => constraint.Name)
                .OrderBy(name => name));
    }

    [Fact]
    public void Unidade_possui_mapeamento_relacional_e_tenancy_explicitos()
    {
        using var context = CreateContext();
        var entityType = GetDesignTimeModel(context).FindEntityType(typeof(Unidade));

        Assert.NotNull(entityType);
        Assert.Equal("unidades", entityType.GetTableName());
        Assert.Equal("pk_unidades", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(Unidade.Id), "id", "uuid");
        AssertColumn(entityType, nameof(Unidade.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(entityType, nameof(Unidade.Nome), "nome", "varchar(150)", 150);
        AssertColumn(entityType, nameof(Unidade.Slug), "slug", "varchar(100)", 100);
        AssertColumn(entityType, nameof(Unidade.Ativa), "ativa", "boolean");
        AssertColumn(
            entityType,
            nameof(Unidade.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(Unidade.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");

        var alternateKeys = entityType.GetKeys()
            .Where(key => !key.IsPrimaryKey())
            .ToDictionary(key => key.GetName()!);
        Assert.Single(alternateKeys);
        Assert.Equal(
            [nameof(Unidade.OrganizacaoId), nameof(Unidade.Id)],
            alternateKeys["uq_unidades_organizacao_id_id"]
                .Properties
                .Select(property => property.Name));

        var indexes = entityType.GetIndexes()
            .ToDictionary(index => index.GetDatabaseName()!);
        Assert.Equal(2, indexes.Count);
        AssertIndex(
            indexes["uq_unidades_organizacao_id_slug"],
            true,
            nameof(Unidade.OrganizacaoId),
            nameof(Unidade.Slug));
        AssertIndex(
            indexes["ix_unidades_organizacao_id"],
            false,
            nameof(Unidade.OrganizacaoId));

        var foreignKey = Assert.Single(entityType.GetForeignKeys());
        Assert.Equal("fk_unidades_organizacoes_organizacao_id", foreignKey.GetConstraintName());
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(typeof(Organizacao), foreignKey.PrincipalEntityType.ClrType);

        Assert.Equal(
            [
                "ck_unidades_nome_nao_vazio",
                "ck_unidades_slug_nao_vazio",
                "ck_unidades_slug_normalizado"
            ],
            entityType.GetCheckConstraints()
                .Select(constraint => constraint.Name)
                .OrderBy(name => name));
    }

    [Fact]
    public void Vinculo_acesso_possui_mapeamento_relacional_e_tenancy_explicitos()
    {
        using var context = CreateContext();
        var entityType = GetDesignTimeModel(context).FindEntityType(typeof(VinculoAcesso));

        Assert.NotNull(entityType);
        Assert.Equal("vinculos_acesso", entityType.GetTableName());
        Assert.Equal("pk_vinculos_acesso", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(VinculoAcesso.Id), "id", "uuid");
        AssertColumn(entityType, nameof(VinculoAcesso.UsuarioId), "usuario_id", "uuid");
        AssertColumn(entityType, nameof(VinculoAcesso.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(
            entityType,
            nameof(VinculoAcesso.UnidadeId),
            "unidade_id",
            "uuid",
            isNullable: true);
        AssertColumn(
            entityType,
            nameof(VinculoAcesso.Perfil),
            "perfil",
            "varchar(50)",
            50);
        AssertColumn(entityType, nameof(VinculoAcesso.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType,
            nameof(VinculoAcesso.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(VinculoAcesso.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");

        var perfil = entityType.FindProperty(nameof(VinculoAcesso.Perfil));
        Assert.NotNull(perfil);
        var converter = perfil.GetTypeMapping().Converter;
        Assert.NotNull(converter);
        Assert.Equal(typeof(string), converter.ProviderClrType);
        Assert.Equal("Professor", converter.ConvertToProvider(PerfilAcesso.Professor));

        Assert.Equal(
            ["ck_vinculos_acesso_escopo_perfil", "ck_vinculos_acesso_perfil_valido"],
            entityType.GetCheckConstraints()
                .Select(constraint => constraint.Name)
                .OrderBy(name => name));

        var indexes = entityType.GetIndexes()
            .ToDictionary(index => index.GetDatabaseName()!);
        Assert.Equal(4, indexes.Count);
        AssertIndex(
            indexes["ix_vinculos_acesso_usuario_id_ativo"],
            false,
            nameof(VinculoAcesso.UsuarioId),
            nameof(VinculoAcesso.Ativo));
        AssertIndex(
            indexes["ix_vinculos_acesso_organizacao_id_unidade_id"],
            false,
            nameof(VinculoAcesso.OrganizacaoId),
            nameof(VinculoAcesso.UnidadeId));
        AssertIndex(
            indexes["ix_vinculos_acesso_unidade_id"],
            false,
            nameof(VinculoAcesso.UnidadeId));

        var uniqueIndex = indexes["uq_vinculos_acesso_usuario_organizacao_unidade_perfil"];
        AssertIndex(
            uniqueIndex,
            true,
            nameof(VinculoAcesso.UsuarioId),
            nameof(VinculoAcesso.OrganizacaoId),
            nameof(VinculoAcesso.UnidadeId),
            nameof(VinculoAcesso.Perfil));
        Assert.Equal(false, uniqueIndex.GetAreNullsDistinct());

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        Assert.Equal(3, foreignKeys.Count);

        var usuarioForeignKey = foreignKeys["fk_vinculos_acesso_usuarios_usuario_id"];
        AssertForeignKey(
            usuarioForeignKey,
            typeof(UsuarioIdentity),
            nameof(VinculoAcesso.UsuarioId));

        var organizacaoForeignKey =
            foreignKeys["fk_vinculos_acesso_organizacoes_organizacao_id"];
        AssertForeignKey(
            organizacaoForeignKey,
            typeof(Organizacao),
            nameof(VinculoAcesso.OrganizacaoId));

        var unidadeForeignKey =
            foreignKeys["fk_vinculos_acesso_unidades_organizacao_id_unidade_id"];
        AssertForeignKey(
            unidadeForeignKey,
            typeof(Unidade),
            nameof(VinculoAcesso.OrganizacaoId),
            nameof(VinculoAcesso.UnidadeId));
        Assert.Equal(
            [nameof(Unidade.OrganizacaoId), nameof(Unidade.Id)],
            unidadeForeignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal("uq_unidades_organizacao_id_id", unidadeForeignKey.PrincipalKey.GetName());
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

    private static void AssertColumn(
        IEntityType entityType,
        string propertyName,
        string columnName,
        string columnType,
        int? maxLength = null,
        bool isNullable = false)
    {
        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(columnName, property.GetColumnName());
        Assert.Equal(columnType, property.GetColumnType());
        Assert.Equal(maxLength, property.GetMaxLength());
        Assert.Equal(isNullable, property.IsNullable);
    }

    private static void AssertIndex(
        IIndex index,
        bool isUnique,
        params string[] propertyNames)
    {
        Assert.Equal(isUnique, index.IsUnique);
        Assert.Equal(propertyNames, index.Properties.Select(property => property.Name));
    }

    private static void AssertForeignKey(
        IForeignKey foreignKey,
        Type principalType,
        params string[] propertyNames)
    {
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(principalType, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(propertyNames, foreignKey.Properties.Select(property => property.Name));
    }
}

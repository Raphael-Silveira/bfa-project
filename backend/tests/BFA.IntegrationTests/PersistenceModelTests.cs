using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
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

        var alternateKey = entityType.GetKeys().Single(key => !key.IsPrimaryKey());
        Assert.Equal("uq_unidades_organizacao_id_slug", alternateKey.GetName());
        Assert.Equal(
            [nameof(Unidade.OrganizacaoId), nameof(Unidade.Slug)],
            alternateKey.Properties.Select(property => property.Name));

        var index = Assert.Single(entityType.GetIndexes());
        Assert.Equal("ix_unidades_organizacao_id", index.GetDatabaseName());
        Assert.False(index.IsUnique);
        Assert.Equal(
            [nameof(Unidade.OrganizacaoId)],
            index.Properties.Select(property => property.Name));

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
        int? maxLength = null)
    {
        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(columnName, property.GetColumnName());
        Assert.Equal(columnType, property.GetColumnType());
        Assert.Equal(maxLength, property.GetMaxLength());
        Assert.False(property.IsNullable);
    }
}

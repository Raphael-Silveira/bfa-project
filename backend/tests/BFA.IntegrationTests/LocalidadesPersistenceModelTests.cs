using BFA.Domain.Localidades;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed class LocalidadesPersistenceModelTests
{
    [Fact]
    public void Estado_possui_mapeamento_exato_da_v006()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(Estado));

        Assert.NotNull(entityType);
        Assert.Equal("estados", entityType.GetTableName());
        Assert.Equal("pk_estados", entityType.FindPrimaryKey()!.GetName());
        AssertColumn(entityType, nameof(Estado.CodigoIbge), "codigo_ibge", "integer");
        AssertColumn(entityType, nameof(Estado.Sigla), "sigla", "varchar(2)", 2);
        AssertColumn(entityType, nameof(Estado.Nome), "nome", "varchar(100)", 100);
        AssertColumn(entityType, nameof(Estado.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType,
            nameof(Estado.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(Estado.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");
        Assert.Equal(ValueGenerated.Never, entityType.FindProperty(nameof(Estado.CodigoIbge))!.ValueGenerated);

        var alternateKey = entityType.GetKeys().Single(key => !key.IsPrimaryKey());
        Assert.Equal("uq_estados_sigla", alternateKey.GetName());
        Assert.Equal([nameof(Estado.Sigla)], alternateKey.Properties.Select(property => property.Name));
        Assert.Equal(
            [
                "ck_estados_codigo_ibge_positivo",
                "ck_estados_nome_nao_vazio",
                "ck_estados_sigla_formato",
            ],
            entityType.GetCheckConstraints().Select(item => item.Name).OrderBy(name => name));
    }

    [Fact]
    public void Municipio_possui_mapeamento_fk_restritiva_e_indice_da_v006()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(Municipio));

        Assert.NotNull(entityType);
        Assert.Equal("municipios", entityType.GetTableName());
        Assert.Equal("pk_municipios", entityType.FindPrimaryKey()!.GetName());
        AssertColumn(entityType, nameof(Municipio.CodigoIbge), "codigo_ibge", "integer");
        AssertColumn(
            entityType,
            nameof(Municipio.EstadoCodigoIbge),
            "estado_codigo_ibge",
            "integer");
        AssertColumn(entityType, nameof(Municipio.Nome), "nome", "varchar(150)", 150);
        AssertColumn(entityType, nameof(Municipio.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType,
            nameof(Municipio.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(Municipio.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");
        Assert.Equal(ValueGenerated.Never, entityType.FindProperty(nameof(Municipio.CodigoIbge))!.ValueGenerated);

        var foreignKey = Assert.Single(entityType.GetForeignKeys());
        Assert.Equal("fk_municipios_estados_estado_codigo_ibge", foreignKey.GetConstraintName());
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(typeof(Estado), foreignKey.PrincipalEntityType.ClrType);

        var index = Assert.Single(entityType.GetIndexes());
        Assert.Equal("ix_municipios_estado_ativo_nome", index.GetDatabaseName());
        Assert.Equal(
            [nameof(Municipio.EstadoCodigoIbge), nameof(Municipio.Ativo), nameof(Municipio.Nome)],
            index.Properties.Select(property => property.Name));
        Assert.False(index.IsUnique);
        Assert.Equal(
            ["ck_municipios_codigo_ibge_positivo", "ck_municipios_nome_nao_vazio"],
            entityType.GetCheckConstraints().Select(item => item.Name).OrderBy(name => name));
    }

    [Fact]
    public void Contexto_expoe_catalogo_global_sem_tenancy()
    {
        using var context = CreateContext();
        var model = GetModel(context);

        Assert.NotNull(model.FindEntityType(typeof(Estado)));
        Assert.NotNull(model.FindEntityType(typeof(Municipio)));
        Assert.Null(model.FindEntityType(typeof(Estado))!.FindProperty("OrganizacaoId"));
        Assert.Null(model.FindEntityType(typeof(Estado))!.FindProperty("UnidadeId"));
        Assert.Null(model.FindEntityType(typeof(Municipio))!.FindProperty("OrganizacaoId"));
        Assert.Null(model.FindEntityType(typeof(Municipio))!.FindProperty("UnidadeId"));
    }

    private static BfaDbContext CreateContext()
    {
        return new BfaDbContext(new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql()
            .Options);
    }

    private static IModel GetModel(BfaDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

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

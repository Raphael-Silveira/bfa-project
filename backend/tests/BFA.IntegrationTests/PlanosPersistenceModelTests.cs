using BFA.Domain.Organizacoes;
using BFA.Domain.Planos;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed class PlanosPersistenceModelTests
{
    [Fact]
    public void Plano_alinha_colunas_escopo_indices_e_fks_restritivas()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(Plano));

        Assert.NotNull(entityType);
        Assert.Equal("planos", entityType.GetTableName());
        Assert.Equal("pk_planos", entityType.FindPrimaryKey()!.GetName());
        Assert.Equal(
            "uq_planos_organizacao_id_id",
            entityType.GetKeys().Single(key => !key.IsPrimaryKey()).GetName());

        AssertColumn(entityType, nameof(Plano.Id), "id", "uuid");
        AssertColumn(entityType, nameof(Plano.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(entityType, nameof(Plano.UnidadeId), "unidade_id", "uuid", isNullable: true);
        AssertColumn(
            entityType, nameof(Plano.Nome), "nome", "varchar(150)", Plano.NomeTamanhoMaximo);
        AssertColumn(entityType, nameof(Plano.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType, nameof(Plano.CriadoPorUsuarioId), "criado_por_usuario_id", "uuid");
        AssertColumn(
            entityType, nameof(Plano.AtualizadoPorUsuarioId),
            "atualizado_por_usuario_id", "uuid");
        AssertColumn(
            entityType, nameof(Plano.CriadoEmUtc), "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType, nameof(Plano.AtualizadoEmUtc), "atualizado_em_utc",
            "timestamp with time zone");

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(
            indexes["ix_planos_organizacao_unidade_ativo"], false,
            nameof(Plano.OrganizacaoId), nameof(Plano.UnidadeId), nameof(Plano.Ativo));
        AssertIndex(
            indexes["ix_planos_criado_por_usuario_id"], false,
            nameof(Plano.CriadoPorUsuarioId));
        AssertIndex(
            indexes["ix_planos_atualizado_por_usuario_id"], false,
            nameof(Plano.AtualizadoPorUsuarioId));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_planos_organizacao"], typeof(Organizacao),
            nameof(Plano.OrganizacaoId));
        AssertForeignKey(
            foreignKeys["fk_planos_unidade"], typeof(Unidade),
            nameof(Plano.OrganizacaoId), nameof(Plano.UnidadeId));
        AssertForeignKey(
            foreignKeys["fk_planos_criado_por_usuario_id"], typeof(UsuarioIdentity),
            nameof(Plano.CriadoPorUsuarioId));
        AssertForeignKey(
            foreignKeys["fk_planos_atualizado_por_usuario_id"], typeof(UsuarioIdentity),
            nameof(Plano.AtualizadoPorUsuarioId));
        Assert.Single(entityType.GetCheckConstraints());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_plano");
    }

    [Fact]
    public void PlanoVersao_alinha_termos_historicos_indices_e_fks_tenant_safe()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(PlanoVersao));

        Assert.NotNull(entityType);
        Assert.Equal("planos_versoes", entityType.GetTableName());
        Assert.Equal("pk_planos_versoes", entityType.FindPrimaryKey()!.GetName());
        Assert.Equal(
            "uq_planos_versoes_organizacao_id_id",
            entityType.GetKeys().Single(key => !key.IsPrimaryKey()).GetName());

        AssertColumn(entityType, nameof(PlanoVersao.Id), "id", "uuid");
        AssertColumn(
            entityType, nameof(PlanoVersao.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(entityType, nameof(PlanoVersao.PlanoId), "plano_id", "uuid");
        AssertColumn(
            entityType, nameof(PlanoVersao.NumeroVersao), "numero_versao", "integer");
        AssertColumn(
            entityType, nameof(PlanoVersao.DuracaoMeses), "duracao_meses", "smallint");
        Assert.Equal(
            typeof(short),
            entityType.FindProperty(nameof(PlanoVersao.DuracaoMeses))!
                .GetTypeMapping().Converter!.ProviderClrType);
        AssertColumn(
            entityType, nameof(PlanoVersao.FrequenciaSemanal),
            "frequencia_semanal", "smallint");
        Assert.Equal(
            typeof(short),
            entityType.FindProperty(nameof(PlanoVersao.FrequenciaSemanal))!
                .GetTypeMapping().Converter!.ProviderClrType);
        AssertColumn(
            entityType, nameof(PlanoVersao.ValorMensal), "valor_mensal", "numeric(12,2)");
        Assert.Equal(12, entityType.FindProperty(nameof(PlanoVersao.ValorMensal))!.GetPrecision());
        Assert.Equal(2, entityType.FindProperty(nameof(PlanoVersao.ValorMensal))!.GetScale());
        AssertColumn(
            entityType, nameof(PlanoVersao.CobraMatricula), "cobra_matricula", "boolean");
        AssertColumn(
            entityType, nameof(PlanoVersao.ValorMatricula),
            "valor_matricula", "numeric(12,2)", isNullable: true);
        AssertColumn(
            entityType, nameof(PlanoVersao.VigenciaInicio), "vigencia_inicio", "date");
        AssertColumn(
            entityType, nameof(PlanoVersao.VigenciaFim),
            "vigencia_fim", "date", isNullable: true);
        AssertColumn(
            entityType, nameof(PlanoVersao.CriadoPorUsuarioId),
            "criado_por_usuario_id", "uuid");
        AssertColumn(
            entityType, nameof(PlanoVersao.CriadoEmUtc), "criado_em_utc",
            "timestamp with time zone");

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(
            indexes["uq_planos_versoes_plano_numero"], true,
            nameof(PlanoVersao.PlanoId), nameof(PlanoVersao.NumeroVersao));
        AssertIndex(
            indexes["uq_planos_versoes_aberta"], true,
            nameof(PlanoVersao.PlanoId));
        Assert.Equal(
            "vigencia_fim IS NULL",
            indexes["uq_planos_versoes_aberta"].GetFilter());
        AssertIndex(
            indexes["ix_planos_versoes_organizacao_plano_vigencia"], false,
            nameof(PlanoVersao.OrganizacaoId), nameof(PlanoVersao.PlanoId),
            nameof(PlanoVersao.VigenciaInicio), nameof(PlanoVersao.VigenciaFim));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_planos_versoes_plano"], typeof(Plano),
            nameof(PlanoVersao.OrganizacaoId), nameof(PlanoVersao.PlanoId));
        AssertForeignKey(
            foreignKeys["fk_planos_versoes_organizacao"], typeof(Organizacao),
            nameof(PlanoVersao.OrganizacaoId));
        AssertForeignKey(
            foreignKeys["fk_planos_versoes_criado_por_usuario_id"], typeof(UsuarioIdentity),
            nameof(PlanoVersao.CriadoPorUsuarioId));
        Assert.Equal(6, entityType.GetCheckConstraints().Count());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_plano_versao");
    }

    [Fact]
    public void Contexto_unico_contem_planos_e_versoes_sem_cascade()
    {
        using var context = CreateContext();
        var model = GetModel(context);

        Assert.NotNull(context.Planos);
        Assert.NotNull(context.PlanosVersoes);
        foreach (var type in new[] { typeof(Plano), typeof(PlanoVersao) })
        {
            var entityType = model.FindEntityType(type);
            Assert.NotNull(entityType);
            Assert.All(
                entityType.GetForeignKeys(),
                foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        }
    }

    private static BfaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql()
            .Options;
        return new BfaDbContext(options);
    }

    private static IModel GetModel(BfaDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

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

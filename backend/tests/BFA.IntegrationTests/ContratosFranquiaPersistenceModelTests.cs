using BFA.Domain.Contratos;
using BFA.Domain.Franqueados;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed class ContratosFranquiaPersistenceModelTests
{
    [Fact]
    public void Contrato_possui_colunas_indices_checks_e_fk_restritiva()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(ContratoFranquia));

        Assert.NotNull(entityType);
        Assert.Equal("contratos_franquia", entityType.GetTableName());
        Assert.Equal("pk_contratos_franquia", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(ContratoFranquia.Id), "id", "uuid");
        AssertColumn(
            entityType,
            nameof(ContratoFranquia.FranqueadoUnidadeId),
            "franqueado_unidade_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(ContratoFranquia.Numero),
            "numero",
            "varchar(100)",
            ContratoFranquia.NumeroTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(ContratoFranquia.Status),
            "status",
            "varchar(30)",
            ContratoFranquia.StatusTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(ContratoFranquia.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(ContratoFranquia.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");
        Assert.Equal(
            typeof(string),
            entityType.FindProperty(nameof(ContratoFranquia.Status))!
                .GetTypeMapping().Converter!.ProviderClrType);

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(
            indexes["ix_contratos_franquia_franqueado_unidade_id"],
            false,
            nameof(ContratoFranquia.FranqueadoUnidadeId));
        var ativo = indexes["uq_contratos_franquia_franqueado_unidade_ativo"];
        AssertIndex(ativo, true, nameof(ContratoFranquia.FranqueadoUnidadeId));
        Assert.Equal("status = 'Ativo'", ativo.GetFilter());

        var foreignKey = Assert.Single(entityType.GetForeignKeys());
        AssertForeignKey(
            foreignKey,
            "fk_contratos_franquia_franqueado_unidade_id",
            typeof(FranqueadoUnidade),
            nameof(ContratoFranquia.FranqueadoUnidadeId));
        Assert.Equal(2, entityType.GetCheckConstraints().Count());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_contrato_franquia");
    }

    [Fact]
    public void Versao_possui_tipos_precisoes_historicas_indices_e_fks_restritivas()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(ContratoFranquiaVersao));

        Assert.NotNull(entityType);
        Assert.Equal("contratos_franquia_versoes", entityType.GetTableName());
        Assert.Equal("pk_contratos_franquia_versoes", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(ContratoFranquiaVersao.Id), "id", "uuid");
        AssertColumn(
            entityType,
            nameof(ContratoFranquiaVersao.ContratoFranquiaId),
            "contrato_franquia_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(ContratoFranquiaVersao.NumeroVersao),
            "numero_versao",
            "integer");
        AssertColumn(
            entityType,
            nameof(ContratoFranquiaVersao.DataInicio),
            "data_inicio",
            "date");
        AssertColumn(
            entityType,
            nameof(ContratoFranquiaVersao.DataFim),
            "data_fim",
            "date",
            isNullable: true);
        AssertNumeric(
            entityType,
            nameof(ContratoFranquiaVersao.PercentualRoyalties),
            "percentual_royalties",
            "numeric(5,2)",
            5,
            2);
        AssertNumeric(
            entityType,
            nameof(ContratoFranquiaVersao.MensalidadeFixa),
            "mensalidade_fixa",
            "numeric(12,2)",
            12,
            2);
        AssertNumeric(
            entityType,
            nameof(ContratoFranquiaVersao.TaxaAdesao),
            "taxa_adesao",
            "numeric(12,2)",
            12,
            2,
            true);
        AssertColumn(
            entityType,
            nameof(ContratoFranquiaVersao.DiaVencimento),
            "dia_vencimento",
            "smallint",
            isNullable: true);
        Assert.Equal(
            typeof(short),
            entityType.FindProperty(nameof(ContratoFranquiaVersao.DiaVencimento))!
                .GetTypeMapping().Converter!.ProviderClrType);
        AssertColumn(
            entityType,
            nameof(ContratoFranquiaVersao.Status),
            "status",
            "varchar(30)",
            ContratoFranquiaVersao.StatusTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(ContratoFranquiaVersao.MotivoAlteracao),
            "motivo_alteracao",
            "varchar(1000)",
            ContratoFranquiaVersao.MotivoAlteracaoTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(ContratoFranquiaVersao.Observacoes),
            "observacoes",
            "varchar(4000)",
            ContratoFranquiaVersao.ObservacoesTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(ContratoFranquiaVersao.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(ContratoFranquiaVersao.CriadoPorUsuarioId),
            "criado_por_usuario_id",
            "uuid");
        Assert.Equal(
            typeof(string),
            entityType.FindProperty(nameof(ContratoFranquiaVersao.Status))!
                .GetTypeMapping().Converter!.ProviderClrType);

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        Assert.Equal(3, indexes.Count);
        AssertIndex(
            indexes["uq_contratos_franquia_versoes_contrato_numero"],
            true,
            nameof(ContratoFranquiaVersao.ContratoFranquiaId),
            nameof(ContratoFranquiaVersao.NumeroVersao));
        var vigente = indexes["uq_contratos_franquia_versoes_vigente"];
        AssertIndex(vigente, true, nameof(ContratoFranquiaVersao.ContratoFranquiaId));
        Assert.Equal("status = 'Vigente'", vigente.GetFilter());
        AssertIndex(
            indexes["ix_contratos_franquia_versoes_criado_por_usuario_id"],
            false,
            nameof(ContratoFranquiaVersao.CriadoPorUsuarioId));
        Assert.DoesNotContain("ix_contratos_franquia_versoes_contrato_id", indexes.Keys);

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_contratos_franquia_versoes_contrato_id"],
            "fk_contratos_franquia_versoes_contrato_id",
            typeof(ContratoFranquia),
            nameof(ContratoFranquiaVersao.ContratoFranquiaId));
        AssertForeignKey(
            foreignKeys["fk_contratos_franquia_versoes_criado_por_usuario_id"],
            "fk_contratos_franquia_versoes_criado_por_usuario_id",
            typeof(UsuarioIdentity),
            nameof(ContratoFranquiaVersao.CriadoPorUsuarioId));
        Assert.Equal(9, entityType.GetCheckConstraints().Count());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_versao_contrato_formalizada");
    }

    [Fact]
    public void Documento_possui_somente_metadata_chave_unica_e_fks_restritivas()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(DocumentoContratoFranquia));

        Assert.NotNull(entityType);
        Assert.Equal("documentos_contrato_franquia", entityType.GetTableName());
        Assert.Equal("pk_documentos_contrato_franquia", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(DocumentoContratoFranquia.Id), "id", "uuid");
        AssertColumn(
            entityType,
            nameof(DocumentoContratoFranquia.ContratoFranquiaVersaoId),
            "contrato_franquia_versao_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(DocumentoContratoFranquia.TipoDocumento),
            "tipo_documento",
            "varchar(30)",
            DocumentoContratoFranquia.TipoDocumentoTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(DocumentoContratoFranquia.NomeOriginal),
            "nome_original",
            "varchar(255)",
            DocumentoContratoFranquia.NomeOriginalTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(DocumentoContratoFranquia.ChaveArmazenamento),
            "chave_armazenamento",
            "varchar(500)",
            DocumentoContratoFranquia.ChaveArmazenamentoTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(DocumentoContratoFranquia.ContentType),
            "content_type",
            "varchar(100)",
            DocumentoContratoFranquia.ContentTypeTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(DocumentoContratoFranquia.TamanhoBytes),
            "tamanho_bytes",
            "bigint");
        AssertColumn(
            entityType,
            nameof(DocumentoContratoFranquia.HashSha256),
            "hash_sha256",
            "varchar(64)",
            DocumentoContratoFranquia.HashSha256Tamanho,
            true);
        AssertColumn(
            entityType,
            nameof(DocumentoContratoFranquia.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(DocumentoContratoFranquia.EnviadoPorUsuarioId),
            "enviado_por_usuario_id",
            "uuid");
        Assert.Equal(
            typeof(string),
            entityType.FindProperty(nameof(DocumentoContratoFranquia.TipoDocumento))!
                .GetTypeMapping().Converter!.ProviderClrType);
        Assert.Null(entityType.FindProperty("Conteudo"));

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(
            indexes["ix_documentos_contrato_franquia_versao_id"],
            false,
            nameof(DocumentoContratoFranquia.ContratoFranquiaVersaoId));
        AssertIndex(
            indexes["uq_documentos_contrato_franquia_chave_armazenamento"],
            true,
            nameof(DocumentoContratoFranquia.ChaveArmazenamento));
        AssertIndex(
            indexes["ix_documentos_contrato_franquia_enviado_por_usuario_id"],
            false,
            nameof(DocumentoContratoFranquia.EnviadoPorUsuarioId));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_documentos_contrato_franquia_versao_id"],
            "fk_documentos_contrato_franquia_versao_id",
            typeof(ContratoFranquiaVersao),
            nameof(DocumentoContratoFranquia.ContratoFranquiaVersaoId));
        AssertForeignKey(
            foreignKeys["fk_documentos_contrato_franquia_enviado_por_usuario_id"],
            "fk_documentos_contrato_franquia_enviado_por_usuario_id",
            typeof(UsuarioIdentity),
            nameof(DocumentoContratoFranquia.EnviadoPorUsuarioId));
        Assert.Equal(6, entityType.GetCheckConstraints().Count());
    }

    [Fact]
    public void Contexto_unico_contem_contratos_versoes_documentos_e_nenhum_cascade_novo()
    {
        using var context = CreateContext();
        var model = GetModel(context);
        var contractTypes = new[]
        {
            typeof(ContratoFranquia),
            typeof(ContratoFranquiaVersao),
            typeof(DocumentoContratoFranquia)
        };

        foreach (var contractType in contractTypes)
        {
            var entityType = model.FindEntityType(contractType);
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

    private static void AssertNumeric(
        IEntityType entityType,
        string propertyName,
        string columnName,
        string columnType,
        int precision,
        int scale,
        bool isNullable = false)
    {
        AssertColumn(entityType, propertyName, columnName, columnType, isNullable: isNullable);
        var property = entityType.FindProperty(propertyName)!;
        Assert.Equal(precision, property.GetPrecision());
        Assert.Equal(scale, property.GetScale());
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
        string constraintName,
        Type principalType,
        params string[] propertyNames)
    {
        Assert.Equal(constraintName, foreignKey.GetConstraintName());
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(principalType, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(propertyNames, foreignKey.Properties.Select(property => property.Name));
    }
}

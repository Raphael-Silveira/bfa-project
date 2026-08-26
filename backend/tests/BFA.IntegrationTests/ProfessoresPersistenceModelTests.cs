using BFA.Domain.Organizacoes;
using BFA.Domain.Professores;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed class ProfessoresPersistenceModelTests
{
    [Fact]
    public void Professor_possui_colunas_opcionais_uniques_e_fks_restritivas()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(Professor));

        Assert.NotNull(entityType);
        Assert.Equal("professores", entityType.GetTableName());
        Assert.Equal("pk_professores", entityType.FindPrimaryKey()!.GetName());
        Assert.Equal(
            "uq_professores_organizacao_id_id",
            entityType.GetKeys().Single(key => !key.IsPrimaryKey()).GetName());

        AssertColumn(entityType, nameof(Professor.Id), "id", "uuid");
        AssertColumn(entityType, nameof(Professor.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(entityType, nameof(Professor.UsuarioId), "usuario_id", "uuid", isNullable: true);
        AssertColumn(
            entityType,
            nameof(Professor.NomeCompleto),
            "nome_completo",
            "varchar(150)",
            Professor.NomeCompletoTamanhoMaximo);
        AssertColumn(
            entityType,
            nameof(Professor.Cpf),
            "cpf",
            "varchar(11)",
            Professor.CpfTamanho,
            true);
        AssertColumn(
            entityType,
            nameof(Professor.Telefone),
            "telefone",
            "varchar(30)",
            Professor.TelefoneTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(Professor.Email),
            "email",
            "varchar(256)",
            Professor.EmailTamanhoMaximo,
            true);
        AssertColumn(entityType, nameof(Professor.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType,
            nameof(Professor.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(Professor.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        var cpf = indexes["uq_professores_organizacao_cpf"];
        AssertIndex(cpf, true, nameof(Professor.OrganizacaoId), nameof(Professor.Cpf));
        Assert.Equal("cpf IS NOT NULL", cpf.GetFilter());
        var usuario = indexes["uq_professores_organizacao_usuario"];
        AssertIndex(usuario, true, nameof(Professor.OrganizacaoId), nameof(Professor.UsuarioId));
        Assert.Equal("usuario_id IS NOT NULL", usuario.GetFilter());
        AssertIndex(
            indexes["ix_professores_organizacao_ativo"],
            false,
            nameof(Professor.OrganizacaoId),
            nameof(Professor.Ativo));
        AssertIndex(indexes["ix_professores_usuario_id"], false, nameof(Professor.UsuarioId));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_professores_organizacoes_organizacao_id"],
            typeof(Organizacao),
            nameof(Professor.OrganizacaoId));
        AssertForeignKey(
            foreignKeys["fk_professores_usuarios_usuario_id"],
            typeof(UsuarioIdentity),
            nameof(Professor.UsuarioId));
        Assert.Equal(4, entityType.GetCheckConstraints().Count());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_inativacao_professor");
    }

    [Fact]
    public void ProfessorUnidade_impede_duplicidade_e_protege_tenant_com_fks_compostas()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(ProfessorUnidade));

        Assert.NotNull(entityType);
        Assert.Equal("professores_unidades", entityType.GetTableName());
        Assert.Equal("pk_professores_unidades", entityType.FindPrimaryKey()!.GetName());
        var alternateKeys = entityType.GetKeys()
            .Where(key => !key.IsPrimaryKey())
            .ToDictionary(key => key.GetName()!);
        Assert.Contains("uq_professores_unidades_organizacao_id_id", alternateKeys);
        Assert.Contains("uq_professores_unidades_organizacao_unidade_id", alternateKeys);

        AssertColumn(entityType, nameof(ProfessorUnidade.Id), "id", "uuid");
        AssertColumn(
            entityType,
            nameof(ProfessorUnidade.OrganizacaoId),
            "organizacao_id",
            "uuid");
        AssertColumn(entityType, nameof(ProfessorUnidade.ProfessorId), "professor_id", "uuid");
        AssertColumn(entityType, nameof(ProfessorUnidade.UnidadeId), "unidade_id", "uuid");
        AssertColumn(entityType, nameof(ProfessorUnidade.Ativo), "ativo", "boolean");

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(
            indexes["uq_professores_unidades_professor_unidade"],
            true,
            nameof(ProfessorUnidade.OrganizacaoId),
            nameof(ProfessorUnidade.ProfessorId),
            nameof(ProfessorUnidade.UnidadeId));
        AssertIndex(
            indexes["ix_professores_unidades_organizacao_unidade_ativo"],
            false,
            nameof(ProfessorUnidade.OrganizacaoId),
            nameof(ProfessorUnidade.UnidadeId),
            nameof(ProfessorUnidade.Ativo));
        AssertIndex(
            indexes["ix_professores_unidades_organizacao_professor_ativo"],
            false,
            nameof(ProfessorUnidade.OrganizacaoId),
            nameof(ProfessorUnidade.ProfessorId),
            nameof(ProfessorUnidade.Ativo));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_professores_unidades_professor"],
            typeof(Professor),
            nameof(ProfessorUnidade.OrganizacaoId),
            nameof(ProfessorUnidade.ProfessorId));
        AssertForeignKey(
            foreignKeys["fk_professores_unidades_organizacao"],
            typeof(Organizacao),
            nameof(ProfessorUnidade.OrganizacaoId));
        AssertForeignKey(
            foreignKeys["fk_professores_unidades_unidade"],
            typeof(Unidade),
            nameof(ProfessorUnidade.OrganizacaoId),
            nameof(ProfessorUnidade.UnidadeId));
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_estado_professor_unidade");
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_professor_unidade_turmas");
    }

    [Fact]
    public void Remuneracao_alinha_tipos_historico_indices_trigger_e_fks_restritivas()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(ProfessorRemuneracao));

        Assert.NotNull(entityType);
        Assert.Equal("professores_remuneracoes", entityType.GetTableName());
        Assert.Equal("pk_professores_remuneracoes", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(ProfessorRemuneracao.Id), "id", "uuid");
        AssertColumn(
            entityType,
            nameof(ProfessorRemuneracao.OrganizacaoId),
            "organizacao_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(ProfessorRemuneracao.ProfessorUnidadeId),
            "professor_unidade_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(ProfessorRemuneracao.Modalidade),
            "modalidade",
            "varchar(30)",
            ProfessorRemuneracao.ModalidadeTamanhoMaximo);
        Assert.Equal(
            typeof(string),
            entityType.FindProperty(nameof(ProfessorRemuneracao.Modalidade))!
                .GetTypeMapping().Converter!.ProviderClrType);
        AssertNumeric(
            entityType,
            nameof(ProfessorRemuneracao.Valor),
            "valor",
            "numeric(12,2)",
            12,
            2);
        AssertColumn(
            entityType,
            nameof(ProfessorRemuneracao.VigenciaInicio),
            "vigencia_inicio",
            "date");
        AssertColumn(
            entityType,
            nameof(ProfessorRemuneracao.VigenciaFim),
            "vigencia_fim",
            "date",
            isNullable: true);
        AssertColumn(
            entityType,
            nameof(ProfessorRemuneracao.Observacao),
            "observacao",
            "varchar(1000)",
            ProfessorRemuneracao.ObservacaoTamanhoMaximo,
            true);
        AssertColumn(
            entityType,
            nameof(ProfessorRemuneracao.CriadoPorUsuarioId),
            "criado_por_usuario_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(ProfessorRemuneracao.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        Assert.Null(entityType.FindProperty("AtualizadoEmUtc"));

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        var aberta = indexes["uq_professores_remuneracoes_aberta"];
        AssertIndex(aberta, true, nameof(ProfessorRemuneracao.ProfessorUnidadeId));
        Assert.Equal("vigencia_fim IS NULL", aberta.GetFilter());
        AssertIndex(
            indexes["uq_professores_remuneracoes_vigencia_inicio"],
            true,
            nameof(ProfessorRemuneracao.OrganizacaoId),
            nameof(ProfessorRemuneracao.ProfessorUnidadeId),
            nameof(ProfessorRemuneracao.VigenciaInicio));
        AssertIndex(
            indexes["ix_professores_remuneracoes_criado_por_usuario_id"],
            false,
            nameof(ProfessorRemuneracao.CriadoPorUsuarioId));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_professores_remuneracoes_professor_unidade"],
            typeof(ProfessorUnidade),
            nameof(ProfessorRemuneracao.OrganizacaoId),
            nameof(ProfessorRemuneracao.ProfessorUnidadeId));
        AssertForeignKey(
            foreignKeys["fk_professores_remuneracoes_organizacao"],
            typeof(Organizacao),
            nameof(ProfessorRemuneracao.OrganizacaoId));
        AssertForeignKey(
            foreignKeys["fk_professores_remuneracoes_criado_por_usuario_id"],
            typeof(UsuarioIdentity),
            nameof(ProfessorRemuneracao.CriadoPorUsuarioId));
        Assert.Equal(4, entityType.GetCheckConstraints().Count());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_remuneracao_professor");
    }

    [Fact]
    public void Contexto_unico_contem_modulo_de_professores_sem_cascade()
    {
        using var context = CreateContext();
        var model = GetModel(context);

        foreach (var type in new[]
        {
            typeof(Professor),
            typeof(ProfessorUnidade),
            typeof(ProfessorRemuneracao)
        })
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

    private static void AssertNumeric(
        IEntityType entityType,
        string propertyName,
        string columnName,
        string columnType,
        int precision,
        int scale)
    {
        AssertColumn(entityType, propertyName, columnName, columnType);
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
        Type principalType,
        params string[] propertyNames)
    {
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(principalType, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(propertyNames, foreignKey.Properties.Select(property => property.Name));
    }
}

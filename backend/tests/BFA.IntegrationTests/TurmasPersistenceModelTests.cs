using BFA.Domain.Organizacoes;
using BFA.Domain.Professores;
using BFA.Domain.Turmas;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed class TurmasPersistenceModelTests
{
    [Fact]
    public void Turma_alinha_colunas_constraints_indices_e_fks_tenant_safe()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(Turma));

        Assert.NotNull(entityType);
        Assert.Equal("turmas", entityType.GetTableName());
        Assert.Equal("pk_turmas", entityType.FindPrimaryKey()!.GetName());
        Assert.Equal(
            "uq_turmas_organizacao_unidade_id",
            entityType.GetKeys().Single(key => !key.IsPrimaryKey()).GetName());

        AssertColumn(entityType, nameof(Turma.Id), "id", "uuid");
        AssertColumn(entityType, nameof(Turma.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(entityType, nameof(Turma.UnidadeId), "unidade_id", "uuid");
        AssertColumn(
            entityType,
            nameof(Turma.ProfessorUnidadeId),
            "professor_unidade_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(Turma.Nome),
            "nome",
            "varchar(150)",
            Turma.NomeTamanhoMaximo);
        AssertColumn(entityType, nameof(Turma.Capacidade), "capacidade", "integer");
        AssertColumn(entityType, nameof(Turma.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType,
            nameof(Turma.CriadoPorUsuarioId),
            "criado_por_usuario_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(Turma.AtualizadoPorUsuarioId),
            "atualizado_por_usuario_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(Turma.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(Turma.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(
            indexes["ix_turmas_organizacao_unidade_ativo"],
            false,
            nameof(Turma.OrganizacaoId),
            nameof(Turma.UnidadeId),
            nameof(Turma.Ativo));
        AssertIndex(
            indexes["ix_turmas_organizacao_professor_unidade_ativo"],
            false,
            nameof(Turma.OrganizacaoId),
            nameof(Turma.ProfessorUnidadeId),
            nameof(Turma.Ativo));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_turmas_organizacoes_organizacao_id"],
            typeof(Organizacao),
            nameof(Turma.OrganizacaoId));
        AssertForeignKey(
            foreignKeys["fk_turmas_unidade"],
            typeof(Unidade),
            nameof(Turma.OrganizacaoId),
            nameof(Turma.UnidadeId));
        AssertForeignKey(
            foreignKeys["fk_turmas_professor_unidade"],
            typeof(ProfessorUnidade),
            nameof(Turma.OrganizacaoId),
            nameof(Turma.UnidadeId),
            nameof(Turma.ProfessorUnidadeId));
        AssertForeignKey(
            foreignKeys["fk_turmas_criado_por_usuario_id"],
            typeof(UsuarioIdentity),
            nameof(Turma.CriadoPorUsuarioId));
        AssertForeignKey(
            foreignKeys["fk_turmas_atualizado_por_usuario_id"],
            typeof(UsuarioIdentity),
            nameof(Turma.AtualizadoPorUsuarioId));
        Assert.Equal(2, entityType.GetCheckConstraints().Count());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_estado_turma");
    }

    [Fact]
    public void TurmaHorario_alinha_tipos_historico_indices_e_fks_restritivas()
    {
        using var context = CreateContext();
        var entityType = GetModel(context).FindEntityType(typeof(TurmaHorario));

        Assert.NotNull(entityType);
        Assert.Equal("turmas_horarios", entityType.GetTableName());
        Assert.Equal("pk_turmas_horarios", entityType.FindPrimaryKey()!.GetName());

        AssertColumn(entityType, nameof(TurmaHorario.Id), "id", "uuid");
        AssertColumn(
            entityType,
            nameof(TurmaHorario.OrganizacaoId),
            "organizacao_id",
            "uuid");
        AssertColumn(entityType, nameof(TurmaHorario.UnidadeId), "unidade_id", "uuid");
        AssertColumn(entityType, nameof(TurmaHorario.TurmaId), "turma_id", "uuid");
        AssertColumn(
            entityType,
            nameof(TurmaHorario.ProfessorUnidadeId),
            "professor_unidade_id",
            "uuid");
        AssertColumn(entityType, nameof(TurmaHorario.DiaSemana), "dia_semana", "smallint");
        Assert.Equal(
            typeof(short),
            entityType.FindProperty(nameof(TurmaHorario.DiaSemana))!
                .GetTypeMapping().Converter!.ProviderClrType);
        AssertColumn(
            entityType,
            nameof(TurmaHorario.HoraInicio),
            "hora_inicio",
            "time without time zone");
        AssertColumn(
            entityType,
            nameof(TurmaHorario.HoraFim),
            "hora_fim",
            "time without time zone");
        AssertColumn(
            entityType,
            nameof(TurmaHorario.VigenciaInicio),
            "vigencia_inicio",
            "date");
        AssertColumn(
            entityType,
            nameof(TurmaHorario.VigenciaFim),
            "vigencia_fim",
            "date",
            isNullable: true);
        AssertColumn(entityType, nameof(TurmaHorario.Ativo), "ativo", "boolean");
        AssertColumn(
            entityType,
            nameof(TurmaHorario.CriadoPorUsuarioId),
            "criado_por_usuario_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(TurmaHorario.AtualizadoPorUsuarioId),
            "atualizado_por_usuario_id",
            "uuid");
        AssertColumn(
            entityType,
            nameof(TurmaHorario.CriadoEmUtc),
            "criado_em_utc",
            "timestamp with time zone");
        AssertColumn(
            entityType,
            nameof(TurmaHorario.AtualizadoEmUtc),
            "atualizado_em_utc",
            "timestamp with time zone");

        var indexes = entityType.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(
            indexes["uq_turmas_horarios_regra"],
            true,
            nameof(TurmaHorario.OrganizacaoId),
            nameof(TurmaHorario.TurmaId),
            nameof(TurmaHorario.DiaSemana),
            nameof(TurmaHorario.HoraInicio),
            nameof(TurmaHorario.HoraFim),
            nameof(TurmaHorario.VigenciaInicio));
        AssertIndex(
            indexes["ix_turmas_horarios_organizacao_unidade_dia_ativo"],
            false,
            nameof(TurmaHorario.OrganizacaoId),
            nameof(TurmaHorario.UnidadeId),
            nameof(TurmaHorario.DiaSemana),
            nameof(TurmaHorario.Ativo));
        AssertIndex(
            indexes["ix_turmas_horarios_organizacao_turma_ativo"],
            false,
            nameof(TurmaHorario.OrganizacaoId),
            nameof(TurmaHorario.TurmaId),
            nameof(TurmaHorario.Ativo));
        AssertIndex(
            indexes["ix_turmas_horarios_conflito_professor"],
            false,
            nameof(TurmaHorario.OrganizacaoId),
            nameof(TurmaHorario.ProfessorUnidadeId),
            nameof(TurmaHorario.DiaSemana),
            nameof(TurmaHorario.Ativo),
            nameof(TurmaHorario.HoraInicio),
            nameof(TurmaHorario.HoraFim));

        var foreignKeys = entityType.GetForeignKeys()
            .ToDictionary(foreignKey => foreignKey.GetConstraintName()!);
        AssertForeignKey(
            foreignKeys["fk_turmas_horarios_organizacao"],
            typeof(Organizacao),
            nameof(TurmaHorario.OrganizacaoId));
        AssertForeignKey(
            foreignKeys["fk_turmas_horarios_unidade"],
            typeof(Unidade),
            nameof(TurmaHorario.OrganizacaoId),
            nameof(TurmaHorario.UnidadeId));
        AssertForeignKey(
            foreignKeys["fk_turmas_horarios_turma"],
            typeof(Turma),
            nameof(TurmaHorario.OrganizacaoId),
            nameof(TurmaHorario.UnidadeId),
            nameof(TurmaHorario.TurmaId));
        AssertForeignKey(
            foreignKeys["fk_turmas_horarios_professor_unidade"],
            typeof(ProfessorUnidade),
            nameof(TurmaHorario.OrganizacaoId),
            nameof(TurmaHorario.UnidadeId),
            nameof(TurmaHorario.ProfessorUnidadeId));
        AssertForeignKey(
            foreignKeys["fk_turmas_horarios_criado_por_usuario_id"],
            typeof(UsuarioIdentity),
            nameof(TurmaHorario.CriadoPorUsuarioId));
        AssertForeignKey(
            foreignKeys["fk_turmas_horarios_atualizado_por_usuario_id"],
            typeof(UsuarioIdentity),
            nameof(TurmaHorario.AtualizadoPorUsuarioId));
        Assert.Equal(3, entityType.GetCheckConstraints().Count());
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_turma_horario");
    }

    [Fact]
    public void Contexto_unico_contem_turmas_e_horarios_sem_cascade()
    {
        using var context = CreateContext();
        var model = GetModel(context);

        Assert.NotNull(context.Turmas);
        Assert.NotNull(context.TurmasHorarios);

        foreach (var type in new[] { typeof(Turma), typeof(TurmaHorario) })
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

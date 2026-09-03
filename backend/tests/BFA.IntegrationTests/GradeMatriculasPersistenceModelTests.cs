using BFA.Domain.Matriculas;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed class GradeMatriculasPersistenceModelTests
{
    [Fact]
    public void MatriculaHorario_alinha_colunas_chaves_indices_e_vigencia()
    {
        using var context = CreateContext();
        var entity = Model(context).FindEntityType(typeof(MatriculaHorario));
        Assert.NotNull(entity);
        Assert.Equal("matriculas_horarios", entity.GetTableName());
        Assert.Equal("pk_matriculas_horarios", entity.FindPrimaryKey()!.GetName());
        AssertColumn(entity, nameof(MatriculaHorario.Id), "id", "uuid");
        AssertColumn(entity, nameof(MatriculaHorario.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(entity, nameof(MatriculaHorario.UnidadeId), "unidade_id", "uuid");
        AssertColumn(entity, nameof(MatriculaHorario.MatriculaId), "matricula_id", "uuid");
        AssertColumn(entity, nameof(MatriculaHorario.TurmaHorarioId), "turma_horario_id", "uuid");
        AssertColumn(entity, nameof(MatriculaHorario.VigenciaInicio), "vigencia_inicio", "date");
        AssertColumn(entity, nameof(MatriculaHorario.VigenciaFim), "vigencia_fim", "date", true);
        AssertColumn(entity, nameof(MatriculaHorario.CriadoEmUtc),
            "criado_em_utc", "timestamp with time zone");
        AssertColumn(entity, nameof(MatriculaHorario.AtualizadoEmUtc),
            "atualizado_em_utc", "timestamp with time zone");
        Assert.Single(entity.GetCheckConstraints());

        var indexes = entity.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(indexes["uq_matriculas_horarios_aberto"], true,
            nameof(MatriculaHorario.OrganizacaoId), nameof(MatriculaHorario.UnidadeId),
            nameof(MatriculaHorario.MatriculaId), nameof(MatriculaHorario.TurmaHorarioId));
        Assert.Equal("vigencia_fim IS NULL",
            indexes["uq_matriculas_horarios_aberto"].GetFilter());
        AssertIndex(indexes["uq_matriculas_horarios_historico"], true,
            nameof(MatriculaHorario.OrganizacaoId), nameof(MatriculaHorario.MatriculaId),
            nameof(MatriculaHorario.TurmaHorarioId), nameof(MatriculaHorario.VigenciaInicio));
    }

    [Fact]
    public void MatriculaHorario_usa_fks_tenant_safe_restritivas_e_auditoria()
    {
        using var context = CreateContext();
        var entity = Model(context).FindEntityType(typeof(MatriculaHorario))!;
        var fks = entity.GetForeignKeys().ToDictionary(fk => fk.GetConstraintName()!);

        AssertForeignKey(fks["fk_matriculas_horarios_matricula"], typeof(Matricula),
            nameof(MatriculaHorario.OrganizacaoId), nameof(MatriculaHorario.UnidadeId),
            nameof(MatriculaHorario.MatriculaId));
        AssertForeignKey(fks["fk_matriculas_horarios_turma_horario"], typeof(TurmaHorario),
            nameof(MatriculaHorario.OrganizacaoId), nameof(MatriculaHorario.UnidadeId),
            nameof(MatriculaHorario.TurmaHorarioId));
        AssertForeignKey(fks["fk_matriculas_horarios_criado_por_usuario_id"],
            typeof(UsuarioIdentity), nameof(MatriculaHorario.CriadoPorUsuarioId));
        AssertForeignKey(fks["fk_matriculas_horarios_atualizado_por_usuario_id"],
            typeof(UsuarioIdentity), nameof(MatriculaHorario.AtualizadoPorUsuarioId));
        Assert.All(entity.GetForeignKeys(),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void Contexto_e_tabelas_existentes_declaram_integridade_da_grade()
    {
        using var context = CreateContext();
        var model = Model(context);
        Assert.NotNull(context.MatriculasHorarios);
        Assert.Contains(model.FindEntityType(typeof(MatriculaHorario))!.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_matricula_horario");
        Assert.Contains(model.FindEntityType(typeof(Matricula))!.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_matricula_grade_aberta");
        Assert.Contains(model.FindEntityType(typeof(TurmaHorario))!.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_turma_horario_grade_aberta");
        Assert.Contains(model.FindEntityType(typeof(Turma))!.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_capacidade_turma_grade");
        Assert.Contains(model.FindEntityType(typeof(TurmaHorario))!.GetKeys(), key =>
            key.GetName() == "uq_turmas_horarios_organizacao_unidade_id");
    }

    private static BfaDbContext CreateContext() => new(
        new DbContextOptionsBuilder<BfaDbContext>().UseNpgsql().Options);

    private static IModel Model(BfaDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    private static void AssertColumn(
        IEntityType entity, string property, string column, string type, bool nullable = false)
    {
        var metadata = entity.FindProperty(property);
        Assert.NotNull(metadata);
        Assert.Equal(column, metadata.GetColumnName());
        Assert.Equal(type, metadata.GetColumnType());
        Assert.Equal(nullable, metadata.IsNullable);
    }

    private static void AssertIndex(IIndex index, bool unique, params string[] properties)
    {
        Assert.Equal(unique, index.IsUnique);
        Assert.Equal(properties, index.Properties.Select(property => property.Name));
    }

    private static void AssertForeignKey(
        IForeignKey foreignKey, Type principal, params string[] properties)
    {
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(principal, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(properties, foreignKey.Properties.Select(property => property.Name));
    }
}

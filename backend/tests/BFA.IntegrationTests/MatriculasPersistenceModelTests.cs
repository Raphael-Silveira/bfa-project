using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Organizacoes;
using BFA.Domain.Planos;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BFA.IntegrationTests;

public sealed class MatriculasPersistenceModelTests
{
    [Fact]
    public void Disponibilidade_alinha_colunas_indices_fks_e_trigger()
    {
        using var context = CreateContext();
        var entity = Model(context).FindEntityType(typeof(PlanoDisponibilidadeUnidade));
        Assert.NotNull(entity);
        Assert.Equal("planos_disponibilidades_unidades", entity.GetTableName());
        Assert.Equal("pk_planos_disponibilidades_unidades", entity.FindPrimaryKey()!.GetName());
        AssertColumn(entity, nameof(PlanoDisponibilidadeUnidade.Id), "id", "uuid");
        AssertColumn(entity, nameof(PlanoDisponibilidadeUnidade.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(entity, nameof(PlanoDisponibilidadeUnidade.PlanoId), "plano_id", "uuid");
        AssertColumn(entity, nameof(PlanoDisponibilidadeUnidade.UnidadeId), "unidade_id", "uuid");
        AssertColumn(entity, nameof(PlanoDisponibilidadeUnidade.Ativo), "ativo", "boolean");
        AssertColumn(entity, nameof(PlanoDisponibilidadeUnidade.CriadoEmUtc), "criado_em_utc", "timestamp with time zone");
        AssertColumn(entity, nameof(PlanoDisponibilidadeUnidade.AtualizadoEmUtc), "atualizado_em_utc", "timestamp with time zone");

        var indexes = entity.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(indexes["uq_planos_disponibilidades_unidades_organizacao_plano_unidade"], true,
            nameof(PlanoDisponibilidadeUnidade.OrganizacaoId), nameof(PlanoDisponibilidadeUnidade.PlanoId), nameof(PlanoDisponibilidadeUnidade.UnidadeId));
        AssertIndex(indexes["ix_planos_disponibilidades_unidades_organizacao_unidade_ativo"], false,
            nameof(PlanoDisponibilidadeUnidade.OrganizacaoId), nameof(PlanoDisponibilidadeUnidade.UnidadeId), nameof(PlanoDisponibilidadeUnidade.Ativo));
        AssertIndex(indexes["ix_planos_disponibilidades_unidades_organizacao_plano_ativo"], false,
            nameof(PlanoDisponibilidadeUnidade.OrganizacaoId), nameof(PlanoDisponibilidadeUnidade.PlanoId), nameof(PlanoDisponibilidadeUnidade.Ativo));

        var fks = entity.GetForeignKeys().ToDictionary(fk => fk.GetConstraintName()!);
        AssertForeignKey(fks["fk_planos_disponibilidades_unidades_organizacao"], typeof(Organizacao), nameof(PlanoDisponibilidadeUnidade.OrganizacaoId));
        AssertForeignKey(fks["fk_planos_disponibilidades_unidades_plano"], typeof(Plano), nameof(PlanoDisponibilidadeUnidade.OrganizacaoId), nameof(PlanoDisponibilidadeUnidade.PlanoId));
        AssertForeignKey(fks["fk_planos_disponibilidades_unidades_unidade"], typeof(Unidade), nameof(PlanoDisponibilidadeUnidade.OrganizacaoId), nameof(PlanoDisponibilidadeUnidade.UnidadeId));
        Assert.Contains(entity.GetDeclaredTriggers(), trigger => trigger.ModelName == "trg_proteger_plano_disponibilidade_unidade");
    }

    [Fact]
    public void Matricula_alinha_snapshot_checks_indices_fks_e_conversao_de_status()
    {
        using var context = CreateContext();
        var entity = Model(context).FindEntityType(typeof(Matricula));
        Assert.NotNull(entity);
        Assert.Equal("matriculas", entity.GetTableName());
        Assert.Equal("pk_matriculas", entity.FindPrimaryKey()!.GetName());
        AssertColumn(entity, nameof(Matricula.Id), "id", "uuid");
        AssertColumn(entity, nameof(Matricula.OrganizacaoId), "organizacao_id", "uuid");
        AssertColumn(entity, nameof(Matricula.UnidadeId), "unidade_id", "uuid");
        AssertColumn(entity, nameof(Matricula.AlunoId), "aluno_id", "uuid");
        AssertColumn(entity, nameof(Matricula.PlanoVersaoId), "plano_versao_id", "uuid");
        AssertColumn(entity, nameof(Matricula.DataInicio), "data_inicio", "date");
        AssertColumn(entity, nameof(Matricula.DataFimPrevista), "data_fim_prevista", "date");
        AssertColumn(entity, nameof(Matricula.DataFimReal), "data_fim_real", "date", true);
        AssertColumn(entity, nameof(Matricula.Status), "status", "varchar(20)");
        Assert.Equal(typeof(string), entity.FindProperty(nameof(Matricula.Status))!.GetTypeMapping().Converter!.ProviderClrType);
        AssertColumn(entity, nameof(Matricula.ValorMensalContratado), "valor_mensal_contratado", "numeric(12,2)");
        Assert.Equal(12, entity.FindProperty(nameof(Matricula.ValorMensalContratado))!.GetPrecision());
        Assert.Equal(2, entity.FindProperty(nameof(Matricula.ValorMensalContratado))!.GetScale());
        AssertColumn(entity, nameof(Matricula.ValorTaxaMatricula), "valor_taxa_matricula", "numeric(12,2)", true);
        Assert.Equal(5, entity.GetCheckConstraints().Count());

        var indexes = entity.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        AssertIndex(indexes["uq_matriculas_ativa_organizacao_unidade_aluno"], true,
            nameof(Matricula.OrganizacaoId), nameof(Matricula.UnidadeId), nameof(Matricula.AlunoId));
        Assert.Equal("status = 'Ativa'", indexes["uq_matriculas_ativa_organizacao_unidade_aluno"].GetFilter());
        AssertIndex(indexes["ix_matriculas_organizacao_unidade_status"], false,
            nameof(Matricula.OrganizacaoId), nameof(Matricula.UnidadeId), nameof(Matricula.Status));
        AssertIndex(indexes["ix_matriculas_organizacao_aluno_status"], false,
            nameof(Matricula.OrganizacaoId), nameof(Matricula.AlunoId), nameof(Matricula.Status));

        var fks = entity.GetForeignKeys().ToDictionary(fk => fk.GetConstraintName()!);
        AssertForeignKey(fks["fk_matriculas_organizacao"], typeof(Organizacao), nameof(Matricula.OrganizacaoId));
        AssertForeignKey(fks["fk_matriculas_unidade"], typeof(Unidade), nameof(Matricula.OrganizacaoId), nameof(Matricula.UnidadeId));
        AssertForeignKey(fks["fk_matriculas_aluno"], typeof(Aluno), nameof(Matricula.OrganizacaoId), nameof(Matricula.AlunoId));
        AssertForeignKey(fks["fk_matriculas_plano_versao"], typeof(PlanoVersao), nameof(Matricula.OrganizacaoId), nameof(Matricula.PlanoVersaoId));
        AssertForeignKey(fks["fk_matriculas_criado_por_usuario_id"], typeof(UsuarioIdentity), nameof(Matricula.CriadoPorUsuarioId));
        Assert.Contains(entity.GetDeclaredTriggers(), trigger => trigger.ModelName == "trg_proteger_matricula");
    }

    [Fact]
    public void Contexto_unico_expoe_V012_sem_cascade_e_aluno_declara_novo_trigger()
    {
        using var context = CreateContext();
        Assert.NotNull(context.PlanosDisponibilidadesUnidades);
        Assert.NotNull(context.Matriculas);
        foreach (var type in new[] { typeof(PlanoDisponibilidadeUnidade), typeof(Matricula) })
        {
            Assert.All(Model(context).FindEntityType(type)!.GetForeignKeys(),
                fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        }
        var aluno = Model(context).FindEntityType(typeof(Aluno));
        Assert.Contains(aluno!.GetDeclaredTriggers(), trigger => trigger.ModelName == "trg_proteger_aluno_matriculas");
        var versao = Model(context).FindEntityType(typeof(PlanoVersao));
        Assert.Contains(
            versao!.GetDeclaredTriggers(),
            trigger => trigger.ModelName == "trg_proteger_plano_versao_matriculas");
    }

    private static BfaDbContext CreateContext() => new(
        new DbContextOptionsBuilder<BfaDbContext>().UseNpgsql().Options);

    private static IModel Model(BfaDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    private static void AssertColumn(IEntityType entity, string property, string column, string type, bool nullable = false)
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

    private static void AssertForeignKey(IForeignKey fk, Type principal, params string[] properties)
    {
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.Equal(principal, fk.PrincipalEntityType.ClrType);
        Assert.Equal(properties, fk.Properties.Select(property => property.Name));
    }
}

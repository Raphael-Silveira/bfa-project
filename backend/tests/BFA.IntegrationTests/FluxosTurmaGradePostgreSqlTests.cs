using BFA.Application.Unidades.Turmas;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Unidades;
using Microsoft.EntityFrameworkCore;

namespace BFA.IntegrationTests;

public sealed class FluxosTurmaGradePostgreSqlTests(
    PostgreSqlEfemeroV013Fixture fixture)
    : IClassFixture<PostgreSqlEfemeroV013Fixture>
{
    [Fact]
    public async Task Troca_real_migra_multiplas_grades_e_horarios_por_identidade_material()
    {
        await fixture.ResetAsync(frequencia: 2, capacidade: 4);
        var segundaMatricula = await fixture.CreateEnrollmentAsync(fixture.UnidadeUmId);
        foreach (var matriculaId in new[]
        {
            fixture.MatriculaUnidadeUmId,
            segundaMatricula
        })
        {
            await fixture.InsertGradeAsync(matriculaId, fixture.HorariosUnidadeUm[1]);
            await fixture.InsertGradeAsync(matriculaId, fixture.HorariosUnidadeUm[0]);
        }
        var novoProfessor = await fixture.CreateProfessorAsync(
            fixture.UnidadeUmId, "Professor Novo");
        await using var db = CreateContext();

        var resultado = await CreateRepository(db).TrocarAsync(
            fixture.OrganizacaoId, fixture.UnidadeUmId, fixture.TurmaUnidadeUmId,
            novoProfessor, new DateOnly(2026, 9, 1), fixture.UsuarioId,
            new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.Equal(EstadoTrocaProfessorTurma.Sucesso, resultado.Estado);
        Assert.Equal(8, resultado.HorariosMigrados);
        Assert.Equal(4, resultado.GradesMigradas);
        var horarios = await db.TurmasHorarios.AsNoTracking()
            .Where(item => item.TurmaId == fixture.TurmaUnidadeUmId)
            .OrderBy(item => item.DiaSemana).ThenBy(item => item.HoraInicio)
            .ThenBy(item => item.VigenciaInicio).ToArrayAsync();
        Assert.Equal(16, horarios.Length);
        Assert.Equal(8, horarios.Count(item =>
            item.VigenciaFim == new DateOnly(2026, 8, 31)));
        Assert.Equal(8, horarios.Count(item =>
            item.VigenciaInicio == new DateOnly(2026, 9, 1)
            && item.ProfessorUnidadeId == novoProfessor));
        foreach (var antigo in horarios.Where(item =>
            item.VigenciaFim == new DateOnly(2026, 8, 31)))
        {
            Assert.Contains(horarios, novo =>
                novo.VigenciaInicio == new DateOnly(2026, 9, 1)
                && novo.DiaSemana == antigo.DiaSemana
                && novo.HoraInicio == antigo.HoraInicio
                && novo.HoraFim == antigo.HoraFim);
        }
        var grades = await db.MatriculasHorarios.AsNoTracking().ToArrayAsync();
        Assert.Equal(8, grades.Length);
        Assert.Equal(4, grades.Count(item =>
            item.VigenciaFim == new DateOnly(2026, 8, 31)));
        Assert.Equal(4, grades.Count(item =>
            item.VigenciaInicio == new DateOnly(2026, 9, 1)
            && item.VigenciaFim == null));
    }

    [Fact]
    public async Task Fechamento_concorrente_de_grade_no_mesmo_aluno_nao_deadlocka_troca()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        var novoProfessor = await fixture.CreateProfessorAsync(
            fixture.UnidadeUmId, "Professor Concorrente");

        await using var gradeConnection = await fixture.OpenAsync();
        await using var gradeTransaction = await gradeConnection.BeginTransactionAsync();
        await fixture.ExecuteAsync(gradeConnection,
            "UPDATE matriculas_horarios SET vigencia_fim = DATE '2026-08-31', "
            + "atualizado_por_usuario_id = @usuario, atualizado_em_utc = now() "
            + "WHERE matricula_id = @matricula AND vigencia_fim IS NULL",
            ("usuario", fixture.UsuarioId),
            ("matricula", fixture.MatriculaUnidadeUmId));

        var trocar = Task.Run(async () =>
        {
            await using var db = CreateContext();
            return await CreateRepository(db).TrocarAsync(
                fixture.OrganizacaoId, fixture.UnidadeUmId, fixture.TurmaUnidadeUmId,
                novoProfessor, new DateOnly(2026, 9, 1), fixture.UsuarioId,
                new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
                CancellationToken.None);
        });
        await Task.Delay(150);
        Assert.False(trocar.IsCompleted);

        await gradeTransaction.CommitAsync();
        var resultado = await trocar.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(EstadoTrocaProfessorTurma.Sucesso, resultado.Estado);
        Assert.Equal(0, resultado.GradesMigradas);
    }

    [Fact]
    public async Task Falha_real_na_nova_grade_reverte_professor_horarios_e_grade_antigos()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        var novoProfessor = await fixture.CreateProfessorAsync(
            fixture.UnidadeUmId, "Professor Rollback");
        await fixture.ExecuteAsync(
            """
            CREATE FUNCTION falhar_grade_migrada_teste()
            RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
                IF NEW.vigencia_inicio = DATE '2026-09-01' THEN
                    RAISE EXCEPTION 'falha controlada da nova Grade'
                        USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER trg_falhar_grade_migrada_teste
            BEFORE INSERT ON matriculas_horarios
            FOR EACH ROW EXECUTE FUNCTION falhar_grade_migrada_teste();
            """);
        try
        {
            await using var db = CreateContext();
            var resultado = await CreateRepository(db).TrocarAsync(
                fixture.OrganizacaoId, fixture.UnidadeUmId, fixture.TurmaUnidadeUmId,
                novoProfessor, new DateOnly(2026, 9, 1), fixture.UsuarioId,
                new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
                CancellationToken.None);

            Assert.Equal(
                EstadoTrocaProfessorTurma.MigracaoGradeInvalida,
                resultado.Estado);
        }
        finally
        {
            await fixture.ExecuteAsync(
                "DROP TRIGGER trg_falhar_grade_migrada_teste ON matriculas_horarios; "
                + "DROP FUNCTION falhar_grade_migrada_teste()");
        }

        await using var verificacao = CreateContext();
        Assert.NotEqual(novoProfessor,
            (await verificacao.Turmas.SingleAsync(item =>
                item.Id == fixture.TurmaUnidadeUmId)).ProfessorUnidadeId);
        Assert.All(await verificacao.TurmasHorarios.Where(item =>
            item.TurmaId == fixture.TurmaUnidadeUmId).ToArrayAsync(),
            item => Assert.Null(item.VigenciaFim));
        var grade = await verificacao.MatriculasHorarios.SingleAsync();
        Assert.Null(grade.VigenciaFim);
        Assert.Equal(fixture.HorariosUnidadeUm[0], grade.TurmaHorarioId);
    }

    [Fact]
    public async Task Ajuste_real_bloqueia_grade_afetada_e_preserva_todos_os_ids()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await using var db = CreateContext();
        var solicitados = await db.TurmasHorarios.AsNoTracking()
            .Where(item => item.TurmaId == fixture.TurmaUnidadeUmId
                && item.VigenciaFim == null
                && item.Id != fixture.HorariosUnidadeUm[0])
            .Select(item => new NovoHorarioTurmaSolicitacao(
                item.DiaSemana, item.HoraInicio, item.HoraFim))
            .ToArrayAsync();

        var estado = await new AjusteHorariosTurmaRepositorio(db).AjustarAsync(
            fixture.OrganizacaoId, fixture.UnidadeUmId, fixture.TurmaUnidadeUmId,
            fixture.UsuarioId, new(new DateOnly(2026, 9, 1), solicitados),
            new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.Equal(EstadoAjusteHorariosTurma.ExisteGradeAfetada, estado);
        var idsAbertos = await db.TurmasHorarios.AsNoTracking()
            .Where(item => item.TurmaId == fixture.TurmaUnidadeUmId
                && item.VigenciaFim == null)
            .Select(item => item.Id).ToArrayAsync();
        Assert.Equal(fixture.HorariosUnidadeUm.OrderBy(id => id),
            idsAbertos.OrderBy(id => id));
    }

    private BfaDbContext CreateContext() => new(
        new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);

    private static TrocaProfessorTurmaRepositorio CreateRepository(BfaDbContext db) =>
        new(db, new AjusteHorariosTurmaRepositorio(db));
}

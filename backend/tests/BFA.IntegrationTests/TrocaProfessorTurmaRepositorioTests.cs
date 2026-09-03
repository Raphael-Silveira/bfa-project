using BFA.Application.Unidades.Turmas;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Professores;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Unidades;
using BFA.Infrastructure.Professores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BFA.IntegrationTests;

public sealed class TrocaProfessorTurmaRepositorioTests
{
    private static readonly DateTime Agora = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Troca_respeita_tres_saves_na_ordem_exigida()
    {
        var cenario = await CriarCenarioAsync();
        var interceptor = new ObservarOrdem();
        await using var db = CriarContexto(cenario.Banco, interceptor);
        var repositorio = CriarRepositorio(db);

        var estado = await repositorio.TrocarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.TurmaId,
            cenario.NovoVinculoId, new DateOnly(2026, 9, 1), Guid.NewGuid(),
            Agora.AddMonths(1), CancellationToken.None);

        Assert.Equal(EstadoTrocaProfessorTurma.Sucesso, estado.Estado);
        Assert.Equal(3, interceptor.Quantidade);
        Assert.True(interceptor.EncerrouPrimeiro);
        Assert.True(interceptor.TrocouTurmaSegundo);
        Assert.True(interceptor.InseriuHorarioTerceiro);
    }

    [Fact]
    public async Task Falha_no_terceiro_save_preserva_professor_e_horario_anteriores()
    {
        var cenario = await CriarCenarioAsync();
        var interceptor = new FalharTerceiroSave();
        await using var db = CriarContexto(cenario.Banco, interceptor);
        var repositorio = CriarRepositorio(db);

        var estado = await repositorio.TrocarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.TurmaId,
            cenario.NovoVinculoId, new DateOnly(2026, 9, 1), Guid.NewGuid(),
            Agora.AddMonths(1), CancellationToken.None);

        Assert.Equal(EstadoTrocaProfessorTurma.Falha, estado.Estado);
        await using var verificacao = CriarContexto(cenario.Banco);
        Assert.Equal(cenario.VinculoAnteriorId,
            (await verificacao.Turmas.SingleAsync()).ProfessorUnidadeId);
        Assert.Null((await verificacao.TurmasHorarios.SingleAsync()).VigenciaFim);
    }

    [Fact]
    public async Task Atribuicao_sai_do_professor_anterior_e_aparece_para_o_novo()
    {
        var cenario = await CriarCenarioAsync();
        await using var db = CriarContexto(cenario.Banco);
        var consulta = new MinhasTurmasProfessorRepositorio(db);
        Assert.Single(await consulta.ListarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.VinculoAnteriorId,
            new DateOnly(2026, 8, 1), CancellationToken.None));

        var estado = await CriarRepositorio(db).TrocarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.TurmaId,
            cenario.NovoVinculoId, new DateOnly(2026, 9, 1), Guid.NewGuid(),
            Agora.AddMonths(1), CancellationToken.None);

        Assert.Equal(EstadoTrocaProfessorTurma.Sucesso, estado.Estado);
        Assert.Empty(await consulta.ListarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.VinculoAnteriorId,
            new DateOnly(2026, 9, 1), CancellationToken.None));
        Assert.Single(await consulta.ListarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.NovoVinculoId,
            new DateOnly(2026, 9, 1), CancellationToken.None));
    }

    [Fact]
    public async Task Troca_com_grades_abertas_migra_um_para_um_e_preserva_historico()
    {
        var cenario = await CriarCenarioAsync(quantidadeGradesAbertas: 2,
            incluirGradeHistorica: true);
        await using var db = CriarContexto(cenario.Banco);

        var resultado = await CriarRepositorio(db).TrocarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.TurmaId,
            cenario.NovoVinculoId, new DateOnly(2026, 9, 1), Guid.NewGuid(),
            Agora.AddMonths(1), CancellationToken.None);

        Assert.Equal(EstadoTrocaProfessorTurma.Sucesso, resultado.Estado);
        Assert.Equal(1, resultado.HorariosMigrados);
        Assert.Equal(2, resultado.GradesMigradas);
        var horarios = await db.TurmasHorarios.OrderBy(item => item.VigenciaInicio).ToArrayAsync();
        Assert.Equal(2, horarios.Length);
        Assert.Equal(new DateOnly(2026, 8, 31), horarios[0].VigenciaFim);
        Assert.Equal(cenario.VinculoAnteriorId, horarios[0].ProfessorUnidadeId);
        Assert.Equal(new DateOnly(2026, 9, 1), horarios[1].VigenciaInicio);
        Assert.Equal(cenario.NovoVinculoId, horarios[1].ProfessorUnidadeId);
        Assert.Equal(horarios[0].DiaSemana, horarios[1].DiaSemana);
        Assert.Equal(horarios[0].HoraInicio, horarios[1].HoraInicio);
        Assert.Equal(horarios[0].HoraFim, horarios[1].HoraFim);

        var grades = await db.MatriculasHorarios
            .OrderBy(item => item.VigenciaInicio).ThenBy(item => item.Id).ToArrayAsync();
        Assert.Equal(5, grades.Length);
        Assert.Equal(3, grades.Count(item => item.TurmaHorarioId == horarios[0].Id));
        Assert.Equal(2, grades.Count(item => item.TurmaHorarioId == horarios[1].Id));
        Assert.All(grades.Where(item => item.VigenciaInicio == new DateOnly(2026, 9, 1)),
            item => Assert.Null(item.VigenciaFim));
    }

    [Fact]
    public async Task Falha_ao_criar_novas_grades_desfaz_toda_troca()
    {
        var cenario = await CriarCenarioAsync(quantidadeGradesAbertas: 1);
        var interceptor = new FalharQuintoSave();
        await using var db = CriarContexto(cenario.Banco, interceptor);

        var resultado = await CriarRepositorio(db).TrocarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.TurmaId,
            cenario.NovoVinculoId, new DateOnly(2026, 9, 1), Guid.NewGuid(),
            Agora.AddMonths(1), CancellationToken.None);

        Assert.Equal(EstadoTrocaProfessorTurma.Falha, resultado.Estado);
        Assert.Equal(5, interceptor.Quantidade);
        await using var verificacao = CriarContexto(cenario.Banco);
        Assert.Equal(cenario.VinculoAnteriorId,
            (await verificacao.Turmas.SingleAsync()).ProfessorUnidadeId);
        Assert.Null((await verificacao.TurmasHorarios.SingleAsync()).VigenciaFim);
        Assert.Null((await verificacao.MatriculasHorarios.SingleAsync()).VigenciaFim);
    }

    private static TrocaProfessorTurmaRepositorio CriarRepositorio(BfaDbContext db) =>
        new(db, new AjusteHorariosTurmaRepositorio(db));

    private static async Task<Cenario> CriarCenarioAsync(
        int quantidadeGradesAbertas = 0,
        bool incluirGradeHistorica = false)
    {
        var banco = $"troca-professor-{Guid.NewGuid():N}";
        var org = Guid.NewGuid();
        var unidade = Guid.NewGuid();
        var anterior = new Professor(Guid.NewGuid(), org, "Anterior", Agora);
        var novo = new Professor(Guid.NewGuid(), org, "Novo", Agora);
        var vinculoAnterior = new ProfessorUnidade(
            Guid.NewGuid(), org, anterior.Id, unidade, Agora);
        var vinculoNovo = new ProfessorUnidade(Guid.NewGuid(), org, novo.Id, unidade, Agora);
        var usuario = Guid.NewGuid();
        var turma = new Turma(Guid.NewGuid(), org, unidade, vinculoAnterior.Id,
            "Turma", 12, usuario, Agora);
        var horario = new TurmaHorario(Guid.NewGuid(), org, unidade, turma.Id,
            vinculoAnterior.Id, DiaSemana.Segunda, new TimeOnly(19, 0),
            new TimeOnly(20, 0), new DateOnly(2026, 8, 1), null, usuario, Agora);
        await using var db = CriarContexto(banco);
        db.AddRange(anterior, novo, vinculoAnterior, vinculoNovo, turma, horario);
        for (var indice = 0; indice < quantidadeGradesAbertas; indice++)
        {
            var aluno = new Aluno(
                Guid.NewGuid(), org, $"Aluno {indice}", new DateOnly(2000, 1, 1),
                new DateOnly(2026, 8, 1), Agora);
            var matricula = new Matricula(
                Guid.NewGuid(), org, unidade, aluno.Id, Guid.NewGuid(),
                new DateOnly(2026, 8, 1), 12, 100, false, null, usuario, Agora);
            var grade = new MatriculaHorario(
                Guid.NewGuid(), org, unidade, matricula.Id, horario.Id,
                new DateOnly(2026, 8, 1), usuario, Agora);
            db.AddRange(aluno, matricula, grade);
        }
        if (incluirGradeHistorica)
        {
            var gradeHistorica = new MatriculaHorario(
                Guid.NewGuid(), org, unidade, Guid.NewGuid(), horario.Id,
                new DateOnly(2026, 8, 1), usuario, Agora);
            gradeHistorica.Encerrar(
                new DateOnly(2026, 8, 15), usuario, Agora.AddDays(14));
            db.Add(gradeHistorica);
        }
        await db.SaveChangesAsync();
        return new(banco, org, unidade, turma.Id, vinculoAnterior.Id, vinculoNovo.Id);
    }

    private static BfaDbContext CriarContexto(string banco, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseInMemoryDatabase(banco)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(interceptors).Options;
        return new(options);
    }

    private sealed record Cenario(string Banco, Guid OrganizacaoId, Guid UnidadeId,
        Guid TurmaId, Guid VinculoAnteriorId, Guid NovoVinculoId);

    private class ObservarOrdem : SaveChangesInterceptor
    {
        public int Quantidade { get; private set; }
        public bool EncerrouPrimeiro { get; private set; }
        public bool TrocouTurmaSegundo { get; private set; }
        public bool InseriuHorarioTerceiro { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Quantidade++;
            var db = Assert.IsType<BfaDbContext>(eventData.Context);
            if (Quantidade == 1)
                EncerrouPrimeiro = db.ChangeTracker.Entries<TurmaHorario>()
                    .Any(item => item.State == EntityState.Modified)
                    && !db.ChangeTracker.Entries<Turma>().Any(item =>
                        item.State == EntityState.Modified);
            else if (Quantidade == 2)
                TrocouTurmaSegundo = db.ChangeTracker.Entries<Turma>()
                    .Any(item => item.State == EntityState.Modified)
                    && !db.ChangeTracker.Entries<TurmaHorario>()
                        .Any(item => item.State == EntityState.Added);
            else if (Quantidade == 3)
                InseriuHorarioTerceiro = db.ChangeTracker.Entries<TurmaHorario>()
                    .Any(item => item.State == EntityState.Added);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FalharTerceiroSave : ObservarOrdem
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var observado = base.SavingChangesAsync(eventData, result, cancellationToken);
            if (Quantidade < 3)
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
            return ValueTask.FromException<InterceptionResult<int>>(
                new DbUpdateException("Falha simulada na criação dos novos horários."));
        }
    }

    private sealed class FalharQuintoSave : ObservarOrdem
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var observado = base.SavingChangesAsync(eventData, result, cancellationToken);
            if (Quantidade < 5)
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
            return ValueTask.FromException<InterceptionResult<int>>(
                new DbUpdateException("Falha simulada na migracao da Grade."));
        }
    }
}

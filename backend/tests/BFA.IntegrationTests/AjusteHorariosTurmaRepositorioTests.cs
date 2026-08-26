using BFA.Application.Unidades.Turmas;
using BFA.Domain.Professores;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Unidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BFA.IntegrationTests;

public sealed class AjusteHorariosTurmaRepositorioTests
{
    private static readonly DateTime Agora = new(
        2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Encerra_atuais_antes_de_inserir_novos_na_mesma_operacao()
    {
        var cenario = await CriarCenarioAsync();
        var interceptor = new ObservarOrdemInterceptor();
        await using var db = CriarContexto(cenario.Banco, interceptor);
        var repositorio = new AjusteHorariosTurmaRepositorio(db);

        var resultado = await repositorio.AjustarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.TurmaId,
            Guid.NewGuid(), new(new DateOnly(2026, 9, 1),
                [new(DiaSemana.Segunda, new TimeOnly(20, 0), new TimeOnly(21, 0))]),
            Agora.AddMonths(1), CancellationToken.None);

        Assert.Equal(EstadoAjusteHorariosTurma.Sucesso, resultado);
        Assert.Equal(2, interceptor.Quantidade);
        Assert.True(interceptor.PrimeiroSaveSomenteEncerrou);
        Assert.True(interceptor.SegundoSaveSomenteInseriu);
    }

    [Fact]
    public async Task Falha_no_segundo_save_nao_deixa_programacao_antiga_encerrada()
    {
        var cenario = await CriarCenarioAsync();
        var interceptor = new FalharSegundoSaveInterceptor();
        await using var db = CriarContexto(cenario.Banco, interceptor);
        var repositorio = new AjusteHorariosTurmaRepositorio(db);

        var resultado = await repositorio.AjustarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.TurmaId,
            Guid.NewGuid(), new(new DateOnly(2026, 9, 1),
                [new(DiaSemana.Segunda, new TimeOnly(20, 0), new TimeOnly(21, 0))]),
            Agora.AddMonths(1), CancellationToken.None);

        Assert.Equal(EstadoAjusteHorariosTurma.Falha, resultado);
        Assert.Equal(2, interceptor.Quantidade);
        await using var verificacao = CriarContexto(cenario.Banco);
        var horario = await verificacao.TurmasHorarios.SingleAsync();
        Assert.Null(horario.VigenciaFim);
    }

    private static async Task<Cenario> CriarCenarioAsync()
    {
        var banco = $"ajuste-horarios-{Guid.NewGuid():N}";
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var professor = new Professor(Guid.NewGuid(), organizacaoId, "Professor", Agora);
        var vinculo = new ProfessorUnidade(
            Guid.NewGuid(), organizacaoId, professor.Id, unidadeId, Agora);
        var usuarioId = Guid.NewGuid();
        var turma = new Turma(Guid.NewGuid(), organizacaoId, unidadeId,
            vinculo.Id, "Turma", 12, usuarioId, Agora);
        var horario = new TurmaHorario(Guid.NewGuid(), organizacaoId, unidadeId,
            turma.Id, vinculo.Id, DiaSemana.Segunda, new TimeOnly(19, 0),
            new TimeOnly(20, 0), new DateOnly(2026, 8, 1), null,
            usuarioId, Agora);
        await using var db = CriarContexto(banco);
        db.Professores.Add(professor);
        db.ProfessoresUnidades.Add(vinculo);
        db.Turmas.Add(turma);
        db.TurmasHorarios.Add(horario);
        await db.SaveChangesAsync();
        return new(banco, organizacaoId, unidadeId, turma.Id);
    }

    private static BfaDbContext CriarContexto(
        string banco, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseInMemoryDatabase(banco)
            .ConfigureWarnings(warnings => warnings.Ignore(
                InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(interceptors)
            .Options;
        return new(options);
    }

    private sealed record Cenario(
        string Banco, Guid OrganizacaoId, Guid UnidadeId, Guid TurmaId);

    private class ObservarOrdemInterceptor : SaveChangesInterceptor
    {
        public int Quantidade { get; private set; }
        public bool PrimeiroSaveSomenteEncerrou { get; private set; }
        public bool SegundoSaveSomenteInseriu { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Quantidade++;
            var contexto = Assert.IsType<BfaDbContext>(eventData.Context);
            var entradas = contexto.ChangeTracker.Entries<TurmaHorario>().ToArray();
            if (Quantidade == 1)
                PrimeiroSaveSomenteEncerrou = entradas.Any(item =>
                    item.State == EntityState.Modified)
                    && entradas.All(item => item.State != EntityState.Added);
            else if (Quantidade == 2)
                SegundoSaveSomenteInseriu = entradas.Any(item =>
                    item.State == EntityState.Added);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FalharSegundoSaveInterceptor : ObservarOrdemInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var observado = base.SavingChangesAsync(
                eventData, result, cancellationToken);
            if (Quantidade == 1)
                return ValueTask.FromResult(
                    InterceptionResult<int>.SuppressWithResult(1));
            return ValueTask.FromException<InterceptionResult<int>>(
                new DbUpdateException("Falha simulada no segundo SaveChanges."));
        }
    }
}

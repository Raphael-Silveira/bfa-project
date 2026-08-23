using BFA.Application.Unidades.Professores;
using BFA.Domain.Professores;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Unidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BFA.IntegrationTests;

public sealed class ProfessoresUnidadeRepositorioTests
{
    private static readonly DateTime CriadoEmUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly VigenciaFimAnterior = new(2026, 8, 22);

    [Fact]
    public async Task Reativacao_persiste_vinculo_antes_de_inserir_remuneracao_aberta()
    {
        var cenario = await CriarCenarioAsync();
        var interceptor = new ObservarOrdemSaveChangesInterceptor();
        await using var dbContext = CriarContexto(cenario.Banco, interceptor);
        var repositorio = new ProfessoresUnidadeRepositorio(dbContext);

        var resultado = await repositorio.VincularExistenteAsync(
            cenario.OrganizacaoId,
            cenario.UnidadeId,
            cenario.ProfessorId,
            ModalidadeRemuneracaoProfessor.Mensal,
            2000m,
            VigenciaFimAnterior.AddDays(1),
            null,
            Guid.NewGuid(),
            CriadoEmUtc.AddYears(1),
            CancellationToken.None);

        Assert.Equal(EstadoPersistenciaProfessorUnidade.Sucesso, resultado);
        Assert.Equal(2, interceptor.QuantidadeSaveChanges);
        Assert.True(interceptor.PrimeiroSaveSomenteReativouVinculo);
        Assert.True(interceptor.SegundoSaveInseriuRemuneracao);

        await using var verificacao = CriarContexto(cenario.Banco);
        var vinculo = await verificacao.ProfessoresUnidades.SingleAsync();
        var remuneracoes = await verificacao.ProfessoresRemuneracoes
            .OrderBy(item => item.VigenciaInicio)
            .ToArrayAsync();
        Assert.Equal(cenario.VinculoId, vinculo.Id);
        Assert.True(vinculo.Ativo);
        Assert.Equal(2, remuneracoes.Length);
        Assert.Equal(VigenciaFimAnterior, remuneracoes[0].VigenciaFim);
        Assert.Equal(VigenciaFimAnterior.AddDays(1), remuneracoes[1].VigenciaInicio);
        Assert.Null(remuneracoes[1].VigenciaFim);
    }

    [Fact]
    public async Task Falha_no_segundo_save_retorna_falha_e_nao_deixa_reativacao_parcial()
    {
        var cenario = await CriarCenarioAsync();
        var interceptor = new FalharSegundoSaveChangesInterceptor();
        await using var dbContext = CriarContexto(cenario.Banco, interceptor);
        var repositorio = new ProfessoresUnidadeRepositorio(dbContext);

        var resultado = await repositorio.VincularExistenteAsync(
            cenario.OrganizacaoId,
            cenario.UnidadeId,
            cenario.ProfessorId,
            ModalidadeRemuneracaoProfessor.PorHora,
            75m,
            VigenciaFimAnterior.AddDays(1),
            null,
            Guid.NewGuid(),
            CriadoEmUtc.AddYears(1),
            CancellationToken.None);

        Assert.Equal(EstadoPersistenciaProfessorUnidade.Falha, resultado);
        Assert.Equal(2, interceptor.QuantidadeSaveChanges);
        Assert.True(interceptor.PrimeiroSaveSomenteReativouVinculo);
        Assert.True(interceptor.SegundoSaveInseriuRemuneracao);

        await using var verificacao = CriarContexto(cenario.Banco);
        var vinculo = await verificacao.ProfessoresUnidades.SingleAsync();
        var remuneracao = await verificacao.ProfessoresRemuneracoes.SingleAsync();
        Assert.False(vinculo.Ativo);
        Assert.Equal(cenario.VinculoId, vinculo.Id);
        Assert.Equal(VigenciaFimAnterior, remuneracao.VigenciaFim);
    }

    [Fact]
    public async Task Alteracao_de_remuneracao_fecha_atual_antes_de_inserir_nova()
    {
        var cenario = await CriarCenarioComRemuneracaoAtivaAsync();
        var interceptor = new ObservarAlteracaoRemuneracaoInterceptor();
        await using var dbContext = CriarContexto(cenario.Banco, interceptor);
        var repositorio = new ProfessoresUnidadeRepositorio(dbContext);

        var resultado = await repositorio.AlterarRemuneracaoAsync(
            cenario.OrganizacaoId,
            cenario.UnidadeId,
            cenario.ProfessorId,
            ModalidadeRemuneracaoProfessor.PorAula,
            100m,
            new DateOnly(2026, 9, 1),
            "Nova remuneração",
            Guid.NewGuid(),
            CriadoEmUtc.AddYears(1),
            CancellationToken.None);

        Assert.Equal(EstadoPersistenciaProfessorUnidade.Sucesso, resultado);
        Assert.Equal(2, interceptor.QuantidadeSaveChanges);
        Assert.True(interceptor.PrimeiroSaveSomenteEncerrouRemuneracao);
        Assert.True(interceptor.SegundoSaveInseriuNovaRemuneracao);
    }

    [Fact]
    public async Task Falha_ao_inserir_nova_remuneracao_nao_deixa_historico_encerrado_sem_substituta()
    {
        var cenario = await CriarCenarioComRemuneracaoAtivaAsync();
        var interceptor = new FalharInsercaoNovaRemuneracaoInterceptor();
        await using var dbContext = CriarContexto(cenario.Banco, interceptor);
        var repositorio = new ProfessoresUnidadeRepositorio(dbContext);

        var resultado = await repositorio.AlterarRemuneracaoAsync(
            cenario.OrganizacaoId,
            cenario.UnidadeId,
            cenario.ProfessorId,
            ModalidadeRemuneracaoProfessor.PorHora,
            80m,
            new DateOnly(2026, 9, 1),
            null,
            Guid.NewGuid(),
            CriadoEmUtc.AddYears(1),
            CancellationToken.None);

        Assert.Equal(EstadoPersistenciaProfessorUnidade.Falha, resultado);
        Assert.Equal(2, interceptor.QuantidadeSaveChanges);
        Assert.True(interceptor.PrimeiroSaveSomenteEncerrouRemuneracao);
        Assert.True(interceptor.SegundoSaveInseriuNovaRemuneracao);

        await using var verificacao = CriarContexto(cenario.Banco);
        var vinculo = await verificacao.ProfessoresUnidades.SingleAsync();
        var remuneracao = await verificacao.ProfessoresRemuneracoes.SingleAsync();
        Assert.True(vinculo.Ativo);
        Assert.Null(remuneracao.VigenciaFim);
        Assert.Equal(1000m, remuneracao.Valor);
    }

    private static async Task<Cenario> CriarCenarioAsync()
    {
        var banco = $"professores-repositorio-{Guid.NewGuid():N}";
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var professor = new Professor(
            Guid.NewGuid(), organizacaoId, "Professor existente", CriadoEmUtc);
        var vinculo = new ProfessorUnidade(
            Guid.NewGuid(), organizacaoId, professor.Id, unidadeId, CriadoEmUtc);
        var remuneracao = new ProfessorRemuneracao(
            Guid.NewGuid(), organizacaoId, vinculo.Id,
            ModalidadeRemuneracaoProfessor.Mensal, 1000m,
            new DateOnly(2026, 1, 1), null, Guid.NewGuid(), CriadoEmUtc);
        remuneracao.Encerrar(VigenciaFimAnterior);
        vinculo.Desativar(CriadoEmUtc.AddDays(1));

        await using var dbContext = CriarContexto(banco);
        dbContext.Professores.Add(professor);
        dbContext.ProfessoresUnidades.Add(vinculo);
        dbContext.ProfessoresRemuneracoes.Add(remuneracao);
        await dbContext.SaveChangesAsync();
        return new(banco, organizacaoId, unidadeId, professor.Id, vinculo.Id);
    }

    private static async Task<Cenario> CriarCenarioComRemuneracaoAtivaAsync()
    {
        var banco = $"professores-remuneracao-repositorio-{Guid.NewGuid():N}";
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var professor = new Professor(
            Guid.NewGuid(), organizacaoId, "Professor existente", CriadoEmUtc);
        var vinculo = new ProfessorUnidade(
            Guid.NewGuid(), organizacaoId, professor.Id, unidadeId, CriadoEmUtc);
        var remuneracao = new ProfessorRemuneracao(
            Guid.NewGuid(), organizacaoId, vinculo.Id,
            ModalidadeRemuneracaoProfessor.Mensal, 1000m,
            new DateOnly(2026, 1, 1), null, Guid.NewGuid(), CriadoEmUtc);

        await using var dbContext = CriarContexto(banco);
        dbContext.Professores.Add(professor);
        dbContext.ProfessoresUnidades.Add(vinculo);
        dbContext.ProfessoresRemuneracoes.Add(remuneracao);
        await dbContext.SaveChangesAsync();
        return new(banco, organizacaoId, unidadeId, professor.Id, vinculo.Id);
    }

    private static BfaDbContext CriarContexto(
        string banco,
        params IInterceptor[] interceptors)
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
        string Banco,
        Guid OrganizacaoId,
        Guid UnidadeId,
        Guid ProfessorId,
        Guid VinculoId);

    private class ObservarOrdemSaveChangesInterceptor : SaveChangesInterceptor
    {
        public int QuantidadeSaveChanges { get; private set; }
        public bool PrimeiroSaveSomenteReativouVinculo { get; private set; }
        public bool SegundoSaveInseriuRemuneracao { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            QuantidadeSaveChanges++;
            var context = Assert.IsType<BfaDbContext>(eventData.Context);
            if (QuantidadeSaveChanges == 1)
            {
                PrimeiroSaveSomenteReativouVinculo =
                    context.ChangeTracker.Entries<ProfessorUnidade>()
                        .Single().State == EntityState.Modified
                    && !context.ChangeTracker.Entries<ProfessorRemuneracao>()
                        .Any(item => item.State == EntityState.Added);
            }
            else if (QuantidadeSaveChanges == 2)
            {
                SegundoSaveInseriuRemuneracao = context.ChangeTracker
                    .Entries<ProfessorRemuneracao>()
                    .Single(item => item.State == EntityState.Added) is not null;
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FalharSegundoSaveChangesInterceptor
        : ObservarOrdemSaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var observacao = base.SavingChangesAsync(
                eventData, result, cancellationToken);
            if (QuantidadeSaveChanges == 1)
            {
                return ValueTask.FromResult(
                    InterceptionResult<int>.SuppressWithResult(1));
            }

            return ValueTask.FromException<InterceptionResult<int>>(
                new DbUpdateException("Falha simulada no segundo SaveChanges."));
        }
    }

    private class ObservarAlteracaoRemuneracaoInterceptor : SaveChangesInterceptor
    {
        public int QuantidadeSaveChanges { get; private set; }
        public bool PrimeiroSaveSomenteEncerrouRemuneracao { get; private set; }
        public bool SegundoSaveInseriuNovaRemuneracao { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            QuantidadeSaveChanges++;
            var context = Assert.IsType<BfaDbContext>(eventData.Context);
            if (QuantidadeSaveChanges == 1)
            {
                PrimeiroSaveSomenteEncerrouRemuneracao =
                    context.ChangeTracker.Entries<ProfessorRemuneracao>()
                        .Single().State == EntityState.Modified
                    && !context.ChangeTracker.Entries<ProfessorRemuneracao>()
                        .Any(item => item.State == EntityState.Added);
            }
            else if (QuantidadeSaveChanges == 2)
            {
                _ = context.ChangeTracker.Entries<ProfessorRemuneracao>()
                    .Single(item => item.State == EntityState.Added);
                SegundoSaveInseriuNovaRemuneracao = true;
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FalharInsercaoNovaRemuneracaoInterceptor
        : ObservarAlteracaoRemuneracaoInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _ = base.SavingChangesAsync(eventData, result, cancellationToken);
            if (QuantidadeSaveChanges == 1)
            {
                return ValueTask.FromResult(
                    InterceptionResult<int>.SuppressWithResult(1));
            }

            return ValueTask.FromException<InterceptionResult<int>>(
                new DbUpdateException("Falha simulada ao inserir a nova remuneração."));
        }
    }
}

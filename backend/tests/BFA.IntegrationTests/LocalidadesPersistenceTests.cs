using BFA.Application.Localidades;
using BFA.Infrastructure.Localidades;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.IntegrationTests;

public sealed class LocalidadesPersistenceTests
{
    private static readonly DateTime PrimeiroInstante = new(
        2026,
        8,
        18,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public async Task Reexecucao_faz_upsert_por_codigo_sem_duplicar_e_inativa_ausentes()
    {
        await using var context = CreateContext();
        var repositorio = new LocalidadesSincronizacaoRepositorio(context);
        var catalogoInicial = CatalogoLocalidadesDados.Criar(
            [
                new(35, "SP", "São Paulo"),
                new(33, "RJ", "Rio de Janeiro"),
            ],
            [
                new(3550308, 35, "São Paulo"),
                new(3509502, 35, "Campinas"),
                new(3304557, 33, "Rio de Janeiro"),
            ]);

        await repositorio.SincronizarAsync(
            catalogoInicial,
            PrimeiroInstante,
            CancellationToken.None);
        var catalogoAtualizado = CatalogoLocalidadesDados.Criar(
            [new(35, "SP", "Estado de São Paulo")],
            [
                new(3550308, 35, "São Paulo Capital"),
                new(3543402, 35, "Ribeirão Preto"),
            ]);
        await repositorio.SincronizarAsync(
            catalogoAtualizado,
            PrimeiroInstante.AddHours(1),
            CancellationToken.None);

        var estados = await context.Estados.OrderBy(item => item.CodigoIbge).ToListAsync();
        var municipios = await context.Municipios.OrderBy(item => item.CodigoIbge).ToListAsync();
        Assert.Equal(2, estados.Count);
        Assert.Equal(4, municipios.Count);
        Assert.Equal("Estado de São Paulo", estados.Single(item => item.CodigoIbge == 35).Nome);
        Assert.True(estados.Single(item => item.CodigoIbge == 35).Ativo);
        Assert.False(estados.Single(item => item.CodigoIbge == 33).Ativo);
        Assert.Equal(
            "São Paulo Capital",
            municipios.Single(item => item.CodigoIbge == 3550308).Nome);
        Assert.True(municipios.Single(item => item.CodigoIbge == 3543402).Ativo);
        Assert.False(municipios.Single(item => item.CodigoIbge == 3509502).Ativo);
        Assert.False(municipios.Single(item => item.CodigoIbge == 3304557).Ativo);
        Assert.All(estados, item => Assert.True(item.CodigoIbge > 0));
        Assert.All(municipios, item => Assert.True(item.CodigoIbge > 0));
    }

    [Fact]
    public async Task Item_inativo_presente_no_lote_e_reativado_no_mesmo_registro()
    {
        await using var context = CreateContext();
        var repositorio = new LocalidadesSincronizacaoRepositorio(context);
        var completo = CatalogoLocalidadesDados.Criar(
            [new(35, "SP", "São Paulo")],
            [new(3550308, 35, "São Paulo")]);
        await repositorio.SincronizarAsync(completo, PrimeiroInstante, CancellationToken.None);
        context.Municipios.Single().Desativar(PrimeiroInstante.AddMinutes(1));
        await context.SaveChangesAsync();

        await repositorio.SincronizarAsync(
            completo,
            PrimeiroInstante.AddHours(1),
            CancellationToken.None);

        var municipio = Assert.Single(await context.Municipios.ToListAsync());
        Assert.Equal(3550308, municipio.CodigoIbge);
        Assert.True(municipio.Ativo);
    }

    [Fact]
    public async Task Falha_http_antes_da_persistencia_preserva_catalogo_anterior_integro()
    {
        await using var context = CreateContext();
        var repositorio = new LocalidadesSincronizacaoRepositorio(context);
        await repositorio.SincronizarAsync(
            CatalogoLocalidadesDados.Criar(
                [new(35, "SP", "São Paulo")],
                [new(3550308, 35, "São Paulo")]),
            PrimeiroInstante,
            CancellationToken.None);
        var servico = new LocalidadesSincronizacaoServico(
            new IbgeClientComFalhaParcial(),
            repositorio,
            new FixedTimeProvider(new DateTimeOffset(PrimeiroInstante.AddHours(1))));

        await Assert.ThrowsAsync<IbgeLocalidadesException>(() =>
            servico.SincronizarAsync(CancellationToken.None));

        var estado = Assert.Single(await context.Estados.ToListAsync());
        var municipio = Assert.Single(await context.Municipios.ToListAsync());
        Assert.Equal("São Paulo", estado.Nome);
        Assert.True(estado.Ativo);
        Assert.Equal("São Paulo", municipio.Nome);
        Assert.True(municipio.Ativo);
        Assert.Equal(PrimeiroInstante, estado.AtualizadoEmUtc);
        Assert.Equal(PrimeiroInstante, municipio.AtualizadoEmUtc);
    }

    [Fact]
    public async Task Consulta_usa_somente_base_local_filtra_ativos_estado_e_ordena_por_nome()
    {
        await using var context = CreateContext();
        var repositorio = new LocalidadesSincronizacaoRepositorio(context);
        await repositorio.SincronizarAsync(
            CatalogoLocalidadesDados.Criar(
                [
                    new(35, "SP", "São Paulo"),
                    new(33, "RJ", "Rio de Janeiro"),
                    new(31, "MG", "Minas Gerais"),
                ],
                [
                    new(3509502, 35, "Campinas"),
                    new(3506003, 35, "Bauru"),
                    new(3550308, 35, "São Paulo"),
                    new(3304557, 33, "Rio de Janeiro"),
                    new(3106200, 31, "Belo Horizonte"),
                ]),
            PrimeiroInstante,
            CancellationToken.None);
        context.Municipios.Single(item => item.CodigoIbge == 3509502)
            .Desativar(PrimeiroInstante.AddMinutes(1));
        context.Estados.Single(item => item.CodigoIbge == 33)
            .Desativar(PrimeiroInstante.AddMinutes(1));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var consulta = new LocalidadesConsulta(context);

        var estados = await consulta.ListarEstadosAtivosAsync(CancellationToken.None);
        var municipiosSp = await consulta.ListarMunicipiosAtivosAsync(35, CancellationToken.None);
        var municipiosEstadoInativo = await consulta.ListarMunicipiosAtivosAsync(
            33,
            CancellationToken.None);
        var municipiosEstadoInexistente = await consulta.ListarMunicipiosAtivosAsync(
            99,
            CancellationToken.None);

        Assert.Equal(["Minas Gerais", "São Paulo"], estados.Select(item => item.Nome));
        Assert.Equal(["Bauru", "São Paulo"], municipiosSp.Select(item => item.Nome));
        Assert.DoesNotContain(municipiosSp, item => item.CodigoIbge == 3304557);
        Assert.Empty(municipiosEstadoInativo);
        Assert.Empty(municipiosEstadoInexistente);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Consulta_interrompe_execucao_quando_token_esta_cancelado()
    {
        await using var context = CreateContext();
        var consulta = new LocalidadesConsulta(context);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            consulta.ListarMunicipiosAtivosAsync(
                35,
                cancellationTokenSource.Token));
    }

    private static BfaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseInMemoryDatabase($"localidades-{Guid.NewGuid():N}")
            .Options;
        return new BfaDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class IbgeClientComFalhaParcial : IIbgeLocalidadesClient
    {
        public Task<IReadOnlyList<EstadoIbgeDados>> ListarEstadosAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<EstadoIbgeDados> estados =
            [
                new(33, "RJ", "Rio de Janeiro atualizado"),
                new(35, "SP", "São Paulo atualizado"),
            ];
            return Task.FromResult(estados);
        }

        public Task<IReadOnlyList<MunicipioIbgeDados>> ListarMunicipiosAsync(
            string siglaEstado,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (siglaEstado == "SP")
            {
                throw new IbgeLocalidadesException("Falha HTTP simulada.");
            }

            IReadOnlyList<MunicipioIbgeDados> municipios =
                [new(3304557, "Rio de Janeiro atualizado")];
            return Task.FromResult(municipios);
        }
    }
}

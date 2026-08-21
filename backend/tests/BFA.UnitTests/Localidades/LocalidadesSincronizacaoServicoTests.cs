using BFA.Application.Localidades;

namespace BFA.UnitTests.Localidades;

public sealed class LocalidadesSincronizacaoServicoTests
{
    private static readonly DateTimeOffset Agora = new(
        2026,
        8,
        18,
        12,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public async Task Busca_catalogo_completo_antes_de_persistir_uma_unica_vez()
    {
        var client = new TestIbgeClient(
            [new(35, "SP", "São Paulo"), new(33, "RJ", "Rio de Janeiro")],
            new Dictionary<string, IReadOnlyList<MunicipioIbgeDados>>
            {
                ["SP"] = [new(3550308, "São Paulo")],
                ["RJ"] = [new(3304557, "Rio de Janeiro")],
            });
        var repositorio = new TestRepositorio();
        var servico = CreateService(client, repositorio);

        var resultado = await servico.SincronizarAsync(CancellationToken.None);

        Assert.Equal(2, resultado.EstadosProcessados);
        Assert.Equal(2, resultado.MunicipiosProcessados);
        Assert.Equal(1, repositorio.Execucoes);
        Assert.NotNull(repositorio.Catalogo);
        Assert.Equal(Agora.UtcDateTime, repositorio.AtualizadoEmUtc);
        Assert.Equal(["estados", "municipios:RJ", "municipios:SP"], client.Chamadas);
    }

    [Fact]
    public async Task Falha_parcial_do_ibge_nao_inicia_persistencia()
    {
        var client = new TestIbgeClient(
            [new(33, "RJ", "Rio de Janeiro"), new(35, "SP", "São Paulo")],
            new Dictionary<string, IReadOnlyList<MunicipioIbgeDados>>
            {
                ["RJ"] = [new(3304557, "Rio de Janeiro")],
            },
            falharEm: "SP");
        var repositorio = new TestRepositorio();

        await Assert.ThrowsAsync<IbgeLocalidadesException>(() =>
            CreateService(client, repositorio).SincronizarAsync(CancellationToken.None));

        Assert.Equal(0, repositorio.Execucoes);
    }

    [Fact]
    public async Task Estado_sem_municipios_impede_persistencia()
    {
        var client = new TestIbgeClient(
            [new(35, "SP", "São Paulo")],
            new Dictionary<string, IReadOnlyList<MunicipioIbgeDados>>
            {
                ["SP"] = [],
            });
        var repositorio = new TestRepositorio();

        await Assert.ThrowsAsync<LocalidadesSincronizacaoException>(() =>
            CreateService(client, repositorio).SincronizarAsync(CancellationToken.None));

        Assert.Equal(0, repositorio.Execucoes);
    }

    [Fact]
    public async Task Codigos_de_municipio_duplicados_impedem_persistencia()
    {
        var client = new TestIbgeClient(
            [new(35, "SP", "São Paulo")],
            new Dictionary<string, IReadOnlyList<MunicipioIbgeDados>>
            {
                ["SP"] = [new(1, "São Paulo"), new(1, "Campinas")],
            });
        var repositorio = new TestRepositorio();

        await Assert.ThrowsAsync<LocalidadesSincronizacaoException>(() =>
            CreateService(client, repositorio).SincronizarAsync(CancellationToken.None));

        Assert.Equal(0, repositorio.Execucoes);
    }

    [Fact]
    public void Catalogo_rejeita_estado_ausente_e_duplicidades()
    {
        Assert.Throws<LocalidadesSincronizacaoException>(() =>
            CatalogoLocalidadesDados.Criar(
                [new(35, "SP", "São Paulo")],
                [new(1, 33, "Rio de Janeiro")]));
        Assert.Throws<LocalidadesSincronizacaoException>(() =>
            CatalogoLocalidadesDados.Criar(
                [new(35, "SP", "São Paulo"), new(35, "RJ", "Rio")],
                [new(1, 35, "São Paulo")]));
        Assert.Throws<LocalidadesSincronizacaoException>(() =>
            CatalogoLocalidadesDados.Criar(
                [new(35, "sp", "São Paulo"), new(33, "SP", "Rio")],
                [new(1, 35, "São Paulo")]));
    }

    [Fact]
    public void Catalogo_preserva_acentos_sem_quantidades_hardcoded()
    {
        var catalogo = CatalogoLocalidadesDados.Criar(
            [new(31, "MG", "Minas Gerais")],
            [new(3100104, 31, "Abadia dos Dourados"), new(3100203, 31, "Abaeté")]);

        Assert.Equal("Abaeté", catalogo.Municipios[1].Nome);
        Assert.Single(catalogo.Estados);
        Assert.Equal(2, catalogo.Municipios.Count);
    }

    private static LocalidadesSincronizacaoServico CreateService(
        IIbgeLocalidadesClient client,
        ILocalidadesSincronizacaoRepositorio repositorio)
    {
        return new LocalidadesSincronizacaoServico(
            client,
            repositorio,
            new FixedTimeProvider(Agora));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestRepositorio : ILocalidadesSincronizacaoRepositorio
    {
        public int Execucoes { get; private set; }

        public CatalogoLocalidadesDados? Catalogo { get; private set; }

        public DateTime AtualizadoEmUtc { get; private set; }

        public Task SincronizarAsync(
            CatalogoLocalidadesDados catalogo,
            DateTime atualizadoEmUtc,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Execucoes++;
            Catalogo = catalogo;
            AtualizadoEmUtc = atualizadoEmUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class TestIbgeClient(
        IReadOnlyList<EstadoIbgeDados> estados,
        IReadOnlyDictionary<string, IReadOnlyList<MunicipioIbgeDados>> municipios,
        string? falharEm = null) : IIbgeLocalidadesClient
    {
        public List<string> Chamadas { get; } = [];

        public Task<IReadOnlyList<EstadoIbgeDados>> ListarEstadosAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Chamadas.Add("estados");
            return Task.FromResult(estados);
        }

        public Task<IReadOnlyList<MunicipioIbgeDados>> ListarMunicipiosAsync(
            string siglaEstado,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Chamadas.Add($"municipios:{siglaEstado}");

            if (string.Equals(falharEm, siglaEstado, StringComparison.Ordinal))
            {
                throw new IbgeLocalidadesException("Falha controlada.");
            }

            return Task.FromResult(municipios[siglaEstado]);
        }
    }
}

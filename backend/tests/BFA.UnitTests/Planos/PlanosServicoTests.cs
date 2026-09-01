using BFA.Application.Acessos;
using BFA.Application.Planos;
using BFA.Application.Unidades;
using BFA.Domain.Acessos;
using BFA.Domain.Planos;

namespace BFA.UnitTests.Planos;

public sealed class PlanosServicoTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly Guid OrganizacaoId = Guid.NewGuid();
    private static readonly Guid UnidadeId = Guid.NewGuid();
    private static readonly DateTime AgoraUtc = new(
        2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AdministradorRede_cria_plano_da_rede_e_versao_um_atomicamente()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(repositorio: repositorio);

        var resultado = await servico.CriarRedeAsync(
            UsuarioId,
            new("Plano 9 meses", Termos(9, 7, 321.45m, true, 99.90m)),
            CancellationToken.None);

        Assert.Equal(EstadoPlanos.Sucesso, resultado.Estado);
        Assert.NotNull(repositorio.PlanoCriado);
        Assert.Null(repositorio.PlanoCriado.UnidadeId);
        Assert.Equal(OrganizacaoId, repositorio.PlanoCriado.OrganizacaoId);
        Assert.Equal("Plano 9 meses", repositorio.PlanoCriado.Nome);
        Assert.NotNull(repositorio.VersaoCriada);
        Assert.Equal(1, repositorio.VersaoCriada.NumeroVersao);
        Assert.Equal(9, repositorio.VersaoCriada.DuracaoMeses);
        Assert.Equal(7, repositorio.VersaoCriada.FrequenciaSemanal);
        Assert.Equal(321.45m, repositorio.VersaoCriada.ValorMensal);
        Assert.Equal(99.90m, repositorio.VersaoCriada.ValorMatricula);
    }

    [Fact]
    public async Task Plano_sem_taxa_persiste_valor_nulo()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(repositorio: repositorio);

        var resultado = await servico.CriarRedeAsync(
            UsuarioId,
            new("Plano sem taxa", Termos(2, 1, 180m, false, null)),
            CancellationToken.None);

        Assert.Equal(EstadoPlanos.Sucesso, resultado.Estado);
        Assert.False(repositorio.VersaoCriada!.CobraMatricula);
        Assert.Null(repositorio.VersaoCriada.ValorMatricula);
    }

    [Fact]
    public async Task Usuario_de_outro_tenant_nao_cria_plano_da_rede()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(
            acesso: new AcessoFake { OrganizacoesRede = [] }, repositorio: repositorio);

        var resultado = await servico.CriarRedeAsync(
            UsuarioId, new("Plano", Termos()), CancellationToken.None);

        Assert.Equal(EstadoPlanos.SemAcesso, resultado.Estado);
        Assert.Null(repositorio.PlanoCriado);
    }

    [Fact]
    public async Task AdministradorRede_gerencia_plano_local_antes_da_franquia()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(
            governanca: new GovernancaFake(new(true, false, false)),
            repositorio: repositorio);

        var resultado = await servico.CriarLocalAsync(
            UsuarioId, UnidadeId, new("Plano local", Termos()),
            CancellationToken.None);

        Assert.Equal(EstadoPlanos.Sucesso, resultado.Estado);
        Assert.Equal(UnidadeId, repositorio.PlanoCriado!.UnidadeId);
    }

    [Fact]
    public async Task AdministradorRede_somente_visualiza_plano_local_apos_franquia()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(
            governanca: new GovernancaFake(new(true, false, true)),
            repositorio: repositorio);

        var lista = await servico.ListarLocalAsync(
            UsuarioId, UnidadeId, FiltroPlanos.Todos,
            CancellationToken.None);
        var criacao = await servico.CriarLocalAsync(
            UsuarioId, UnidadeId, new("Bloqueado", Termos()),
            CancellationToken.None);

        Assert.Equal(EstadoPlanos.Sucesso, lista.Estado);
        Assert.False(lista.Valor!.Contexto.PodeGerenciar);
        Assert.True(lista.Valor.Contexto.PossuiFranqueadoAtivo);
        Assert.Equal(EstadoPlanos.SemAcesso, criacao.Estado);
        Assert.Null(repositorio.PlanoCriado);
    }

    [Fact]
    public async Task AdministradorUnidade_gerencia_plano_local_de_unidade_franqueada()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(
            governanca: new GovernancaFake(new(false, true, true)),
            repositorio: repositorio);

        var resultado = await servico.CriarLocalAsync(
            UsuarioId, UnidadeId, new("Plano local", Termos()),
            CancellationToken.None);

        Assert.Equal(EstadoPlanos.Sucesso, resultado.Estado);
        Assert.Equal(UnidadeId, repositorio.PlanoCriado!.UnidadeId);
    }

    [Fact]
    public async Task AdministradorRede_pos_franquia_nao_versiona_nem_altera_estado_local()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(
            governanca: new GovernancaFake(new(true, false, true)),
            repositorio: repositorio);

        var versao = await servico.CriarNovaVersaoLocalAsync(
            UsuarioId, UnidadeId, Guid.NewGuid(), Termos(), CancellationToken.None);
        var estado = await servico.AlterarEstadoLocalAsync(
            UsuarioId, UnidadeId, Guid.NewGuid(), false, CancellationToken.None);

        Assert.Equal(EstadoPlanos.SemAcesso, versao.Estado);
        Assert.Equal(EstadoPlanos.SemAcesso, estado.Estado);
        Assert.Null(repositorio.TermosNovaVersao);
        Assert.Null(repositorio.PlanoEstadoAlteradoId);
    }

    [Fact]
    public async Task AdministradorUnidade_de_outra_unidade_nao_acessa_plano_local()
    {
        var servico = CriarServico();

        var resultado = await servico.ListarLocalAsync(
            UsuarioId, Guid.NewGuid(), FiltroPlanos.Todos, CancellationToken.None);

        Assert.Equal(EstadoPlanos.ContextoNaoEncontrado, resultado.Estado);
    }

    [Fact]
    public async Task Professor_nao_lista_nem_administra_planos_locais()
    {
        var servico = CriarServico(
            governanca: new GovernancaFake(new(false, false, false)));

        var lista = await servico.ListarLocalAsync(
            UsuarioId, UnidadeId, FiltroPlanos.Todos,
            CancellationToken.None);

        Assert.Equal(EstadoPlanos.SemAcesso, lista.Estado);
    }

    [Fact]
    public async Task Nova_versao_delega_fluxo_transacional_com_termos_validos()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(repositorio: repositorio);
        var planoId = Guid.NewGuid();
        var termos = Termos(18, 6, 450m, true, 120m);

        var resultado = await servico.CriarNovaVersaoRedeAsync(
            UsuarioId, planoId, termos, CancellationToken.None);

        Assert.Equal(EstadoPlanos.Sucesso, resultado.Estado);
        Assert.Equal(planoId, repositorio.PlanoAlteradoId);
        Assert.Equal(termos, repositorio.TermosNovaVersao);
    }

    [Theory]
    [InlineData(0, 1, 100)]
    [InlineData(1, 0, 100)]
    [InlineData(1, 8, 100)]
    [InlineData(1, 1, 0)]
    public async Task Nova_versao_rejeita_termos_invalidos_antes_da_persistencia(
        int duracao, int frequencia, int valor)
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(repositorio: repositorio);

        var resultado = await servico.CriarNovaVersaoRedeAsync(
            UsuarioId, Guid.NewGuid(),
            Termos(duracao, frequencia, valor, false, null),
            CancellationToken.None);

        Assert.Equal(EstadoPlanos.DadosInvalidos, resultado.Estado);
        Assert.Null(repositorio.TermosNovaVersao);
    }

    [Fact]
    public async Task Concorrencia_de_nova_versao_retorna_estado_amigavel()
    {
        var repositorio = new RepositorioFake
        {
            EstadoNovaVersao = EstadoPersistenciaPlano.ConflitoConcorrencia
        };
        var servico = CriarServico(repositorio: repositorio);

        var resultado = await servico.CriarNovaVersaoRedeAsync(
            UsuarioId, Guid.NewGuid(), Termos(), CancellationToken.None);

        Assert.Equal(EstadoPlanos.ConflitoConcorrencia, resultado.Estado);
    }

    [Fact]
    public async Task Reativacao_sem_versao_aberta_e_bloqueada()
    {
        var repositorio = new RepositorioFake
        {
            EstadoAlteracao = EstadoPersistenciaPlano.SemVersaoAberta
        };
        var servico = CriarServico(repositorio: repositorio);

        var resultado = await servico.AlterarEstadoRedeAsync(
            UsuarioId, Guid.NewGuid(), true, CancellationToken.None);

        Assert.Equal(EstadoPlanos.SemVersaoAberta, resultado.Estado);
    }

    [Fact]
    public void Governanca_de_plano_local_e_explicita_e_independente_de_turmas()
    {
        Assert.True(new GovernancaOperacionalUnidade(false, true, true)
            .PodeGerenciarPlanoLocal);
        Assert.True(new GovernancaOperacionalUnidade(true, false, false)
            .PodeGerenciarPlanoLocal);
        Assert.False(new GovernancaOperacionalUnidade(true, false, true)
            .PodeGerenciarPlanoLocal);
    }

    private static PlanosServico CriarServico(
        AcessoFake? acesso = null,
        GovernancaFake? governanca = null,
        RepositorioFake? repositorio = null) => new(
            acesso ?? new AcessoFake { OrganizacoesRede = [OrganizacaoId] },
            new UnidadeContextoFake(),
            governanca ?? new GovernancaFake(new(false, true, false)),
            repositorio ?? new RepositorioFake(),
            new TimeProviderFake(AgoraUtc));

    private static PlanoTermosSolicitacao Termos(
        int duracao = 12, int frequencia = 3, decimal valor = 280m,
        bool cobra = true, decimal? taxa = 100m) =>
        new(duracao, frequencia, valor, cobra, taxa, new DateOnly(2026, 9, 1));

    private sealed class RepositorioFake : IPlanosRepositorio
    {
        public Plano? PlanoCriado { get; private set; }
        public PlanoVersao? VersaoCriada { get; private set; }
        public Guid? PlanoAlteradoId { get; private set; }
        public PlanoTermosSolicitacao? TermosNovaVersao { get; private set; }
        public Guid? PlanoEstadoAlteradoId { get; private set; }
        public EstadoPersistenciaPlano EstadoNovaVersao { get; init; } =
            EstadoPersistenciaPlano.Sucesso;
        public EstadoPersistenciaPlano EstadoAlteracao { get; init; } =
            EstadoPersistenciaPlano.Sucesso;

        public Task<IReadOnlyList<PlanoResumo>> ListarAsync(
            Guid organizacaoId, Guid? unidadeId, FiltroPlanos filtro,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PlanoResumo>>([]);

        public Task<PlanoDetalheResumo?> ObterAsync(
            Guid organizacaoId, Guid? unidadeId, Guid planoId,
            CancellationToken cancellationToken) => Task.FromResult<PlanoDetalheResumo?>(null);

        public Task<EstadoPersistenciaPlano> CriarAsync(
            Plano plano, PlanoVersao versao, CancellationToken cancellationToken)
        {
            PlanoCriado = plano;
            VersaoCriada = versao;
            return Task.FromResult(EstadoPersistenciaPlano.Sucesso);
        }

        public Task<EstadoPersistenciaPlano> CriarNovaVersaoAsync(
            Guid organizacaoId, Guid? unidadeId, Guid planoId,
            PlanoTermosSolicitacao termos, Guid usuarioId, DateTime agoraUtc,
            CancellationToken cancellationToken)
        {
            PlanoAlteradoId = planoId;
            TermosNovaVersao = termos;
            return Task.FromResult(EstadoNovaVersao);
        }

        public Task<EstadoPersistenciaPlano> AlterarEstadoAsync(
            Guid organizacaoId, Guid? unidadeId, Guid planoId, bool ativar,
            Guid usuarioId, DateTime agoraUtc, CancellationToken cancellationToken)
        {
            PlanoEstadoAlteradoId = planoId;
            return Task.FromResult(EstadoAlteracao);
        }
    }

    private sealed class UnidadeContextoFake : IUnidadeContextoConsulta
    {
        public Task<UnidadeContextoResumo?> ObterAtivaAsync(
            Guid unidadeId, CancellationToken cancellationToken) =>
            Task.FromResult<UnidadeContextoResumo?>(
                unidadeId == UnidadeId
                    ? new(OrganizacaoId, UnidadeId, "BFA Cerquilho")
                    : null);
    }

    private sealed class GovernancaFake(GovernancaOperacionalUnidade valor)
        : IGovernancaOperacionalUnidade
    {
        public Task<GovernancaOperacionalUnidade> ObterAsync(
            Guid usuarioId, Guid organizacaoId, Guid unidadeId,
            CancellationToken cancellationToken) => Task.FromResult(valor);
    }

    private sealed class AcessoFake : IAcessoUsuarioConsulta
    {
        public IReadOnlyList<Guid> OrganizacoesRede { get; init; } = [];
        public Task<IReadOnlyList<Guid>> ListarOrganizacoesAdministradorRedeAsync(
            Guid usuarioId, CancellationToken cancellationToken) =>
            Task.FromResult(OrganizacoesRede);
        public Task<bool> EhAdministradorRedeAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(OrganizacoesRede.Count > 0);
        public Task<bool> EhAdministradorRedeNaOrganizacaoAsync(
            Guid usuarioId, Guid organizacaoId, CancellationToken ct) =>
            Task.FromResult(OrganizacoesRede.Contains(organizacaoId));
        public Task<bool> PossuiAlgumPerfilAsync(
            Guid usuarioId, IReadOnlyCollection<PerfilAcesso> perfis, CancellationToken ct) =>
            Task.FromResult(false);
        public Task<bool> PossuiPerfilNaOrganizacaoAsync(
            Guid usuarioId, Guid organizacaoId, PerfilAcesso perfil, CancellationToken ct) =>
            Task.FromResult(false);
        public Task<bool> PossuiAcessoUnidadeAsync(
            Guid usuarioId, Guid organizacaoId, Guid unidadeId, CancellationToken ct) =>
            Task.FromResult(false);
        public Task<bool> PossuiPerfilNaUnidadeAsync(
            Guid usuarioId, Guid organizacaoId, Guid unidadeId,
            PerfilAcesso perfil, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> PossuiAlgumPerfilNaUnidadeAsync(
            Guid usuarioId, Guid organizacaoId, Guid unidadeId,
            IReadOnlyCollection<PerfilAcesso> perfis, CancellationToken ct) =>
            Task.FromResult(false);
    }

    private sealed class TimeProviderFake(DateTime agoraUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(agoraUtc);
    }
}

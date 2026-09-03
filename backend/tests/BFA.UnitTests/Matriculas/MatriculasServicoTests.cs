using BFA.Application.Matriculas;
using BFA.Application.Unidades;
using BFA.Domain.Matriculas;

namespace BFA.UnitTests.Matriculas;

public sealed class MatriculasServicoTests
{
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly Guid _organizacaoId = Guid.NewGuid();
    private readonly Guid _unidadeId = Guid.NewGuid();

    [Fact]
    public async Task Administrador_unidade_pode_gerenciar_matriculas()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(
            new(false, true, true), repositorio);

        var resultado = await servico.CriarAsync(
            _usuarioId, _unidadeId, SolicitacaoValida(), CancellationToken.None);

        Assert.Equal(EstadoMatriculas.Sucesso, resultado.Estado);
        Assert.True(repositorio.CriarChamado);
        Assert.False(repositorio.PermitiuReusoOrganizacional);
    }

    [Fact]
    public async Task Administrador_rede_em_unidade_franqueada_tem_somente_leitura()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(
            new(true, false, true), repositorio);

        var lista = await servico.ListarAsync(
            _usuarioId, _unidadeId, null, null, CancellationToken.None);
        var criacao = await servico.CriarAsync(
            _usuarioId, _unidadeId, SolicitacaoValida(), CancellationToken.None);

        Assert.Equal(EstadoMatriculas.Sucesso, lista.Estado);
        Assert.False(lista.Contexto!.PodeGerenciar);
        Assert.Equal(EstadoMatriculas.SemAcesso, criacao.Estado);
        Assert.False(repositorio.CriarChamado);
    }

    [Fact]
    public async Task Administrador_rede_sem_franqueado_pode_reuso_organizacional_explicito()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(
            new(true, false, false), repositorio);

        var resultado = await servico.CriarAsync(
            _usuarioId, _unidadeId, SolicitacaoValida(), CancellationToken.None);

        Assert.Equal(EstadoMatriculas.Sucesso, resultado.Estado);
        Assert.True(repositorio.PermitiuReusoOrganizacional);
    }

    [Fact]
    public async Task Professor_sem_perfil_administrativo_nao_lista_nem_altera()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(new(false, false, false), repositorio);

        var lista = await servico.ListarAsync(
            _usuarioId, _unidadeId, null, null, CancellationToken.None);
        var alterar = await servico.AlterarGradeAsync(
            _usuarioId, _unidadeId, Guid.NewGuid(),
            new(new DateOnly(2026, 10, 1), [Guid.NewGuid()]),
            CancellationToken.None);

        Assert.Equal(EstadoMatriculas.SemAcesso, lista.Estado);
        Assert.Equal(EstadoMatriculas.SemAcesso, alterar.Estado);
    }

    [Fact]
    public async Task Horarios_duplicados_sao_rejeitados_antes_do_repositorio()
    {
        var repositorio = new RepositorioFake();
        var servico = CriarServico(new(false, true, false), repositorio);
        var horario = Guid.NewGuid();

        var resultado = await servico.CriarAsync(
            _usuarioId, _unidadeId,
            SolicitacaoValida() with { TurmaHorarioIds = [horario, horario] },
            CancellationToken.None);

        Assert.Equal(EstadoMatriculas.HorarioDuplicado, resultado.Estado);
        Assert.False(repositorio.CriarChamado);
    }

    [Fact]
    public void Governanca_possui_capacidade_explicita_de_matriculas()
    {
        Assert.True(new GovernancaOperacionalUnidade(false, true, true)
            .PodeGerenciarMatriculas);
        Assert.True(new GovernancaOperacionalUnidade(true, false, false)
            .PodeGerenciarMatriculas);
        Assert.False(new GovernancaOperacionalUnidade(true, false, true)
            .PodeGerenciarMatriculas);
    }

    private MatriculasServico CriarServico(
        GovernancaOperacionalUnidade governanca,
        RepositorioFake repositorio) => new(
            new UnidadeContextoFake(_organizacaoId, _unidadeId),
            new GovernancaFake(governanca),
            repositorio,
            TimeProvider.System);

    private static CriarMatriculaSolicitacao SolicitacaoValida() => new(
        Guid.NewGuid(), null, [], Guid.NewGuid(), new DateOnly(2026, 9, 1),
        100, false, null, [Guid.NewGuid()]);

    private sealed class UnidadeContextoFake(Guid organizacaoId, Guid unidadeId)
        : IUnidadeContextoConsulta
    {
        public Task<UnidadeContextoResumo?> ObterAtivaAsync(
            Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<UnidadeContextoResumo?>(id == unidadeId
                ? new(organizacaoId, unidadeId, "Unidade") : null);
    }

    private sealed class GovernancaFake(GovernancaOperacionalUnidade governanca)
        : IGovernancaOperacionalUnidade
    {
        public Task<GovernancaOperacionalUnidade> ObterAsync(
            Guid usuarioId, Guid organizacaoId, Guid unidadeId,
            CancellationToken cancellationToken) => Task.FromResult(governanca);
    }

    private sealed class RepositorioFake : IMatriculasRepositorio
    {
        public bool CriarChamado { get; private set; }
        public bool PermitiuReusoOrganizacional { get; private set; }

        public Task<IReadOnlyList<MatriculaListaItem>> ListarAsync(
            Guid organizacaoId, Guid unidadeId, string? texto,
            StatusMatricula? status, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MatriculaListaItem>>([]);

        public Task<MatriculaDetalhe?> ObterAsync(
            Guid organizacaoId, Guid unidadeId, Guid matriculaId,
            CancellationToken cancellationToken) =>
            Task.FromResult<MatriculaDetalhe?>(null);

        public Task<IReadOnlyList<AlunoRelacionadoUnidadeResumo>>
            ListarAlunosRelacionadosAsync(
                Guid organizacaoId, Guid unidadeId, string? texto,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AlunoRelacionadoUnidadeResumo>>([]);

        public Task<IReadOnlyList<PlanoElegivelMatriculaResumo>>
            ListarPlanosElegiveisAsync(
                Guid organizacaoId, Guid unidadeId, DateOnly dataInicio,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PlanoElegivelMatriculaResumo>>([]);

        public Task<IReadOnlyList<HorarioElegivelMatriculaResumo>>
            ListarHorariosElegiveisAsync(
                Guid organizacaoId, Guid unidadeId, DateOnly dataInicio,
                DateOnly dataFim, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HorarioElegivelMatriculaResumo>>([]);

        public Task<ResultadoMatriculas<ResultadoCriacaoMatricula>> CriarAsync(
            Guid organizacaoId, Guid unidadeId, Guid usuarioId,
            bool permitirReusoOrganizacional, CriarMatriculaSolicitacao solicitacao,
            DateOnly dataCivilAtual, DateTime agoraUtc,
            CancellationToken cancellationToken)
        {
            CriarChamado = true;
            PermitiuReusoOrganizacional = permitirReusoOrganizacional;
            return Task.FromResult(new ResultadoMatriculas<ResultadoCriacaoMatricula>(
                EstadoMatriculas.Sucesso,
                new(Guid.NewGuid(), solicitacao.AlunoId!.Value,
                    solicitacao.TurmaHorarioIds.Count)));
        }

        public Task<ResultadoMatriculas<ResultadoAlteracaoGrade>> AlterarGradeAsync(
            Guid organizacaoId, Guid unidadeId, Guid matriculaId, Guid usuarioId,
            AlterarGradeMatriculaSolicitacao solicitacao, DateTime agoraUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ResultadoMatriculas<ResultadoAlteracaoGrade>(
                EstadoMatriculas.Sucesso, new(0, 0, solicitacao.TurmaHorarioIds.Count)));

        public Task<EstadoMatriculas> FinalizarAsync(
            Guid organizacaoId, Guid unidadeId, Guid matriculaId, Guid usuarioId,
            DateOnly dataFinalEfetiva, bool cancelar, DateTime agoraUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(EstadoMatriculas.Sucesso);
    }
}

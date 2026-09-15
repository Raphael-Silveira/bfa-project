using BFA.Application.Cobrancas;
using BFA.Domain.Cobrancas;
using BFA.Infrastructure.Cobrancas;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.UnitTests.Cobrancas;

public sealed class GeracaoCobrancasJobTests
{
    private static readonly Guid OrganizacaoId = Guid.NewGuid();
    private static readonly Guid UnidadeId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid MatriculaId = Guid.NewGuid();
    private static readonly Guid PlanoVersaoId = Guid.NewGuid();

    [Fact]
    public async Task Matricula_ativa_elegivel_gera_mensalidade()
    {
        var repositorio = new RepositorioFake
        {
            Matriculas = [Matricula(valor: 275m)]
        };
        var job = CriarJob(repositorio);

        await job.GerarMensalidadesAsync(CancellationToken.None);

        var cobranca = Assert.Single(repositorio.Criadas);
        Assert.Equal(TipoCobranca.Mensalidade, cobranca.Tipo);
        Assert.Equal(275m, cobranca.Valor);
        Assert.Equal(MatriculaId, cobranca.MatriculaId);
    }

    [Fact]
    public async Task Mesma_matricula_e_mes_nao_gera_segunda_cobranca_em_execucao_sequencial()
    {
        var repositorio = new RepositorioFake
        {
            Matriculas = [Matricula(valor: 275m)]
        };
        var job = CriarJob(repositorio);

        await job.GerarMensalidadesAsync(CancellationToken.None);
        await job.GerarMensalidadesAsync(CancellationToken.None);

        Assert.Single(repositorio.Criadas);
    }

    [Fact]
    public async Task Matricula_encerrada_ou_cancelada_nao_gera_nova_mensalidade()
    {
        var repositorio = new RepositorioFake { Matriculas = [] };
        var job = CriarJob(repositorio);

        await job.GerarMensalidadesAsync(CancellationToken.None);

        Assert.Empty(repositorio.Criadas);
    }

    [Fact]
    public async Task Mensalidade_cancelada_nao_e_recriada_automaticamente()
    {
        var repositorio = new RepositorioFake
        {
            Matriculas = [Matricula(valor: 275m)]
        };
        repositorio.Criadas.Add(CobrancaCancelada(TipoCobranca.Mensalidade, 275m));
        var job = CriarJob(repositorio);

        await job.GerarMensalidadesAsync(CancellationToken.None);

        var cobranca = Assert.Single(repositorio.Criadas);
        Assert.Equal(StatusCobranca.Cancelada, cobranca.Status);
    }

    [Fact]
    public async Task Taxa_cancelada_nao_e_recriada_automaticamente()
    {
        var repositorio = new RepositorioFake
        {
            Matriculas = [Matricula(valor: 275m, cobraTaxa: true, taxa: 90m)]
        };
        repositorio.Criadas.Add(CobrancaCancelada(TipoCobranca.Matricula, 90m));
        var job = CriarJob(repositorio);

        await job.GerarTaxasMatriculaAsync(CancellationToken.None);

        var cobranca = Assert.Single(repositorio.Criadas);
        Assert.Equal(StatusCobranca.Cancelada, cobranca.Status);
    }

    [Fact]
    public async Task Valor_usa_snapshot_financeiro_da_matricula()
    {
        var repositorio = new RepositorioFake
        {
            Matriculas = [Matricula(valor: 418.37m)]
        };
        var job = CriarJob(repositorio);

        await job.GerarMensalidadesAsync(CancellationToken.None);

        Assert.Equal(418.37m, Assert.Single(repositorio.Criadas).Valor);
    }

    [Fact]
    public async Task Taxa_de_matricula_configurada_gera_cobranca_adicional()
    {
        var repositorio = new RepositorioFake
        {
            Matriculas = [Matricula(valor: 275m, cobraTaxa: true, taxa: 90m)]
        };
        var job = CriarJob(repositorio);

        await job.GerarMensalidadesAsync(CancellationToken.None);

        Assert.Collection(
            repositorio.Criadas,
            mensalidade => Assert.Equal(TipoCobranca.Mensalidade, mensalidade.Tipo),
            taxa =>
            {
                Assert.Equal(TipoCobranca.Matricula, taxa.Tipo);
                Assert.Equal(90m, taxa.Valor);
            });
    }

    [Fact]
    public async Task Job_de_atraso_delega_a_alteracao_apenas_das_cobrancas_elegiveis()
    {
        var repositorio = new RepositorioFake { AtrasadasMarcadas = 2 };
        var job = CriarJob(repositorio);

        await job.MarcarAtrasadasAsync(CancellationToken.None);

        Assert.Equal(1, repositorio.ChamadasMarcarAtrasadas);
        Assert.Equal(2, repositorio.AtrasadasMarcadas);
    }

    private static GeracaoCobrancasJob CriarJob(RepositorioFake repositorio) =>
        new(repositorio, TimeProvider.System, NullLogger<GeracaoCobrancasJob>.Instance);

    private static MatriculaParaGeracao Matricula(
        decimal valor,
        bool cobraTaxa = false,
        decimal? taxa = null) =>
        new(
            OrganizacaoId,
            UnidadeId,
            AlunoId,
            "Aluno Teste",
            MatriculaId,
            PlanoVersaoId,
            "Plano Teste",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1),
            valor,
            cobraTaxa,
            taxa);

    private static Cobranca CobrancaCancelada(TipoCobranca tipo, decimal valor)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var cobranca = new Cobranca(
            Guid.NewGuid(),
            OrganizacaoId,
            UnidadeId,
            AlunoId,
            MatriculaId,
            tipo,
            "Cobranca existente",
            valor,
            hoje,
            hoje.AddDays(1),
            null,
            DateTime.UtcNow);

        cobranca.Cancelar(Guid.NewGuid(), DateTime.UtcNow);
        return cobranca;
    }

    private sealed class RepositorioFake : ICobrancasRepositorio
    {
        public IReadOnlyList<MatriculaParaGeracao> Matriculas { get; init; } = [];
        public List<Cobranca> Criadas { get; } = [];
        public int AtrasadasMarcadas { get; init; }
        public int ChamadasMarcarAtrasadas { get; private set; }

        public Task<IReadOnlyList<MatriculaParaGeracao>> ListarMatriculasAtivasParaGeracaoAsync(
            CancellationToken cancellationToken) => Task.FromResult(Matriculas);

        public Task<bool> ExisteMensalidadeNoMesAsync(
            Guid matriculaId, int ano, int mes, CancellationToken cancellationToken) =>
            Task.FromResult(Criadas.Any(c => c.MatriculaId == matriculaId
                && c.Tipo == TipoCobranca.Mensalidade
                && c.DataVencimento.Year == ano
                && c.DataVencimento.Month == mes));

        public Task<bool> ExisteTaxaMatriculaAsync(
            Guid matriculaId, CancellationToken cancellationToken) =>
            Task.FromResult(Criadas.Any(c => c.MatriculaId == matriculaId
                && c.Tipo == TipoCobranca.Matricula));

        public Task<bool> CriarAsync(Cobranca cobranca, CancellationToken cancellationToken)
        {
            Criadas.Add(cobranca);
            return Task.FromResult(true);
        }

        public Task<Cobranca> CriarAutomaticaIdempotenteAsync(
            Cobranca cobranca, CancellationToken cancellationToken)
        {
            Criadas.Add(cobranca);
            return Task.FromResult(cobranca);
        }

        public Task<int> MarcarAtrasadasAsync(CancellationToken cancellationToken)
        {
            ChamadasMarcarAtrasadas++;
            return Task.FromResult(AtrasadasMarcadas);
        }

        public Task<IReadOnlyList<CobrancaListaItem>> ListarAsync(Guid organizacaoId, Guid unidadeId, FiltroCobrancas filtro, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CobrancaDetalhe?> ObterAsync(Guid organizacaoId, Guid unidadeId, Guid cobrancaId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Cobranca?> ObterPorIdAsync(Guid organizacaoId, Guid unidadeId, Guid cobrancaId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> CancelarAsync(Cobranca cobranca, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Pagamento?> ObterPagamentoAsync(Guid organizacaoId, Guid cobrancaId, Guid pagamentoId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PagamentoResumo>> RegistrarPagamentoConsolidadoAsync(Guid organizacaoId, Guid unidadeId, IReadOnlyList<Guid> cobrancaIds, DateOnly dataPagamento, FormaPagamento formaPagamento, string? observacoes, Guid usuarioId, DateTime agora, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AlunoParaSelecao>> ListarAlunosAsync(Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CobrancaListaItem>> ListarPorAlunoAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResumoFinanceiro> ObterResumoAsync(Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

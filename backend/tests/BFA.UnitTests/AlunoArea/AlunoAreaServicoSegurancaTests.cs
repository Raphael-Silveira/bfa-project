using BFA.Application.AlunoArea;
using BFA.Domain.Alunos;
using BFA.Domain.Cobrancas;
using BFA.Domain.Matriculas;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.UnitTests.AlunoArea;

public sealed class AlunoAreaServicoSegurancaTests
{
    [Fact]
    public async Task Frequencia_expoe_somente_presencas_retornadas_para_o_aluno()
    {
        var alunoId = Guid.NewGuid();
        IReadOnlyList<(DateOnly Data, string TurmaNome, string HoraInicio, string HoraFim, string Status, string? Observacoes)> presencas = new[]
        {
            (new DateOnly(2026, 9, 10), "Turma A", "08:00", "09:00", "Presente", (string?)null)
        };
        var repositorio = new RepositorioFake(alunoId, presencas);
        var servico = new AlunoAreaServico(
            repositorio,
            NullLogger<AlunoAreaServico>.Instance,
            TimeProvider.System,
            TimeZoneInfo.Utc);

        var resultado = await servico.ObterFrequenciaAsync(
            repositorio.UsuarioId,
            repositorio.UnidadeId,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            CancellationToken.None);

        var presenca = Assert.Single(resultado!.Presencas);
        Assert.Equal("Turma A", presenca.TurmaNome);
        Assert.Equal("Presente", presenca.Status);
        Assert.Equal(1, repositorio.QuantidadeDeListagensDePresenca);
    }

    [Fact]
    public async Task Atualizacao_de_perfil_valida_email_e_preserva_o_aluno_do_vinculo()
    {
        var repositorio = new RepositorioFake(
            Guid.NewGuid(),
            Array.Empty<(DateOnly, string, string, string, string, string?)>());
        var servico = new AlunoAreaServico(
            repositorio,
            NullLogger<AlunoAreaServico>.Instance,
            TimeProvider.System,
            TimeZoneInfo.Utc);

        var resultado = await servico.AtualizarPerfilAsync(
            repositorio.UsuarioId,
            repositorio.UnidadeId,
            "(11) 99999-0000",
            " aluno@exemplo.com ",
            CancellationToken.None);

        Assert.Equal(ResultadoAtualizacaoPerfilAluno.Sucesso, resultado);
        Assert.Equal(repositorio.Aluno.Id, repositorio.UltimoAlunoAtualizado);
        Assert.Equal("aluno@exemplo.com", repositorio.UltimoEmail);
    }

    [Fact]
    public async Task Atualizacao_de_perfil_rejeita_email_invalido_antes_da_persistencia()
    {
        var repositorio = new RepositorioFake(
            Guid.NewGuid(),
            Array.Empty<(DateOnly, string, string, string, string, string?)>());
        var servico = new AlunoAreaServico(
            repositorio,
            NullLogger<AlunoAreaServico>.Instance,
            TimeProvider.System,
            TimeZoneInfo.Utc);

        var resultado = await servico.AtualizarPerfilAsync(
            repositorio.UsuarioId,
            repositorio.UnidadeId,
            null,
            "invalido",
            CancellationToken.None);

        Assert.Equal(ResultadoAtualizacaoPerfilAluno.EmailInvalido, resultado);
        Assert.Null(repositorio.UltimoAlunoAtualizado);
    }

    [Fact]
    public async Task Atualizacao_de_perfil_rejeita_telefone_invalido_antes_da_persistencia()
    {
        var repositorio = new RepositorioFake(
            Guid.NewGuid(),
            Array.Empty<(DateOnly, string, string, string, string, string?)>());
        var servico = new AlunoAreaServico(
            repositorio,
            NullLogger<AlunoAreaServico>.Instance,
            TimeProvider.System,
            TimeZoneInfo.Utc);

        var resultado = await servico.AtualizarPerfilAsync(
            repositorio.UsuarioId,
            repositorio.UnidadeId,
            "55119926822355",
            "aluno@exemplo.com",
            CancellationToken.None);

        Assert.Equal(ResultadoAtualizacaoPerfilAluno.TelefoneInvalido, resultado);
        Assert.Null(repositorio.UltimoAlunoAtualizado);
    }

    [Fact]
    public async Task Financeiro_preserva_tipo_da_cobranca_e_periodo_consultado()
    {
        var repositorio = new RepositorioFake(
            Guid.NewGuid(),
            Array.Empty<(DateOnly, string, string, string, string, string?)>());
        var cobranca = new Cobranca(
            Guid.NewGuid(), repositorio.OrganizacaoId, repositorio.UnidadeId,
            repositorio.Aluno.Id, Guid.NewGuid(), TipoCobranca.Mensalidade,
            "Plano mensal", 280m, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10),
            null, DateTime.UtcNow);
        cobranca.RegistrarPagamento(280m, DateTime.UtcNow);
        var pagamento = new Pagamento(
            Guid.NewGuid(), repositorio.OrganizacaoId, repositorio.UnidadeId,
            cobranca.Id, 280m, new DateOnly(2026, 9, 11), FormaPagamento.Pix,
            repositorio.UsuarioId, DateTime.UtcNow);
        repositorio.Cobrancas = [cobranca];
        repositorio.Pagamentos = [(pagamento, TipoCobranca.Mensalidade)];

        var servico = new AlunoAreaServico(
            repositorio,
            NullLogger<AlunoAreaServico>.Instance,
            TimeProvider.System,
            TimeZoneInfo.Utc);

        var resultado = await servico.ObterFinanceiroAsync(
            repositorio.UsuarioId,
            repositorio.UnidadeId,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            CancellationToken.None);

        var registro = Assert.Single(resultado!.Pagamentos);
        Assert.Equal("Mensalidade", registro.Tipo);
        Assert.Equal("R$ 280,00", registro.Valor);
        Assert.Equal(new DateOnly(2026, 9, 1), repositorio.UltimoFinanceiroInicio);
        Assert.Equal(new DateOnly(2026, 9, 30), repositorio.UltimoFinanceiroFim);
    }

    private sealed class RepositorioFake : IAlunoAreaRepositorio
    {
        private readonly IReadOnlyList<(DateOnly Data, string TurmaNome, string HoraInicio, string HoraFim, string Status, string? Observacoes)> _presencas;

        public RepositorioFake(
            Guid alunoId,
            IReadOnlyList<(DateOnly Data, string TurmaNome, string HoraInicio, string HoraFim, string Status, string? Observacoes)> presencas)
        {
            UsuarioId = Guid.NewGuid();
            OrganizacaoId = Guid.NewGuid();
            UnidadeId = Guid.NewGuid();
            _presencas = presencas;
            Aluno = new Aluno(
                alunoId,
                OrganizacaoId,
                "Aluno Teste",
                new DateOnly(2000, 1, 1),
                new DateOnly(2026, 9, 16),
                DateTime.UtcNow,
                UsuarioId);
        }

        public Guid UsuarioId { get; }
        public Guid OrganizacaoId { get; }
        public Guid UnidadeId { get; }
        public Aluno Aluno { get; }
        public int QuantidadeDeListagensDePresenca { get; private set; }
        public Guid? UltimoAlunoAtualizado { get; private set; }
        public string? UltimoEmail { get; private set; }
        public IReadOnlyList<Cobranca> Cobrancas { get; set; } = [];
        public IReadOnlyList<(Pagamento Pagamento, TipoCobranca Tipo)> Pagamentos { get; set; } = [];
        public DateOnly? UltimoFinanceiroInicio { get; private set; }
        public DateOnly? UltimoFinanceiroFim { get; private set; }

        public Task<AlunoComUnidade?> ObterAlunoPorUsuarioAsync(Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
            => Task.FromResult<AlunoComUnidade?>(
                usuarioId == UsuarioId && unidadeId == UnidadeId
                    ? new(Aluno, OrganizacaoId, UnidadeId)
                    : null);

        public Task<bool> AtualizarPerfilAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, string? telefone, string? email, DateTime atualizadoEmUtc, CancellationToken cancellationToken)
        {
            if (organizacaoId != OrganizacaoId || unidadeId != UnidadeId || alunoId != Aluno.Id)
                return Task.FromResult(false);

            UltimoAlunoAtualizado = alunoId;
            UltimoEmail = email;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<MatriculaAlunoConsulta>> ListarMatriculasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatriculaAlunoConsulta>>([]);

        public Task<IReadOnlyList<(Guid AulaId, string TurmaNome, DateOnly Data, string HoraInicio, string HoraFim, string Status, bool ConfirmacaoAtiva)>> ListarAulasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<(Guid, string, DateOnly, string, string, string, bool)>>([]);

        public Task<IReadOnlyList<(DateOnly Data, string TurmaNome, string HoraInicio, string HoraFim, string Status, string? Observacoes)>> ListarPresencasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken)
        {
            QuantidadeDeListagensDePresenca++;
            return Task.FromResult(_presencas);
        }

        public Task<int> ContarAulasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken)
            => Task.FromResult(1);

        public Task<int> ContarPresencasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken)
            => Task.FromResult(1);

        public Task<int> ContarAusenciasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<int> ContarJustificativasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<IReadOnlyList<Cobranca>> ListarCobrancasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly? dataInicio, DateOnly? dataFim, CancellationToken cancellationToken)
        {
            UltimoFinanceiroInicio = dataInicio;
            UltimoFinanceiroFim = dataFim;
            return Task.FromResult(Cobrancas);
        }

        public Task<IReadOnlyList<(Pagamento Pagamento, TipoCobranca Tipo)>> ListarPagamentosAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly? dataInicio, DateOnly? dataFim, CancellationToken cancellationToken)
            => Task.FromResult(Pagamentos);

        public Task<string?> ObterNomeUnidadeAsync(Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken)
            => Task.FromResult<string?>("Unidade Teste");

        public Task<AulaConfirmacaoConsulta?> ObterAulaParaConfirmacaoAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, Guid aulaId, CancellationToken cancellationToken)
            => Task.FromResult<AulaConfirmacaoConsulta?>(null);

        public Task<bool> ConfirmarAulaAsync(Guid organizacaoId, Guid unidadeId, Guid aulaId, Guid alunoId, DateTime agoraUtc, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<bool> CancelarConfirmacaoAulaAsync(Guid organizacaoId, Guid unidadeId, Guid aulaId, Guid alunoId, DateTime agoraUtc, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }
}

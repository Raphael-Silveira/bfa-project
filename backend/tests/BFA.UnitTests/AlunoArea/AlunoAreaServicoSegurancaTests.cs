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
            NullLogger<AlunoAreaServico>.Instance);

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

        public Task<AlunoComUnidade?> ObterAlunoPorUsuarioAsync(Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
            => Task.FromResult<AlunoComUnidade?>(
                usuarioId == UsuarioId && unidadeId == UnidadeId
                    ? new(Aluno, OrganizacaoId, UnidadeId)
                    : null);

        public Task<IReadOnlyList<Matricula>> ListarMatriculasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Matricula>>([]);

        public Task<IReadOnlyList<(string TurmaNome, DateOnly Data, string HoraInicio, string HoraFim, string Status)>> ListarAulasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<(string, DateOnly, string, string, string)>>([]);

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

        public Task<IReadOnlyList<Cobranca>> ListarCobrancasAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Cobranca>>([]);

        public Task<IReadOnlyList<Pagamento>> ListarPagamentosAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Pagamento>>([]);

        public Task<string?> ObterNomeUnidadeAsync(Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken)
            => Task.FromResult<string?>("Unidade Teste");
    }
}

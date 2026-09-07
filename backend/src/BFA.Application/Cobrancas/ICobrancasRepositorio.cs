using BFA.Application.Cobrancas;
using BFA.Domain.Cobrancas;

namespace BFA.Application.Cobrancas;

public interface ICobrancasRepositorio
{
    Task<IReadOnlyList<CobrancaListaItem>> ListarAsync(
        Guid organizacaoId, Guid unidadeId, FiltroCobrancas filtro,
        CancellationToken cancellationToken);

    Task<CobrancaDetalhe?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid cobrancaId,
        CancellationToken cancellationToken);

    Task<Cobranca?> ObterPorIdAsync(
        Guid organizacaoId, Guid unidadeId, Guid cobrancaId,
        CancellationToken cancellationToken);

    Task<bool> CriarAsync(Cobranca cobranca, CancellationToken cancellationToken);

    Task<bool> CancelarAsync(Cobranca cobranca, CancellationToken cancellationToken);

    Task<Pagamento?> ObterPagamentoAsync(
        Guid organizacaoId, Guid cobrancaId, Guid pagamentoId,
        CancellationToken cancellationToken);

    Task<bool> RegistrarPagamentoAsync(Pagamento pagamento, CancellationToken cancellationToken);

    Task<IReadOnlyList<AlunoParaSelecao>> ListarAlunosAsync(
        Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CobrancaListaItem>> ListarPorAlunoAsync(
        Guid organizacaoId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken);

    Task<ResumoFinanceiro> ObterResumoAsync(
        Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MatriculaParaGeracao>> ListarMatriculasAtivasParaGeracaoAsync(
        CancellationToken cancellationToken);

    Task<bool> ExisteMensalidadeNoMesAsync(
        Guid matriculaId, int ano, int mes,
        CancellationToken cancellationToken);

    Task<bool> ExisteTaxaMatriculaAsync(
        Guid matriculaId,
        CancellationToken cancellationToken);

    Task<int> MarcarAtrasadasAsync(CancellationToken cancellationToken);
}

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

    Task<ResumoFinanceiro> ObterResumoAsync(
        Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken);
}

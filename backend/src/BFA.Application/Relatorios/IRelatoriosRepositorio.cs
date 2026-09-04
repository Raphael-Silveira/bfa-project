using BFA.Application.Relatorios;

using BFA.Domain.Cobrancas;

namespace BFA.Application.Relatorios;

public interface IRelatoriosRepositorio
{
    Task<ResumoGeralRelatorios> ObterResumoGeralAsync(
        Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CobrancaRelatorio>> ListarCobrancasAsync(
        Guid organizacaoId, Guid unidadeId,
        DateOnly? dataInicio, DateOnly? dataFim,
        CancellationToken cancellationToken);

    Task<int> ContarAlunosAtivosAsync(
        Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken);

    Task<int> ContarAulasConcluidasAsync(
        Guid organizacaoId, Guid unidadeId, DateOnly ate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CobrancaRelatorio>> ListarCobrancasAtrasadasAsync(
        Guid organizacaoId, Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CobrancaRelatorio>> ListarCobrancasAtrasadasPorAlunoAsync(
        Guid organizacaoId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken);
}

public sealed record CobrancaRelatorio(
    Guid CobrancaId,
    TipoCobranca Tipo,
    StatusCobranca Status,
    decimal Valor,
    decimal ValorPago,
    string? Descricao,
    DateOnly DataEmissao,
    DateOnly DataVencimento,
    Guid AlunoId);

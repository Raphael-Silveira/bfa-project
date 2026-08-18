namespace BFA.Application.Franqueadora.AcessosUnidade;

public interface IAcessosUnidadeConsulta
{
    Task<ResultadoAcessosUnidade<AcessosUnidadeDetalhe>> ObterAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);
}

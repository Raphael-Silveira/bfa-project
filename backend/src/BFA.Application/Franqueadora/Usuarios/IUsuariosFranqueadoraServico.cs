namespace BFA.Application.Franqueadora.Usuarios;

public interface IUsuariosFranqueadoraServico
{
    Task<ResultadoCriacaoUsuarioFranqueadora> CriarAsync(
        Guid usuarioAtualId,
        CriarUsuarioFranqueadoraSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoAtualizacaoUsuarioFranqueadora> EditarAsync(
        Guid usuarioAtualId,
        EditarUsuarioFranqueadoraSolicitacao solicitacao,
        CancellationToken cancellationToken);
}

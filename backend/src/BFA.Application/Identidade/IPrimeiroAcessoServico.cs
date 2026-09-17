namespace BFA.Application.Identidade;

public interface IPrimeiroAcessoServico
{
    Task<bool> TokenValidoAsync(
        Guid usuarioId,
        string token,
        CancellationToken cancellationToken);

    Task<ResultadoDefinicaoSenha> DefinirSenhaAsync(
        Guid usuarioId,
        string token,
        string novaSenha,
        CancellationToken cancellationToken);

    Task<bool> TrocaObrigatoriaAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);

    Task<ResultadoDefinicaoSenha> TrocarSenhaObrigatoriaAsync(
        Guid usuarioId,
        string senhaAtual,
        string novaSenha,
        CancellationToken cancellationToken);
}

public enum EstadoDefinicaoSenha
{
    Sucesso = 1,
    LinkInvalido = 2,
    SenhaInvalida = 3
}

public sealed record ResultadoDefinicaoSenha(
    EstadoDefinicaoSenha Estado,
    IReadOnlyList<string> Erros);

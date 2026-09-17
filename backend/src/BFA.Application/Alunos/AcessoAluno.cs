namespace BFA.Application.Alunos;

public enum EstadoAcessoAluno
{
    Sucesso,
    AcessoJaExistente,
    SemAcesso,
    AlunoNaoEncontrado,
    CpfNaoInformado,
    CpfDuplicado,
    UsuarioIncompativel,
    Falha
}

public sealed record ResultadoAcessoAluno(
    EstadoAcessoAluno Estado,
    Guid? UsuarioId = null,
    string? Usuario = null,
    string? SenhaTemporaria = null);

public interface IAcessoAlunoServico
{
    Task<ResultadoAcessoAluno> ConcederAsync(
        Guid usuarioOperadorId,
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken);

    Task<ResultadoAcessoAluno> RedefinirSenhaAsync(
        Guid usuarioOperadorId,
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken);
}

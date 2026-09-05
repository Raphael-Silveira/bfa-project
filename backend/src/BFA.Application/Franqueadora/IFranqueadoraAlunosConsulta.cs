namespace BFA.Application.Franqueadora;

public interface IFranqueadoraAlunosConsulta
{
    Task<FranqueadoraAlunosResultado> ListarAsync(
        Guid usuarioId,
        Guid? unidadeId,
        string? busca,
        CancellationToken cancellationToken);
}

public enum EstadoFranqueadoraAlunos
{
    Sucesso = 1,
    SemAcesso = 2,
    SelecaoOrganizacaoNecessaria = 3
}

public sealed record FranqueadoraAlunosResumo(
    Guid OrganizacaoId,
    string NomeOrganizacao,
    int TotalAlunos,
    IReadOnlyList<FranqueadoraAlunoItem> Alunos,
    IReadOnlyList<FranqueadoraUnidadeSelecao> Unidades);

public sealed record FranqueadoraAlunoItem(
    Guid AlunoId,
    string NomeCompleto,
    string? Cpf,
    string? Email,
    string? Telefone,
    bool Ativo,
    string NomeUnidade,
    Guid UnidadeId,
    DateOnly DataNascimento,
    DateTime CriadoEmUtc);

public sealed record FranqueadoraUnidadeSelecao(
    Guid UnidadeId,
    string Nome);

public sealed record FranqueadoraAlunosResultado(
    EstadoFranqueadoraAlunos Estado,
    FranqueadoraAlunosResumo? Resumo)
{
    public static FranqueadoraAlunosResultado Sucesso(
        FranqueadoraAlunosResumo resumo)
    {
        ArgumentNullException.ThrowIfNull(resumo);
        return new(EstadoFranqueadoraAlunos.Sucesso, resumo);
    }

    public static FranqueadoraAlunosResultado SemAcesso()
    {
        return new(EstadoFranqueadoraAlunos.SemAcesso, null);
    }

    public static FranqueadoraAlunosResultado SelecaoOrganizacaoNecessaria()
    {
        return new(EstadoFranqueadoraAlunos.SelecaoOrganizacaoNecessaria, null);
    }
}

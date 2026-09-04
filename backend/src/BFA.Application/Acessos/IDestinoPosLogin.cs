namespace BFA.Application.Acessos;

public enum DestinoAcesso
{
    Padrao = 1,
    AdministradorRede = 2,
    Unidade = 3,
    SelecionarUnidade = 4,
    SemAcesso = 5,
    ProfessorUnidade = 6,
    SelecionarUnidadeProfessor = 7,
    AlunoUnidade = 8
}

public sealed record DestinoPosLoginResultado(
    DestinoAcesso Destino,
    Guid? UnidadeId = null);

public interface IDestinoPosLogin
{
    Task<DestinoPosLoginResultado> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);
}

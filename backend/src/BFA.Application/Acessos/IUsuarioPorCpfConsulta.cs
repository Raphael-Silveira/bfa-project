namespace BFA.Application.Acessos;

public interface IUsuarioPorCpfConsulta
{
    Task<Guid?> ObterAlunoAsync(
        string cpfNormalizado,
        CancellationToken cancellationToken);
}

namespace BFA.Application.Franqueadora;

public interface IPainelFranqueadoraConsulta
{
    Task<PainelFranqueadoraResultado> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);
}

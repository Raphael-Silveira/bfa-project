namespace BFA.Application.Bootstrap;

public sealed class BootstrapInicialResultado
{
    public BootstrapInicialResultado(
        bool organizacaoCriada,
        IReadOnlyCollection<AdministradorBootstrapResultado> administradores)
    {
        OrganizacaoCriada = organizacaoCriada;
        Administradores = administradores
            ?? throw new ArgumentNullException(nameof(administradores));
    }

    public bool OrganizacaoCriada { get; }

    public IReadOnlyCollection<AdministradorBootstrapResultado> Administradores { get; }
}

public sealed record AdministradorBootstrapResultado(
    int Numero,
    bool UsuarioCriado,
    bool VinculoCriado);

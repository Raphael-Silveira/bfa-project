namespace BFA.Application.Bootstrap;

public sealed class BootstrapInicialSolicitacao
{
    public BootstrapInicialSolicitacao(
        CredenciaisAdministradorBootstrap administrador1,
        CredenciaisAdministradorBootstrap administrador2)
    {
        Administrador1 = administrador1
            ?? throw new ArgumentNullException(nameof(administrador1));
        Administrador2 = administrador2
            ?? throw new ArgumentNullException(nameof(administrador2));
    }

    public CredenciaisAdministradorBootstrap Administrador1 { get; }

    public CredenciaisAdministradorBootstrap Administrador2 { get; }
}

public sealed class CredenciaisAdministradorBootstrap
{
    public CredenciaisAdministradorBootstrap(string email, string senha)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Senha = senha ?? throw new ArgumentNullException(nameof(senha));
    }

    public string Email { get; }

    public string Senha { get; }
}

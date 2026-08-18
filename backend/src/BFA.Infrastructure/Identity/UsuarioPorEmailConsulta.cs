using BFA.Application.Acessos;
using Microsoft.AspNetCore.Identity;

namespace BFA.Infrastructure.Identity;

public sealed class UsuarioPorEmailConsulta(
    UserManager<UsuarioIdentity> userManager) : IUsuarioPorEmailConsulta
{
    public async Task<UsuarioPorEmail?> ObterAsync(
        string email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var emailInformado = email.Trim();
        var usuario = await userManager.FindByEmailAsync(emailInformado);
        cancellationToken.ThrowIfCancellationRequested();

        return usuario is null
            ? null
            : new UsuarioPorEmail(usuario.Id, usuario.Email ?? emailInformado);
    }
}

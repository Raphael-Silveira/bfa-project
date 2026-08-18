using BFA.Domain.Acessos;
using Microsoft.AspNetCore.Authorization;

namespace BFA.Web.Authorization;

public sealed class PerfilAcessoRequirement : IAuthorizationRequirement
{
    public PerfilAcessoRequirement(params PerfilAcesso[] perfisPermitidos)
    {
        ArgumentNullException.ThrowIfNull(perfisPermitidos);

        if (perfisPermitidos.Length == 0)
        {
            throw new ArgumentException(
                "Ao menos um perfil de acesso deve ser informado.",
                nameof(perfisPermitidos));
        }

        if (perfisPermitidos.Any(perfil => !Enum.IsDefined(perfil)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(perfisPermitidos),
                "Todos os perfis de acesso devem ser validos.");
        }

        PerfisPermitidos = [.. perfisPermitidos.Distinct()];
    }

    public IReadOnlyCollection<PerfilAcesso> PerfisPermitidos { get; }
}

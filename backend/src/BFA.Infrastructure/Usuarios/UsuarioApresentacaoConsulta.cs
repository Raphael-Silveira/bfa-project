using BFA.Application.Usuarios;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Usuarios;

public sealed class UsuarioApresentacaoConsulta(BfaDbContext dbContext)
    : IUsuarioApresentacaoConsulta
{
    public Task<string?> ObterNomeCompletoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        return dbContext.PerfisUsuario
            .AsNoTracking()
            .Where(perfil => perfil.UsuarioId == usuarioId)
            .Select(perfil => perfil.NomeCompleto)
            .SingleOrDefaultAsync(cancellationToken);
    }
}

using BFA.Application.Usuarios;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Usuarios;

public sealed class UsuarioApresentacaoConsulta(BfaDbContext dbContext)
    : IUsuarioApresentacaoConsulta
{
    public async Task<string?> ObterNomeCompletoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var nomePerfil = await dbContext.PerfisUsuario
            .AsNoTracking()
            .Where(perfil => perfil.UsuarioId == usuarioId)
            .Select(perfil => perfil.NomeCompleto)
            .SingleOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(nomePerfil))
        {
            return nomePerfil;
        }

        return await dbContext.Professores
            .AsNoTracking()
            .Where(professor => professor.UsuarioId == usuarioId)
            .Select(professor => professor.NomeCompleto)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

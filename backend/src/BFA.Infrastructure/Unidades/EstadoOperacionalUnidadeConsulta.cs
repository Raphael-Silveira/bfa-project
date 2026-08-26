using BFA.Application.Unidades;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Unidades;

public sealed class EstadoOperacionalUnidadeConsulta(BfaDbContext dbContext)
    : IEstadoOperacionalUnidadeConsulta
{
    public Task<bool> PossuiFranqueadoAtivoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken) =>
        dbContext.FranqueadosUnidades
            .AsNoTracking()
            .AnyAsync(
                vinculo => vinculo.OrganizacaoId == organizacaoId
                    && vinculo.UnidadeId == unidadeId
                    && vinculo.Ativo,
                cancellationToken);
}

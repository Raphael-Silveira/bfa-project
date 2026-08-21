using BFA.Application.Localidades;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Localidades;

public sealed class LocalidadesConsulta(BfaDbContext dbContext) : ILocalidadesConsulta
{
    public async Task<IReadOnlyList<EstadoLocalidadeResumo>> ListarEstadosAtivosAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Estados
            .AsNoTracking()
            .Where(estado => estado.Ativo)
            .OrderBy(estado => estado.Nome)
            .ThenBy(estado => estado.CodigoIbge)
            .Select(estado => new EstadoLocalidadeResumo(
                estado.CodigoIbge,
                estado.Sigla,
                estado.Nome))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MunicipioLocalidadeResumo>> ListarMunicipiosAtivosAsync(
        int estadoCodigoIbge,
        CancellationToken cancellationToken)
    {
        var estadoAtivo = await dbContext.Estados
            .AsNoTracking()
            .AnyAsync(
                estado => estado.CodigoIbge == estadoCodigoIbge && estado.Ativo,
                cancellationToken);

        if (!estadoAtivo)
        {
            return [];
        }

        return await dbContext.Municipios
            .AsNoTracking()
            .Where(municipio => municipio.Ativo
                && municipio.EstadoCodigoIbge == estadoCodigoIbge)
            .OrderBy(municipio => municipio.Nome)
            .ThenBy(municipio => municipio.CodigoIbge)
            .Select(municipio => new MunicipioLocalidadeResumo(
                municipio.CodigoIbge,
                municipio.Nome))
            .ToListAsync(cancellationToken);
    }
}

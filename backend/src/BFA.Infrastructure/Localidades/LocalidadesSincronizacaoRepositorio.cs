using BFA.Application.Localidades;
using BFA.Domain.Localidades;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BFA.Infrastructure.Localidades;

public sealed class LocalidadesSincronizacaoRepositorio(BfaDbContext dbContext)
    : ILocalidadesSincronizacaoRepositorio
{
    public async Task SincronizarAsync(
        CatalogoLocalidadesDados catalogo,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        IDbContextTransaction? transaction = null;

        try
        {
            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            }

            var estadosExistentes = await dbContext.Estados
                .ToDictionaryAsync(estado => estado.CodigoIbge, cancellationToken);
            var codigosEstadosRecebidos = catalogo.Estados
                .Select(estado => estado.CodigoIbge)
                .ToHashSet();

            foreach (var estadoRecebido in catalogo.Estados)
            {
                if (estadosExistentes.TryGetValue(estadoRecebido.CodigoIbge, out var estado))
                {
                    estado.Atualizar(
                        estadoRecebido.Sigla,
                        estadoRecebido.Nome,
                        atualizadoEmUtc);
                }
                else
                {
                    dbContext.Estados.Add(new Estado(
                        estadoRecebido.CodigoIbge,
                        estadoRecebido.Sigla,
                        estadoRecebido.Nome,
                        atualizadoEmUtc));
                }
            }

            foreach (var estado in estadosExistentes.Values.Where(
                estado => !codigosEstadosRecebidos.Contains(estado.CodigoIbge)))
            {
                estado.Desativar(atualizadoEmUtc);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var municipiosExistentes = await dbContext.Municipios
                .ToDictionaryAsync(municipio => municipio.CodigoIbge, cancellationToken);
            var codigosMunicipiosRecebidos = catalogo.Municipios
                .Select(municipio => municipio.CodigoIbge)
                .ToHashSet();

            foreach (var municipioRecebido in catalogo.Municipios)
            {
                if (municipiosExistentes.TryGetValue(
                    municipioRecebido.CodigoIbge,
                    out var municipio))
                {
                    municipio.Atualizar(
                        municipioRecebido.EstadoCodigoIbge,
                        municipioRecebido.Nome,
                        atualizadoEmUtc);
                }
                else
                {
                    dbContext.Municipios.Add(new Municipio(
                        municipioRecebido.CodigoIbge,
                        municipioRecebido.EstadoCodigoIbge,
                        municipioRecebido.Nome,
                        atualizadoEmUtc));
                }
            }

            foreach (var municipio in municipiosExistentes.Values.Where(
                municipio => !codigosMunicipiosRecebidos.Contains(municipio.CodigoIbge)))
            {
                municipio.Desativar(atualizadoEmUtc);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Persistence;

internal sealed class DatabaseConnectionProbe(BfaDbContext dbContext)
    : IDatabaseConnectionProbe
{
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}

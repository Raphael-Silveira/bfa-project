namespace BFA.Infrastructure.Persistence;

public interface IDatabaseConnectionProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}

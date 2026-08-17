using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Persistence;

public sealed class BfaDbContext(DbContextOptions<BfaDbContext> options)
    : DbContext(options);

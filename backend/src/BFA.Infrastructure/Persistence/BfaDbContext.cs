using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Persistence;

public sealed class BfaDbContext(DbContextOptions<BfaDbContext> options)
    : IdentityUserContext<UsuarioIdentity, Guid>(options)
{
    public DbSet<Organizacao> Organizacoes => Set<Organizacao>();

    public DbSet<Unidade> Unidades => Set<Unidade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BfaDbContext).Assembly);
    }
}

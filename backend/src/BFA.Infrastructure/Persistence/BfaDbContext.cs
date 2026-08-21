using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using BFA.Domain.Localidades;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Domain.Usuarios;
using BFA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Persistence;

public sealed class BfaDbContext(DbContextOptions<BfaDbContext> options)
    : IdentityUserContext<UsuarioIdentity, Guid>(options)
{
    public DbSet<Organizacao> Organizacoes => Set<Organizacao>();

    public DbSet<Unidade> Unidades => Set<Unidade>();

    public DbSet<VinculoAcesso> VinculosAcesso => Set<VinculoAcesso>();

    public DbSet<PerfilUsuario> PerfisUsuario => Set<PerfilUsuario>();

    public DbSet<Franqueado> Franqueados => Set<Franqueado>();

    public DbSet<FranqueadoUsuario> FranqueadosUsuarios => Set<FranqueadoUsuario>();

    public DbSet<FranqueadoUnidade> FranqueadosUnidades => Set<FranqueadoUnidade>();

    public DbSet<Estado> Estados => Set<Estado>();

    public DbSet<Municipio> Municipios => Set<Municipio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BfaDbContext).Assembly);
    }
}

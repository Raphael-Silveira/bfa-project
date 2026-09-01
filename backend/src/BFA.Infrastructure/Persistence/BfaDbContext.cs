using BFA.Domain.Acessos;
using BFA.Domain.Contratos;
using BFA.Domain.Franqueados;
using BFA.Domain.Localidades;
using BFA.Domain.Organizacoes;
using BFA.Domain.Planos;
using BFA.Domain.Professores;
using BFA.Domain.Turmas;
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

    public DbSet<ContratoFranquia> ContratosFranquia => Set<ContratoFranquia>();

    public DbSet<ContratoFranquiaVersao> ContratosFranquiaVersoes =>
        Set<ContratoFranquiaVersao>();

    public DbSet<DocumentoContratoFranquia> DocumentosContratoFranquia =>
        Set<DocumentoContratoFranquia>();

    public DbSet<Professor> Professores => Set<Professor>();

    public DbSet<ProfessorUnidade> ProfessoresUnidades => Set<ProfessorUnidade>();

    public DbSet<ProfessorRemuneracao> ProfessoresRemuneracoes =>
        Set<ProfessorRemuneracao>();

    public DbSet<Turma> Turmas => Set<Turma>();

    public DbSet<TurmaHorario> TurmasHorarios => Set<TurmaHorario>();

    public DbSet<Plano> Planos => Set<Plano>();

    public DbSet<PlanoVersao> PlanosVersoes => Set<PlanoVersao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BfaDbContext).Assembly);
    }
}

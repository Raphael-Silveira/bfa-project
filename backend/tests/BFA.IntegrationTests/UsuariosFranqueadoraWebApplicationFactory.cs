using BFA.Application.Acessos;
using BFA.Application.Localidades;
using BFA.Domain.Acessos;
using BFA.Domain.Localidades;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public sealed class UsuariosFranqueadoraWebApplicationFactory : BfaWebApplicationFactory
{
    public const int EstadoPadraoCodigoIbge = 35;
    public const int MunicipioPadraoCodigoIbge = 3554508;

    private readonly string _databaseName = $"bfa-usuarios-web-{Guid.NewGuid():N}";

    public string AdministradorEmail { get; } = $"admin-{Guid.NewGuid():N}@bfa.test";

    public string AdministradorSenha { get; } = "Senha.Admin!123";

    public Guid AdministradorId { get; } = Guid.NewGuid();

    public TestAcessoUsuarioConsulta Acessos =>
        Services.GetRequiredService<TestAcessoUsuarioConsulta>();

    public TestIbgeLocalidadesClient IbgeClient =>
        Services.GetRequiredService<TestIbgeLocalidadesClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<BfaDbContext>>();
            services.RemoveAll<DbContextOptions<BfaDbContext>>();
            services.RemoveAll<BfaDbContext>();
            services.RemoveAll<IAcessoUsuarioConsulta>();
            services.RemoveAll<IIbgeLocalidadesClient>();
            services.AddDbContext<BfaDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(warnings => warnings.Ignore(
                        InMemoryEventId.TransactionIgnoredWarning)));
            services.AddSingleton<TestAcessoUsuarioConsulta>();
            services.AddSingleton<IAcessoUsuarioConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<TestAcessoUsuarioConsulta>());
            services.AddSingleton<TestIbgeLocalidadesClient>();
            services.AddSingleton<IIbgeLocalidadesClient>(serviceProvider =>
                serviceProvider.GetRequiredService<TestIbgeLocalidadesClient>());
        });
    }

    public async Task<Guid> InicializarAdministradorAsync(
        PerfilAcesso perfil = PerfilAcesso.AdministradorRede,
        Guid? organizacaoId = null,
        bool incluirCatalogoLocalidades = true)
    {
        var organizacaoAtualId = organizacaoId ?? Guid.NewGuid();
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();

        if (await userManager.FindByIdAsync(AdministradorId.ToString()) is null)
        {
            var usuario = new UsuarioIdentity
            {
                Id = AdministradorId,
                UserName = AdministradorEmail,
                Email = AdministradorEmail
            };
            var identityResult = await userManager.CreateAsync(usuario, AdministradorSenha);
            Assert.True(
                identityResult.Succeeded,
                string.Join(", ", identityResult.Errors.Select(erro => erro.Code)));
        }

        if (!await dbContext.Organizacoes.AnyAsync(item => item.Id == organizacaoAtualId))
        {
            dbContext.Organizacoes.Add(new Organizacao(
                organizacaoAtualId,
                "Organização BFA",
                $"bfa-{organizacaoAtualId:N}",
                DateTime.UtcNow));
        }

        Guid? unidadeId = null;

        if (perfil != PerfilAcesso.AdministradorRede)
        {
            unidadeId = Guid.NewGuid();
            dbContext.Unidades.Add(new Unidade(
                unidadeId.Value,
                organizacaoAtualId,
                "Unidade administrativa",
                $"unidade-{unidadeId:N}",
                DateTime.UtcNow));
        }

        dbContext.VinculosAcesso.Add(new VinculoAcesso(
            Guid.NewGuid(),
            AdministradorId,
            organizacaoAtualId,
            unidadeId,
            perfil,
            DateTime.UtcNow));

        if (incluirCatalogoLocalidades
            && !await dbContext.Estados.AnyAsync())
        {
            var agoraUtc = DateTime.UtcNow;
            dbContext.Estados.Add(new Estado(
                EstadoPadraoCodigoIbge,
                "SP",
                "São Paulo",
                agoraUtc));
            dbContext.Municipios.Add(new Municipio(
                MunicipioPadraoCodigoIbge,
                EstadoPadraoCodigoIbge,
                "Tietê",
                agoraUtc));
        }

        await dbContext.SaveChangesAsync();

        Acessos.Limpar();
        Acessos.Adicionar(
            AdministradorId,
            organizacaoAtualId,
            unidadeId,
            perfil);
        return organizacaoAtualId;
    }

    public sealed class TestIbgeLocalidadesClient : IIbgeLocalidadesClient
    {
        public int Execucoes { get; private set; }

        public Task<IReadOnlyList<EstadoIbgeDados>> ListarEstadosAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Execucoes++;
            throw new InvalidOperationException(
                "O cliente IBGE não pode ser utilizado durante o cadastro.");
        }

        public Task<IReadOnlyList<MunicipioIbgeDados>> ListarMunicipiosAsync(
            string siglaEstado,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Execucoes++;
            throw new InvalidOperationException(
                "O cliente IBGE não pode ser utilizado durante o cadastro.");
        }
    }
}

using BFA.Application.Acessos;
using BFA.Domain.Acessos;
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
    private readonly string _databaseName = $"bfa-usuarios-web-{Guid.NewGuid():N}";

    public string AdministradorEmail { get; } = $"admin-{Guid.NewGuid():N}@bfa.test";

    public string AdministradorSenha { get; } = "Senha.Admin!123";

    public Guid AdministradorId { get; } = Guid.NewGuid();

    public TestAcessoUsuarioConsulta Acessos =>
        Services.GetRequiredService<TestAcessoUsuarioConsulta>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<BfaDbContext>>();
            services.RemoveAll<DbContextOptions<BfaDbContext>>();
            services.RemoveAll<BfaDbContext>();
            services.RemoveAll<IAcessoUsuarioConsulta>();
            services.AddDbContext<BfaDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(warnings => warnings.Ignore(
                        InMemoryEventId.TransactionIgnoredWarning)));
            services.AddSingleton<TestAcessoUsuarioConsulta>();
            services.AddSingleton<IAcessoUsuarioConsulta>(serviceProvider =>
                serviceProvider.GetRequiredService<TestAcessoUsuarioConsulta>());
        });
    }

    public async Task<Guid> InicializarAdministradorAsync(
        PerfilAcesso perfil = PerfilAcesso.AdministradorRede,
        Guid? organizacaoId = null)
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
        await dbContext.SaveChangesAsync();

        Acessos.Limpar();
        Acessos.Adicionar(
            AdministradorId,
            organizacaoAtualId,
            unidadeId,
            perfil);
        return organizacaoAtualId;
    }
}

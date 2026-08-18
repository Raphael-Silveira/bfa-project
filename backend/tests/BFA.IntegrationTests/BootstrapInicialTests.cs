using BFA.Application.Bootstrap;
using BFA.Domain.Acessos;
using BFA.Domain.Organizacoes;
using BFA.Infrastructure.Bootstrap;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed class BootstrapInicialTests
{
    [Fact]
    public async Task Organizacao_inexistente_e_criada_com_dados_iniciais()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IBootstrapInicial>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();

        var resultado = await bootstrap.ExecutarAsync(
            CreateRequest(),
            CancellationToken.None);
        var organizacao = await dbContext.Organizacoes.SingleAsync();

        Assert.True(resultado.OrganizacaoCriada);
        Assert.Equal("Brazilian Footvolley Academy", organizacao.Nome);
        Assert.Equal("bfa", organizacao.Slug);
        Assert.True(organizacao.Ativa);
    }

    [Fact]
    public async Task Organizacao_existente_e_reutilizada_sem_duplicar()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var organizacaoExistente = new Organizacao(
            Guid.NewGuid(),
            "Brazilian Footvolley Academy",
            "bfa",
            DateTime.UtcNow);
        dbContext.Organizacoes.Add(organizacaoExistente);
        await dbContext.SaveChangesAsync();

        var resultado = await scope.ServiceProvider
            .GetRequiredService<IBootstrapInicial>()
            .ExecutarAsync(CreateRequest(), CancellationToken.None);

        Assert.False(resultado.OrganizacaoCriada);
        Assert.Equal(1, await dbContext.Organizacoes.CountAsync());
        Assert.Equal(organizacaoExistente.Id, (await dbContext.Organizacoes.SingleAsync()).Id);
    }

    [Fact]
    public async Task Usuario_inexistente_e_criado_pelo_user_manager()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var request = CreateRequest();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IBootstrapInicial>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();

        var resultado = await bootstrap.ExecutarAsync(request, CancellationToken.None);
        var usuario = await userManager.FindByEmailAsync(request.Administrador1.Email);

        Assert.True(resultado.Administradores.Single(item => item.Numero == 1).UsuarioCriado);
        Assert.NotNull(usuario);
        Assert.True(await userManager.CheckPasswordAsync(usuario, request.Administrador1.Senha));
    }

    [Fact]
    public async Task Usuario_existente_e_reutilizado_sem_duplicar()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var request = CreateRequest();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();
        var usuarioExistente = new UsuarioIdentity
        {
            Id = Guid.NewGuid(),
            UserName = request.Administrador1.Email,
            Email = request.Administrador1.Email
        };
        var criacao = await userManager.CreateAsync(
            usuarioExistente,
            request.Administrador1.Senha);
        Assert.True(criacao.Succeeded);

        var resultado = await scope.ServiceProvider
            .GetRequiredService<IBootstrapInicial>()
            .ExecutarAsync(request, CancellationToken.None);
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();

        Assert.False(resultado.Administradores.Single(item => item.Numero == 1).UsuarioCriado);
        Assert.Equal(2, await dbContext.Users.CountAsync());
        Assert.Equal(
            usuarioExistente.Id,
            (await userManager.FindByEmailAsync(request.Administrador1.Email))?.Id);
    }

    [Fact]
    public async Task Vinculo_administrador_rede_e_criado_sem_unidade()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var request = CreateRequest();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IBootstrapInicial>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();

        var resultado = await bootstrap.ExecutarAsync(request, CancellationToken.None);
        var usuario = Assert.IsType<UsuarioIdentity>(
            await userManager.FindByEmailAsync(request.Administrador1.Email));
        var vinculo = await dbContext.VinculosAcesso.SingleAsync(item =>
            item.UsuarioId == usuario.Id);

        Assert.True(resultado.Administradores.Single(item => item.Numero == 1).VinculoCriado);
        Assert.Equal(PerfilAcesso.AdministradorRede, vinculo.Perfil);
        Assert.Null(vinculo.UnidadeId);
        Assert.True(vinculo.Ativo);
    }

    [Fact]
    public async Task Segunda_execucao_nao_duplica_organizacao_usuarios_ou_vinculos()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var request = CreateRequest();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IBootstrapInicial>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        await bootstrap.ExecutarAsync(request, CancellationToken.None);

        var segundaExecucao = await bootstrap.ExecutarAsync(request, CancellationToken.None);

        Assert.False(segundaExecucao.OrganizacaoCriada);
        Assert.All(segundaExecucao.Administradores, administrador =>
        {
            Assert.False(administrador.UsuarioCriado);
            Assert.False(administrador.VinculoCriado);
        });
        Assert.Equal(1, await dbContext.Organizacoes.CountAsync());
        Assert.Equal(2, await dbContext.Users.CountAsync());
        Assert.Equal(2, await dbContext.VinculosAcesso.CountAsync());
    }

    [Fact]
    public async Task Duas_contas_administrativas_distintas_sao_criadas()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var request = CreateRequest();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IBootstrapInicial>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();

        var resultado = await bootstrap.ExecutarAsync(request, CancellationToken.None);

        Assert.Equal(2, resultado.Administradores.Count);
        Assert.All(resultado.Administradores, item => Assert.True(item.UsuarioCriado));
        Assert.Equal(2, await dbContext.Users.Select(usuario => usuario.Id).Distinct().CountAsync());
        Assert.Equal(2, await dbContext.VinculosAcesso.Select(vinculo => vinculo.UsuarioId)
            .Distinct()
            .CountAsync());
    }

    [Fact]
    public async Task Vinculo_equivalente_inativo_causa_erro_claro_sem_reativacao()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var request = CreateRequest();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IBootstrapInicial>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentity>>();
        await bootstrap.ExecutarAsync(request, CancellationToken.None);
        var usuario = Assert.IsType<UsuarioIdentity>(
            await userManager.FindByEmailAsync(request.Administrador1.Email));
        var vinculo = await dbContext.VinculosAcesso.SingleAsync(item =>
            item.UsuarioId == usuario.Id);
        dbContext.Entry(vinculo).Property(item => item.Ativo).CurrentValue = false;
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BootstrapInicialException>(() =>
            bootstrap.ExecutarAsync(request, CancellationToken.None));

        Assert.Contains("vínculo AdministradorRede inativo", exception.Message);
        Assert.False(vinculo.Ativo);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<BfaDbContext>(options => options
            .UseInMemoryDatabase($"bfa-bootstrap-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddIdentityCore<UsuarioIdentity>()
            .AddEntityFrameworkStores<BfaDbContext>();
        services.AddScoped<IBootstrapInicial, BootstrapInicial>();

        return services.BuildServiceProvider();
    }

    private static BootstrapInicialSolicitacao CreateRequest()
    {
        return new BootstrapInicialSolicitacao(
            new CredenciaisAdministradorBootstrap(CreateEmail(), CreatePassword()),
            new CredenciaisAdministradorBootstrap(CreateEmail(), CreatePassword()));
    }

    private static string CreateEmail()
    {
        return $"bootstrap-{Guid.NewGuid():N}@example.invalid";
    }

    private static string CreatePassword()
    {
        return $"Aa1!{Guid.NewGuid():N}";
    }
}

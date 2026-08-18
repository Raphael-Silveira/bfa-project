using System.Security.Claims;
using BFA.Domain.Acessos;
using BFA.Web.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace BFA.IntegrationTests;

public sealed class AuthorizationHandlerTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Administrador_rede_requirement_respeita_estado_do_vinculo(
        bool ativo,
        bool esperado)
    {
        var fixture = CreateFixture();
        fixture.Acessos.Adicionar(
            fixture.UsuarioId,
            fixture.OrganizacaoId,
            null,
            PerfilAcesso.AdministradorRede,
            ativo);
        var requirement = new AdministradorRedeRequirement();
        var context = CreateContext(requirement);
        var handler = new AdministradorRedeHandler(fixture.UsuarioAtual, fixture.Acessos);

        await handler.HandleAsync(context);

        Assert.Equal(esperado, context.HasSucceeded);
    }

    [Fact]
    public async Task Administrador_unidade_autoriza_unidade_correta_mas_nao_outra()
    {
        var fixture = CreateFixture();
        fixture.Acessos.Adicionar(
            fixture.UsuarioId,
            fixture.OrganizacaoId,
            fixture.UnidadeId,
            PerfilAcesso.AdministradorUnidade);
        var requirement = new AcessoUnidadeRequirement();
        var handler = new AcessoUnidadeHandler(fixture.UsuarioAtual, fixture.Acessos);
        var contextCorreto = CreateContext(
            requirement,
            new ContextoUnidade(fixture.OrganizacaoId, fixture.UnidadeId));
        var contextOutraUnidade = CreateContext(
            requirement,
            new ContextoUnidade(fixture.OrganizacaoId, Guid.NewGuid()));

        await handler.HandleAsync(contextCorreto);
        await handler.HandleAsync(contextOutraUnidade);

        Assert.True(contextCorreto.HasSucceeded);
        Assert.False(contextOutraUnidade.HasSucceeded);
    }

    [Fact]
    public async Task Administrador_rede_autoriza_unidades_da_propria_organizacao_apenas()
    {
        var fixture = CreateFixture();
        fixture.Acessos.Adicionar(
            fixture.UsuarioId,
            fixture.OrganizacaoId,
            null,
            PerfilAcesso.AdministradorRede);
        var requirement = new AcessoUnidadeRequirement();
        var handler = new AcessoUnidadeHandler(fixture.UsuarioAtual, fixture.Acessos);
        var contextOrganizacaoCorreta = CreateContext(
            requirement,
            new ContextoUnidade(fixture.OrganizacaoId, Guid.NewGuid()));
        var contextOutraOrganizacao = CreateContext(
            requirement,
            new ContextoUnidade(Guid.NewGuid(), Guid.NewGuid()));

        await handler.HandleAsync(contextOrganizacaoCorreta);
        await handler.HandleAsync(contextOutraOrganizacao);

        Assert.True(contextOrganizacaoCorreta.HasSucceeded);
        Assert.False(contextOutraOrganizacao.HasSucceeded);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Policy_professor_respeita_estado_do_vinculo(bool ativo, bool esperado)
    {
        var fixture = CreateFixture();
        fixture.Acessos.Adicionar(
            fixture.UsuarioId,
            fixture.OrganizacaoId,
            fixture.UnidadeId,
            PerfilAcesso.Professor,
            ativo);
        var requirement = new PerfilAcessoRequirement(PerfilAcesso.Professor);
        var context = CreateContext(requirement);
        var handler = new PerfilAcessoHandler(fixture.UsuarioAtual, fixture.Acessos);

        await handler.HandleAsync(context);

        Assert.Equal(esperado, context.HasSucceeded);
    }

    [Theory]
    [InlineData(PerfilAcesso.Aluno)]
    [InlineData(PerfilAcesso.Responsavel)]
    public async Task Policies_de_experiencia_autorizam_perfil_ativo(PerfilAcesso perfil)
    {
        var fixture = CreateFixture();
        fixture.Acessos.Adicionar(
            fixture.UsuarioId,
            fixture.OrganizacaoId,
            fixture.UnidadeId,
            perfil);
        var requirement = new PerfilAcessoRequirement(perfil);
        var context = CreateContext(requirement);
        var handler = new PerfilAcessoHandler(fixture.UsuarioAtual, fixture.Acessos);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Usuario_autenticado_sem_vinculo_nao_e_autorizado()
    {
        var fixture = CreateFixture();
        var requirement = new PerfilAcessoRequirement(PerfilAcesso.Aluno);
        var context = CreateContext(requirement);
        var handler = new PerfilAcessoHandler(fixture.UsuarioAtual, fixture.Acessos);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Administracao_aceita_administrador_de_unidade()
    {
        var fixture = CreateFixture();
        fixture.Acessos.Adicionar(
            fixture.UsuarioId,
            fixture.OrganizacaoId,
            fixture.UnidadeId,
            PerfilAcesso.AdministradorUnidade);
        var requirement = new PerfilAcessoRequirement(
            PerfilAcesso.AdministradorRede,
            PerfilAcesso.AdministradorUnidade);
        var context = CreateContext(requirement);
        var handler = new PerfilAcessoHandler(fixture.UsuarioAtual, fixture.Acessos);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Acesso_por_perfil_exige_unidade_e_perfil_corretos()
    {
        var fixture = CreateFixture();
        fixture.Acessos.Adicionar(
            fixture.UsuarioId,
            fixture.OrganizacaoId,
            fixture.UnidadeId,
            PerfilAcesso.Professor);
        var requirement = new AcessoUnidadePorPerfilRequirement(PerfilAcesso.Professor);
        var handler = new AcessoUnidadePorPerfilHandler(fixture.UsuarioAtual, fixture.Acessos);
        var contextCorreto = CreateContext(
            requirement,
            new ContextoUnidade(fixture.OrganizacaoId, fixture.UnidadeId));
        var contextOutraUnidade = CreateContext(
            requirement,
            new ContextoUnidade(fixture.OrganizacaoId, Guid.NewGuid()));

        await handler.HandleAsync(contextCorreto);
        await handler.HandleAsync(contextOutraUnidade);

        Assert.True(contextCorreto.HasSucceeded);
        Assert.False(contextOutraUnidade.HasSucceeded);
    }

    [Fact]
    public async Task Administrador_rede_tem_superacesso_por_perfil_na_propria_organizacao()
    {
        var fixture = CreateFixture();
        fixture.Acessos.Adicionar(
            fixture.UsuarioId,
            fixture.OrganizacaoId,
            null,
            PerfilAcesso.AdministradorRede);
        var requirement = new AcessoUnidadePorPerfilRequirement(PerfilAcesso.Professor);
        var handler = new AcessoUnidadePorPerfilHandler(fixture.UsuarioAtual, fixture.Acessos);
        var context = CreateContext(
            requirement,
            new ContextoUnidade(fixture.OrganizacaoId, Guid.NewGuid()));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext CreateContext(
        IAuthorizationRequirement requirement,
        object? resource = null)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("Test"));
        return new AuthorizationHandlerContext([requirement], principal, resource);
    }

    private static AuthorizationFixture CreateFixture()
    {
        var usuarioId = Guid.NewGuid();

        return new AuthorizationFixture(
            usuarioId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new TestUsuarioAtual
            {
                Autenticado = true,
                UsuarioId = usuarioId
            },
            new TestAcessoUsuarioConsulta());
    }

    private sealed record AuthorizationFixture(
        Guid UsuarioId,
        Guid OrganizacaoId,
        Guid UnidadeId,
        TestUsuarioAtual UsuarioAtual,
        TestAcessoUsuarioConsulta Acessos);
}

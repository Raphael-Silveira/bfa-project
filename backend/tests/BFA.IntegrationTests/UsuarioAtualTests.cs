using System.Security.Claims;
using BFA.Web.Acessos;
using Microsoft.AspNetCore.Http;

namespace BFA.IntegrationTests;

public sealed class UsuarioAtualTests
{
    [Fact]
    public void Usuario_nao_autenticado_nao_expoe_identificador()
    {
        var usuarioId = Guid.NewGuid();
        var usuarioAtual = CreateUsuarioAtual(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString())]));

        Assert.False(usuarioAtual.Autenticado);
        Assert.Null(usuarioAtual.UsuarioId);
    }

    [Fact]
    public void Usuario_autenticado_expoe_identificador_guid_do_identity()
    {
        var usuarioId = Guid.NewGuid();
        var usuarioAtual = CreateUsuarioAtual(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString())],
            authenticationType: "Test"));

        Assert.True(usuarioAtual.Autenticado);
        Assert.Equal(usuarioId, usuarioAtual.UsuarioId);
    }

    [Fact]
    public void Identificador_invalido_retorna_null()
    {
        var usuarioAtual = CreateUsuarioAtual(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "identificador-invalido")],
            authenticationType: "Test"));

        Assert.True(usuarioAtual.Autenticado);
        Assert.Null(usuarioAtual.UsuarioId);
    }

    private static UsuarioAtual CreateUsuarioAtual(ClaimsIdentity identity)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        return new UsuarioAtual(new HttpContextAccessor { HttpContext = context });
    }
}

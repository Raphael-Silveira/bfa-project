using BFA.Infrastructure.Persistence;
using BFA.Web.Areas.Franqueadora.Controllers;
using BFA.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.IntegrationTests;

public sealed class UsuariosControllerArchitectureTests
{
    [Fact]
    public void Controller_exige_administrador_rede_e_nao_depende_do_db_context()
    {
        var tipo = typeof(UsuariosController);
        var authorize = Assert.Single(tipo
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        var dependencias = tipo
            .GetConstructors()
            .SelectMany(construtor => construtor.GetParameters())
            .Select(parametro => parametro.ParameterType)
            .ToArray();

        Assert.Equal(PoliticasAcesso.AdministradorRede, authorize.Policy);
        Assert.DoesNotContain(typeof(BfaDbContext), dependencias);
    }

    [Fact]
    public void Post_novo_exige_antiforgery()
    {
        var metodo = typeof(UsuariosController)
            .GetMethods()
            .Single(candidato => candidato.Name == nameof(UsuariosController.Novo)
                && candidato.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).Any());

        Assert.NotEmpty(metodo.GetCustomAttributes(
            typeof(ValidateAntiForgeryTokenAttribute),
            inherit: true));
    }
}

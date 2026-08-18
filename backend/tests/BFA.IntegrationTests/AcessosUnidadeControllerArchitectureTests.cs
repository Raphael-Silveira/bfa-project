using BFA.Infrastructure.Persistence;
using BFA.Web.Areas.Franqueadora.Controllers;
using BFA.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.IntegrationTests;

public sealed class AcessosUnidadeControllerArchitectureTests
{
    [Fact]
    public void Controller_exige_administrador_rede_e_nao_depende_do_db_context()
    {
        var tipo = typeof(AcessosUnidadeController);
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

    [Theory]
    [InlineData(nameof(AcessosUnidadeController.Adicionar), 3)]
    [InlineData(nameof(AcessosUnidadeController.Ativar), 3)]
    [InlineData(nameof(AcessosUnidadeController.Desativar), 3)]
    public void Acoes_post_exigem_antiforgery(string actionName, int parameterCount)
    {
        var method = typeof(AcessosUnidadeController)
            .GetMethods()
            .Single(candidate => candidate.Name == actionName
                && candidate.GetParameters().Length == parameterCount
                && candidate.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).Any());

        Assert.NotEmpty(method.GetCustomAttributes(
            typeof(ValidateAntiForgeryTokenAttribute),
            inherit: true));
    }

    [Fact]
    public void Controller_nao_expoe_delete_fisico()
    {
        Assert.DoesNotContain(
            typeof(AcessosUnidadeController).GetMethods(),
            method => method.Name.Contains("Excluir", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }
}

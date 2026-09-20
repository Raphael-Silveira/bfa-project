using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;

namespace BFA.IntegrationTests;

public sealed class DayUsesControllerSecurityTests
{
    [Theory]
    [InlineData(typeof(BFA.Web.Areas.Unidade.Controllers.DayUsesController))]
    [InlineData(typeof(BFA.Web.Areas.Professor.Controllers.DayUsesController))]
    public void Excluir_e_post_com_antiforgery_e_rota_scopada_por_unidade(Type controllerType)
    {
        var method = controllerType.GetMethod("Excluir", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal("{dayUseId:guid}/excluir", method.GetCustomAttribute<HttpPostAttribute>()!.Template);
        var prefixoEsperado = controllerType.Namespace!.Contains("Areas.Unidade", StringComparison.Ordinal)
            ? "unidade/{unidadeId:guid}/day-use"
            : "professor/unidade/{unidadeId:guid}/day-use";
        Assert.Equal(prefixoEsperado, controllerType.GetCustomAttribute<RouteAttribute>()?.Template);
    }
}

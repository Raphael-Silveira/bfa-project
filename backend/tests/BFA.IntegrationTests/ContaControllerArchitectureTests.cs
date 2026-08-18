using BFA.Application.Acessos;
using BFA.Infrastructure.Persistence;
using BFA.Web.Controllers;

namespace BFA.IntegrationTests;

public sealed class ContaControllerArchitectureTests
{
    [Fact]
    public void Conta_controller_depende_do_destino_tipado_e_nao_do_db_context()
    {
        var tipoController = typeof(ContaController);
        var dependenciasConstrutor = tipoController
            .GetConstructors()
            .SelectMany(construtor => construtor.GetParameters())
            .Select(parametro => parametro.ParameterType)
            .ToArray();
        var tiposCampos = tipoController
            .GetFields(System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public)
            .Select(campo => campo.FieldType)
            .ToArray();

        Assert.Contains(typeof(IDestinoPosLogin), dependenciasConstrutor);
        Assert.Contains(typeof(IUsuarioAtual), dependenciasConstrutor);
        Assert.DoesNotContain(typeof(BfaDbContext), dependenciasConstrutor);
        Assert.DoesNotContain(typeof(BfaDbContext), tiposCampos);
    }
}

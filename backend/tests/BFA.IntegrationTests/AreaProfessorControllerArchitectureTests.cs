using BFA.Application.Professores.Turmas;
using BFA.Infrastructure.Persistence;
using BFA.Web.Areas.Professor.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace BFA.IntegrationTests;

public sealed class AreaProfessorControllerArchitectureTests
{
    [Fact]
    public void Controllers_nao_acessam_db_context_diretamente()
    {
        var controllers = new[] { typeof(InicioController), typeof(TurmasController) };

        foreach (var controller in controllers)
        {
            var dependencias = controller
                .GetConstructors()
                .SelectMany(construtor => construtor.GetParameters())
                .Select(parametro => parametro.ParameterType)
                .ToArray();

            Assert.DoesNotContain(typeof(BfaDbContext), dependencias);
        }
    }

    [Fact]
    public void Turmas_usa_consulta_application_e_autorizacao_por_recurso()
    {
        var dependencias = typeof(TurmasController)
            .GetConstructors()
            .SelectMany(construtor => construtor.GetParameters())
            .Select(parametro => parametro.ParameterType)
            .ToArray();

        Assert.Contains(typeof(IMinhasTurmasProfessorConsulta), dependencias);
        Assert.Contains(typeof(IAuthorizationService), dependencias);
        Assert.DoesNotContain(typeof(
            BFA.Application.Unidades.Turmas.IAjusteHorariosTurmaServico), dependencias);
        Assert.DoesNotContain(typeof(BfaDbContext), dependencias);
    }
}

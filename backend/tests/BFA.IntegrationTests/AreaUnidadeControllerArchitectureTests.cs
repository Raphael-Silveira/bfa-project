using BFA.Application.Unidades;
using BFA.Application.Unidades.Contratos;
using BFA.Application.Usuarios;
using BFA.Infrastructure.Persistence;
using BFA.Web.Areas.Unidade.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.IntegrationTests;

public sealed class AreaUnidadeControllerArchitectureTests
{
    [Fact]
    public void Controllers_nao_acessam_db_context_diretamente()
    {
        var controllers = new[]
        {
            typeof(InicioController),
            typeof(ContratoController),
            typeof(BFA.Web.Controllers.SelecaoUnidadeController)
        };

        foreach (var controller in controllers)
        {
            var dependencias = controller
                .GetConstructors()
                .SelectMany(construtor => construtor.GetParameters())
                .Select(parametro => parametro.ParameterType)
                .ToArray();
            var campos = controller
                .GetFields(System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public)
                .Select(campo => campo.FieldType)
                .ToArray();

            Assert.DoesNotContain(typeof(BfaDbContext), dependencias);
            Assert.DoesNotContain(typeof(BfaDbContext), campos);
        }
    }

    [Fact]
    public void Contrato_da_unidade_usa_consulta_application_e_autorizacao_por_recurso()
    {
        var dependencias = typeof(ContratoController)
            .GetConstructors()
            .SelectMany(construtor => construtor.GetParameters())
            .Select(parametro => parametro.ParameterType)
            .ToArray();

        Assert.Contains(typeof(IContratoUnidadeConsulta), dependencias);
        Assert.Contains(typeof(IUnidadeContextoConsulta), dependencias);
        Assert.Contains(typeof(IAuthorizationService), dependencias);
        Assert.DoesNotContain(typeof(BfaDbContext), dependencias);
    }

    [Fact]
    public void Area_unidade_usa_consultas_application_e_autorizacao_por_recurso()
    {
        var dependencias = typeof(InicioController)
            .GetConstructors()
            .SelectMany(construtor => construtor.GetParameters())
            .Select(parametro => parametro.ParameterType)
            .ToArray();

        Assert.Contains(typeof(IUnidadeContextoConsulta), dependencias);
        Assert.Contains(typeof(IUnidadesUsuarioConsulta), dependencias);
        Assert.Contains(typeof(IAuthorizationService), dependencias);
    }

    [Fact]
    public void Selecao_post_exige_antiforgery()
    {
        var metodo = typeof(BFA.Web.Controllers.SelecaoUnidadeController)
            .GetMethod(nameof(BFA.Web.Controllers.SelecaoUnidadeController.Selecionar));

        Assert.NotNull(metodo);
        Assert.NotNull(metodo.GetCustomAttributes(
            typeof(ValidateAntiForgeryTokenAttribute),
            inherit: true).SingleOrDefault());
        Assert.NotNull(metodo.GetCustomAttributes(
            typeof(HttpPostAttribute),
            inherit: true).SingleOrDefault());
    }

    [Fact]
    public void Selecao_usa_consulta_de_apresentacao_sem_acessar_persistencia()
    {
        var dependencias = typeof(BFA.Web.Controllers.SelecaoUnidadeController)
            .GetConstructors()
            .SelectMany(construtor => construtor.GetParameters())
            .Select(parametro => parametro.ParameterType)
            .ToArray();

        Assert.Contains(typeof(IUsuarioApresentacaoConsulta), dependencias);
        Assert.DoesNotContain(typeof(BfaDbContext), dependencias);
    }
}

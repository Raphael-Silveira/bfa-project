using BFA.Application.Localidades;
using BFA.Infrastructure.Persistence;
using BFA.Web.Areas.Franqueadora.Controllers;
using BFA.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BFA.IntegrationTests;

public sealed class LocalidadesControllerArchitectureTests
{
    [Fact]
    public void Controller_usa_consulta_application_sem_dbcontext_ou_cliente_ibge()
    {
        var tipo = typeof(LocalidadesController);
        var authorize = Assert.Single(tipo
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        var dependencias = tipo.GetConstructors()
            .SelectMany(construtor => construtor.GetParameters())
            .Select(parametro => parametro.ParameterType)
            .ToArray();

        Assert.Equal(PoliticasAcesso.AdministradorRede, authorize.Policy);
        Assert.Contains(typeof(ILocalidadesConsulta), dependencias);
        Assert.DoesNotContain(typeof(BfaDbContext), dependencias);
        Assert.DoesNotContain(typeof(IIbgeLocalidadesClient), dependencias);
    }

    [Fact]
    public async Task Request_aborted_e_tratado_como_cancelamento_esperado_sem_erro_500()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var controller = CriarController(
            new LocalidadesConsultaCancelavel(),
            cancellationTokenSource.Token);

        var resultado = await controller.Municipios(
            35,
            cancellationTokenSource.Token);

        Assert.IsType<EmptyResult>(resultado);
    }

    [Fact]
    public async Task Cancelamento_sem_request_aborted_continua_visivel_para_diagnostico()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var controller = CriarController(
            new LocalidadesConsultaCancelavel(),
            CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            controller.Municipios(35, cancellationTokenSource.Token));
    }

    private static LocalidadesController CriarController(
        ILocalidadesConsulta consulta,
        CancellationToken requestAborted)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestAborted = requestAborted,
        };

        return new LocalidadesController(consulta)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
        };
    }

    private sealed class LocalidadesConsultaCancelavel : ILocalidadesConsulta
    {
        public Task<IReadOnlyList<EstadoLocalidadeResumo>> ListarEstadosAtivosAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<EstadoLocalidadeResumo>>([]);
        }

        public Task<IReadOnlyList<MunicipioLocalidadeResumo>> ListarMunicipiosAtivosAsync(
            int estadoCodigoIbge,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<MunicipioLocalidadeResumo>>([]);
        }
    }
}

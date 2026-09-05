using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Application.Unidades.Contratos;
using BFA.Domain.Acessos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[ServiceFilter(typeof(GovernancaOperacionalUnidadeResultFilter))]
[Route("unidade/{unidadeId:guid}")]
public sealed class InicioController(
    IUsuarioAtual usuarioAtual,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IContratoUnidadeConsulta contratoUnidadeConsulta,
    IUnidadeDashboardConsulta unidadeDashboardConsulta,
    IAuthorizationService authorizationService,
    ILogger<InicioController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId,
            cancellationToken);

        if (unidade is null)
        {
            return NotFound();
        }

        var autorizacao = await authorizationService.AuthorizeAsync(
            User,
            new ContextoUnidade(unidade.OrganizacaoId, unidade.UnidadeId),
            new AcessoUnidadePorPerfilRequirement(PerfilAcesso.AdministradorUnidade));

        if (!autorizacao.Succeeded)
        {
            return Forbid();
        }

        var unidadesAdministradas = await unidadesUsuarioConsulta.ListarAdministradasAsync(
            usuarioId,
            cancellationToken);
        var contrato = await contratoUnidadeConsulta.ObterAtivoAsync(
            usuarioId,
            unidadeId,
            cancellationToken);

        if (contrato.Estado == EstadoConsultaContratoUnidade.SemAcesso)
        {
            return Forbid();
        }

        var metricas = await unidadeDashboardConsulta.ObterMetricasAsync(
            unidadeId,
            cancellationToken);

        return View(new PainelUnidadeViewModel
        {
            OrganizacaoId = unidade.OrganizacaoId,
            UnidadeId = unidade.UnidadeId,
            NomeUnidade = unidade.Nome,
            PodeTrocarUnidade = unidadesAdministradas.Count > 1,
            Contrato = ContratoUnidadeViewModelMapper.Mapear(contrato.Valor?.Contrato),
            TotalAlunosAtivos = metricas?.TotalAlunosAtivos ?? 0,
            TotalTurmasAtivas = metricas?.TotalTurmasAtivas ?? 0,
            TotalAulasSemana = metricas?.TotalAulasSemana ?? 0,
            FrequenciaMedia = metricas is { PercentualFrequencia: var freq }
                ? $"{freq:N1}%"
                : "-",
            ReceitaMes = metricas is { ReceitaMes: var receita }
                ? receita.ToString("C", CulturaPtBr)
                : "R$ 0,00",
            Pendente = metricas is { Pendente: var pendente }
                ? pendente.ToString("C", CulturaPtBr)
                : "R$ 0,00",
            EmAtraso = metricas is { EmAtraso: var atraso }
                ? atraso.ToString("C", CulturaPtBr)
                : "R$ 0,00",
            AulasHoje = metricas?.AulasHoje
                .Select(a => new AulaHojeViewModel(
                    a.Horario,
                    a.TurmaNome,
                    a.ProfessorNome,
                    a.Inscritos,
                    a.Capacidade,
                    a.Status,
                    MapearDotClass(a.Inscritos, a.Capacidade)))
                .ToList() ?? [],
            AtividadesRecentes = metricas?.AtividadesRecentes
                .Select(a => new AtividadeRecenteViewModel(
                    a.IconeTipo,
                    a.Titulo,
                    a.Subtitulo,
                    a.TempoRelativo))
                .ToList() ?? []
        });
    }

    private static string MapearDotClass(int inscritos, int capacidade) =>
        capacidade == 0 ? "bfa-unidade-lesson-dot--gold"
        : (double)inscritos / capacidade >= 0.9 ? "bfa-unidade-lesson-dot--gold"
        : (double)inscritos / capacidade >= 0.5 ? "bfa-unidade-lesson-dot--blue"
        : "bfa-unidade-lesson-dot--green";

    private static readonly System.Globalization.CultureInfo CulturaPtBr =
        System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
}

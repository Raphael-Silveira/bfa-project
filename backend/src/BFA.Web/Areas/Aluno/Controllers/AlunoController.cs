using BFA.Application.AlunoArea;
using BFA.Application.Unidades;
using BFA.Web.ViewModels.AlunoArea;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Aluno.Controllers;

[Area("Aluno")]
[Authorize]
public sealed class AlunoController(
    IAlunoAreaServico alunoAreaServico,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    ILogger<AlunoController> logger)
    : Controller
{
    [HttpGet("aluno/{unidadeId:guid}")]
    public async Task<IActionResult> Dashboard(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Dashboard Aluno iniciado para unidade {UnidadeId}", unidadeId);

        var usuarioId = ObterUsuarioId();
        if (usuarioId is null)
        {
            return Forbid();
        }

        var unidade = await unidadesUsuarioConsulta.ObterAlunoAsync(
            usuarioId.Value, unidadeId, cancellationToken);

        if (unidade is null)
        {
            logger.LogWarning("Acesso negado: {UsuarioId} não é aluno na unidade {UnidadeId}",
                usuarioId.Value, unidadeId);
            return Forbid();
        }

        var dashboard = await alunoAreaServico.ObterDashboardAsync(
            usuarioId.Value, unidadeId, cancellationToken);

        if (dashboard is null)
        {
            return NotFound();
        }

        var viewModel = DashboardAlunoViewModel.Mapear(dashboard, unidadeId);
        return View(viewModel);
    }

    [HttpGet("aluno/{unidadeId:guid}/perfil")]
    public async Task<IActionResult> Perfil(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null) return Forbid();

        var unidade = await unidadesUsuarioConsulta.ObterAlunoAsync(
            usuarioId.Value, unidadeId, cancellationToken);
        if (unidade is null) return Forbid();

        var perfil = await alunoAreaServico.ObterPerfilAsync(
            usuarioId.Value, unidadeId, cancellationToken);

        if (perfil is null) return NotFound();

        return View(PerfilAlunoViewModel.Mapear(perfil));
    }

    [HttpGet("aluno/{unidadeId:guid}/matriculas")]
    public async Task<IActionResult> Matriculas(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null) return Forbid();

        var unidade = await unidadesUsuarioConsulta.ObterAlunoAsync(
            usuarioId.Value, unidadeId, cancellationToken);
        if (unidade is null) return Forbid();

        var matriculas = await alunoAreaServico.ObterMatriculasAsync(
            usuarioId.Value, unidadeId, cancellationToken);

        var viewModel = matriculas
            .Select(MatriculaAlunoViewModel.Mapear)
            .ToList();

        return View(viewModel);
    }

    [HttpGet("aluno/{unidadeId:guid}/agenda")]
    public async Task<IActionResult> Agenda(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null) return Forbid();

        var unidade = await unidadesUsuarioConsulta.ObterAlunoAsync(
            usuarioId.Value, unidadeId, cancellationToken);
        if (unidade is null) return Forbid();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var dataInicio = hoje.AddDays(-7);
        var dataFim = hoje.AddDays(30);

        var aulas = await alunoAreaServico.ObterAgendaAsync(
            usuarioId.Value, unidadeId, dataInicio, dataFim, cancellationToken);

        var viewModel = aulas
            .Select(AulaAlunoViewModel.Mapear)
            .ToList();

        return View(viewModel);
    }

    [HttpGet("aluno/{unidadeId:guid}/frequencia")]
    public async Task<IActionResult> Frequencia(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null) return Forbid();

        var unidade = await unidadesUsuarioConsulta.ObterAlunoAsync(
            usuarioId.Value, unidadeId, cancellationToken);
        if (unidade is null) return Forbid();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var dataInicio = new DateOnly(hoje.Year, hoje.Month, 1);
        var dataFim = hoje;

        var frequencia = await alunoAreaServico.ObterFrequenciaAsync(
            usuarioId.Value, unidadeId, dataInicio, dataFim, cancellationToken);

        if (frequencia is null) return NotFound();

        var presencas = await alunoAreaServico.ObterAgendaAsync(
            usuarioId.Value, unidadeId, dataInicio, dataFim, cancellationToken);

        var presencasDto = presencas.Select(a => new PresencaAlunoDto(
            a.Data, a.TurmaNome, a.HoraInicio, a.HoraFim, a.Status, null)).ToList();

        var viewModel = FrequenciaResumoAlunoViewModel.Mapear(
            frequencia, presencasDto, dataInicio, dataFim);

        return View(viewModel);
    }

    [HttpGet("aluno/{unidadeId:guid}/financeiro")]
    public async Task<IActionResult> Financeiro(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null) return Forbid();

        var unidade = await unidadesUsuarioConsulta.ObterAlunoAsync(
            usuarioId.Value, unidadeId, cancellationToken);
        if (unidade is null) return Forbid();

        var financeiro = await alunoAreaServico.ObterFinanceiroAsync(
            usuarioId.Value, unidadeId, cancellationToken);

        if (financeiro is null) return NotFound();

        return View(FinanceiroAlunoViewModel.Mapear(financeiro));
    }

    private Guid? ObterUsuarioId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}

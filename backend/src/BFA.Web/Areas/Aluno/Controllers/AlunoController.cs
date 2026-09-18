using BFA.Application.AlunoArea;
using BFA.Application.Unidades;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.AlunoArea;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Aluno.Controllers;

[Area("Aluno")]
[Authorize(Policy = PoliticasAcesso.Aluno)]
public sealed class AlunoController(
    IAlunoAreaServico alunoAreaServico,
    IConfirmacaoAulaAlunoServico confirmacaoAulaAlunoServico,
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
        ViewData["AlunoNome"] = dashboard.Perfil.NomeCompleto;
        return View("/Areas/Aluno/Views/Dashboard.cshtml", viewModel);
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

        ViewData["AlunoNome"] = perfil.NomeCompleto;
        return View("/Areas/Aluno/Views/Perfil.cshtml", PerfilAlunoViewModel.Mapear(perfil));
    }

    [HttpGet("aluno/{unidadeId:guid}/perfil/editar")]
    public async Task<IActionResult> EditarPerfil(
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

        ViewData["AlunoNome"] = perfil.NomeCompleto;
        return View("/Areas/Aluno/Views/EditarPerfil.cshtml",
            EditarPerfilAlunoViewModel.Mapear(perfil));
    }

    [HttpPost("aluno/{unidadeId:guid}/perfil/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarPerfil(
        Guid unidadeId,
        EditarPerfilAlunoViewModel model,
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

        model.AplicarDadosSomenteLeitura(perfil);
        ViewData["AlunoNome"] = perfil.NomeCompleto;

        if (!ModelState.IsValid)
        {
            return View("/Areas/Aluno/Views/EditarPerfil.cshtml", model);
        }

        var resultado = await alunoAreaServico.AtualizarPerfilAsync(
            usuarioId.Value,
            unidadeId,
            model.Telefone,
            model.Email,
            cancellationToken);

        if (resultado == ResultadoAtualizacaoPerfilAluno.EmailInvalido)
        {
            ModelState.AddModelError(nameof(model.Email), "Informe um e-mail válido.");
            return View("/Areas/Aluno/Views/EditarPerfil.cshtml", model);
        }

        if (resultado == ResultadoAtualizacaoPerfilAluno.TelefoneInvalido)
        {
            ModelState.AddModelError(nameof(model.Telefone), "Informe um telefone brasileiro válido com DDD.");
            return View("/Areas/Aluno/Views/EditarPerfil.cshtml", model);
        }

        if (resultado != ResultadoAtualizacaoPerfilAluno.Sucesso)
        {
            return Forbid();
        }

        TempData["Sucesso"] = "Dados cadastrais atualizados com sucesso.";
        return RedirectToAction(nameof(Perfil), new { unidadeId });
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

        await ConfigurarContextoAsync(usuarioId.Value, unidadeId, cancellationToken);
        return View("/Areas/Aluno/Views/Matriculas.cshtml", viewModel);
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

        await ConfigurarContextoAsync(usuarioId.Value, unidadeId, cancellationToken);
        return View("/Areas/Aluno/Views/Agenda.cshtml", viewModel);
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

        var viewModel = FrequenciaResumoAlunoViewModel.Mapear(
            frequencia, dataInicio, dataFim);

        await ConfigurarContextoAsync(usuarioId.Value, unidadeId, cancellationToken);
        return View("/Areas/Aluno/Views/Frequencia.cshtml", viewModel);
    }

    [HttpGet("aluno/{unidadeId:guid}/financeiro")]
    public async Task<IActionResult> Financeiro(
        Guid unidadeId,
        string? periodo,
        string? dataInicio,
        string? dataFim,
        CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null) return Forbid();

        if (!TentarObterPeriodoFinanceiro(periodo, dataInicio, dataFim, out var inicio, out var fim))
            return BadRequest("Informe um período válido para consultar o financeiro.");

        var unidade = await unidadesUsuarioConsulta.ObterAlunoAsync(
            usuarioId.Value, unidadeId, cancellationToken);
        if (unidade is null) return Forbid();

        var financeiro = await alunoAreaServico.ObterFinanceiroAsync(
            usuarioId.Value, unidadeId, inicio, fim, cancellationToken);

        if (financeiro is null) return NotFound();

        await ConfigurarContextoAsync(usuarioId.Value, unidadeId, cancellationToken);
        return View("/Areas/Aluno/Views/Financeiro.cshtml",
            FinanceiroAlunoViewModel.Mapear(financeiro, periodo ?? "todos", inicio, fim));
    }

    private static bool TentarObterPeriodoFinanceiro(
        string? periodo,
        string? dataInicioTexto,
        string? dataFimTexto,
        out DateOnly? dataInicio,
        out DateOnly? dataFim)
    {
        dataInicio = null;
        dataFim = null;
        periodo = string.IsNullOrWhiteSpace(periodo) ? "todos" : periodo.Trim().ToLowerInvariant();

        if (periodo == "todos") return true;

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        if (periodo == "hoje")
        {
            dataInicio = hoje;
            dataFim = hoje;
            return true;
        }

        if (periodo == "mes")
        {
            dataInicio = new DateOnly(hoje.Year, hoje.Month, 1);
            dataFim = dataInicio.Value.AddMonths(1).AddDays(-1);
            return true;
        }

        if (periodo != "personalizado"
            || !DateOnly.TryParseExact(dataInicioTexto, "dd/MM/yyyy", out var inicio)
            || !DateOnly.TryParseExact(dataFimTexto, "dd/MM/yyyy", out var fim)
            || inicio > fim)
        {
            return false;
        }

        dataInicio = inicio;
        dataFim = fim;
        return true;
    }

    [HttpPost("aluno/{unidadeId:guid}/aulas/{aulaId:guid}/confirmar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarParticipacao(
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null) return Forbid();

        var resultado = await confirmacaoAulaAlunoServico.ConfirmarAsync(
            usuarioId.Value, unidadeId, aulaId, cancellationToken);

        if (resultado != ResultadoConfirmacaoAula.Sucesso)
            TempData["AlunoAviso"] = "Não foi possível confirmar esta participação.";

        return RedirectToAction(nameof(Agenda), new { unidadeId });
    }

    [HttpPost("aluno/{unidadeId:guid}/aulas/{aulaId:guid}/cancelar-confirmacao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarConfirmacao(
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null) return Forbid();

        var resultado = await confirmacaoAulaAlunoServico.CancelarAsync(
            usuarioId.Value, unidadeId, aulaId, cancellationToken);

        if (resultado != ResultadoConfirmacaoAula.Sucesso)
            TempData["AlunoAviso"] = "Não foi possível cancelar esta confirmação.";

        return RedirectToAction(nameof(Agenda), new { unidadeId });
    }

    private async Task ConfigurarContextoAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var perfil = await alunoAreaServico.ObterPerfilAsync(
            usuarioId, unidadeId, cancellationToken);

        if (perfil is not null)
        {
            ViewData["AlunoNome"] = perfil.NomeCompleto;
        }
    }

    private Guid? ObterUsuarioId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}

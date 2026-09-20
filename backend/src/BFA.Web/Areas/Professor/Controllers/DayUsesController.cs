using BFA.Application.DayUses;
using BFA.Application.Acessos;
using BFA.Web.ViewModels.DayUse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace BFA.Web.Areas.Professor.Controllers;

[Area("Professor")]
[Authorize]
[Route("professor/unidade/{unidadeId:guid}/day-use")]
public sealed class DayUsesController(IUsuarioAtual usuarioAtual, IDayUsesServico dayUsesServico, ILogger<DayUsesController> logger, TimeProvider timeProvider, TimeZoneInfo timeZoneInfo) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid unidadeId, string? dataInicial, string? dataFinal, string? participante, int pagina = 1, CancellationToken cancellationToken = default)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var unidade = await dayUsesServico.ObterUnidadeAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.Professor, cancellationToken);
        if (unidade.Estado != EstadoDayUse.Sucesso || unidade.Unidade is null) return Forbid();
        var paginaResultado = await dayUsesServico.ListarAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.Professor, new(ParseDate(dataInicial), ParseDate(dataFinal), participante, pagina), cancellationToken);
        var alunos = await dayUsesServico.ListarAlunosAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.Professor, null, cancellationToken);
        return View(Mapear(unidade.Unidade, paginaResultado.Pagina ?? new([], 1, 1, 0), dataInicial, dataFinal, participante, CriarFormulario(unidade.Unidade, unidadeId, alunos.Alunos)));
    }

    [HttpPost("registrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(Guid unidadeId, DayUseFormViewModel model, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var solicitacao = DateOnly.TryParseExact(model.DataUso, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? new RegistrarDayUseSolicitacao(model.TipoParticipante == "Aluno" ? model.AlunoId : null, model.TipoParticipante == "Avulso" ? model.NomeAvulso : null, model.TipoParticipante == "Avulso" ? model.TelefoneAvulso : null, model.TipoParticipante == "Avulso" ? model.EmailAvulso : null, data, model.ValorCobrado, model.Pago)
            : null;
        var estado = solicitacao is null ? EstadoDayUse.DadosInvalidos : await dayUsesServico.RegistrarAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.Professor, solicitacao, cancellationToken);
        if (estado == EstadoDayUse.Sucesso) { TempData["Sucesso"] = "Day Use registrado."; return RedirectToAction(nameof(Index), new { unidadeId }); }
        ModelState.AddModelError(string.Empty, estado == EstadoDayUse.ValorNaoConfigurado ? "A Unidade ainda não configurou o valor sugerido do Day Use." : "Não foi possível registrar o Day Use.");
        var unidade = await dayUsesServico.ObterUnidadeAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.Professor, cancellationToken);
        var alunos = await dayUsesServico.ListarAlunosAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.Professor, null, cancellationToken);
        if (unidade.Unidade is null) return Forbid();
        model.Alunos = alunos.Alunos; model.ValorSugerido = unidade.Unidade.ValorSugerido ?? 0;
        return View("Index", Mapear(unidade.Unidade, new([], 1, 1, 0), null, null, null, model));
    }

    [HttpPost("{dayUseId:guid}/pago")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pago(Guid unidadeId, Guid dayUseId, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        await dayUsesServico.MarcarComoPagoAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.Professor, dayUseId, cancellationToken);
        return RedirectToAction(nameof(Index), new { unidadeId });
    }

    [HttpPost("{dayUseId:guid}/excluir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(Guid unidadeId, Guid dayUseId, string? dataInicial, string? dataFinal, string? participante, int pagina = 1, CancellationToken cancellationToken = default)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        logger.LogInformation("Exclusão de Day Use solicitada para {DayUseId} na unidade {UnidadeId}", dayUseId, unidadeId);
        var estado = await dayUsesServico.ExcluirAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.Professor, dayUseId, cancellationToken);
        if (estado == EstadoDayUse.SemAcesso) { logger.LogWarning("Exclusão de Day Use negada para unidade {UnidadeId}", unidadeId); return Forbid(); }
        if (estado == EstadoDayUse.DayUseNaoEncontrado) { logger.LogWarning("Day Use não encontrado ou não pertence ao Professor para exclusão: {DayUseId}", dayUseId); return NotFound(); }
        TempData["Sucesso"] = "Day Use excluído.";
        return RedirectToAction(nameof(Index), new { unidadeId, dataInicial, dataFinal, participante, pagina = Math.Max(1, pagina) });
    }

    private DayUseProfessorViewModel Mapear(DayUseUnidadeResumo unidade, DayUsePagina pagina, string? inicio, string? fim, string? participante, DayUseFormViewModel formulario) => new() { UnidadeId = unidade.UnidadeId, NomeUnidade = unidade.Nome, PodeTrocarUnidade = false, Pagina = pagina, DataInicial = inicio, DataFinal = fim, Participante = participante, Formulario = formulario };
    private DayUseFormViewModel CriarFormulario(DayUseUnidadeResumo unidade, Guid unidadeId, IReadOnlyList<DayUseAlunoResumo> alunos) { var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZoneInfo).DateTime); return new() { UnidadeId = unidadeId, DataUso = hoje.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), ValorSugerido = unidade.ValorSugerido ?? 0, ValorCobrado = unidade.ValorSugerido ?? 0, Alunos = alunos }; }
    private static DateOnly? ParseDate(string? value) => DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data : null;
}

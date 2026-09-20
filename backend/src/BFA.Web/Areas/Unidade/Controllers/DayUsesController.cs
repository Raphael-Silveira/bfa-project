using BFA.Application.DayUses;
using BFA.Application.Acessos;
using BFA.Web.ViewModels.DayUse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[Route("unidade/{unidadeId:guid}/day-use")]
public sealed class DayUsesController(
    IUsuarioAtual usuarioAtual,
    IDayUsesServico dayUsesServico,
    ILogger<DayUsesController> logger,
    TimeProvider timeProvider,
    TimeZoneInfo timeZoneInfo) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid unidadeId, string? dataInicial, string? dataFinal, string? participante, int pagina = 1, CancellationToken cancellationToken = default)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var unidade = await dayUsesServico.ObterUnidadeAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, cancellationToken);
        if (unidade.Estado != EstadoDayUse.Sucesso || unidade.Unidade is null) return Forbid();
        var filtro = new FiltroDayUses(ParseDate(dataInicial), ParseDate(dataFinal), participante, pagina);
        var resultado = await dayUsesServico.ListarAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, filtro, cancellationToken);
        if (resultado.Pagina is null) return Forbid();
        var alunos = await dayUsesServico.ListarAlunosAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, null, cancellationToken);
        var formulario = CriarFormulario(unidade.Unidade, unidadeId);
        formulario.Alunos = alunos.Alunos;
        return View(Mapear(unidade.Unidade, resultado.Pagina, dataInicial, dataFinal, participante, formulario));
    }

    [HttpPost("configuracao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configuracao(Guid unidadeId, decimal? valor, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var estado = await dayUsesServico.ConfigurarValorAsync(usuarioId, unidadeId, valor, cancellationToken);
        if (estado != EstadoDayUse.Sucesso) return Forbid();
        TempData["Sucesso"] = "Valor sugerido atualizado.";
        return RedirectToAction(nameof(Index), new { unidadeId });
    }

    [HttpPost("registrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(Guid unidadeId, DayUseFormViewModel model, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var solicitacao = CriarSolicitacao(model);
        var estado = solicitacao is null ? EstadoDayUse.DadosInvalidos : await dayUsesServico.RegistrarAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, solicitacao, cancellationToken);
        if (estado == EstadoDayUse.Sucesso)
        {
            TempData["Sucesso"] = "Day Use registrado.";
            return RedirectToAction(nameof(Index), new { unidadeId });
        }
        ModelState.AddModelError(string.Empty, Mensagem(estado));
        var unidade = await dayUsesServico.ObterUnidadeAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, cancellationToken);
        if (unidade.Unidade is null) return Forbid();
        var alunos = await dayUsesServico.ListarAlunosAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, null, cancellationToken);
        model.UnidadeId = unidadeId; model.Alunos = alunos.Alunos;
        model.ValorSugerido = unidade.Unidade.ValorSugerido ?? 0;
        return View("Index", Mapear(unidade.Unidade, new DayUsePagina([], 1, 1, 0), null, null, null, model));
    }

    [HttpPost("{dayUseId:guid}/pago")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pago(Guid unidadeId, Guid dayUseId, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var estado = await dayUsesServico.MarcarComoPagoAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, dayUseId, cancellationToken);
        if (estado == EstadoDayUse.Sucesso) TempData["Sucesso"] = "Day Use marcado como pago.";
        return RedirectToAction(nameof(Index), new { unidadeId });
    }

    [HttpPost("{dayUseId:guid}/excluir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(Guid unidadeId, Guid dayUseId, string? dataInicial, string? dataFinal, string? participante, int pagina = 1, CancellationToken cancellationToken = default)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        logger.LogInformation("Exclusão de Day Use solicitada para {DayUseId} na unidade {UnidadeId}", dayUseId, unidadeId);
        var estado = await dayUsesServico.ExcluirAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, dayUseId, cancellationToken);
        if (estado == EstadoDayUse.SemAcesso) { logger.LogWarning("Exclusão de Day Use negada para unidade {UnidadeId}", unidadeId); return Forbid(); }
        if (estado == EstadoDayUse.DayUseNaoEncontrado) { logger.LogWarning("Day Use não encontrado para exclusão: {DayUseId}", dayUseId); return NotFound(); }
        TempData["Sucesso"] = "Day Use excluído.";
        return RedirectToAction(nameof(Index), new { unidadeId, dataInicial, dataFinal, participante, pagina = Math.Max(1, pagina) });
    }

    private DayUseConfiguracaoViewModel Mapear(DayUseUnidadeResumo unidade, DayUsePagina pagina, string? inicio, string? fim, string? participante, DayUseFormViewModel formulario) => new()
    {
        OrganizacaoId = unidade.OrganizacaoId, UnidadeId = unidade.UnidadeId, NomeUnidade = unidade.Nome, PodeTrocarUnidade = false, ValorSugerido = unidade.ValorSugerido,
        Pagina = pagina, DataInicial = inicio, DataFinal = fim, Participante = participante,
        Formulario = formulario
    };

    private DayUseFormViewModel CriarFormulario(DayUseUnidadeResumo unidade, Guid unidadeId)
    {
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZoneInfo).DateTime);
        return new() { UnidadeId = unidadeId, DataUso = hoje.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), ValorSugerido = unidade.ValorSugerido ?? 0, ValorCobrado = unidade.ValorSugerido ?? 0 };
    }

    private static RegistrarDayUseSolicitacao? CriarSolicitacao(DayUseFormViewModel model) =>
        DateOnly.TryParseExact(model.DataUso, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? new(model.TipoParticipante == "Aluno" ? model.AlunoId : null, model.TipoParticipante == "Avulso" ? model.NomeAvulso : null, model.TipoParticipante == "Avulso" ? model.TelefoneAvulso : null, model.TipoParticipante == "Avulso" ? model.EmailAvulso : null, data, model.ValorCobrado, model.Pago)
            : null;

    private static DateOnly? ParseDate(string? value) => DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data : null;
    private static string Mensagem(EstadoDayUse estado) => estado switch { EstadoDayUse.ValorNaoConfigurado => "Configure o valor sugerido do Day Use antes de registrar.", EstadoDayUse.DayUseDuplicado => "Este aluno já possui Day Use nesta data.", EstadoDayUse.ParticipanteNaoEncontrado => "O aluno não pertence a esta Unidade.", _ => "Não foi possível registrar o Day Use." };
}

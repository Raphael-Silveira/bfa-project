using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Application.Usuarios;
using BFA.Application.Professores.Turmas;
using BFA.Application.Professores.Confirmacoes;
using BFA.Domain.Aulas;
using BFA.Domain.Acessos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Professor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace BFA.Web.Areas.Professor.Controllers;

[Area("Professor")]
[Authorize]
[Route("professor")]
public sealed class InicioController(
    IUsuarioAtual usuarioAtual,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IUsuarioApresentacaoConsulta usuarioApresentacaoConsulta,
    IMinhasTurmasProfessorConsulta minhasTurmasConsulta,
    IConfirmacoesParticipacaoProfessorConsulta confirmacoesConsulta,
    IAuthorizationService authorizationService,
    TimeProvider timeProvider,
    TimeZoneInfo timeZoneInfo) : Controller
{
    [HttpGet("selecionar-unidade")]
    public async Task<IActionResult> SelecionarUnidade(CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var unidades = await unidadesUsuarioConsulta.ListarProfessorAsync(
            usuarioId, cancellationToken);
        if (unidades.Count == 1)
        {
            return Redirect($"/professor/unidade/{unidades[0].UnidadeId:D}");
        }

        var nome = await usuarioApresentacaoConsulta.ObterNomeCompletoAsync(
            usuarioId, cancellationToken);
        return View(new ProfessorSelecaoUnidadeViewModel(
            PrimeiroNome(nome),
            unidades.Select(item => new BFA.Web.ViewModels.Unidade.UnidadeSelecaoItemViewModel(
                item.UnidadeId, item.Nome)).ToArray()));
    }

    [HttpPost("selecionar-unidade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelecionarUnidade(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var unidade = await unidadesUsuarioConsulta.ObterProfessorAsync(
            usuarioId, unidadeId, cancellationToken);
        return unidade is null
            ? Forbid()
            : Redirect($"/professor/unidade/{unidadeId:D}");
    }

    [HttpGet("unidade/{unidadeId:guid}")]
    public async Task<IActionResult> Unidade(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var unidade = await unidadesUsuarioConsulta.ObterProfessorAsync(
            usuarioId, unidadeId, cancellationToken);
        if (unidade is null) return Forbid();
        var autorizacao = await authorizationService.AuthorizeAsync(
            User,
            new ContextoUnidade(unidade.OrganizacaoId, unidadeId),
            new AcessoUnidadePorPerfilRequirement(PerfilAcesso.Professor));
        if (!autorizacao.Succeeded) return Forbid();
        var todas = await unidadesUsuarioConsulta.ListarProfessorAsync(
            usuarioId, cancellationToken);
        var nome = await usuarioApresentacaoConsulta.ObterNomeCompletoAsync(
            usuarioId, cancellationToken);
        var dashboard = await minhasTurmasConsulta.ObterDadosDashboardAsync(
            usuarioId, unidadeId, cancellationToken);
        if (dashboard.Estado is EstadoMinhasTurmasProfessor.SemAcesso
            or EstadoMinhasTurmasProfessor.VinculoProfissionalNaoEncontrado)
        {
            return Forbid();
        }
        var dados = dashboard.Valor!;
        var aniversarios = ProfessorDashboardAniversarios.Calcular(
                dados.Alunos,
                DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime))
            .Select(item => new AniversarioProfessorViewModel(
                item.AlunoId,
                item.Nome,
                item.Data.ToString("dd/MM", CultureInfo.InvariantCulture),
                item.DiasAteAniversario))
            .ToArray();
        return View(new ProfessorInicioViewModel(
            unidadeId, unidade.Nome, todas.Count > 1, PrimeiroNome(nome),
            dados.QuantidadeTurmas,
            dados.Alunos.Count,
            dados.QuantidadeAulasHoje,
            aniversarios));
    }

    [HttpGet("unidade/{unidadeId:guid}/aulas")]
    public async Task<IActionResult> MinhasAulas(
        Guid unidadeId, string? status, string? dataInicial, string? dataFinal,
        int pagina = 1, CancellationToken cancellationToken = default)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var unidade = await unidadesUsuarioConsulta.ObterProfessorAsync(
            usuarioId, unidadeId, cancellationToken);
        if (unidade is null) return Forbid();
        var autorizacao = await authorizationService.AuthorizeAsync(
            User, new ContextoUnidade(unidade.OrganizacaoId, unidadeId),
            new AcessoUnidadePorPerfilRequirement(PerfilAcesso.Professor));
        if (!autorizacao.Succeeded) return Forbid();

        var incluirTodas = string.Equals(status, "Todas", StringComparison.OrdinalIgnoreCase);
        StatusAula? statusFiltro = incluirTodas
            ? null
            : Enum.TryParse<StatusAula>(status, true, out var statusParsed)
                && Enum.IsDefined(statusParsed) ? statusParsed : StatusAula.Programada;
        var agoraLocal = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZoneInfo).DateTime;
        var hoje = DateOnly.FromDateTime(agoraLocal);
        var ultimoDiaMes = new DateOnly(agoraLocal.Year, agoraLocal.Month, 1)
            .AddMonths(1).AddDays(-1);
        var inicio = ParseDate(dataInicial) ?? hoje;
        var fim = ParseDate(dataFinal) ?? ultimoDiaMes;
        var paginaResultado = await confirmacoesConsulta.ListarAgendaAsync(
            usuarioId, unidadeId, inicio, fim, statusFiltro, incluirTodas, pagina, cancellationToken);
        if (paginaResultado.Estado == EstadoConfirmacoesProfessor.SemAcesso) return Forbid();
        var todas = await unidadesUsuarioConsulta.ListarProfessorAsync(usuarioId, cancellationToken);
        return View(new ProfessorMinhasAulasViewModel(
            unidadeId, unidade.Nome, todas.Count > 1, statusFiltro,
            FormatDate(inicio), FormatDate(fim), incluirTodas, paginaResultado.Valor!));
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var result) ? result : null;

    private static string? FormatDate(DateOnly? value) =>
        value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string? PrimeiroNome(string? nome) => string.IsNullOrWhiteSpace(nome)
        ? null
        : nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
}

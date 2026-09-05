using BFA.Application.Acessos;
using BFA.Application.Planos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Planos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Franqueadora.Controllers;

[Area("Franqueadora")]
[Authorize(Policy = PoliticasAcesso.AdministradorRede)]
[Route("franqueadora/planos")]
public sealed class PlanosController(
    IUsuarioAtual usuarioAtual,
    IPlanosServico planosServico,
    ILogger<PlanosController> logger) : Controller
{
    private const string RotaBase = "/franqueadora/planos";

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? status, int? pagina, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var filtro = ParseFiltro(status);
        var resultado = await planosServico.ListarRedeAsync(
            usuarioId, filtro, cancellationToken);
        if (resultado.Estado != EstadoPlanos.Sucesso || resultado.Valor is null)
            return Forbid();

        var todos = resultado.Valor.Planos;
        var totalItens = todos.Count;
        const int tamanhoPagina = 10;
        var paginaAtual = Math.Max(1, pagina ?? 1);
        var totalPaginas = (int)Math.Ceiling((double)totalItens / tamanhoPagina);
        if (paginaAtual > totalPaginas && totalPaginas > 0) paginaAtual = totalPaginas;

        var itensPagina = todos
            .Skip((paginaAtual - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToList();

        return View(new PlanosListaViewModel
        {
            OrganizacaoId = resultado.Valor.Contexto.OrganizacaoId,
            EhLocal = false,
            PodeGerenciar = true,
            Filtro = filtro,
            RotaBase = RotaBase,
            Planos = itensPagina,
            PaginaAtual = paginaAtual,
            TamanhoPagina = tamanhoPagina,
            TotalItens = totalItens
        });
    }

    [HttpGet("novo")]
    public IActionResult Novo() => View("Formulario", PlanoViewModelMapper.Novo(
        local: false, RotaBase));

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(
        PlanoFormViewModel model, CancellationToken cancellationToken)
    {
        model.EhLocal = false;
        model.RotaBase = RotaBase;
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        if (!model.TentarCriarTermos(out var termos))
            ModelState.AddModelError(string.Empty, "Revise os dados comerciais do plano.");
        if (!ModelState.IsValid) return View("Formulario", model);
        var resultado = await planosServico.CriarRedeAsync(
            usuarioId, new(model.Nome!, termos!), cancellationToken);
        if (resultado.Estado != EstadoPlanos.Sucesso)
            return ErroFormulario(model, resultado.Estado);
        logger.LogInformation(
            "{Controller} {Action} concluído: {EntityId}",
            "Planos", "Novo", resultado.Valor);
        TempData["Sucesso"] = "Plano da Rede criado com a versão 1.";
        return Redirect($"{RotaBase}/{resultado.Valor:D}");
    }

    [HttpGet("{planoId:guid}")]
    public async Task<IActionResult> Detalhes(
        Guid planoId, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var resultado = await planosServico.ObterRedeAsync(
            usuarioId, planoId, cancellationToken);
        if (resultado.Estado == EstadoPlanos.PlanoNaoEncontrado) return NotFound();
        if (resultado.Valor is null) return Forbid();
        return View(new PlanoDetalheViewModel
        {
            OrganizacaoId = resultado.Valor.Contexto.OrganizacaoId,
            EhLocal = false,
            PodeGerenciar = true,
            RotaBase = RotaBase,
            Plano = resultado.Valor.Plano
        });
    }

    [HttpGet("{planoId:guid}/nova-versao")]
    public async Task<IActionResult> NovaVersao(
        Guid planoId, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var resultado = await planosServico.ObterRedeAsync(
            usuarioId, planoId, cancellationToken);
        if (resultado.Estado == EstadoPlanos.PlanoNaoEncontrado) return NotFound();
        if (resultado.Valor is null) return Forbid();
        return View("Formulario", PlanoViewModelMapper.NovaVersao(
            resultado.Valor, RotaBase, local: false, podeTrocar: false));
    }

    [HttpPost("{planoId:guid}/nova-versao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NovaVersao(
        Guid planoId, PlanoFormViewModel model, CancellationToken cancellationToken)
    {
        model.EhLocal = false;
        model.NovaVersao = true;
        model.PlanoId = planoId;
        model.RotaBase = RotaBase;
        // A validação do modelo ocorre antes de o contexto de nova versão ser restaurado.
        ModelState.Remove(nameof(PlanoFormViewModel.Nome));
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var atual = await planosServico.ObterRedeAsync(usuarioId, planoId, cancellationToken);
        if (atual.Estado == EstadoPlanos.PlanoNaoEncontrado) return NotFound();
        if (atual.Valor is null) return Forbid();
        model.NomePlanoAtual = atual.Valor.Plano.Nome;
        if (!model.TentarCriarTermos(out var termos))
            ModelState.AddModelError(string.Empty, "Revise os dados da nova versão.");
        if (!ModelState.IsValid) return View("Formulario", model);
        var resultado = await planosServico.CriarNovaVersaoRedeAsync(
            usuarioId, planoId, termos!, cancellationToken);
        if (resultado.Estado != EstadoPlanos.Sucesso)
            return ErroFormulario(model, resultado.Estado);
        logger.LogInformation(
            "{Controller} {Action} concluído: {EntityId}",
            "Planos", "NovaVersao", planoId);
        TempData["Sucesso"] = "Nova versão comercial criada com sucesso.";
        return Redirect($"{RotaBase}/{planoId:D}");
    }

    [HttpPost("{planoId:guid}/ativar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Ativar(
        Guid planoId, CancellationToken cancellationToken) =>
        AlterarEstadoAsync(planoId, ativar: true, cancellationToken);

    [HttpPost("{planoId:guid}/inativar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Inativar(
        Guid planoId, CancellationToken cancellationToken) =>
        AlterarEstadoAsync(planoId, ativar: false, cancellationToken);

    private async Task<IActionResult> AlterarEstadoAsync(
        Guid planoId, bool ativar, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var resultado = await planosServico.AlterarEstadoRedeAsync(
            usuarioId, planoId, ativar, cancellationToken);
        if (resultado.Estado == EstadoPlanos.PlanoNaoEncontrado) return NotFound();
        if (resultado.Estado == EstadoPlanos.SemVersaoAberta)
        {
            TempData["Erro"] = "O plano precisa possuir uma versão comercial aberta para ser reativado.";
            return Redirect($"{RotaBase}/{planoId:D}");
        }
        if (resultado.Estado != EstadoPlanos.Sucesso) return Forbid();
        logger.LogInformation(
            "{Controller} {Action} concluído: {EntityId}",
            "Planos", ativar ? "Ativar" : "Inativar", planoId);
        TempData["Sucesso"] = ativar ? "Plano reativado." : "Plano inativado.";
        return Redirect($"{RotaBase}/{planoId:D}");
    }

    private IActionResult ErroFormulario(PlanoFormViewModel model, EstadoPlanos estado)
    {
        ModelState.AddModelError(string.Empty, estado switch
        {
            EstadoPlanos.VigenciaInvalida =>
                "A nova vigência deve iniciar depois do início da versão atual.",
            EstadoPlanos.ConflitoConcorrencia =>
                "Outra alteração comercial ocorreu simultaneamente. Atualize e tente novamente.",
            _ => "Não foi possível salvar o plano. Revise os dados e tente novamente."
        });
        return View("Formulario", model);
    }

    private static FiltroPlanos ParseFiltro(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "inativos" => FiltroPlanos.Inativos,
            "todos" => FiltroPlanos.Todos,
            _ => FiltroPlanos.Ativos
        };
}

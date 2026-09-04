using BFA.Application.Acessos;
using BFA.Application.Planos;
using BFA.Application.Unidades;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Planos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[ServiceFilter(typeof(GovernancaOperacionalUnidadeResultFilter))]
[Route("unidade/{unidadeId:guid}/planos")]
public sealed class PlanosController(
    IUsuarioAtual usuarioAtual,
    IPlanosServico planosServico,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    ILogger<PlanosController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId, string? status, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var filtro = ParseFiltro(status);
        var resultado = await planosServico.ListarLocalAsync(
            usuarioId, unidadeId, filtro, cancellationToken);
        if (resultado.Estado == EstadoPlanos.ContextoNaoEncontrado) return NotFound();
        if (resultado.Valor is null) return Forbid();
        return View(new PlanosListaViewModel
        {
            OrganizacaoId = resultado.Valor.Contexto.OrganizacaoId,
            UnidadeId = unidadeId,
            NomeUnidade = resultado.Valor.Contexto.NomeUnidade!,
            PodeTrocarUnidade = await PodeTrocarAsync(usuarioId, cancellationToken),
            EhLocal = true,
            PodeGerenciar = resultado.Valor.Contexto.PodeGerenciar,
            PossuiFranqueadoAtivo = resultado.Valor.Contexto.PossuiFranqueadoAtivo,
            Filtro = filtro,
            RotaBase = RotaBase(unidadeId),
            Planos = resultado.Valor.Planos
        });
    }

    [HttpGet("novo")]
    public async Task<IActionResult> Novo(
        Guid unidadeId, CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(unidadeId, cancellationToken);
        if (contexto.Resultado is not null) return contexto.Resultado;
        if (!contexto.Valor!.PodeGerenciar) return Forbid();
        return View("Formulario", PlanoViewModelMapper.Novo(
            true, RotaBase(unidadeId), contexto.Valor,
            await PodeTrocarAsync(usuarioAtual.UsuarioId!.Value, cancellationToken)));
    }

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(
        Guid unidadeId, PlanoFormViewModel model, CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(unidadeId, cancellationToken);
        if (contexto.Resultado is not null) return contexto.Resultado;
        if (!contexto.Valor!.PodeGerenciar)
        {
            logger.LogWarning("{Controller} {Action} negado: {Motivo}", "Planos", "Novo", "Sem permissão de gerenciamento");
            return Forbid();
        }
        PreencherContexto(model, contexto.Valor,
            await PodeTrocarAsync(usuarioAtual.UsuarioId!.Value, cancellationToken));
        if (!model.TentarCriarTermos(out var termos))
            ModelState.AddModelError(string.Empty, "Revise os dados comerciais do plano.");
        if (!ModelState.IsValid) return View("Formulario", model);
        var resultado = await planosServico.CriarLocalAsync(
            usuarioAtual.UsuarioId.Value, unidadeId,
            new(model.Nome!, termos!), cancellationToken);
        if (resultado.Estado != EstadoPlanos.Sucesso)
            return ErroFormulario(model, resultado.Estado);
        TempData["Sucesso"] = "Plano local criado com a versão 1.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Planos", "Novo", resultado.Valor);
        return Redirect($"{RotaBase(unidadeId)}/{resultado.Valor:D}");
    }

    [HttpGet("{planoId:guid}")]
    public async Task<IActionResult> Detalhes(
        Guid unidadeId, Guid planoId, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var resultado = await planosServico.ObterLocalAsync(
            usuarioId, unidadeId, planoId, cancellationToken);
        if (resultado.Estado is EstadoPlanos.ContextoNaoEncontrado
            or EstadoPlanos.PlanoNaoEncontrado) return NotFound();
        if (resultado.Valor is null) return Forbid();
        return View(new PlanoDetalheViewModel
        {
            OrganizacaoId = resultado.Valor.Contexto.OrganizacaoId,
            UnidadeId = unidadeId,
            NomeUnidade = resultado.Valor.Contexto.NomeUnidade!,
            PodeTrocarUnidade = await PodeTrocarAsync(usuarioId, cancellationToken),
            EhLocal = true,
            PodeGerenciar = resultado.Valor.Contexto.PodeGerenciar,
            PossuiFranqueadoAtivo = resultado.Valor.Contexto.PossuiFranqueadoAtivo,
            RotaBase = RotaBase(unidadeId),
            Plano = resultado.Valor.Plano
        });
    }

    [HttpGet("{planoId:guid}/nova-versao")]
    public async Task<IActionResult> NovaVersao(
        Guid unidadeId, Guid planoId, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var resultado = await planosServico.ObterLocalAsync(
            usuarioId, unidadeId, planoId, cancellationToken);
        if (resultado.Estado is EstadoPlanos.ContextoNaoEncontrado
            or EstadoPlanos.PlanoNaoEncontrado) return NotFound();
        if (resultado.Valor is null || !resultado.Valor.Contexto.PodeGerenciar)
            return Forbid();
        return View("Formulario", PlanoViewModelMapper.NovaVersao(
            resultado.Valor, RotaBase(unidadeId), local: true,
            await PodeTrocarAsync(usuarioId, cancellationToken)));
    }

    [HttpPost("{planoId:guid}/nova-versao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NovaVersao(
        Guid unidadeId, Guid planoId, PlanoFormViewModel model,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(unidadeId, cancellationToken);
        if (contexto.Resultado is not null) return contexto.Resultado;
        if (!contexto.Valor!.PodeGerenciar)
        {
            logger.LogWarning("{Controller} {Action} negado: {Motivo}", "Planos", "NovaVersao", "Sem permissão de gerenciamento");
            return Forbid();
        }
        PreencherContexto(model, contexto.Valor,
            await PodeTrocarAsync(usuarioAtual.UsuarioId!.Value, cancellationToken));
        model.NovaVersao = true;
        model.PlanoId = planoId;
        var atual = await planosServico.ObterLocalAsync(
            usuarioAtual.UsuarioId.Value, unidadeId, planoId, cancellationToken);
        if (atual.Valor is null) return NotFound();
        model.NomePlanoAtual = atual.Valor.Plano.Nome;
        if (!model.TentarCriarTermos(out var termos))
            ModelState.AddModelError(string.Empty, "Revise os dados da nova versão.");
        if (!ModelState.IsValid) return View("Formulario", model);
        var resultado = await planosServico.CriarNovaVersaoLocalAsync(
            usuarioAtual.UsuarioId.Value, unidadeId, planoId, termos!, cancellationToken);
        if (resultado.Estado != EstadoPlanos.Sucesso)
            return ErroFormulario(model, resultado.Estado);
        TempData["Sucesso"] = "Nova versão comercial criada com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Planos", "NovaVersao", planoId);
        return Redirect($"{RotaBase(unidadeId)}/{planoId:D}");
    }

    [HttpPost("{planoId:guid}/ativar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Ativar(
        Guid unidadeId, Guid planoId, CancellationToken cancellationToken) =>
        AlterarEstadoAsync(unidadeId, planoId, ativar: true, cancellationToken);

    [HttpPost("{planoId:guid}/inativar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Inativar(
        Guid unidadeId, Guid planoId, CancellationToken cancellationToken) =>
        AlterarEstadoAsync(unidadeId, planoId, ativar: false, cancellationToken);

    private async Task<IActionResult> AlterarEstadoAsync(
        Guid unidadeId, Guid planoId, bool ativar, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var resultado = await planosServico.AlterarEstadoLocalAsync(
            usuarioId, unidadeId, planoId, ativar, cancellationToken);
        if (resultado.Estado is EstadoPlanos.ContextoNaoEncontrado
            or EstadoPlanos.PlanoNaoEncontrado) return NotFound();
        if (resultado.Estado == EstadoPlanos.SemVersaoAberta)
        {
            TempData["Erro"] = "O plano precisa possuir uma versão comercial aberta para ser reativado.";
            return Redirect($"{RotaBase(unidadeId)}/{planoId:D}");
        }
        if (resultado.Estado != EstadoPlanos.Sucesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Planos", ativar ? "Ativar" : "Inativar", resultado.Estado);
            return Forbid();
        }
        TempData["Sucesso"] = ativar ? "Plano reativado." : "Plano inativado.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Planos", ativar ? "Ativar" : "Inativar", planoId);
        return Redirect($"{RotaBase(unidadeId)}/{planoId:D}");
    }

    private async Task<(ContextoPlanosResumo? Valor, IActionResult? Resultado)>
        ObterContextoAsync(Guid unidadeId, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return (null, Forbid());
        var resultado = await planosServico.ListarLocalAsync(
            usuarioId, unidadeId, FiltroPlanos.Todos, cancellationToken);
        if (resultado.Estado == EstadoPlanos.ContextoNaoEncontrado)
            return (null, NotFound());
        return resultado.Valor is null
            ? (null, Forbid())
            : (resultado.Valor.Contexto, null);
    }

    private async Task<bool> PodeTrocarAsync(
        Guid usuarioId, CancellationToken cancellationToken) =>
        (await unidadesUsuarioConsulta.ListarAdministradasAsync(
            usuarioId, cancellationToken)).Count > 1;

    private static void PreencherContexto(
        PlanoFormViewModel model, ContextoPlanosResumo contexto, bool podeTrocar)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId!.Value;
        model.NomeUnidade = contexto.NomeUnidade!;
        model.PodeTrocarUnidade = podeTrocar;
        model.EhLocal = true;
        model.RotaBase = RotaBase(model.UnidadeId);
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

    private static string RotaBase(Guid unidadeId) => $"/unidade/{unidadeId:D}/planos";

    private static FiltroPlanos ParseFiltro(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "inativos" => FiltroPlanos.Inativos,
            "todos" => FiltroPlanos.Todos,
            _ => FiltroPlanos.Ativos
        };
}

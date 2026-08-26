using BFA.Application.Acessos;
using BFA.Application.Franqueadora.Unidades;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Franqueadora;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Franqueadora.Controllers;

[Area("Franqueadora")]
[Authorize(Policy = PoliticasAcesso.AdministradorRede)]
[Route("franqueadora/unidades")]
public sealed class UnidadesController(
    IUsuarioAtual usuarioAtual,
    IUnidadesFranqueadoraConsulta consulta,
    IUnidadesFranqueadoraServico servico) : Controller
{
    private const string MensagemSlugDuplicado =
        "Já existe uma unidade com este identificador.";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await consulta.ListarAsync(usuarioId, cancellationToken);

        if (resultado.Estado != EstadoGerenciamentoUnidade.Sucesso
            || resultado.Valor is not { } unidades)
        {
            return Forbid();
        }

        return View(new UnidadesFranqueadoraIndexViewModel
        {
            Unidades = unidades
                .Select(unidade => new UnidadeFranqueadoraItemViewModel(
                    unidade.Id,
                    unidade.Nome,
                    unidade.Slug,
                    unidade.Ativa,
                    unidade.CriadoEmUtc,
                    unidade.PossuiFranqueadoAtivo))
                .ToArray()
        });
    }

    [HttpGet("nova")]
    public IActionResult Nova()
    {
        return View(new NovaUnidadeViewModel());
    }

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nova(
        NovaUnidadeViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await servico.CriarAsync(
            usuarioId,
            new CriarUnidadeSolicitacao(model.Nome, model.Slug),
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoUnidade.SlugDuplicado)
        {
            ModelState.AddModelError(nameof(model.Slug), MensagemSlugDuplicado);
            return View(model);
        }

        return resultado.Estado == EstadoGerenciamentoUnidade.Sucesso
            ? Redirect("/franqueadora/unidades")
            : Forbid();
    }

    [HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Editar(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await consulta.ObterAsync(usuarioId, id, cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoUnidade.NaoEncontrada)
        {
            return NotFound();
        }

        if (resultado.Estado != EstadoGerenciamentoUnidade.Sucesso
            || resultado.Valor is not { } unidade)
        {
            return Forbid();
        }

        return View(new EditarUnidadeViewModel
        {
            Id = unidade.Id,
            Nome = unidade.Nome,
            Slug = unidade.Slug
        });
    }

    [HttpPost("{id:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        Guid id,
        EditarUnidadeViewModel model,
        CancellationToken cancellationToken)
    {
        model = new EditarUnidadeViewModel
        {
            Id = id,
            Nome = model.Nome,
            Slug = model.Slug
        };

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await servico.AtualizarAsync(
            usuarioId,
            id,
            new AtualizarUnidadeSolicitacao(model.Nome, model.Slug),
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoUnidade.SlugDuplicado)
        {
            ModelState.AddModelError(nameof(model.Slug), MensagemSlugDuplicado);
            return View(model);
        }

        if (resultado.Estado == EstadoGerenciamentoUnidade.NaoEncontrada)
        {
            return NotFound();
        }

        return resultado.Estado == EstadoGerenciamentoUnidade.Sucesso
            ? Redirect("/franqueadora/unidades")
            : Forbid();
    }

    [HttpPost("{id:guid}/ativar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Ativar(Guid id, CancellationToken cancellationToken)
    {
        return AlterarEstadoAsync(id, ativar: true, cancellationToken);
    }

    [HttpPost("{id:guid}/desativar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        return AlterarEstadoAsync(id, ativar: false, cancellationToken);
    }

    private async Task<IActionResult> AlterarEstadoAsync(
        Guid id,
        bool ativar,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = ativar
            ? await servico.AtivarAsync(usuarioId, id, cancellationToken)
            : await servico.DesativarAsync(usuarioId, id, cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoUnidade.NaoEncontrada)
        {
            return NotFound();
        }

        return resultado.Estado == EstadoGerenciamentoUnidade.Sucesso
            ? Redirect("/franqueadora/unidades")
            : Forbid();
    }
}

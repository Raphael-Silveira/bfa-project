using BFA.Application.Identidade;
using BFA.Web.Identidade;
using BFA.Web.ViewModels.Conta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Controllers;

[AllowAnonymous]
public sealed class PrimeiroAcessoController(IPrimeiroAcessoServico primeiroAcessoServico)
    : Controller
{
    public const string MensagemSenhaDefinida =
        "Senha definida com sucesso. Você já pode entrar na sua conta.";

    [HttpGet("definir-senha")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> DefinirSenha(
        Guid usuarioId,
        string? token,
        CancellationToken cancellationToken)
    {
        var linkValido = TokenPrimeiroAcesso.TentarDecodificar(token, out var tokenDecodificado)
            && await primeiroAcessoServico.TokenValidoAsync(
                usuarioId,
                tokenDecodificado,
                cancellationToken);
        var model = new DefinirSenhaViewModel
        {
            UsuarioId = usuarioId,
            Token = token ?? string.Empty,
            LinkValido = linkValido
        };

        if (!linkValido)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        return View(model);
    }

    [HttpPost("definir-senha")]
    [ValidateAntiForgeryToken]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> DefinirSenha(
        DefinirSenhaViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TokenPrimeiroAcesso.TentarDecodificar(model.Token, out var tokenDecodificado))
        {
            return ExibirLinkInvalido(model);
        }

        model.LinkValido = await primeiroAcessoServico.TokenValidoAsync(
            model.UsuarioId,
            tokenDecodificado,
            cancellationToken);

        if (!model.LinkValido)
        {
            return ExibirLinkInvalido(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var resultado = await primeiroAcessoServico.DefinirSenhaAsync(
            model.UsuarioId,
            tokenDecodificado,
            model.NovaSenha,
            cancellationToken);

        if (resultado.Estado == EstadoDefinicaoSenha.Sucesso)
        {
            TempData[nameof(MensagemSenhaDefinida)] = MensagemSenhaDefinida;
            return Redirect("/login");
        }

        if (resultado.Estado == EstadoDefinicaoSenha.LinkInvalido)
        {
            return ExibirLinkInvalido(model);
        }

        foreach (var erro in resultado.Erros)
        {
            ModelState.AddModelError(nameof(model.NovaSenha), erro);
        }

        return View(model);
    }

    private IActionResult ExibirLinkInvalido(DefinirSenhaViewModel model)
    {
        model.LinkValido = false;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("DefinirSenha", model);
    }
}

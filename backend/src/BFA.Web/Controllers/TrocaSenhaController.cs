using System.Security.Claims;
using BFA.Application.Identidade;
using BFA.Web.ViewModels.Conta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Controllers;

[Authorize]
public sealed class TrocaSenhaController(
    IPrimeiroAcessoServico primeiroAcessoServico,
    SignInManager<BFA.Infrastructure.Identity.UsuarioIdentity> signInManager,
    ILogger<TrocaSenhaController> logger) : Controller
{
    [HttpGet("trocar-senha")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null
            || !await primeiroAcessoServico.TrocaObrigatoriaAsync(
                usuarioId.Value,
                cancellationToken))
        {
            return Redirect("/acessar");
        }

        return View(new TrocarSenhaViewModel());
    }

    [HttpPost("trocar-senha")]
    [ValidateAntiForgeryToken]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(
        TrocarSenhaViewModel model,
        CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var resultado = await primeiroAcessoServico.TrocarSenhaObrigatoriaAsync(
            usuarioId.Value,
            model.SenhaAtual,
            model.NovaSenha,
            cancellationToken);
        if (resultado.Estado != EstadoDefinicaoSenha.Sucesso)
        {
            foreach (var erro in resultado.Erros)
            {
                ModelState.AddModelError(string.Empty, erro);
            }

            return View(model);
        }

        await signInManager.SignOutAsync();
        logger.LogInformation("TrocaSenha {Action} concluída", "Index");
        TempData[nameof(PrimeiroAcessoController.MensagemSenhaDefinida)] =
            PrimeiroAcessoController.MensagemSenhaDefinida;
        return Redirect("/login");
    }

    private Guid? ObterUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}

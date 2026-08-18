using BFA.Infrastructure.Identity;
using BFA.Web.ViewModels.Conta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Controllers;

public sealed class ContaController : Controller
{
    private const string CredenciaisInvalidas = "Email ou senha inválidos.";
    private readonly UserManager<UsuarioIdentity> _userManager;
    private readonly SignInManager<UsuarioIdentity> _signInManager;

    public ContaController(
        UserManager<UsuarioIdentity> userManager,
        SignInManager<UsuarioIdentity> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Entrar(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect("/");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var usuario = await _userManager.FindByEmailAsync(model.Email);

        if (usuario is null)
        {
            ModelState.AddModelError(string.Empty, CredenciaisInvalidas);
            return View(model);
        }

        var resultado = await _signInManager.PasswordSignInAsync(
            usuario,
            model.Senha,
            model.LembrarMe,
            lockoutOnFailure: false);

        if (!resultado.Succeeded)
        {
            ModelState.AddModelError(string.Empty, CredenciaisInvalidas);
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return Redirect("/");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        await _signInManager.SignOutAsync();

        return Redirect("/");
    }

    [AllowAnonymous]
    [HttpGet("acesso-negado")]
    public IActionResult AcessoNegado()
    {
        return View();
    }

    [Authorize]
    [HttpGet("conta/autenticado")]
    public IActionResult Autenticado()
    {
        return Content("Usuário autenticado.");
    }
}

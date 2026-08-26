using BFA.Application.Acessos;
using BFA.Infrastructure.Identity;
using BFA.Web.Acessos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Conta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Controllers;

public sealed class ContaController : Controller
{
    private const string CredenciaisInvalidas = "E-mail/usuário ou senha inválidos.";
    private readonly UserManager<UsuarioIdentity> _userManager;
    private readonly SignInManager<UsuarioIdentity> _signInManager;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IDestinoPosLogin _destinoPosLogin;

    public ContaController(
        UserManager<UsuarioIdentity> userManager,
        SignInManager<UsuarioIdentity> signInManager,
        IUsuarioAtual usuarioAtual,
        IDestinoPosLogin destinoPosLogin)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _usuarioAtual = usuarioAtual;
        _destinoPosLogin = destinoPosLogin;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public async Task<IActionResult> Entrar(
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (_usuarioAtual.Autenticado)
        {
            return await RedirecionarUsuarioAtualAsync(cancellationToken);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var usuario = await _userManager.FindByNameAsync(model.Email.Trim());

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

        return await RedirecionarParaDestinoAsync(usuario.Id, cancellationToken);
    }

    [AllowAnonymous]
    [HttpGet("acessar")]
    public async Task<IActionResult> Acessar(CancellationToken cancellationToken)
    {
        if (!_usuarioAtual.Autenticado)
        {
            return Redirect("/login");
        }

        return await RedirecionarUsuarioAtualAsync(cancellationToken);
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

    [Authorize(Policy = PoliticasAcesso.AdministradorRede)]
    [HttpGet("conta/admin-rede")]
    public IActionResult AdministradorRede()
    {
        return Content("Administrador de rede autorizado.");
    }

    private async Task<IActionResult> RedirecionarUsuarioAtualAsync(
        CancellationToken cancellationToken)
    {
        return _usuarioAtual.UsuarioId is { } usuarioId
            ? await RedirecionarParaDestinoAsync(usuarioId, cancellationToken)
            : Redirect("/");
    }

    private async Task<IActionResult> RedirecionarParaDestinoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var destino = await _destinoPosLogin.ObterAsync(usuarioId, cancellationToken);

        return Redirect(DestinoPosLoginUrl.Obter(destino));
    }
}

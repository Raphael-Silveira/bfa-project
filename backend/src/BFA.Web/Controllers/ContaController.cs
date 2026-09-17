using BFA.Application.Acessos;
using BFA.Application.Identidade;
using BFA.Infrastructure.Identity;
using BFA.Web.Acessos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Conta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Controllers;

public sealed class ContaController(
    UserManager<UsuarioIdentity> userManager,
    SignInManager<UsuarioIdentity> signInManager,
    IUsuarioAtual usuarioAtual,
    IUsuarioPorCpfConsulta usuarioPorCpfConsulta,
    IPrimeiroAcessoServico primeiroAcessoServico,
    IDestinoPosLogin destinoPosLogin,
    ILogger<ContaController> logger) : Controller
{
    private const string CredenciaisInvalidas = "E-mail/usuário ou senha inválidos.";

    [AllowAnonymous]
    [HttpGet("login")]
    public async Task<IActionResult> Entrar(
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (usuarioAtual.Autenticado)
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

        UsuarioIdentity? usuario;
        if (CpfIdentificador.TentarNormalizar(model.Email, out var cpf))
        {
            var usuarioId = await usuarioPorCpfConsulta.ObterAlunoAsync(
                cpf,
                cancellationToken);
            usuario = usuarioId is { } id
                ? await userManager.FindByIdAsync(id.ToString())
                : null;
        }
        else
        {
            usuario = await userManager.FindByNameAsync(model.Email.Trim());
        }

        if (usuario is null)
        {
            logger.LogWarning("Conta {Action} falhou: credencial não localizada", "Entrar");
            ModelState.AddModelError(string.Empty, CredenciaisInvalidas);
            return View(model);
        }

        var resultado = await signInManager.PasswordSignInAsync(
            usuario,
            model.Senha,
            model.LembrarMe,
            lockoutOnFailure: false);

        if (!resultado.Succeeded)
        {
            logger.LogWarning("Conta {Action} falhou: credencial inválida", "Entrar");
            ModelState.AddModelError(string.Empty, CredenciaisInvalidas);
            return View(model);
        }

        logger.LogInformation("Conta {Action} bem-sucedido", "Entrar");

        if (await primeiroAcessoServico.TrocaObrigatoriaAsync(
                usuario.Id,
                cancellationToken))
        {
            return Redirect("/trocar-senha");
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
        if (!usuarioAtual.Autenticado)
        {
            return Redirect("/login");
        }

        return await RedirecionarUsuarioAtualAsync(cancellationToken);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        await signInManager.SignOutAsync();

        logger.LogInformation("Conta {Action} executado", "Sair");

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
        return usuarioAtual.UsuarioId is { } usuarioId
            ? await RedirecionarParaDestinoAsync(usuarioId, cancellationToken)
            : Redirect("/");
    }

    private async Task<IActionResult> RedirecionarParaDestinoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var destino = await destinoPosLogin.ObterAsync(usuarioId, cancellationToken);

        return Redirect(DestinoPosLoginUrl.Obter(destino));
    }
}

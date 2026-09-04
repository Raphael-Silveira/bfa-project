using BFA.Application.Identidade;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BFA.Infrastructure.Identity;

public sealed class PrimeiroAcessoServico(UserManager<UsuarioIdentity> userManager, ILogger<PrimeiroAcessoServico> logger)
    : IPrimeiroAcessoServico
{
    public async Task<bool> TokenValidoAsync(
        Guid usuarioId,
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (usuarioId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());

        return usuario is not null
            && await userManager.VerifyUserTokenAsync(
                usuario,
                TokenOptions.DefaultProvider,
                UserManager<UsuarioIdentity>.ResetPasswordTokenPurpose,
                token);
    }

    public async Task<ResultadoDefinicaoSenha> DefinirSenhaAsync(
        Guid usuarioId,
        string token,
        string novaSenha,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (usuarioId == Guid.Empty
            || string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(novaSenha))
        {
            return LinkInvalido();
        }

        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());

        if (usuario is null)
        {
            return LinkInvalido();
        }

        var resultado = await userManager.ResetPasswordAsync(usuario, token, novaSenha);

        if (resultado.Succeeded)
        {
            return new(EstadoDefinicaoSenha.Sucesso, []);
        }

        var linkInvalido = resultado.Errors.Any(erro =>
            string.Equals(erro.Code, "InvalidToken", StringComparison.Ordinal));

        if (linkInvalido)
        {
            return LinkInvalido();
        }

        logger.LogWarning(
            "Falha ao definir senha para usuario {UsuarioId}: {Erros}",
            usuarioId,
            string.Join(", ", resultado.Errors.Select(e => e.Code)));

        var erros = resultado.Errors
            .Select(MapearErroSenha)
            .Where(descricao => !string.IsNullOrWhiteSpace(descricao))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new(
            EstadoDefinicaoSenha.SenhaInvalida,
            erros.Length == 0 ? ["A senha informada não atende aos requisitos de segurança."] : erros);
    }

    private static ResultadoDefinicaoSenha LinkInvalido()
    {
        return new(
            EstadoDefinicaoSenha.LinkInvalido,
            ["O link de definição de senha é inválido ou expirou."]);
    }

    private static string MapearErroSenha(IdentityError erro)
    {
        return erro.Code switch
        {
            "PasswordTooShort" => "A senha deve ter no mínimo 6 caracteres.",
            "PasswordRequiresNonAlphanumeric" =>
                "A senha deve conter ao menos um caractere especial.",
            "PasswordRequiresDigit" => "A senha deve conter ao menos um número.",
            "PasswordRequiresLower" => "A senha deve conter ao menos uma letra minúscula.",
            "PasswordRequiresUpper" => "A senha deve conter ao menos uma letra maiúscula.",
            "PasswordRequiresUniqueChars" =>
                "A senha deve conter uma quantidade maior de caracteres diferentes.",
            _ => "A senha informada não atende aos requisitos de segurança."
        };
    }
}

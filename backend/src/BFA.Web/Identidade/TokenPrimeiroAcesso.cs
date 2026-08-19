using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace BFA.Web.Identidade;

public static class TokenPrimeiroAcesso
{
    public static string Codificar(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    public static bool TentarDecodificar(string? tokenCodificado, out string token)
    {
        token = string.Empty;

        if (string.IsNullOrWhiteSpace(tokenCodificado))
        {
            return false;
        }

        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(tokenCodificado));
            return token.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

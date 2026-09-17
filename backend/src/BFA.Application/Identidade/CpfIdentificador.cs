namespace BFA.Application.Identidade;

public static class CpfIdentificador
{
    public static bool TentarNormalizar(string? valor, out string cpf)
    {
        cpf = string.Empty;
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        foreach (var caractere in valor)
        {
            if (!char.IsDigit(caractere)
                && caractere is not ('.' or '-' or ' '))
            {
                return false;
            }
        }

        var digitos = new string(valor.Where(char.IsDigit).ToArray());
        if (digitos.Length != 11)
        {
            return false;
        }

        cpf = digitos;
        return true;
    }
}

namespace BFA.Domain.Alunos;

public static class TelefoneBrasileiro
{
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var digitos = new string(valor.Where(caractere => caractere is >= '0' and <= '9').ToArray());
        if (digitos.Length is 10 or 11)
        {
            digitos = $"55{digitos}";
        }

        if (digitos.Length is not (12 or 13) || !digitos.StartsWith("55", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Informe um telefone brasileiro válido com DDD e DDI 55.",
                nameof(valor));
        }

        var ddd = int.Parse(digitos.AsSpan(2, 2));
        var numero = digitos[4..];
        if (ddd is < 11 or > 99
            || (numero.Length == 8 && numero[0] is < '2' or > '5')
            || (numero.Length == 9 && numero[0] != '9'))
        {
            throw new ArgumentException(
                "Informe um telefone brasileiro válido com DDD e DDI 55.",
                nameof(valor));
        }

        return digitos;
    }

    public static string? Formatar(string? valor)
    {
        string? normalizado;
        try
        {
            normalizado = Normalizar(valor);
        }
        catch (ArgumentException)
        {
            return valor?.Trim();
        }

        if (normalizado is null)
        {
            return null;
        }

        var ddd = normalizado[2..4];
        var numero = normalizado[4..];
        var prefixo = numero.Length == 9 ? numero[..5] : numero[..4];
        var sufixo = numero.Length == 9 ? numero[5..] : numero[4..];
        return $"+55 ({ddd}) {prefixo}-{sufixo}";
    }

    public static string? FormatarLocal(string? valor)
    {
        string? normalizado;
        try
        {
            normalizado = Normalizar(valor);
        }
        catch (ArgumentException)
        {
            return valor?.Trim();
        }

        if (normalizado is null)
        {
            return null;
        }

        var numero = normalizado[4..];
        var ddd = normalizado[2..4];
        var prefixo = numero.Length == 9 ? numero[..5] : numero[..4];
        var sufixo = numero.Length == 9 ? numero[5..] : numero[4..];
        return $"({ddd}) {prefixo}-{sufixo}";
    }
}

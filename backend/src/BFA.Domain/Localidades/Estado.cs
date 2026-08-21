namespace BFA.Domain.Localidades;

public sealed class Estado
{
    public const int SiglaTamanho = 2;
    public const int NomeTamanhoMaximo = 100;

    private Estado()
    {
    }

    public Estado(int codigoIbge, string sigla, string nome, DateTime criadoEmUtc)
    {
        ValidarCodigoIbge(codigoIbge, nameof(codigoIbge));
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        CodigoIbge = codigoIbge;
        Sigla = NormalizarSigla(sigla);
        Nome = NormalizarNome(nome);
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public int CodigoIbge { get; private set; }

    public string Sigla { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    public bool Ativo { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void Atualizar(string sigla, string nome, DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
        Sigla = NormalizarSigla(sigla);
        Nome = NormalizarNome(nome);
        Ativo = true;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Desativar(DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));

        if (!Ativo)
        {
            return;
        }

        Ativo = false;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static string NormalizarSigla(string sigla)
    {
        if (string.IsNullOrWhiteSpace(sigla))
        {
            throw new ArgumentException("A sigla do Estado deve ser informada.", nameof(sigla));
        }

        var normalizada = sigla.Trim().ToUpperInvariant();

        if (normalizada.Length != SiglaTamanho
            || normalizada.Any(caractere => caractere is not (>= 'A' and <= 'Z')))
        {
            throw new ArgumentException(
                "A sigla do Estado deve possuir exatamente duas letras.",
                nameof(sigla));
        }

        return normalizada;
    }

    private static string NormalizarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome do Estado deve ser informado.", nameof(nome));
        }

        var normalizado = nome.Trim();

        if (normalizado.Length > NomeTamanhoMaximo)
        {
            throw new ArgumentException(
                $"O nome do Estado deve possuir no máximo {NomeTamanhoMaximo} caracteres.",
                nameof(nome));
        }

        return normalizado;
    }

    private static void ValidarCodigoIbge(int codigoIbge, string nomeParametro)
    {
        if (codigoIbge <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nomeParametro,
                codigoIbge,
                "O código IBGE deve ser positivo.");
        }
    }

    private static void ValidarDataUtc(DateTime data, string nomeParametro)
    {
        if (data.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data deve estar em UTC.", nomeParametro);
        }
    }
}

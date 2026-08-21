namespace BFA.Domain.Localidades;

public sealed class Municipio
{
    public const int NomeTamanhoMaximo = 150;

    private Municipio()
    {
    }

    public Municipio(
        int codigoIbge,
        int estadoCodigoIbge,
        string nome,
        DateTime criadoEmUtc)
    {
        ValidarCodigo(codigoIbge, nameof(codigoIbge));
        ValidarCodigo(estadoCodigoIbge, nameof(estadoCodigoIbge));
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        CodigoIbge = codigoIbge;
        EstadoCodigoIbge = estadoCodigoIbge;
        Nome = NormalizarNome(nome);
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public int CodigoIbge { get; private set; }

    public int EstadoCodigoIbge { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public bool Ativo { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void Atualizar(
        int estadoCodigoIbge,
        string nome,
        DateTime atualizadoEmUtc)
    {
        ValidarCodigo(estadoCodigoIbge, nameof(estadoCodigoIbge));
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));

        EstadoCodigoIbge = estadoCodigoIbge;
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

    private static string NormalizarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome do Município deve ser informado.", nameof(nome));
        }

        var normalizado = nome.Trim();

        if (normalizado.Length > NomeTamanhoMaximo)
        {
            throw new ArgumentException(
                $"O nome do Município deve possuir no máximo {NomeTamanhoMaximo} caracteres.",
                nameof(nome));
        }

        return normalizado;
    }

    private static void ValidarCodigo(int codigoIbge, string nomeParametro)
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

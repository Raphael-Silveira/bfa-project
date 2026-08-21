namespace BFA.Domain.Contratos;

public sealed class DocumentoContratoFranquia
{
    public const int TipoDocumentoTamanhoMaximo = 30;
    public const int NomeOriginalTamanhoMaximo = 255;
    public const int ChaveArmazenamentoTamanhoMaximo = 500;
    public const int ContentTypeTamanhoMaximo = 100;
    public const int HashSha256Tamanho = 64;

    private DocumentoContratoFranquia()
    {
    }

    public DocumentoContratoFranquia(
        Guid id,
        Guid contratoFranquiaVersaoId,
        TipoDocumentoContratoFranquia tipoDocumento,
        string nomeOriginal,
        string chaveArmazenamento,
        string contentType,
        long tamanhoBytes,
        string? hashSha256,
        DateTime criadoEmUtc,
        Guid enviadoPorUsuarioId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do documento deve ser informado.",
                nameof(id));
        }

        if (contratoFranquiaVersaoId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da versao do contrato deve ser informado.",
                nameof(contratoFranquiaVersaoId));
        }

        if (!Enum.IsDefined(tipoDocumento))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tipoDocumento),
                tipoDocumento,
                "O tipo do documento e invalido.");
        }

        if (tamanhoBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tamanhoBytes),
                tamanhoBytes,
                "O tamanho do documento deve ser maior que zero.");
        }

        if (criadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        }

        if (enviadoPorUsuarioId == Guid.Empty)
        {
            throw new ArgumentException(
                "O usuario responsavel pelo envio deve ser informado.",
                nameof(enviadoPorUsuarioId));
        }

        Id = id;
        ContratoFranquiaVersaoId = contratoFranquiaVersaoId;
        TipoDocumento = tipoDocumento;
        NomeOriginal = NormalizarObrigatorio(
            nomeOriginal,
            NomeOriginalTamanhoMaximo,
            nameof(nomeOriginal));
        ChaveArmazenamento = NormalizarObrigatorio(
            chaveArmazenamento,
            ChaveArmazenamentoTamanhoMaximo,
            nameof(chaveArmazenamento));
        ContentType = NormalizarObrigatorio(
            contentType,
            ContentTypeTamanhoMaximo,
            nameof(contentType));
        TamanhoBytes = tamanhoBytes;
        HashSha256 = NormalizarHashSha256(hashSha256);
        CriadoEmUtc = criadoEmUtc;
        EnviadoPorUsuarioId = enviadoPorUsuarioId;
    }

    public Guid Id { get; private set; }

    public Guid ContratoFranquiaVersaoId { get; private set; }

    public TipoDocumentoContratoFranquia TipoDocumento { get; private set; }

    public string NomeOriginal { get; private set; } = string.Empty;

    public string ChaveArmazenamento { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long TamanhoBytes { get; private set; }

    public string? HashSha256 { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public Guid EnviadoPorUsuarioId { get; private set; }

    private static string NormalizarObrigatorio(
        string valor,
        int tamanhoMaximo,
        string nomeParametro)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException("O valor deve ser informado.", nomeParametro);
        }

        var valorNormalizado = valor.Trim();

        if (valorNormalizado.Length > tamanhoMaximo)
        {
            throw new ArgumentException(
                $"O valor deve possuir no maximo {tamanhoMaximo} caracteres.",
                nomeParametro);
        }

        return valorNormalizado;
    }

    private static string? NormalizarHashSha256(string? hashSha256)
    {
        if (hashSha256 is null)
        {
            return null;
        }

        var hashNormalizado = hashSha256.Trim().ToLowerInvariant();

        if (hashNormalizado.Length != HashSha256Tamanho
            || hashNormalizado.Any(caractere =>
                caractere is not (>= '0' and <= '9')
                && caractere is not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "O hash SHA-256 deve possuir 64 caracteres hexadecimais.",
                nameof(hashSha256));
        }

        return hashNormalizado;
    }
}

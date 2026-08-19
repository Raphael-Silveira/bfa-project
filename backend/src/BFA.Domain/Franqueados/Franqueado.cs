namespace BFA.Domain.Franqueados;

public sealed class Franqueado
{
    public const int NomeRazaoSocialTamanhoMaximo = 200;
    public const int NomeFantasiaTamanhoMaximo = 200;
    public const int DocumentoTamanhoMaximo = 14;
    public const int TelefoneTamanhoMaximo = 30;
    public const int EmailTamanhoMaximo = 256;
    public const int EmailFinanceiroTamanhoMaximo = 256;
    public const int ResponsavelLegalTamanhoMaximo = 150;
    public const int LogradouroTamanhoMaximo = 200;
    public const int NumeroTamanhoMaximo = 30;
    public const int ComplementoTamanhoMaximo = 100;
    public const int BairroTamanhoMaximo = 100;
    public const int CidadeTamanhoMaximo = 100;
    public const int EstadoTamanhoMaximo = 2;
    public const int CepTamanhoMaximo = 8;
    public const int ObservacoesTamanhoMaximo = 2000;
    public const int TipoPessoaTamanhoMaximo = 30;

    private Franqueado()
    {
    }

    public Franqueado(
        Guid id,
        Guid organizacaoId,
        TipoPessoaFranqueado tipoPessoa,
        string nomeRazaoSocial,
        string documento,
        string email,
        DateTime criadoEmUtc,
        string? nomeFantasia = null,
        string? telefone = null,
        string? emailFinanceiro = null,
        string? responsavelLegal = null,
        string? logradouro = null,
        string? numero = null,
        string? complemento = null,
        string? bairro = null,
        string? cidade = null,
        string? estado = null,
        string? cep = null,
        string? observacoes = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador do franqueado deve ser informado.", nameof(id));
        }

        if (organizacaoId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da organizacao deve ser informado.",
                nameof(organizacaoId));
        }

        if (!Enum.IsDefined(tipoPessoa))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tipoPessoa),
                tipoPessoa,
                "O tipo de pessoa do franqueado e invalido.");
        }

        if (criadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        }

        Id = id;
        OrganizacaoId = organizacaoId;
        TipoPessoa = tipoPessoa;
        NomeRazaoSocial = NormalizarObrigatorio(
            nomeRazaoSocial,
            NomeRazaoSocialTamanhoMaximo,
            nameof(nomeRazaoSocial));
        NomeFantasia = NormalizarOpcional(
            nomeFantasia,
            NomeFantasiaTamanhoMaximo,
            nameof(nomeFantasia));
        Documento = NormalizarDocumento(documento, tipoPessoa);
        Telefone = NormalizarOpcional(telefone, TelefoneTamanhoMaximo, nameof(telefone));
        Email = NormalizarObrigatorio(email, EmailTamanhoMaximo, nameof(email));
        EmailFinanceiro = NormalizarOpcional(
            emailFinanceiro,
            EmailFinanceiroTamanhoMaximo,
            nameof(emailFinanceiro));
        ResponsavelLegal = NormalizarOpcional(
            responsavelLegal,
            ResponsavelLegalTamanhoMaximo,
            nameof(responsavelLegal));
        Logradouro = NormalizarOpcional(
            logradouro,
            LogradouroTamanhoMaximo,
            nameof(logradouro));
        Numero = NormalizarOpcional(numero, NumeroTamanhoMaximo, nameof(numero));
        Complemento = NormalizarOpcional(
            complemento,
            ComplementoTamanhoMaximo,
            nameof(complemento));
        Bairro = NormalizarOpcional(bairro, BairroTamanhoMaximo, nameof(bairro));
        Cidade = NormalizarOpcional(cidade, CidadeTamanhoMaximo, nameof(cidade));
        Estado = NormalizarOpcional(estado, EstadoTamanhoMaximo, nameof(estado));
        Cep = NormalizarCep(cep);
        Observacoes = NormalizarOpcional(
            observacoes,
            ObservacoesTamanhoMaximo,
            nameof(observacoes));
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public TipoPessoaFranqueado TipoPessoa { get; private set; }

    public string NomeRazaoSocial { get; private set; } = string.Empty;

    public string? NomeFantasia { get; private set; }

    public string Documento { get; private set; } = string.Empty;

    public string? Telefone { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string? EmailFinanceiro { get; private set; }

    public string? ResponsavelLegal { get; private set; }

    public string? Logradouro { get; private set; }

    public string? Numero { get; private set; }

    public string? Complemento { get; private set; }

    public string? Bairro { get; private set; }

    public string? Cidade { get; private set; }

    public string? Estado { get; private set; }

    public string? Cep { get; private set; }

    public string? Observacoes { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    private static string NormalizarDocumento(
        string documento,
        TipoPessoaFranqueado tipoPessoa)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            throw new ArgumentException("O documento deve ser informado.", nameof(documento));
        }

        var documentoSemFormatacao = new string(documento
            .Where(caractere => caractere is not '.' and not '-' and not '/'
                && !char.IsWhiteSpace(caractere))
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (documentoSemFormatacao.Any(caractere =>
                caractere is not (>= '0' and <= '9')
                && caractere is not (>= 'A' and <= 'Z')))
        {
            throw new ArgumentException(
                "O documento contém caracteres inválidos.",
                nameof(documento));
        }

        if (tipoPessoa == TipoPessoaFranqueado.PessoaFisica)
        {
            if (documentoSemFormatacao.Length != 11
                || documentoSemFormatacao.Any(caractere => caractere is not (>= '0' and <= '9')))
            {
                throw new ArgumentException(
                    "O CPF deve possuir 11 dígitos.",
                    nameof(documento));
            }

            return documentoSemFormatacao;
        }

        if (documentoSemFormatacao.Length != DocumentoTamanhoMaximo
            || documentoSemFormatacao[..12].Any(caractere =>
                caractere is not (>= '0' and <= '9')
                && caractere is not (>= 'A' and <= 'Z'))
            || documentoSemFormatacao[12..].Any(caractere =>
                caractere is not (>= '0' and <= '9')))
        {
            throw new ArgumentException(
                "O CNPJ deve possuir 12 letras ou dígitos seguidos de 2 dígitos verificadores.",
                nameof(documento));
        }

        return documentoSemFormatacao;
    }

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

    private static string? NormalizarCep(string? cep)
    {
        if (string.IsNullOrWhiteSpace(cep))
        {
            return null;
        }

        if (cep.Any(caractere =>
                caractere is not (>= '0' and <= '9')
                && caractere is not '-'
                && !char.IsWhiteSpace(caractere)))
        {
            throw new ArgumentException(
                "O CEP deve conter apenas digitos ou a formatacao 00000-000.",
                nameof(cep));
        }

        var cepNormalizado = new string(cep
            .Where(caractere => caractere is >= '0' and <= '9')
            .ToArray());

        if (cepNormalizado.Length != CepTamanhoMaximo)
        {
            throw new ArgumentException(
                $"O CEP deve possuir {CepTamanhoMaximo} digitos.",
                nameof(cep));
        }

        return cepNormalizado;
    }

    private static string? NormalizarOpcional(
        string? valor,
        int tamanhoMaximo,
        string nomeParametro)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
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
}

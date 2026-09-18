namespace BFA.Domain.Alunos;

public sealed class Aluno
{
    public const int NomeCompletoTamanhoMaximo = 150;
    public const int CpfTamanho = 11;
    public const int TelefoneTamanhoMaximo = 30;
    public const int EmailTamanhoMaximo = 256;
    public const int ApelidoTamanhoMaximo = 80;
    public const int CepTamanho = 8;
    public const int EstadoCodigoIbgeTamanho = 7;
    public const int BairroTamanhoMaximo = 120;
    public const int LogradouroTamanhoMaximo = 180;
    public const int NumeroTamanhoMaximo = 20;
    public const int ComplementoTamanhoMaximo = 120;
    public const int FotoPerfilChaveTamanhoMaximo = 300;
    public const int FotoPerfilContentTypeTamanhoMaximo = 50;
    public const int IdadeMaioridade = 18;

    private Aluno()
    {
    }

    public Aluno(
        Guid id,
        Guid organizacaoId,
        string nomeCompleto,
        DateOnly dataNascimento,
        DateOnly dataCivilAtual,
        DateTime criadoEmUtc,
        Guid? usuarioId = null,
        string? cpf = null,
        string? telefone = null,
        string? email = null)
    {
        ValidarIdentificadores(id, organizacaoId, usuarioId);
        ValidarDataNascimento(dataNascimento, dataCivilAtual);
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        UsuarioId = usuarioId;
        NomeCompleto = NormalizarObrigatorio(
            nomeCompleto,
            NomeCompletoTamanhoMaximo,
            nameof(nomeCompleto));
        DataNascimento = dataNascimento;
        Cpf = NormalizarCpf(cpf);
        Telefone = TelefoneBrasileiro.Normalizar(telefone);
        Email = NormalizarOpcional(email, EmailTamanhoMaximo, nameof(email));
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid? UsuarioId { get; private set; }

    public string NomeCompleto { get; private set; } = string.Empty;

    public DateOnly DataNascimento { get; private set; }

    public string? Cpf { get; private set; }

    public string? Telefone { get; private set; }

    public string? Email { get; private set; }

    public string? Apelido { get; private set; }

    public string? Cep { get; private set; }

    public int? EstadoCodigoIbge { get; private set; }

    public int? MunicipioCodigoIbge { get; private set; }

    public string? Bairro { get; private set; }

    public string? Logradouro { get; private set; }

    public string? Numero { get; private set; }

    public string? Complemento { get; private set; }

    public string? FotoPerfilChave { get; private set; }

    public string? FotoPerfilContentType { get; private set; }

    public DateTime? FotoPerfilAtualizadaEmUtc { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void AlterarUsuario(Guid? usuarioId, DateTime atualizadoEmUtc)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do usuario, quando informado, deve ser valido.",
                nameof(usuarioId));
        }

        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
        UsuarioId = usuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void AtualizarDados(
        string nomeCompleto,
        DateOnly dataNascimento,
        DateOnly dataCivilAtual,
        string? cpf,
        string? telefone,
        string? email,
        DateTime atualizadoEmUtc)
    {
        ValidarDataNascimento(dataNascimento, dataCivilAtual);
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));

        var nomeNormalizado = NormalizarObrigatorio(
            nomeCompleto,
            NomeCompletoTamanhoMaximo,
            nameof(nomeCompleto));
        var cpfNormalizado = NormalizarCpf(cpf);
        var telefoneNormalizado = TelefoneBrasileiro.Normalizar(telefone);
        var emailNormalizado = NormalizarOpcional(email, EmailTamanhoMaximo, nameof(email));

        NomeCompleto = nomeNormalizado;
        DataNascimento = dataNascimento;
        Cpf = cpfNormalizado;
        Telefone = telefoneNormalizado;
        Email = emailNormalizado;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void AtualizarContato(
        string? telefone,
        string? email,
        DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));

        Telefone = TelefoneBrasileiro.Normalizar(telefone);
        Email = NormalizarOpcional(email, EmailTamanhoMaximo, nameof(email));
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void AtualizarPerfil(
        string? apelido,
        string? cep,
        int? estadoCodigoIbge,
        int? municipioCodigoIbge,
        string? bairro,
        string? logradouro,
        string? numero,
        string? complemento,
        string? fotoPerfilChave,
        string? fotoPerfilContentType,
        DateTime? fotoPerfilAtualizadaEmUtc,
        DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
        if (fotoPerfilAtualizadaEmUtc is { } fotoData)
            ValidarDataUtc(fotoData, nameof(fotoPerfilAtualizadaEmUtc));

        if (municipioCodigoIbge.HasValue && !estadoCodigoIbge.HasValue)
            throw new ArgumentException("O Estado deve ser informado quando o Município for informado.", nameof(estadoCodigoIbge));

        Apelido = NormalizarOpcionalComEspacos(apelido, ApelidoTamanhoMaximo, nameof(apelido));
        Cep = NormalizarCep(cep);
        EstadoCodigoIbge = estadoCodigoIbge;
        MunicipioCodigoIbge = municipioCodigoIbge;
        Bairro = NormalizarOpcional(bairro, BairroTamanhoMaximo, nameof(bairro));
        Logradouro = NormalizarOpcional(logradouro, LogradouroTamanhoMaximo, nameof(logradouro));
        Numero = NormalizarOpcional(numero, NumeroTamanhoMaximo, nameof(numero));
        Complemento = NormalizarOpcional(complemento, ComplementoTamanhoMaximo, nameof(complemento));
        if (!string.IsNullOrWhiteSpace(fotoPerfilChave))
        {
            FotoPerfilChave = NormalizarOpcional(fotoPerfilChave, FotoPerfilChaveTamanhoMaximo, nameof(fotoPerfilChave));
            FotoPerfilContentType = NormalizarOpcional(fotoPerfilContentType, FotoPerfilContentTypeTamanhoMaximo, nameof(fotoPerfilContentType));
            FotoPerfilAtualizadaEmUtc = fotoPerfilAtualizadaEmUtc ?? FotoPerfilAtualizadaEmUtc;
        }
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public bool EhMenorEm(DateOnly dataCivilReferencia)
    {
        if (dataCivilReferencia < DataNascimento)
        {
            throw new ArgumentException(
                "A data civil de referencia nao pode preceder o nascimento.",
                nameof(dataCivilReferencia));
        }

        return dataCivilReferencia < DataNascimento.AddYears(IdadeMaioridade);
    }

    public void Ativar(DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
        Ativo = true;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Desativar(DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
        Ativo = false;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static void ValidarIdentificadores(Guid id, Guid organizacaoId, Guid? usuarioId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador do aluno deve ser informado.", nameof(id));
        }

        if (organizacaoId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da organizacao deve ser informado.",
                nameof(organizacaoId));
        }

        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do usuario, quando informado, deve ser valido.",
                nameof(usuarioId));
        }
    }

    private static void ValidarDataNascimento(
        DateOnly dataNascimento,
        DateOnly dataCivilAtual)
    {
        if (dataNascimento > dataCivilAtual)
        {
            throw new ArgumentException(
                "A data de nascimento nao pode estar no futuro.",
                nameof(dataNascimento));
        }
    }

    private static string? NormalizarCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return null;
        }

        var cpfNormalizado = cpf.Trim();

        if (cpfNormalizado.Length != CpfTamanho
            || cpfNormalizado.Any(caractere => caractere is not (>= '0' and <= '9')))
        {
            throw new ArgumentException("O CPF deve possuir exatamente 11 digitos.", nameof(cpf));
        }

        return cpfNormalizado;
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

    private static string? NormalizarOpcionalComEspacos(
        string? valor,
        int tamanhoMaximo,
        string nomeParametro)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        var normalizado = string.Join(' ', valor.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalizado.Length > tamanhoMaximo)
            throw new ArgumentException($"O valor deve possuir no maximo {tamanhoMaximo} caracteres.", nomeParametro);
        if (normalizado.Any(c => char.IsControl(c)))
            throw new ArgumentException("O valor informado possui caracteres invalidos.", nomeParametro);
        return normalizado;
    }

    private static string? NormalizarCep(string? cep)
    {
        if (string.IsNullOrWhiteSpace(cep))
            return null;

        var digitos = new string(cep.Where(char.IsDigit).ToArray());
        if (digitos.Length != CepTamanho || cep.Any(c => !char.IsDigit(c) && c is not ' ' and not '-' and not '.'))
            throw new ArgumentException("O CEP deve possuir 8 digitos.", nameof(cep));
        return digitos;
    }

    private static void ValidarDataUtc(DateTime data, string nomeParametro)
    {
        if (data.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data deve estar em UTC.", nomeParametro);
        }
    }
}

namespace BFA.Domain.Alunos;

public sealed class Responsavel
{
    public const int NomeCompletoTamanhoMaximo = 150;
    public const int CpfTamanho = 11;
    public const int TelefoneTamanhoMaximo = 30;
    public const int EmailTamanhoMaximo = 256;

    private Responsavel()
    {
    }

    public Responsavel(
        Guid id,
        Guid organizacaoId,
        string nomeCompleto,
        DateTime criadoEmUtc,
        Guid? usuarioId = null,
        string? cpf = null,
        string? telefone = null,
        string? email = null)
    {
        ValidarIdentificadores(id, organizacaoId, usuarioId);
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        var telefoneNormalizado = NormalizarOpcional(
            telefone,
            TelefoneTamanhoMaximo,
            nameof(telefone));
        var emailNormalizado = NormalizarOpcional(email, EmailTamanhoMaximo, nameof(email));
        ValidarContato(telefoneNormalizado, emailNormalizado);

        Id = id;
        OrganizacaoId = organizacaoId;
        UsuarioId = usuarioId;
        NomeCompleto = NormalizarObrigatorio(
            nomeCompleto,
            NomeCompletoTamanhoMaximo,
            nameof(nomeCompleto));
        Cpf = NormalizarCpf(cpf);
        Telefone = telefoneNormalizado;
        Email = emailNormalizado;
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid? UsuarioId { get; private set; }

    public string NomeCompleto { get; private set; } = string.Empty;

    public string? Cpf { get; private set; }

    public string? Telefone { get; private set; }

    public string? Email { get; private set; }

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
        string? cpf,
        string? telefone,
        string? email,
        DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));

        var nomeNormalizado = NormalizarObrigatorio(
            nomeCompleto,
            NomeCompletoTamanhoMaximo,
            nameof(nomeCompleto));
        var cpfNormalizado = NormalizarCpf(cpf);
        var telefoneNormalizado = NormalizarOpcional(
            telefone,
            TelefoneTamanhoMaximo,
            nameof(telefone));
        var emailNormalizado = NormalizarOpcional(email, EmailTamanhoMaximo, nameof(email));
        ValidarContato(telefoneNormalizado, emailNormalizado);

        NomeCompleto = nomeNormalizado;
        Cpf = cpfNormalizado;
        Telefone = telefoneNormalizado;
        Email = emailNormalizado;
        AtualizadoEmUtc = atualizadoEmUtc;
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
            throw new ArgumentException(
                "O identificador do responsavel deve ser informado.",
                nameof(id));
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

    private static void ValidarContato(string? telefone, string? email)
    {
        if (telefone is null && email is null)
        {
            throw new ArgumentException(
                "O responsavel deve possuir ao menos um telefone ou email para contato.");
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

    private static void ValidarDataUtc(DateTime data, string nomeParametro)
    {
        if (data.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data deve estar em UTC.", nomeParametro);
        }
    }
}

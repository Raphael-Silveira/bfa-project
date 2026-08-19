namespace BFA.Domain.Usuarios;

public sealed class PerfilUsuario
{
    public const int NomeCompletoTamanhoMaximo = 150;
    public const int TelefoneTamanhoMaximo = 30;

    private PerfilUsuario()
    {
    }

    public PerfilUsuario(
        Guid id,
        Guid usuarioId,
        string nomeCompleto,
        string? telefone,
        DateTime criadoEmUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador do perfil deve ser informado.", nameof(id));
        }

        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("O identificador do usuario deve ser informado.", nameof(usuarioId));
        }

        if (criadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        }

        Id = id;
        UsuarioId = usuarioId;
        NomeCompleto = NormalizarNomeCompleto(nomeCompleto);
        Telefone = NormalizarOpcional(telefone, TelefoneTamanhoMaximo, nameof(telefone));
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public string NomeCompleto { get; private set; } = string.Empty;

    public string? Telefone { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void AtualizarDados(
        string nomeCompleto,
        string? telefone,
        DateTime atualizadoEmUtc)
    {
        if (atualizadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "A data de atualizacao deve estar em UTC.",
                nameof(atualizadoEmUtc));
        }

        NomeCompleto = NormalizarNomeCompleto(nomeCompleto);
        Telefone = NormalizarOpcional(telefone, TelefoneTamanhoMaximo, nameof(telefone));
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static string NormalizarNomeCompleto(string nomeCompleto)
    {
        if (string.IsNullOrWhiteSpace(nomeCompleto))
        {
            throw new ArgumentException(
                "O nome completo deve ser informado.",
                nameof(nomeCompleto));
        }

        var nomeNormalizado = nomeCompleto.Trim();

        if (nomeNormalizado.Length > NomeCompletoTamanhoMaximo)
        {
            throw new ArgumentException(
                $"O nome completo deve possuir no maximo {NomeCompletoTamanhoMaximo} caracteres.",
                nameof(nomeCompleto));
        }

        return nomeNormalizado;
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

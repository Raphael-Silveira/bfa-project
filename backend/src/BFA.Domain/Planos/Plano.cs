namespace BFA.Domain.Planos;

public sealed class Plano
{
    public const int NomeTamanhoMaximo = 150;

    private Plano()
    {
    }

    public Plano(
        Guid id,
        Guid organizacaoId,
        Guid? unidadeId,
        string nome,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        if (unidadeId.HasValue)
        {
            ValidarIdentificador(unidadeId.Value, nameof(unidadeId));
        }

        ValidarIdentificador(criadoPorUsuarioId, nameof(criadoPorUsuarioId));
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        Nome = NormalizarNome(nome);
        Ativo = true;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        AtualizadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid? UnidadeId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public bool Ativo { get; private set; }

    public Guid CriadoPorUsuarioId { get; private set; }

    public Guid AtualizadoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public bool EhPlanoRede => UnidadeId is null;

    public void Ativar(Guid atualizadoPorUsuarioId, DateTime atualizadoEmUtc)
    {
        ValidarAtualizacao(atualizadoPorUsuarioId, atualizadoEmUtc);
        Ativo = true;
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Desativar(Guid atualizadoPorUsuarioId, DateTime atualizadoEmUtc)
    {
        ValidarAtualizacao(atualizadoPorUsuarioId, atualizadoEmUtc);
        Ativo = false;
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static string NormalizarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome do plano deve ser informado.", nameof(nome));
        }

        var normalizado = nome.Trim();
        if (normalizado.Length > NomeTamanhoMaximo)
        {
            throw new ArgumentException(
                $"O nome do plano deve possuir no maximo {NomeTamanhoMaximo} caracteres.",
                nameof(nome));
        }

        return normalizado;
    }

    private static void ValidarAtualizacao(
        Guid atualizadoPorUsuarioId,
        DateTime atualizadoEmUtc)
    {
        ValidarIdentificador(atualizadoPorUsuarioId, nameof(atualizadoPorUsuarioId));
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
    }

    private static void ValidarIdentificador(Guid valor, string parametro)
    {
        if (valor == Guid.Empty)
        {
            throw new ArgumentException("O identificador deve ser informado.", parametro);
        }
    }

    private static void ValidarDataUtc(DateTime valor, string parametro)
    {
        if (valor.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data deve estar em UTC.", parametro);
        }
    }
}

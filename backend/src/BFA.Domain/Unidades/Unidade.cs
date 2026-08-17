namespace BFA.Domain.Unidades;

public sealed class Unidade
{
    private Unidade()
    {
    }

    public Unidade(
        Guid id,
        Guid organizacaoId,
        string nome,
        string slug,
        DateTime criadoEmUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador da unidade deve ser informado.", nameof(id));
        }

        if (organizacaoId == Guid.Empty)
        {
            throw new ArgumentException("O identificador da organizacao deve ser informado.", nameof(organizacaoId));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome da unidade deve ser informado.", nameof(nome));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("O slug da unidade deve ser informado.", nameof(slug));
        }

        if (criadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        }

        Id = id;
        OrganizacaoId = organizacaoId;
        Nome = nome.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Ativa = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public bool Ativa { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }
}

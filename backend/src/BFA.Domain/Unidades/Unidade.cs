namespace BFA.Domain.Unidades;

public sealed class Unidade
{
    public const int NomeTamanhoMaximo = 150;
    public const int SlugTamanhoMaximo = 100;

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

        if (criadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        }

        Id = id;
        OrganizacaoId = organizacaoId;
        Nome = NormalizarNome(nome);
        Slug = NormalizarSlug(slug);
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

    public void Atualizar(string nome, string slug, DateTime atualizadoEmUtc)
    {
        ValidarDataAtualizacao(atualizadoEmUtc);
        Nome = NormalizarNome(nome);
        Slug = NormalizarSlug(slug);
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Ativar(DateTime atualizadoEmUtc)
    {
        ValidarDataAtualizacao(atualizadoEmUtc);
        Ativa = true;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Desativar(DateTime atualizadoEmUtc)
    {
        ValidarDataAtualizacao(atualizadoEmUtc);
        Ativa = false;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public static string NormalizarSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("O slug da unidade deve ser informado.", nameof(slug));
        }

        var slugNormalizado = slug.Trim().ToLowerInvariant();

        if (slugNormalizado.Length > SlugTamanhoMaximo)
        {
            throw new ArgumentException(
                $"O slug da unidade deve possuir no maximo {SlugTamanhoMaximo} caracteres.",
                nameof(slug));
        }

        return slugNormalizado;
    }

    private static string NormalizarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome da unidade deve ser informado.", nameof(nome));
        }

        var nomeNormalizado = nome.Trim();

        if (nomeNormalizado.Length > NomeTamanhoMaximo)
        {
            throw new ArgumentException(
                $"O nome da unidade deve possuir no maximo {NomeTamanhoMaximo} caracteres.",
                nameof(nome));
        }

        return nomeNormalizado;
    }

    private static void ValidarDataAtualizacao(DateTime atualizadoEmUtc)
    {
        if (atualizadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "A data de atualizacao deve estar em UTC.",
                nameof(atualizadoEmUtc));
        }
    }
}

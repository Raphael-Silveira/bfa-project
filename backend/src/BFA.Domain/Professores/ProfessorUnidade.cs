namespace BFA.Domain.Professores;

public sealed class ProfessorUnidade
{
    private ProfessorUnidade()
    {
    }

    public ProfessorUnidade(
        Guid id,
        Guid organizacaoId,
        Guid professorId,
        Guid unidadeId,
        DateTime criadoEmUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador do vinculo deve ser informado.", nameof(id));
        }

        if (organizacaoId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da organizacao deve ser informado.",
                nameof(organizacaoId));
        }

        if (professorId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do professor deve ser informado.",
                nameof(professorId));
        }

        if (unidadeId == Guid.Empty)
        {
            throw new ArgumentException("O identificador da unidade deve ser informado.", nameof(unidadeId));
        }

        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        ProfessorId = professorId;
        UnidadeId = unidadeId;
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid ProfessorId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

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

    private static void ValidarDataUtc(DateTime data, string nomeParametro)
    {
        if (data.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data deve estar em UTC.", nomeParametro);
        }
    }
}

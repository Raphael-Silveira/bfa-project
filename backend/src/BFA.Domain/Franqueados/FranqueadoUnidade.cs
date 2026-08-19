namespace BFA.Domain.Franqueados;

public sealed class FranqueadoUnidade
{
    private FranqueadoUnidade()
    {
    }

    public FranqueadoUnidade(
        Guid id,
        Guid franqueadoId,
        Guid organizacaoId,
        Guid unidadeId,
        DateTime criadoEmUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador do vinculo deve ser informado.", nameof(id));
        }

        if (franqueadoId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do franqueado deve ser informado.",
                nameof(franqueadoId));
        }

        if (organizacaoId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da organizacao deve ser informado.",
                nameof(organizacaoId));
        }

        if (unidadeId == Guid.Empty)
        {
            throw new ArgumentException("O identificador da unidade deve ser informado.", nameof(unidadeId));
        }

        if (criadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        }

        Id = id;
        FranqueadoId = franqueadoId;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid FranqueadoId { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void Ativar(DateTime atualizadoEmUtc)
    {
        ValidarDataAtualizacao(atualizadoEmUtc);
        Ativo = true;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Desativar(DateTime atualizadoEmUtc)
    {
        ValidarDataAtualizacao(atualizadoEmUtc);
        Ativo = false;
        AtualizadoEmUtc = atualizadoEmUtc;
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

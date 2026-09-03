namespace BFA.Domain.Planos;

public sealed class PlanoDisponibilidadeUnidade
{
    private PlanoDisponibilidadeUnidade()
    {
    }

    public PlanoDisponibilidadeUnidade(
        Guid id,
        Guid organizacaoId,
        Guid planoId,
        Guid unidadeId,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(planoId, nameof(planoId));
        ValidarIdentificador(unidadeId, nameof(unidadeId));
        ValidarIdentificador(criadoPorUsuarioId, nameof(criadoPorUsuarioId));
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        PlanoId = planoId;
        UnidadeId = unidadeId;
        Ativo = true;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        AtualizadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid PlanoId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public bool Ativo { get; private set; }

    public Guid CriadoPorUsuarioId { get; private set; }

    public Guid AtualizadoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

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

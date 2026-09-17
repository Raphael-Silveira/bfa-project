namespace BFA.Domain.Aulas;

public sealed class ConfirmacaoAulaAluno
{
    private ConfirmacaoAulaAluno()
    {
    }

    public ConfirmacaoAulaAluno(
        Guid id,
        Guid organizacaoId,
        Guid unidadeId,
        Guid aulaId,
        Guid alunoId,
        DateTime confirmadaEmUtc,
        DateTime criadoEmUtc)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(unidadeId, nameof(unidadeId));
        ValidarIdentificador(aulaId, nameof(aulaId));
        ValidarIdentificador(alunoId, nameof(alunoId));
        ValidarDataUtc(confirmadaEmUtc, nameof(confirmadaEmUtc));
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        AulaId = aulaId;
        AlunoId = alunoId;
        Ativa = true;
        ConfirmadaEmUtc = confirmadaEmUtc;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizacaoId { get; private set; }
    public Guid UnidadeId { get; private set; }
    public Guid AulaId { get; private set; }
    public Guid AlunoId { get; private set; }
    public bool Ativa { get; private set; }
    public DateTime? ConfirmadaEmUtc { get; private set; }
    public DateTime CriadoEmUtc { get; private set; }
    public DateTime AtualizadoEmUtc { get; private set; }

    public void Confirmar(DateTime agoraUtc)
    {
        ValidarDataUtc(agoraUtc, nameof(agoraUtc));
        Ativa = true;
        ConfirmadaEmUtc = agoraUtc;
        AtualizadoEmUtc = agoraUtc;
    }

    public void Cancelar(DateTime agoraUtc)
    {
        ValidarDataUtc(agoraUtc, nameof(agoraUtc));
        Ativa = false;
        AtualizadoEmUtc = agoraUtc;
    }

    private static void ValidarIdentificador(Guid valor, string parametro)
    {
        if (valor == Guid.Empty)
            throw new ArgumentException("O identificador deve ser informado.", parametro);
    }

    private static void ValidarDataUtc(DateTime valor, string parametro)
    {
        if (valor.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A data deve estar em UTC.", parametro);
    }
}

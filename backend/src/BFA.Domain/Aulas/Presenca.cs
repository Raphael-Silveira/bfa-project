namespace BFA.Domain.Aulas;

public sealed class Presenca
{
    public const int ObservacoesTamanhoMaximo = 500;

    private Presenca()
    {
    }

    public Presenca(
        Guid id,
        Guid organizacaoId,
        Guid unidadeId,
        Guid aulaId,
        Guid alunoId,
        Guid matriculaId,
        StatusPresenca status,
        Guid registradoPorUsuarioId,
        DateTime criadoEmUtc,
        string? observacoes = null)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(unidadeId, nameof(unidadeId));
        ValidarIdentificador(aulaId, nameof(aulaId));
        ValidarIdentificador(alunoId, nameof(alunoId));
        ValidarIdentificador(matriculaId, nameof(matriculaId));
        ValidarIdentificador(registradoPorUsuarioId, nameof(registradoPorUsuarioId));
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        AulaId = aulaId;
        AlunoId = alunoId;
        MatriculaId = matriculaId;
        Status = status;
        Observacoes = NormalizarObservacoes(observacoes);
        RegistradoPorUsuarioId = registradoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public Guid AulaId { get; private set; }

    public Guid AlunoId { get; private set; }

    public Guid MatriculaId { get; private set; }

    public StatusPresenca Status { get; private set; }

    public TimeOnly? ChegouAs { get; private set; }

    public TimeOnly? SaiuAs { get; private set; }

    public string? Observacoes { get; private set; }

    public Guid RegistradoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void Registrar(
        StatusPresenca status,
        string? observacoes,
        DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));

        Status = status;
        Observacoes = NormalizarObservacoes(observacoes);
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void RegistrarHorarios(
        TimeOnly? chegouAs,
        TimeOnly? saiuAs,
        DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));

        ChegouAs = chegouAs;
        SaiuAs = saiuAs;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static string? NormalizarObservacoes(string? observacoes)
    {
        if (string.IsNullOrWhiteSpace(observacoes))
        {
            return null;
        }

        var observacoesNormalizadas = observacoes.Trim();
        if (observacoesNormalizadas.Length > ObservacoesTamanhoMaximo)
        {
            throw new ArgumentException(
                $"As observacoes devem possuir no maximo {ObservacoesTamanhoMaximo} caracteres.",
                nameof(observacoes));
        }

        return observacoesNormalizadas;
    }

    private static void ValidarIdentificador(Guid valor, string parametro)
    {
        if (valor == Guid.Empty)
        {
            throw new ArgumentException("O identificador deve ser informado.", parametro);
        }
    }

    private static void ValidarDataUtc(DateTime data, string parametro)
    {
        if (data.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data deve estar em UTC.", parametro);
        }
    }
}

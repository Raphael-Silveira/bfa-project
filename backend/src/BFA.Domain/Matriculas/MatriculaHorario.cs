namespace BFA.Domain.Matriculas;

public sealed class MatriculaHorario
{
    private MatriculaHorario()
    {
    }

    public MatriculaHorario(
        Guid id,
        Guid organizacaoId,
        Guid unidadeId,
        Guid matriculaId,
        Guid turmaHorarioId,
        DateOnly vigenciaInicio,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(unidadeId, nameof(unidadeId));
        ValidarIdentificador(matriculaId, nameof(matriculaId));
        ValidarIdentificador(turmaHorarioId, nameof(turmaHorarioId));
        ValidarIdentificador(criadoPorUsuarioId, nameof(criadoPorUsuarioId));
        ValidarDataCivil(vigenciaInicio, nameof(vigenciaInicio));
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        MatriculaId = matriculaId;
        TurmaHorarioId = turmaHorarioId;
        VigenciaInicio = vigenciaInicio;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        AtualizadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public Guid MatriculaId { get; private set; }

    public Guid TurmaHorarioId { get; private set; }

    public DateOnly VigenciaInicio { get; private set; }

    public DateOnly? VigenciaFim { get; private set; }

    public Guid CriadoPorUsuarioId { get; private set; }

    public Guid AtualizadoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void Encerrar(
        DateOnly vigenciaFim,
        Guid atualizadoPorUsuarioId,
        DateTime atualizadoEmUtc)
    {
        if (VigenciaFim.HasValue)
        {
            throw new InvalidOperationException(
                "A vigencia final do horario da matricula ja foi preenchida.");
        }

        if (vigenciaFim < VigenciaInicio)
        {
            throw new ArgumentException(
                "A vigencia final nao pode ser anterior a vigencia inicial.",
                nameof(vigenciaFim));
        }

        ValidarIdentificador(atualizadoPorUsuarioId, nameof(atualizadoPorUsuarioId));
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));

        VigenciaFim = vigenciaFim;
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static void ValidarDataCivil(DateOnly valor, string parametro)
    {
        if (valor == default)
        {
            throw new ArgumentException("A data civil deve ser informada.", parametro);
        }
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

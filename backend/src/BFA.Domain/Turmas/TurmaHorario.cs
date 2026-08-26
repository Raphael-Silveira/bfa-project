namespace BFA.Domain.Turmas;

public sealed class TurmaHorario
{
    private TurmaHorario()
    {
    }

    public TurmaHorario(
        Guid id,
        Guid organizacaoId,
        Guid unidadeId,
        Guid turmaId,
        Guid professorUnidadeId,
        DiaSemana diaSemana,
        TimeOnly horaInicio,
        TimeOnly horaFim,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(unidadeId, nameof(unidadeId));
        ValidarIdentificador(turmaId, nameof(turmaId));
        ValidarIdentificador(professorUnidadeId, nameof(professorUnidadeId));
        ValidarIdentificador(criadoPorUsuarioId, nameof(criadoPorUsuarioId));
        ValidarDiaSemana(diaSemana);
        ValidarHorario(horaInicio, horaFim);
        ValidarVigencia(vigenciaInicio, vigenciaFim);
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        TurmaId = turmaId;
        ProfessorUnidadeId = professorUnidadeId;
        DiaSemana = diaSemana;
        HoraInicio = horaInicio;
        HoraFim = horaFim;
        VigenciaInicio = vigenciaInicio;
        VigenciaFim = vigenciaFim;
        Ativo = true;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        AtualizadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public Guid TurmaId { get; private set; }

    public Guid ProfessorUnidadeId { get; private set; }

    public DiaSemana DiaSemana { get; private set; }

    public TimeOnly HoraInicio { get; private set; }

    public TimeOnly HoraFim { get; private set; }

    public DateOnly VigenciaInicio { get; private set; }

    public DateOnly? VigenciaFim { get; private set; }

    public bool Ativo { get; private set; }

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
                "A vigencia final do horario recorrente ja foi preenchida.");
        }

        ValidarVigencia(VigenciaInicio, vigenciaFim);
        ValidarAtualizacao(atualizadoPorUsuarioId, atualizadoEmUtc);
        VigenciaFim = vigenciaFim;
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

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

    private static void ValidarDiaSemana(DiaSemana diaSemana)
    {
        if (!Enum.IsDefined(diaSemana))
        {
            throw new ArgumentOutOfRangeException(
                nameof(diaSemana),
                diaSemana,
                "O dia da semana deve seguir o padrao ISO 8601 de 1 a 7.");
        }
    }

    private static void ValidarHorario(TimeOnly horaInicio, TimeOnly horaFim)
    {
        if (horaInicio >= horaFim)
        {
            throw new ArgumentException(
                "A hora final deve ser posterior a hora inicial e nao pode atravessar a meia-noite.",
                nameof(horaFim));
        }
    }

    private static void ValidarVigencia(DateOnly vigenciaInicio, DateOnly? vigenciaFim)
    {
        if (vigenciaInicio == default)
        {
            throw new ArgumentException(
                "A vigencia inicial deve ser informada.",
                nameof(vigenciaInicio));
        }

        if (vigenciaFim.HasValue && vigenciaFim.Value < vigenciaInicio)
        {
            throw new ArgumentException(
                "A vigencia final deve ser igual ou posterior a vigencia inicial.",
                nameof(vigenciaFim));
        }
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

    private static void ValidarDataUtc(DateTime data, string parametro)
    {
        if (data.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data deve estar em UTC.", parametro);
        }
    }
}

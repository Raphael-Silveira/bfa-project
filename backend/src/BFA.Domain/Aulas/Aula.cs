namespace BFA.Domain.Aulas;

public sealed class Aula
{
    public const int ObservacoesTamanhoMaximo = 500;

    private Aula()
    {
    }

    public Aula(
        Guid id,
        Guid organizacaoId,
        Guid unidadeId,
        Guid turmaId,
        Guid turmaHorarioId,
        DateOnly data,
        TimeOnly horaInicio,
        TimeOnly horaFim,
        int capacidade,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc,
        string? observacoes = null)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(unidadeId, nameof(unidadeId));
        ValidarIdentificador(turmaId, nameof(turmaId));
        ValidarIdentificador(turmaHorarioId, nameof(turmaHorarioId));
        ValidarIdentificador(criadoPorUsuarioId, nameof(criadoPorUsuarioId));
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));
        ValidarIntervalo(horaInicio, horaFim);
        ValidarCapacidade(capacidade);

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        TurmaId = turmaId;
        TurmaHorarioId = turmaHorarioId;
        Data = data;
        HoraInicio = horaInicio;
        HoraFim = horaFim;
        Status = StatusAula.Programada;
        Capacidade = capacidade;
        Observacoes = NormalizarObservacoes(observacoes);
        CriadoPorUsuarioId = criadoPorUsuarioId;
        AtualizadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public Guid TurmaId { get; private set; }

    public Guid TurmaHorarioId { get; private set; }

    public DateOnly Data { get; private set; }

    public TimeOnly HoraInicio { get; private set; }

    public TimeOnly HoraFim { get; private set; }

    public StatusAula Status { get; private set; }

    public int Capacidade { get; private set; }

    public string? Observacoes { get; private set; }

    public Guid CriadoPorUsuarioId { get; private set; }

    public Guid AtualizadoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void Concluir(Guid atualizadoPorUsuarioId, DateTime atualizadoEmUtc)
    {
        ValidarAtualizacao(atualizadoPorUsuarioId, atualizadoEmUtc);

        if (Status != StatusAula.Programada)
        {
            throw new InvalidOperationException(
                "Apenas aulas programadas podem ser concluidas.");
        }

        Status = StatusAula.Concluida;
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Cancelar(Guid atualizadoPorUsuarioId, DateTime atualizadoEmUtc)
    {
        ValidarAtualizacao(atualizadoPorUsuarioId, atualizadoEmUtc);

        if (Status != StatusAula.Programada)
        {
            throw new InvalidOperationException(
                "Apenas aulas programadas podem ser canceladas.");
        }

        Status = StatusAula.Cancelada;
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void AtualizarObservacoes(
        string? observacoes,
        Guid atualizadoPorUsuarioId,
        DateTime atualizadoEmUtc)
    {
        ValidarAtualizacao(atualizadoPorUsuarioId, atualizadoEmUtc);

        Observacoes = NormalizarObservacoes(observacoes);
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
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

    private static void ValidarIntervalo(TimeOnly horaInicio, TimeOnly horaFim)
    {
        if (horaInicio >= horaFim)
        {
            throw new ArgumentException(
                "A hora de inicio deve ser anterior a hora de fim.",
                nameof(horaInicio));
        }
    }

    private static void ValidarCapacidade(int capacidade)
    {
        if (capacidade <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacidade),
                capacidade,
                "A capacidade da aula deve ser maior que zero.");
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

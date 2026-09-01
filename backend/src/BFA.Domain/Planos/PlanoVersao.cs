namespace BFA.Domain.Planos;

public sealed class PlanoVersao
{
    private PlanoVersao()
    {
    }

    public PlanoVersao(
        Guid id,
        Guid organizacaoId,
        Guid planoId,
        int numeroVersao,
        int duracaoMeses,
        int frequenciaSemanal,
        decimal valorMensal,
        bool cobraMatricula,
        decimal? valorMatricula,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(planoId, nameof(planoId));
        ValidarIdentificador(criadoPorUsuarioId, nameof(criadoPorUsuarioId));

        if (numeroVersao <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numeroVersao), numeroVersao,
                "O numero da versao deve ser maior que zero.");
        }

        if (duracaoMeses <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duracaoMeses), duracaoMeses,
                "A duracao do plano deve ser maior que zero.");
        }

        if (frequenciaSemanal is < 1 or > 7)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frequenciaSemanal), frequenciaSemanal,
                "A frequencia semanal deve estar entre um e sete.");
        }

        if (valorMensal <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(valorMensal), valorMensal,
                "O valor mensal deve ser maior que zero.");
        }

        ValidarMatricula(cobraMatricula, valorMatricula);
        ValidarVigencia(vigenciaInicio, vigenciaFim);
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        PlanoId = planoId;
        NumeroVersao = numeroVersao;
        DuracaoMeses = duracaoMeses;
        FrequenciaSemanal = frequenciaSemanal;
        ValorMensal = valorMensal;
        CobraMatricula = cobraMatricula;
        ValorMatricula = valorMatricula;
        VigenciaInicio = vigenciaInicio;
        VigenciaFim = vigenciaFim;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid PlanoId { get; private set; }

    public int NumeroVersao { get; private set; }

    public int DuracaoMeses { get; private set; }

    public int FrequenciaSemanal { get; private set; }

    public decimal ValorMensal { get; private set; }

    public bool CobraMatricula { get; private set; }

    public decimal? ValorMatricula { get; private set; }

    public DateOnly VigenciaInicio { get; private set; }

    public DateOnly? VigenciaFim { get; private set; }

    public Guid CriadoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public void Encerrar(DateOnly vigenciaFim)
    {
        if (VigenciaFim.HasValue)
        {
            throw new InvalidOperationException("A versao do plano ja possui vigencia final.");
        }

        ValidarVigencia(VigenciaInicio, vigenciaFim);
        VigenciaFim = vigenciaFim;
    }

    private static void ValidarMatricula(bool cobraMatricula, decimal? valorMatricula)
    {
        if (cobraMatricula && (!valorMatricula.HasValue || valorMatricula.Value <= 0))
        {
            throw new ArgumentException(
                "Um plano que cobra matricula deve possuir valor de matricula maior que zero.",
                nameof(valorMatricula));
        }

        if (!cobraMatricula && valorMatricula.HasValue)
        {
            throw new ArgumentException(
                "Um plano que nao cobra matricula nao deve possuir valor de matricula.",
                nameof(valorMatricula));
        }
    }

    private static void ValidarVigencia(
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim)
    {
        if (vigenciaInicio == default)
        {
            throw new ArgumentException(
                "A vigencia inicial deve ser informada.", nameof(vigenciaInicio));
        }

        if (vigenciaFim.HasValue && vigenciaFim.Value < vigenciaInicio)
        {
            throw new ArgumentException(
                "A vigencia final nao pode ser anterior a vigencia inicial.",
                nameof(vigenciaFim));
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

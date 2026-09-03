namespace BFA.Domain.Matriculas;

public sealed class Matricula
{
    private Matricula()
    {
    }

    public Matricula(
        Guid id,
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        Guid planoVersaoId,
        DateOnly dataInicio,
        int duracaoMeses,
        decimal valorMensalContratado,
        bool cobraTaxaMatricula,
        decimal? valorTaxaMatricula,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(unidadeId, nameof(unidadeId));
        ValidarIdentificador(alunoId, nameof(alunoId));
        ValidarIdentificador(planoVersaoId, nameof(planoVersaoId));
        ValidarIdentificador(criadoPorUsuarioId, nameof(criadoPorUsuarioId));
        ValidarDataCivil(dataInicio, nameof(dataInicio));
        ValidarPreco(valorMensalContratado);
        ValidarTaxaMatricula(cobraTaxaMatricula, valorTaxaMatricula);
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        AlunoId = alunoId;
        PlanoVersaoId = planoVersaoId;
        DataInicio = dataInicio;
        DataFimPrevista = CalcularDataFimPrevista(dataInicio, duracaoMeses);
        Status = StatusMatricula.Ativa;
        ValorMensalContratado = valorMensalContratado;
        CobraTaxaMatricula = cobraTaxaMatricula;
        ValorTaxaMatricula = valorTaxaMatricula;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        AtualizadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public Guid AlunoId { get; private set; }

    public Guid PlanoVersaoId { get; private set; }

    public DateOnly DataInicio { get; private set; }

    public DateOnly DataFimPrevista { get; private set; }

    public DateOnly? DataFimReal { get; private set; }

    public StatusMatricula Status { get; private set; }

    public decimal ValorMensalContratado { get; private set; }

    public bool CobraTaxaMatricula { get; private set; }

    public decimal? ValorTaxaMatricula { get; private set; }

    public Guid CriadoPorUsuarioId { get; private set; }

    public Guid AtualizadoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public static DateOnly CalcularDataFimPrevista(
        DateOnly dataInicio,
        int duracaoMeses)
    {
        ValidarDataCivil(dataInicio, nameof(dataInicio));

        if (duracaoMeses <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duracaoMeses),
                duracaoMeses,
                "A duracao da matricula deve ser maior que zero.");
        }

        return dataInicio.AddMonths(duracaoMeses).AddDays(-1);
    }

    public void Encerrar(
        DateOnly dataFimReal,
        Guid atualizadoPorUsuarioId,
        DateTime atualizadoEmUtc) =>
        Finalizar(
            StatusMatricula.Encerrada,
            dataFimReal,
            atualizadoPorUsuarioId,
            atualizadoEmUtc);

    public void Cancelar(
        DateOnly dataFimReal,
        Guid atualizadoPorUsuarioId,
        DateTime atualizadoEmUtc) =>
        Finalizar(
            StatusMatricula.Cancelada,
            dataFimReal,
            atualizadoPorUsuarioId,
            atualizadoEmUtc);

    private void Finalizar(
        StatusMatricula novoStatus,
        DateOnly dataFimReal,
        Guid atualizadoPorUsuarioId,
        DateTime atualizadoEmUtc)
    {
        if (Status != StatusMatricula.Ativa)
        {
            throw new InvalidOperationException(
                "Uma matricula em estado terminal nao pode mudar novamente de status.");
        }

        if (novoStatus is not (StatusMatricula.Encerrada or StatusMatricula.Cancelada))
        {
            throw new ArgumentOutOfRangeException(
                nameof(novoStatus),
                "O estado final da matricula e invalido.");
        }

        if (dataFimReal < DataInicio)
        {
            throw new ArgumentException(
                "A data final real nao pode ser anterior ao inicio da matricula.",
                nameof(dataFimReal));
        }

        ValidarIdentificador(atualizadoPorUsuarioId, nameof(atualizadoPorUsuarioId));
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));

        Status = novoStatus;
        DataFimReal = dataFimReal;
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static void ValidarPreco(decimal valorMensalContratado)
    {
        if (valorMensalContratado <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(valorMensalContratado),
                valorMensalContratado,
                "O valor mensal contratado deve ser maior que zero.");
        }
    }

    private static void ValidarTaxaMatricula(
        bool cobraTaxaMatricula,
        decimal? valorTaxaMatricula)
    {
        if (cobraTaxaMatricula && (!valorTaxaMatricula.HasValue
            || valorTaxaMatricula.Value <= 0))
        {
            throw new ArgumentException(
                "Uma matricula que cobra taxa deve possuir valor de taxa maior que zero.",
                nameof(valorTaxaMatricula));
        }

        if (!cobraTaxaMatricula && valorTaxaMatricula.HasValue)
        {
            throw new ArgumentException(
                "Uma matricula isenta de taxa deve possuir valor de taxa nulo.",
                nameof(valorTaxaMatricula));
        }
    }

    private static void ValidarDataCivil(DateOnly data, string parametro)
    {
        if (data == default)
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

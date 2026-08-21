namespace BFA.Domain.Contratos;

public sealed class ContratoFranquiaVersao
{
    public const int StatusTamanhoMaximo = 30;
    public const int MotivoAlteracaoTamanhoMaximo = 1000;
    public const int ObservacoesTamanhoMaximo = 4000;

    private ContratoFranquiaVersao()
    {
    }

    public ContratoFranquiaVersao(
        Guid id,
        Guid contratoFranquiaId,
        int numeroVersao,
        DateOnly dataInicio,
        DateOnly? dataFim,
        decimal percentualRoyalties,
        decimal mensalidadeFixa,
        decimal? taxaAdesao,
        int? diaVencimento,
        StatusVersaoContratoFranquia status,
        string? motivoAlteracao,
        string? observacoes,
        DateTime criadoEmUtc,
        Guid criadoPorUsuarioId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da versao do contrato deve ser informado.",
                nameof(id));
        }

        if (contratoFranquiaId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do contrato deve ser informado.",
                nameof(contratoFranquiaId));
        }

        if (numeroVersao < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numeroVersao),
                numeroVersao,
                "O numero da versao deve ser maior ou igual a um.");
        }

        ValidarTermos(
            dataInicio,
            dataFim,
            percentualRoyalties,
            mensalidadeFixa,
            taxaAdesao,
            diaVencimento);

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "O status da versao do contrato e invalido.");
        }

        if (criadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        }

        if (criadoPorUsuarioId == Guid.Empty)
        {
            throw new ArgumentException(
                "O usuario responsavel pela criacao deve ser informado.",
                nameof(criadoPorUsuarioId));
        }

        Id = id;
        ContratoFranquiaId = contratoFranquiaId;
        NumeroVersao = numeroVersao;
        DataInicio = dataInicio;
        DataFim = dataFim;
        PercentualRoyalties = percentualRoyalties;
        MensalidadeFixa = mensalidadeFixa;
        TaxaAdesao = taxaAdesao;
        DiaVencimento = diaVencimento;
        Status = status;
        MotivoAlteracao = NormalizarOpcionalInformado(
            motivoAlteracao,
            MotivoAlteracaoTamanhoMaximo,
            nameof(motivoAlteracao));
        Observacoes = NormalizarOpcionalInformado(
            observacoes,
            ObservacoesTamanhoMaximo,
            nameof(observacoes));
        CriadoEmUtc = criadoEmUtc;
        CriadoPorUsuarioId = criadoPorUsuarioId;
    }

    public Guid Id { get; private set; }

    public Guid ContratoFranquiaId { get; private set; }

    public int NumeroVersao { get; private set; }

    public DateOnly DataInicio { get; private set; }

    public DateOnly? DataFim { get; private set; }

    public decimal PercentualRoyalties { get; private set; }

    public decimal MensalidadeFixa { get; private set; }

    public decimal? TaxaAdesao { get; private set; }

    public int? DiaVencimento { get; private set; }

    public StatusVersaoContratoFranquia Status { get; private set; }

    public string? MotivoAlteracao { get; private set; }

    public string? Observacoes { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public Guid CriadoPorUsuarioId { get; private set; }

    public void AtualizarTermosRascunho(
        DateOnly dataInicio,
        DateOnly? dataFim,
        decimal percentualRoyalties,
        decimal mensalidadeFixa,
        decimal? taxaAdesao,
        int? diaVencimento,
        string? motivoAlteracao,
        string? observacoes)
    {
        if (Status != StatusVersaoContratoFranquia.Rascunho)
        {
            throw new InvalidOperationException(
                "Somente uma versao em rascunho pode ter seus termos alterados.");
        }

        ValidarTermos(
            dataInicio,
            dataFim,
            percentualRoyalties,
            mensalidadeFixa,
            taxaAdesao,
            diaVencimento);

        DataInicio = dataInicio;
        DataFim = dataFim;
        PercentualRoyalties = percentualRoyalties;
        MensalidadeFixa = mensalidadeFixa;
        TaxaAdesao = taxaAdesao;
        DiaVencimento = diaVencimento;
        MotivoAlteracao = NormalizarOpcionalInformado(
            motivoAlteracao,
            MotivoAlteracaoTamanhoMaximo,
            nameof(motivoAlteracao));
        Observacoes = NormalizarOpcionalInformado(
            observacoes,
            ObservacoesTamanhoMaximo,
            nameof(observacoes));
    }

    public void AlterarStatus(StatusVersaoContratoFranquia novoStatus)
    {
        if (!Enum.IsDefined(novoStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(novoStatus),
                novoStatus,
                "O novo status da versao do contrato e invalido.");
        }

        var transicaoPermitida = Status switch
        {
            StatusVersaoContratoFranquia.Rascunho =>
                novoStatus is StatusVersaoContratoFranquia.Rascunho
                    or StatusVersaoContratoFranquia.Vigente
                    or StatusVersaoContratoFranquia.Cancelada,
            StatusVersaoContratoFranquia.Vigente =>
                novoStatus is StatusVersaoContratoFranquia.Vigente
                    or StatusVersaoContratoFranquia.Substituida
                    or StatusVersaoContratoFranquia.Cancelada,
            StatusVersaoContratoFranquia.Substituida =>
                novoStatus == StatusVersaoContratoFranquia.Substituida,
            StatusVersaoContratoFranquia.Cancelada =>
                novoStatus == StatusVersaoContratoFranquia.Cancelada,
            _ => false
        };

        if (!transicaoPermitida)
        {
            throw new InvalidOperationException(
                $"A transicao de {Status} para {novoStatus} nao e permitida.");
        }

        Status = novoStatus;
    }

    private static void ValidarTermos(
        DateOnly dataInicio,
        DateOnly? dataFim,
        decimal percentualRoyalties,
        decimal mensalidadeFixa,
        decimal? taxaAdesao,
        int? diaVencimento)
    {
        if (dataFim.HasValue && dataFim.Value < dataInicio)
        {
            throw new ArgumentException(
                "A data final nao pode ser anterior a data inicial.",
                nameof(dataFim));
        }

        if (percentualRoyalties is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentualRoyalties),
                percentualRoyalties,
                "O percentual de royalties deve estar entre zero e cem.");
        }

        if (mensalidadeFixa < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mensalidadeFixa),
                mensalidadeFixa,
                "A mensalidade fixa nao pode ser negativa.");
        }

        if (taxaAdesao < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(taxaAdesao),
                taxaAdesao,
                "A taxa de adesao nao pode ser negativa.");
        }

        if (diaVencimento is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diaVencimento),
                diaVencimento,
                "O dia de vencimento deve estar entre um e trinta e um.");
        }
    }

    private static string? NormalizarOpcionalInformado(
        string? valor,
        int tamanhoMaximo,
        string nomeParametro)
    {
        if (valor is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException("O valor informado nao pode ser vazio.", nomeParametro);
        }

        var valorNormalizado = valor.Trim();

        if (valorNormalizado.Length > tamanhoMaximo)
        {
            throw new ArgumentException(
                $"O valor deve possuir no maximo {tamanhoMaximo} caracteres.",
                nomeParametro);
        }

        return valorNormalizado;
    }
}

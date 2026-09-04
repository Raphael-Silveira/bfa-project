namespace BFA.Domain.Cobrancas;

public sealed class Pagamento
{
    private Pagamento()
    {
    }

    public Pagamento(
        Guid id,
        Guid organizacaoId,
        Guid unidadeId,
        Guid cobrancaId,
        decimal valor,
        DateOnly dataPagamento,
        FormaPagamento formaPagamento,
        Guid registradoPorUsuarioId,
        DateTime criadoEmUtc)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(unidadeId, nameof(unidadeId));
        ValidarIdentificador(cobrancaId, nameof(cobrancaId));
        ValidarIdentificador(registradoPorUsuarioId, nameof(registradoPorUsuarioId));

        if (valor <= 0)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "O valor do pagamento deve ser maior que zero.");

        if (dataPagamento == default)
            throw new ArgumentException("A data de pagamento deve ser informada.", nameof(dataPagamento));

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        CobrancaId = cobrancaId;
        Valor = valor;
        DataPagamento = dataPagamento;
        DataRegistro = criadoEmUtc;
        FormaPagamento = formaPagamento;
        Observacoes = null;
        RegistradoPorUsuarioId = registradoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public Guid CobrancaId { get; private set; }

    public decimal Valor { get; private set; }

    public DateOnly DataPagamento { get; private set; }

    public DateTime DataRegistro { get; private set; }

    public FormaPagamento FormaPagamento { get; private set; }

    public string? Observacoes { get; private set; }

    public Guid RegistradoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    private static void ValidarIdentificador(Guid valor, string parametro)
    {
        if (valor == Guid.Empty)
            throw new ArgumentException("O identificador deve ser informado.", parametro);
    }
}

using BFA.Domain.Usuarios;

namespace BFA.Domain.Cobrancas;

public sealed class Cobranca
{
    private Cobranca()
    {
    }

    public Cobranca(
        Guid id,
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        Guid matriculaId,
        TipoCobranca tipo,
        string descricao,
        decimal valor,
        DateOnly dataEmissao,
        DateOnly dataVencimento,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(unidadeId, nameof(unidadeId));
        ValidarIdentificador(alunoId, nameof(alunoId));
        ValidarIdentificador(matriculaId, nameof(matriculaId));
        ValidarIdentificador(criadoPorUsuarioId, nameof(criadoPorUsuarioId));

        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("A descricao deve ser informada.", nameof(descricao));

        if (valor <= 0)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "O valor deve ser maior que zero.");

        if (dataEmissao == default)
            throw new ArgumentException("A data de emissao deve ser informada.", nameof(dataEmissao));

        if (dataVencimento < dataEmissao)
            throw new ArgumentException("A data de vencimento nao pode ser anterior a data de emissao.", nameof(dataVencimento));

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        AlunoId = alunoId;
        MatriculaId = matriculaId;
        Tipo = tipo;
        Descricao = descricao.Trim();
        Valor = valor;
        ValorPago = 0;
        DataEmissao = dataEmissao;
        DataVencimento = dataVencimento;
        DataPagamento = null;
        Status = StatusCobranca.Pendente;
        Observacoes = null;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        AtualizadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public Guid AlunoId { get; private set; }

    public Guid MatriculaId { get; private set; }

    public TipoCobranca Tipo { get; private set; }

    public string Descricao { get; private set; } = string.Empty;

    public decimal Valor { get; private set; }

    public decimal ValorPago { get; private set; }

    public DateOnly DataEmissao { get; private set; }

    public DateOnly DataVencimento { get; private set; }

    public DateOnly? DataPagamento { get; private set; }

    public StatusCobranca Status { get; private set; }

    public string? Observacoes { get; private set; }

    public Guid CriadoPorUsuarioId { get; private set; }

    public Guid AtualizadoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void Cancelar(Guid usuarioId, DateTime atualizadoEmUtc)
    {
        if (Status != StatusCobranca.Pendente)
            throw new InvalidOperationException("Apenas cobrancas pendentes podem ser canceladas.");

        Status = StatusCobranca.Cancelada;
        AtualizadoPorUsuarioId = usuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void AtualizarObservacoes(string? observacoes, Guid usuarioId, DateTime atualizadoEmUtc)
    {
        Observacoes = observacoes?.Trim();
        AtualizadoPorUsuarioId = usuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static void ValidarIdentificador(Guid valor, string parametro)
    {
        if (valor == Guid.Empty)
            throw new ArgumentException("O identificador deve ser informado.", parametro);
    }
}

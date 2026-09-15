using BFA.Domain.Cobrancas;

namespace BFA.UnitTests.Cobrancas;

public sealed class CobrancaTests
{
    private static readonly DateTime Agora = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Pagamento_integral_deixa_cobranca_paga()
    {
        var cobranca = Criar(StatusCobranca.Pendente, 150m);

        cobranca.RegistrarPagamento(150m, Agora);

        Assert.Equal(StatusCobranca.Paga, cobranca.Status);
        Assert.Equal(150m, cobranca.ValorPago);
        Assert.Equal(0m, cobranca.SaldoDevedor);
    }

    [Fact]
    public void Pagamento_integral_de_cobranca_atrasada_deixa_cobranca_paga()
    {
        var cobranca = Criar(StatusCobranca.Pendente, 150m);
        cobranca.MarcarComoAtrasada(Agora);

        cobranca.RegistrarPagamento(150m, Agora.AddDays(1));

        Assert.Equal(StatusCobranca.Paga, cobranca.Status);
        Assert.Equal(150m, cobranca.ValorPago);
    }

    [Theory]
    [InlineData(149.99)]
    [InlineData(150.01)]
    public void Pagamento_diferente_do_valor_integral_e_rejeitado(double valor)
    {
        var cobranca = Criar(StatusCobranca.Pendente, 150m);

        Assert.Throws<InvalidOperationException>(() =>
            cobranca.RegistrarPagamento((decimal)valor, Agora));
        Assert.Equal(StatusCobranca.Pendente, cobranca.Status);
        Assert.Equal(0m, cobranca.ValorPago);
    }

    [Fact]
    public void Pagamento_em_cobranca_paga_e_rejeitado()
    {
        var cobranca = Criar(StatusCobranca.Pendente, 150m);
        cobranca.RegistrarPagamento(150m, Agora);

        Assert.Throws<InvalidOperationException>(() =>
            cobranca.RegistrarPagamento(150m, Agora.AddDays(1)));
    }

    [Fact]
    public void Pagamento_em_cobranca_cancelada_e_rejeitado()
    {
        var cobranca = Criar(StatusCobranca.Pendente, 150m);
        cobranca.Cancelar(Guid.NewGuid(), Agora);

        Assert.Throws<InvalidOperationException>(() =>
            cobranca.RegistrarPagamento(150m, Agora.AddDays(1)));
    }

    [Fact]
    public void Reconciliacao_cancela_somente_mensalidade_pendente_de_competencia_posterior()
    {
        var mensalidadeAtual = Criar(TipoCobranca.Mensalidade, StatusCobranca.Pendente, new(2026, 9, 30));
        var mensalidadePosterior = Criar(TipoCobranca.Mensalidade, StatusCobranca.Pendente, new(2026, 10, 5));
        var pagaPosterior = Criar(TipoCobranca.Mensalidade, StatusCobranca.Paga, new(2026, 10, 10));
        var atrasadaPosterior = Criar(TipoCobranca.Mensalidade, StatusCobranca.Atrasada, new(2026, 10, 15));
        var taxaPosterior = Criar(TipoCobranca.Matricula, StatusCobranca.Pendente, new(2026, 10, 5));
        var avulsaPosterior = Criar(TipoCobranca.Avulso, StatusCobranca.Pendente, new(2026, 10, 5));

        var quantidade = ReconciliacaoCobrancas.Aplicar(
            [mensalidadeAtual, mensalidadePosterior, pagaPosterior,
                atrasadaPosterior, taxaPosterior, avulsaPosterior],
            new DateOnly(2026, 9, 15), Agora);

        Assert.Equal(1, quantidade);
        Assert.Equal(StatusCobranca.Pendente, mensalidadeAtual.Status);
        Assert.Equal(StatusCobranca.Cancelada, mensalidadePosterior.Status);
        Assert.Equal(StatusCobranca.Paga, pagaPosterior.Status);
        Assert.Equal(StatusCobranca.Atrasada, atrasadaPosterior.Status);
        Assert.Equal(StatusCobranca.Pendente, taxaPosterior.Status);
        Assert.Equal(StatusCobranca.Pendente, avulsaPosterior.Status);
    }

    [Fact]
    public void Reconciliacao_repetida_e_idempotente()
    {
        var mensalidade = Criar(TipoCobranca.Mensalidade, StatusCobranca.Pendente, new(2026, 10, 5));

        Assert.Equal(1, ReconciliacaoCobrancas.Aplicar([mensalidade], new(2026, 9, 15), Agora));
        Assert.Equal(0, ReconciliacaoCobrancas.Aplicar([mensalidade], new(2026, 9, 15), Agora));
        Assert.Equal(StatusCobranca.Cancelada, mensalidade.Status);
    }

    private static Cobranca Criar(
        StatusCobranca status,
        decimal valor,
        DateOnly dataVencimento = default) =>
        Criar(TipoCobranca.Mensalidade, status,
            dataVencimento == default ? new DateOnly(2026, 9, 30) : dataVencimento,
            valor);

    private static Cobranca Criar(
        TipoCobranca tipo,
        StatusCobranca status,
        DateOnly dataVencimento,
        decimal valor = 150m)
    {
        var cobranca = new Cobranca(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            tipo, "Cobranca de teste", valor, dataVencimento.AddDays(-1), dataVencimento,
            Guid.NewGuid(), Agora);

        if (status == StatusCobranca.Cancelada)
            cobranca.Cancelar(Guid.NewGuid(), Agora);
        else if (status == StatusCobranca.Atrasada)
            cobranca.MarcarComoAtrasada(Agora);
        else if (status == StatusCobranca.Paga)
            cobranca.RegistrarPagamento(valor, Agora);

        return cobranca;
    }
}

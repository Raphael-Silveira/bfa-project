using BFA.Application.Cobrancas;
using BFA.Domain.Cobrancas;

namespace BFA.UnitTests.Cobrancas;

public sealed class FormaPagamentoValidadorTests
{
    [Fact]
    public void Pix_e_aquelas_formas_permitidas_sao_convertidas()
    {
        Assert.Equal(FormaPagamento.Pix, FormaPagamentoValidador.TentarConverter("Pix"));
        Assert.Equal(FormaPagamento.Dinheiro, FormaPagamentoValidador.TentarConverter("Dinheiro"));
        Assert.Equal(FormaPagamento.CartaoCredito, FormaPagamentoValidador.TentarConverter("CartaoCredito"));
        Assert.Equal(FormaPagamento.CartaoDebito, FormaPagamentoValidador.TentarConverter("CartaoDebito"));
        Assert.Equal(FormaPagamento.Boleto, FormaPagamentoValidador.TentarConverter("Boleto"));
        Assert.Equal(FormaPagamento.Transferencia, FormaPagamentoValidador.TentarConverter("Transferencia"));
        Assert.Equal(FormaPagamento.Outros, FormaPagamentoValidador.TentarConverter("Outros"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Selecione")]
    [InlineData("999")]
    [InlineData("pix")]
    public void Valor_vazio_ou_fora_das_opcoes_e_rejeitado(string? valor)
    {
        Assert.Null(FormaPagamentoValidador.TentarConverter(valor));
    }
}

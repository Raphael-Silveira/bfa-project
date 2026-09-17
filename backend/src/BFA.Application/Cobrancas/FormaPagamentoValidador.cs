using BFA.Domain.Cobrancas;

namespace BFA.Application.Cobrancas;

public static class FormaPagamentoValidador
{
    public static FormaPagamento? TentarConverter(string? valor) => valor switch
    {
        nameof(FormaPagamento.Dinheiro) => FormaPagamento.Dinheiro,
        nameof(FormaPagamento.Pix) => FormaPagamento.Pix,
        nameof(FormaPagamento.CartaoCredito) => FormaPagamento.CartaoCredito,
        nameof(FormaPagamento.CartaoDebito) => FormaPagamento.CartaoDebito,
        nameof(FormaPagamento.Boleto) => FormaPagamento.Boleto,
        nameof(FormaPagamento.Transferencia) => FormaPagamento.Transferencia,
        nameof(FormaPagamento.Outros) => FormaPagamento.Outros,
        _ => null
    };
}

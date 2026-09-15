namespace BFA.Domain.Cobrancas;

public static class ReconciliacaoCobrancas
{
    public static int Aplicar(
        IEnumerable<Cobranca> cobrancas,
        DateOnly dataFimReal,
        DateTime atualizadoEmUtc)
    {
        var competenciaFinal = new DateOnly(dataFimReal.Year, dataFimReal.Month, 1);
        var reconciliadas = 0;

        foreach (var cobranca in cobrancas)
        {
            if (cobranca.Tipo != TipoCobranca.Mensalidade
                || cobranca.Status != StatusCobranca.Pendente)
                continue;

            var competenciaCobranca = new DateOnly(
                cobranca.DataVencimento.Year,
                cobranca.DataVencimento.Month,
                1);
            if (competenciaCobranca <= competenciaFinal)
                continue;

            cobranca.CancelarPorReconciliacao(atualizadoEmUtc);
            reconciliadas++;
        }

        return reconciliadas;
    }
}

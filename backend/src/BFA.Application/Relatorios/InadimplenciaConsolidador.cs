namespace BFA.Application.Relatorios;

public static class InadimplenciaConsolidador
{
    public static IReadOnlyList<InadimplenciaAluno> Consolidar(
        IReadOnlyList<CobrancaInadimplenciaRelatorio> cobrancas,
        DateOnly hoje)
    {
        return cobrancas
            .GroupBy(c => c.AlunoId)
            .Select(grupo =>
            {
                var diasEmAtraso = grupo.Min(c => hoje.DayNumber - c.DataVencimento.DayNumber);
                return new InadimplenciaAluno(
                    grupo.Key,
                    grupo.First().NomeAluno,
                    grupo.First().CpfAluno,
                    grupo.Count(),
                    grupo.Sum(c => c.Valor - c.ValorPago),
                    grupo.Min(c => c.DataVencimento),
                    grupo.Max(c => c.DataVencimento),
                    diasEmAtraso,
                    MapearFaixaAtraso(diasEmAtraso));
            })
            .OrderByDescending(aluno => aluno.ValorTotalAtrasado)
            .ToList();
    }

    private static string MapearFaixaAtraso(int dias) => dias switch
    {
        <= 30 => "1-30 dias",
        <= 60 => "31-60 dias",
        <= 90 => "61-90 dias",
        _ => "90+ dias"
    };
}

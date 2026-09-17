using BFA.Application.Relatorios;
using BFA.Domain.Cobrancas;

namespace BFA.UnitTests.Relatorios;

public sealed class InadimplenciaConsolidadorTests
{
    [Fact]
    public void Consolida_por_AlunoId_e_preserva_nome_cpf_e_valores_de_cada_aluno()
    {
        var hoje = new DateOnly(2026, 9, 16);
        var alunoA = Guid.NewGuid();
        var alunoB = Guid.NewGuid();
        var cobrancas = new[]
        {
            Criar(alunoA, "Ana Silva", "12345678901", 100m, 20m, hoje.AddDays(-10)),
            Criar(alunoA, "Ana Silva", "12345678901", 80m, 0m, hoje.AddDays(-40)),
            Criar(alunoB, "Bruno Lima", null, 50m, 10m, hoje.AddDays(-70))
        };

        var resultado = InadimplenciaConsolidador.Consolidar(cobrancas, hoje);

        var ana = Assert.Single(resultado, x => x.AlunoId == alunoA);
        Assert.Equal("Ana Silva", ana.NomeCompleto);
        Assert.Equal("12345678901", ana.Cpf);
        Assert.Equal(2, ana.CobrancasAtrasadas);
        Assert.Equal(160m, ana.ValorTotalAtrasado);
        Assert.Equal(10, ana.DiasEmAtraso);
        Assert.Equal("1-30 dias", ana.FaixaAtraso);

        var bruno = Assert.Single(resultado, x => x.AlunoId == alunoB);
        Assert.Equal("Bruno Lima", bruno.NomeCompleto);
        Assert.Null(bruno.Cpf);
        Assert.Equal(40m, bruno.ValorTotalAtrasado);
        Assert.Equal("61-90 dias", bruno.FaixaAtraso);
    }

    private static CobrancaInadimplenciaRelatorio Criar(
        Guid alunoId,
        string nome,
        string? cpf,
        decimal valor,
        decimal valorPago,
        DateOnly vencimento) => new(
            Guid.NewGuid(),
            TipoCobranca.Mensalidade,
            valor,
            valorPago,
            "Mensalidade",
            vencimento,
            alunoId,
            nome,
            cpf);
}

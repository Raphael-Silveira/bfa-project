using BFA.Domain.Matriculas;

namespace BFA.UnitTests.Matriculas;

public sealed class MatriculaTests
{
    private static readonly DateTime Agora = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criacao_valida_preserva_snapshot_e_inicia_ativa()
    {
        var matricula = Criar(valor: 137.45m, cobraTaxa: true, valorTaxa: 52.10m);

        Assert.Equal(StatusMatricula.Ativa, matricula.Status);
        Assert.Null(matricula.DataFimReal);
        Assert.Equal(137.45m, matricula.ValorMensalContratado);
        Assert.True(matricula.CobraTaxaMatricula);
        Assert.Equal(52.10m, matricula.ValorTaxaMatricula);
        Assert.Equal(matricula.CriadoPorUsuarioId, matricula.AtualizadoPorUsuarioId);
    }

    [Theory]
    [InlineData(2026, 1, 1, 1, 2026, 1, 31)]
    [InlineData(2026, 1, 1, 3, 2026, 3, 31)]
    [InlineData(2026, 1, 1, 6, 2026, 6, 30)]
    [InlineData(2026, 1, 1, 12, 2026, 12, 31)]
    [InlineData(2024, 1, 31, 1, 2024, 2, 28)]
    [InlineData(2023, 1, 31, 1, 2023, 2, 27)]
    [InlineData(2024, 2, 29, 12, 2025, 2, 27)]
    [InlineData(2026, 8, 31, 6, 2027, 2, 27)]
    public void Data_fim_prevista_usa_meses_civis_menos_um_dia(
        int ano, int mes, int dia, int meses, int anoFim, int mesFim, int diaFim)
    {
        var inicio = new DateOnly(ano, mes, dia);

        Assert.Equal(
            new DateOnly(anoFim, mesFim, diaFim),
            Matricula.CalcularDataFimPrevista(inicio, meses));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Duracao_nao_positiva_e_rejeitada(int duracao)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Criar(duracao: duracao));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Valor_mensal_nao_positivo_e_rejeitado(double valor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Criar(valor: (decimal)valor));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, 0d)]
    [InlineData(true, -1d)]
    [InlineData(false, 10d)]
    public void Combinacao_invalida_de_taxa_e_rejeitada(bool cobra, double? valor)
    {
        Assert.Throws<ArgumentException>(() => Criar(
            cobraTaxa: cobra,
            valorTaxa: valor.HasValue ? (decimal)valor.Value : null));
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, 10d)]
    public void Combinacao_valida_de_taxa_e_aceita(bool cobra, double? valor)
    {
        var matricula = Criar(
            cobraTaxa: cobra,
            valorTaxa: valor.HasValue ? (decimal)valor.Value : null);

        Assert.Equal(cobra, matricula.CobraTaxaMatricula);
    }

    [Fact]
    public void Encerramento_preenche_data_final_e_auditoria()
    {
        var matricula = Criar();
        var usuario = Guid.NewGuid();

        matricula.Encerrar(matricula.DataInicio.AddDays(10), usuario, Agora.AddDays(10));

        Assert.Equal(StatusMatricula.Encerrada, matricula.Status);
        Assert.Equal(matricula.DataInicio.AddDays(10), matricula.DataFimReal);
        Assert.Equal(usuario, matricula.AtualizadoPorUsuarioId);
    }

    [Fact]
    public void Cancelamento_preenche_data_final_e_auditoria()
    {
        var matricula = Criar();

        matricula.Cancelar(matricula.DataInicio, Guid.NewGuid(), Agora.AddDays(1));

        Assert.Equal(StatusMatricula.Cancelada, matricula.Status);
        Assert.Equal(matricula.DataInicio, matricula.DataFimReal);
    }

    [Fact]
    public void Data_final_anterior_ao_inicio_e_rejeitada()
    {
        var matricula = Criar();

        Assert.Throws<ArgumentException>(() => matricula.Encerrar(
            matricula.DataInicio.AddDays(-1), Guid.NewGuid(), Agora.AddDays(1)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Estado_terminal_nao_pode_mudar_novamente(bool encerrar)
    {
        var matricula = Criar();
        if (encerrar)
        {
            matricula.Encerrar(matricula.DataInicio, Guid.NewGuid(), Agora.AddDays(1));
        }
        else
        {
            matricula.Cancelar(matricula.DataInicio, Guid.NewGuid(), Agora.AddDays(1));
        }

        Assert.Throws<InvalidOperationException>(() => matricula.Cancelar(
            matricula.DataInicio, Guid.NewGuid(), Agora.AddDays(2)));
    }

    [Fact]
    public void Snapshot_contratual_nao_possui_setters_publicos()
    {
        foreach (var nome in new[]
        {
            nameof(Matricula.OrganizacaoId), nameof(Matricula.UnidadeId),
            nameof(Matricula.AlunoId), nameof(Matricula.PlanoVersaoId),
            nameof(Matricula.DataInicio), nameof(Matricula.DataFimPrevista),
            nameof(Matricula.ValorMensalContratado), nameof(Matricula.CobraTaxaMatricula),
            nameof(Matricula.ValorTaxaMatricula), nameof(Matricula.CriadoPorUsuarioId),
            nameof(Matricula.CriadoEmUtc)
        })
        {
            Assert.False(typeof(Matricula).GetProperty(nome)!.SetMethod!.IsPublic);
        }
    }

    [Fact]
    public void Status_possui_somente_os_tres_estados_contratuais()
    {
        Assert.Equal(
            ["Ativa", "Encerrada", "Cancelada"],
            Enum.GetNames<StatusMatricula>());
    }

    private static Matricula Criar(
        int duracao = 6,
        decimal valor = 150m,
        bool cobraTaxa = false,
        decimal? valorTaxa = null) => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 9, 1), duracao, valor, cobraTaxa, valorTaxa,
            Guid.NewGuid(), Agora);
}

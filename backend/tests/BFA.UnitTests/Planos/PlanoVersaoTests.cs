using BFA.Domain.Planos;

namespace BFA.UnitTests.Planos;

public sealed class PlanoVersaoTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly VigenciaInicio = new(2026, 9, 1);

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void Criacao_aceita_frequencias_limites(int frequencia)
    {
        var versao = Criar(frequenciaSemanal: frequencia);

        Assert.Equal(frequencia, versao.FrequenciaSemanal);
        Assert.Equal(12, versao.DuracaoMeses);
        Assert.Equal(280m, versao.ValorMensal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criacao_rejeita_duracao_nao_positiva(int duracao)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(duracaoMeses: duracao));

        Assert.Equal("duracaoMeses", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Criacao_rejeita_frequencia_fora_de_um_a_sete(int frequencia)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(frequenciaSemanal: frequencia));

        Assert.Equal("frequenciaSemanal", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Criacao_rejeita_valor_mensal_nao_positivo(decimal valor)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(valorMensal: valor));

        Assert.Equal("valorMensal", exception.ParamName);
    }

    [Fact]
    public void Cobranca_de_matricula_exige_valor_informado()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Criar(cobraMatricula: true, valorMatricula: null));

        Assert.Equal("valorMatricula", exception.ParamName);
    }

    [Fact]
    public void Cobranca_de_matricula_rejeita_valor_nao_positivo()
    {
        foreach (var valor in new[] { 0m, -1m })
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                Criar(cobraMatricula: true, valorMatricula: valor));

            Assert.Equal("valorMatricula", exception.ParamName);
        }
    }

    [Fact]
    public void Ausencia_de_matricula_exige_valor_nulo()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Criar(cobraMatricula: false, valorMatricula: 100m));

        Assert.Equal("valorMatricula", exception.ParamName);
    }

    [Fact]
    public void Ausencia_de_matricula_com_valor_nulo_e_valida()
    {
        var versao = Criar(cobraMatricula: false, valorMatricula: null);

        Assert.False(versao.CobraMatricula);
        Assert.Null(versao.ValorMatricula);
    }

    [Fact]
    public void Criacao_rejeita_vigencia_final_anterior()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Criar(vigenciaFim: VigenciaInicio.AddDays(-1)));

        Assert.Equal("vigenciaFim", exception.ParamName);
    }

    [Fact]
    public void Vigencia_final_pode_ser_preenchida_uma_unica_vez()
    {
        var versao = Criar();
        var fim = new DateOnly(2026, 9, 30);

        versao.Encerrar(fim);

        Assert.Equal(fim, versao.VigenciaFim);
        Assert.Throws<InvalidOperationException>(() => versao.Encerrar(fim.AddDays(1)));
        Assert.Equal(fim, versao.VigenciaFim);
    }

    [Theory]
    [InlineData(nameof(PlanoVersao.Id))]
    [InlineData(nameof(PlanoVersao.OrganizacaoId))]
    [InlineData(nameof(PlanoVersao.PlanoId))]
    [InlineData(nameof(PlanoVersao.NumeroVersao))]
    [InlineData(nameof(PlanoVersao.DuracaoMeses))]
    [InlineData(nameof(PlanoVersao.FrequenciaSemanal))]
    [InlineData(nameof(PlanoVersao.ValorMensal))]
    [InlineData(nameof(PlanoVersao.CobraMatricula))]
    [InlineData(nameof(PlanoVersao.ValorMatricula))]
    [InlineData(nameof(PlanoVersao.VigenciaInicio))]
    [InlineData(nameof(PlanoVersao.CriadoPorUsuarioId))]
    [InlineData(nameof(PlanoVersao.CriadoEmUtc))]
    public void Termos_historicos_nao_possuem_setter_publico(string propriedade)
    {
        var property = typeof(PlanoVersao).GetProperty(propriedade);

        Assert.NotNull(property);
        Assert.False(property.SetMethod?.IsPublic ?? false);
    }

    private static PlanoVersao Criar(
        int duracaoMeses = 12,
        int frequenciaSemanal = 3,
        decimal valorMensal = 280m,
        bool cobraMatricula = true,
        decimal? valorMatricula = 100m,
        DateOnly? vigenciaFim = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            duracaoMeses,
            frequenciaSemanal,
            valorMensal,
            cobraMatricula,
            valorMatricula,
            VigenciaInicio,
            vigenciaFim,
            Guid.NewGuid(),
            CriadoEmUtc);
}

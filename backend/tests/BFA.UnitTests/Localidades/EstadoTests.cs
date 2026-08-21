using BFA.Domain.Localidades;

namespace BFA.UnitTests.Localidades;

public sealed class EstadoTests
{
    private static readonly DateTime Agora = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criacao_usa_codigo_oficial_normaliza_sigla_e_preserva_acentos()
    {
        var estado = new Estado(35, " sp ", " São Paulo ", Agora);

        Assert.Equal(35, estado.CodigoIbge);
        Assert.Equal("SP", estado.Sigla);
        Assert.Equal("São Paulo", estado.Nome);
        Assert.True(estado.Ativo);
        Assert.Equal(Agora, estado.CriadoEmUtc);
        Assert.Equal(Agora, estado.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Codigo_ibge_deve_ser_positivo(int codigoIbge)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Estado(codigoIbge, "SP", "São Paulo", Agora));
    }

    [Theory]
    [InlineData("")]
    [InlineData("S")]
    [InlineData("S1")]
    [InlineData("São")]
    public void Sigla_deve_possuir_duas_letras_ascii(string sigla)
    {
        Assert.Throws<ArgumentException>(() =>
            new Estado(35, sigla, "São Paulo", Agora));
    }

    [Fact]
    public void Nome_vazio_ou_acima_do_limite_e_rejeitado()
    {
        Assert.Throws<ArgumentException>(() => new Estado(35, "SP", " ", Agora));
        Assert.Throws<ArgumentException>(() =>
            new Estado(35, "SP", new string('a', Estado.NomeTamanhoMaximo + 1), Agora));
    }

    [Fact]
    public void Atualizar_reativa_e_desativar_nao_remove_identidade()
    {
        var estado = new Estado(35, "SP", "São Paulo", Agora);
        var depois = Agora.AddHours(1);
        estado.Desativar(depois);

        estado.Atualizar("sp", "São Paulo", depois.AddHours(1));

        Assert.Equal(35, estado.CodigoIbge);
        Assert.True(estado.Ativo);
        Assert.Equal(depois.AddHours(1), estado.AtualizadoEmUtc);
    }
}

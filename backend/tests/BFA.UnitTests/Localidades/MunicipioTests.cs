using BFA.Domain.Localidades;

namespace BFA.UnitTests.Localidades;

public sealed class MunicipioTests
{
    private static readonly DateTime Agora = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criacao_usa_codigos_oficiais_e_preserva_acentos()
    {
        var municipio = new Municipio(3550308, 35, " São Paulo ", Agora);

        Assert.Equal(3550308, municipio.CodigoIbge);
        Assert.Equal(35, municipio.EstadoCodigoIbge);
        Assert.Equal("São Paulo", municipio.Nome);
        Assert.True(municipio.Ativo);
    }

    [Theory]
    [InlineData(0, 35)]
    [InlineData(3550308, 0)]
    [InlineData(-1, 35)]
    public void Codigos_devem_ser_positivos(int codigoIbge, int estadoCodigoIbge)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Municipio(codigoIbge, estadoCodigoIbge, "São Paulo", Agora));
    }

    [Fact]
    public void Nome_vazio_ou_acima_do_limite_e_rejeitado()
    {
        Assert.Throws<ArgumentException>(() => new Municipio(1, 35, " ", Agora));
        Assert.Throws<ArgumentException>(() =>
            new Municipio(1, 35, new string('a', Municipio.NomeTamanhoMaximo + 1), Agora));
    }

    [Fact]
    public void Atualizar_pode_corrigir_estado_e_reativar_sem_trocar_codigo()
    {
        var municipio = new Municipio(3550308, 35, "São Paulo", Agora);
        municipio.Desativar(Agora.AddHours(1));

        municipio.Atualizar(41, "São Paulo", Agora.AddHours(2));

        Assert.Equal(3550308, municipio.CodigoIbge);
        Assert.Equal(41, municipio.EstadoCodigoIbge);
        Assert.True(municipio.Ativo);
    }
}

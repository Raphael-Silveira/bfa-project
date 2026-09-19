using BFA.Application.Identidade;

namespace BFA.UnitTests.Identidade;

public sealed class CpfIdentificadorTests
{
    [Theory]
    [InlineData("12345678901")]
    [InlineData("123.456.789-01")]
    [InlineData(" 123 456 789 01 ")]
    public void Normaliza_cpf_mascarado_ou_nao_mascarado(string valor)
    {
        var valido = CpfIdentificador.TentarNormalizar(valor, out var cpf);

        Assert.True(valido);
        Assert.Equal("12345678901", cpf);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    public void Rejeita_cpf_ausente_incompleto_ou_excedente(string? valor)
    {
        var valido = CpfIdentificador.TentarNormalizar(valor, out var cpf);

        Assert.False(valido);
        Assert.Equal(string.Empty, cpf);
    }

    [Theory]
    [InlineData("738.659.280-99")]
    [InlineData("73865928099")]
    public void Aceita_CPF_de_edicao_mascarado_ou_sem_mascara(string valor)
    {
        var valido = CpfIdentificador.TentarNormalizar(valor, out var cpf);

        Assert.True(valido);
        Assert.Equal("73865928099", cpf);
    }

    [Fact]
    public void Remove_caracteres_nao_numericos_antes_de_validar_quantidade()
    {
        var valido = CpfIdentificador.TentarNormalizar(
            " 738.659.280-99 ",
            out var cpf);

        Assert.True(valido);
        Assert.Equal("73865928099", cpf);
    }
}

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
    [InlineData("123.456.789/01")]
    public void Rejeita_cpf_ausente_incompleto_excedente_ou_com_caractere_invalido(string? valor)
    {
        var valido = CpfIdentificador.TentarNormalizar(valor, out var cpf);

        Assert.False(valido);
        Assert.Equal(string.Empty, cpf);
    }
}

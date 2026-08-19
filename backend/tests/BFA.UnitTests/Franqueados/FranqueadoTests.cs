using BFA.Domain.Franqueados;

namespace BFA.UnitTests.Franqueados;

public sealed class FranqueadoTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        18,
        12,
        30,
        0,
        DateTimeKind.Utc);

    [Fact]
    public void Pessoa_fisica_normaliza_documento_e_inicia_ativa()
    {
        var id = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();

        var franqueado = new Franqueado(
            id,
            organizacaoId,
            TipoPessoaFranqueado.PessoaFisica,
            "  Joao da Silva  ",
            "123.456.789-01",
            "  comercial@example.com  ",
            CriadoEmUtc,
            nomeFantasia: "  BFA Joao  ",
            emailFinanceiro: "  financeiro@example.com  ",
            estado: "  SP  ",
            cep: "  18000000  ");

        Assert.Equal(id, franqueado.Id);
        Assert.Equal(organizacaoId, franqueado.OrganizacaoId);
        Assert.Equal(TipoPessoaFranqueado.PessoaFisica, franqueado.TipoPessoa);
        Assert.Equal("Joao da Silva", franqueado.NomeRazaoSocial);
        Assert.Equal("BFA Joao", franqueado.NomeFantasia);
        Assert.Equal("12345678901", franqueado.Documento);
        Assert.Equal("comercial@example.com", franqueado.Email);
        Assert.Equal("financeiro@example.com", franqueado.EmailFinanceiro);
        Assert.Equal("SP", franqueado.Estado);
        Assert.Equal("18000000", franqueado.Cep);
        Assert.True(franqueado.Ativo);
        Assert.Equal(CriadoEmUtc, franqueado.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, franqueado.AtualizadoEmUtc);
    }

    [Fact]
    public void Pessoa_juridica_aceita_cnpj_alfanumerico_formatado_e_normaliza_em_maiusculas()
    {
        var franqueado = CriarFranqueado(
            TipoPessoaFranqueado.PessoaJuridica,
            "ab.cde.f12/3456-78");

        Assert.Equal("ABCDEF12345678", franqueado.Documento);
    }

    [Theory]
    [InlineData(TipoPessoaFranqueado.PessoaFisica, "123.456.789-01", "12345678901")]
    [InlineData(TipoPessoaFranqueado.PessoaFisica, "12345678901", "12345678901")]
    [InlineData(TipoPessoaFranqueado.PessoaJuridica, "12.345.678/0001-99", "12345678000199")]
    [InlineData(TipoPessoaFranqueado.PessoaJuridica, "12345678000199", "12345678000199")]
    [InlineData(TipoPessoaFranqueado.PessoaJuridica, "AB.CDE.F12/3456-78", "ABCDEF12345678")]
    [InlineData(TipoPessoaFranqueado.PessoaJuridica, "abcdef12345678", "ABCDEF12345678")]
    [InlineData(TipoPessoaFranqueado.PessoaJuridica, " A1.B2C.3D4/E5F6-78 ", "A1B2C3D4E5F678")]
    public void Documento_com_ou_sem_mascara_e_normalizado_no_formato_persistido(
        TipoPessoaFranqueado tipoPessoa,
        string documento,
        string documentoEsperado)
    {
        var franqueado = CriarFranqueado(tipoPessoa, documento);

        Assert.Equal(documentoEsperado, franqueado.Documento);
    }

    [Theory]
    [InlineData("18000-000")]
    [InlineData("18000000")]
    public void Cep_com_ou_sem_mascara_e_normalizado(string cep)
    {
        var franqueado = new Franqueado(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TipoPessoaFranqueado.PessoaFisica,
            "Joao da Silva",
            "12345678901",
            "comercial@example.com",
            CriadoEmUtc,
            cep: cep);

        Assert.Equal("18000000", franqueado.Cep);
    }

    [Fact]
    public void Criacao_rejeita_organizacao_vazia()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Franqueado(
            Guid.NewGuid(),
            Guid.Empty,
            TipoPessoaFranqueado.PessoaFisica,
            "Joao da Silva",
            "12345678901",
            "comercial@example.com",
            CriadoEmUtc));

        Assert.Equal("organizacaoId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_rejeita_nome_razao_social_vazio(string? nomeRazaoSocial)
    {
        var exception = Assert.Throws<ArgumentException>(() => new Franqueado(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TipoPessoaFranqueado.PessoaFisica,
            nomeRazaoSocial!,
            "12345678901",
            "comercial@example.com",
            CriadoEmUtc));

        Assert.Equal("nomeRazaoSocial", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_rejeita_documento_vazio(string? documento)
    {
        var exception = Assert.Throws<ArgumentException>(() => new Franqueado(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TipoPessoaFranqueado.PessoaFisica,
            "Joao da Silva",
            documento!,
            "comercial@example.com",
            CriadoEmUtc));

        Assert.Equal("documento", exception.ParamName);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    public void Pessoa_fisica_rejeita_documento_sem_onze_digitos(string documento)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CriarFranqueado(TipoPessoaFranqueado.PessoaFisica, documento));

        Assert.Equal("documento", exception.ParamName);
    }

    [Theory]
    [InlineData("ABCDEF1234567")]
    [InlineData("ABCDEF123456789")]
    [InlineData("ABCDEF1234567A")]
    [InlineData("ABCDEF12345A7B")]
    public void Pessoa_juridica_rejeita_cnpj_fora_do_padrao_alfanumerico(string documento)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CriarFranqueado(TipoPessoaFranqueado.PessoaJuridica, documento));

        Assert.Equal("documento", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_letras_no_documento()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CriarFranqueado(TipoPessoaFranqueado.PessoaFisica, "1234567890A"));

        Assert.Equal("documento", exception.ParamName);
    }

    [Theory]
    [InlineData("ABCDEF12345@78")]
    [InlineData("ABCDEF123456_78")]
    [InlineData("ÁBCDEF12345678")]
    public void Pessoa_juridica_rejeita_caracteres_invalidos(string documento)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CriarFranqueado(TipoPessoaFranqueado.PessoaJuridica, documento));

        Assert.Equal("documento", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_email_comercial_vazio()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Franqueado(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TipoPessoaFranqueado.PessoaFisica,
            "Joao da Silva",
            "12345678901",
            "   ",
            CriadoEmUtc));

        Assert.Equal("email", exception.ParamName);
    }

    private static Franqueado CriarFranqueado(
        TipoPessoaFranqueado tipoPessoa,
        string documento)
    {
        return new Franqueado(
            Guid.NewGuid(),
            Guid.NewGuid(),
            tipoPessoa,
            "Joao da Silva",
            documento,
            "comercial@example.com",
            CriadoEmUtc);
    }
}

using BFA.Domain.Unidades;

namespace BFA.UnitTests.Unidades;

public sealed class UnidadeTests
{
    private static readonly DateTime CriadoEmUtc = new(2026, 8, 17, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Criacao_define_estado_inicial_e_normaliza_dados()
    {
        var id = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();

        var unidade = new Unidade(
            id,
            organizacaoId,
            "  BFA Tiete  ",
            "  BFA-TIETE  ",
            CriadoEmUtc);

        Assert.Equal(id, unidade.Id);
        Assert.Equal(organizacaoId, unidade.OrganizacaoId);
        Assert.Equal("BFA Tiete", unidade.Nome);
        Assert.Equal("bfa-tiete", unidade.Slug);
        Assert.True(unidade.Ativa);
        Assert.Equal(CriadoEmUtc, unidade.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, unidade.AtualizadoEmUtc);
        Assert.Equal(DateTimeKind.Utc, unidade.CriadoEmUtc.Kind);
    }

    [Fact]
    public void Criacao_rejeita_identificador_vazio()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Unidade(Guid.Empty, Guid.NewGuid(), "BFA Tiete", "bfa-tiete", CriadoEmUtc));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_identificador_de_organizacao_vazio()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Unidade(Guid.NewGuid(), Guid.Empty, "BFA Tiete", "bfa-tiete", CriadoEmUtc));

        Assert.Equal("organizacaoId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_rejeita_nome_vazio(string? nome)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Unidade(Guid.NewGuid(), Guid.NewGuid(), nome!, "bfa-tiete", CriadoEmUtc));

        Assert.Equal("nome", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_rejeita_slug_vazio(string? slug)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Unidade(Guid.NewGuid(), Guid.NewGuid(), "BFA Tiete", slug!, CriadoEmUtc));

        Assert.Equal("slug", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_data_que_nao_esta_em_utc()
    {
        var dataSemFuso = DateTime.SpecifyKind(CriadoEmUtc, DateTimeKind.Unspecified);

        var exception = Assert.Throws<ArgumentException>(() =>
            new Unidade(Guid.NewGuid(), Guid.NewGuid(), "BFA Tiete", "bfa-tiete", dataSemFuso));

        Assert.Equal("criadoEmUtc", exception.ParamName);
    }
}

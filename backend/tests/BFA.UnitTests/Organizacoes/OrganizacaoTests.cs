using BFA.Domain.Organizacoes;

namespace BFA.UnitTests.Organizacoes;

public sealed class OrganizacaoTests
{
    private static readonly DateTime CriadoEmUtc = new(2026, 8, 17, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Criacao_define_estado_inicial_e_normaliza_dados()
    {
        var id = Guid.NewGuid();

        var organizacao = new Organizacao(id, "  BFA  ", "  BFA-Brasil  ", CriadoEmUtc);

        Assert.Equal(id, organizacao.Id);
        Assert.Equal("BFA", organizacao.Nome);
        Assert.Equal("bfa-brasil", organizacao.Slug);
        Assert.True(organizacao.Ativa);
        Assert.Equal(CriadoEmUtc, organizacao.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, organizacao.AtualizadoEmUtc);
        Assert.Equal(DateTimeKind.Utc, organizacao.CriadoEmUtc.Kind);
    }

    [Fact]
    public void Criacao_rejeita_identificador_vazio()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Organizacao(Guid.Empty, "BFA", "bfa", CriadoEmUtc));

        Assert.Equal("id", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_rejeita_nome_vazio(string? nome)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Organizacao(Guid.NewGuid(), nome!, "bfa", CriadoEmUtc));

        Assert.Equal("nome", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_rejeita_slug_vazio(string? slug)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Organizacao(Guid.NewGuid(), "BFA", slug!, CriadoEmUtc));

        Assert.Equal("slug", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_data_que_nao_esta_em_utc()
    {
        var dataSemFuso = DateTime.SpecifyKind(CriadoEmUtc, DateTimeKind.Unspecified);

        var exception = Assert.Throws<ArgumentException>(() =>
            new Organizacao(Guid.NewGuid(), "BFA", "bfa", dataSemFuso));

        Assert.Equal("criadoEmUtc", exception.ParamName);
    }
}

using BFA.Domain.Franqueados;

namespace BFA.UnitTests.Franqueados;

public sealed class FranqueadoUnidadeTests
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
    public void Criacao_define_contexto_e_inicia_ativa()
    {
        var id = Guid.NewGuid();
        var franqueadoId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();

        var vinculo = new FranqueadoUnidade(
            id,
            franqueadoId,
            organizacaoId,
            unidadeId,
            CriadoEmUtc);

        Assert.Equal(id, vinculo.Id);
        Assert.Equal(franqueadoId, vinculo.FranqueadoId);
        Assert.Equal(organizacaoId, vinculo.OrganizacaoId);
        Assert.Equal(unidadeId, vinculo.UnidadeId);
        Assert.True(vinculo.Ativo);
        Assert.Equal(CriadoEmUtc, vinculo.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, vinculo.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("franqueadoId")]
    [InlineData("organizacaoId")]
    [InlineData("unidadeId")]
    public void Criacao_rejeita_identificador_obrigatorio_vazio(string parametro)
    {
        var id = parametro == "id" ? Guid.Empty : Guid.NewGuid();
        var franqueadoId = parametro == "franqueadoId" ? Guid.Empty : Guid.NewGuid();
        var organizacaoId = parametro == "organizacaoId" ? Guid.Empty : Guid.NewGuid();
        var unidadeId = parametro == "unidadeId" ? Guid.Empty : Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() => new FranqueadoUnidade(
            id,
            franqueadoId,
            organizacaoId,
            unidadeId,
            CriadoEmUtc));

        Assert.Equal(parametro, exception.ParamName);
    }

    [Fact]
    public void Mesmo_franqueado_pode_possuir_multiplas_unidades()
    {
        var franqueadoId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();

        var primeiro = new FranqueadoUnidade(
            Guid.NewGuid(),
            franqueadoId,
            organizacaoId,
            Guid.NewGuid(),
            CriadoEmUtc);
        var segundo = new FranqueadoUnidade(
            Guid.NewGuid(),
            franqueadoId,
            organizacaoId,
            Guid.NewGuid(),
            CriadoEmUtc);

        Assert.Equal(primeiro.FranqueadoId, segundo.FranqueadoId);
        Assert.NotEqual(primeiro.UnidadeId, segundo.UnidadeId);
    }

    [Fact]
    public void Vinculo_inativo_e_reativado_no_mesmo_registro()
    {
        var vinculo = new FranqueadoUnidade(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CriadoEmUtc);
        var idOriginal = vinculo.Id;
        var desativadoEmUtc = CriadoEmUtc.AddHours(1);
        var reativadoEmUtc = CriadoEmUtc.AddHours(2);

        vinculo.Desativar(desativadoEmUtc);

        Assert.False(vinculo.Ativo);
        Assert.Equal(desativadoEmUtc, vinculo.AtualizadoEmUtc);

        vinculo.Ativar(reativadoEmUtc);

        Assert.True(vinculo.Ativo);
        Assert.Equal(idOriginal, vinculo.Id);
        Assert.Equal(CriadoEmUtc, vinculo.CriadoEmUtc);
        Assert.Equal(reativadoEmUtc, vinculo.AtualizadoEmUtc);
    }
}

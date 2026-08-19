using BFA.Domain.Franqueados;

namespace BFA.UnitTests.Franqueados;

public sealed class FranqueadoUsuarioTests
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
    public void Criacao_define_usuario_principal_e_inicia_ativa()
    {
        var id = Guid.NewGuid();
        var franqueadoId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        var vinculo = new FranqueadoUsuario(
            id,
            franqueadoId,
            usuarioId,
            true,
            CriadoEmUtc);

        Assert.Equal(id, vinculo.Id);
        Assert.Equal(franqueadoId, vinculo.FranqueadoId);
        Assert.Equal(usuarioId, vinculo.UsuarioId);
        Assert.True(vinculo.Principal);
        Assert.True(vinculo.Ativo);
        Assert.Equal(CriadoEmUtc, vinculo.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, vinculo.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("franqueadoId")]
    [InlineData("usuarioId")]
    public void Criacao_rejeita_identificador_obrigatorio_vazio(string parametro)
    {
        var id = parametro == "id" ? Guid.Empty : Guid.NewGuid();
        var franqueadoId = parametro == "franqueadoId" ? Guid.Empty : Guid.NewGuid();
        var usuarioId = parametro == "usuarioId" ? Guid.Empty : Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() => new FranqueadoUsuario(
            id,
            franqueadoId,
            usuarioId,
            false,
            CriadoEmUtc));

        Assert.Equal(parametro, exception.ParamName);
    }

    [Fact]
    public void Mesmo_franqueado_pode_possuir_varios_usuarios()
    {
        var franqueadoId = Guid.NewGuid();

        var principal = new FranqueadoUsuario(
            Guid.NewGuid(),
            franqueadoId,
            Guid.NewGuid(),
            true,
            CriadoEmUtc);
        var adicional = new FranqueadoUsuario(
            Guid.NewGuid(),
            franqueadoId,
            Guid.NewGuid(),
            false,
            CriadoEmUtc);

        Assert.Equal(principal.FranqueadoId, adicional.FranqueadoId);
        Assert.NotEqual(principal.UsuarioId, adicional.UsuarioId);
        Assert.True(principal.Principal);
        Assert.False(adicional.Principal);
        Assert.True(principal.Ativo);
        Assert.True(adicional.Ativo);
    }

    [Fact]
    public void Usuarios_nao_principais_podem_coexistir()
    {
        var franqueadoId = Guid.NewGuid();

        var primeiro = new FranqueadoUsuario(
            Guid.NewGuid(),
            franqueadoId,
            Guid.NewGuid(),
            false,
            CriadoEmUtc);
        var segundo = new FranqueadoUsuario(
            Guid.NewGuid(),
            franqueadoId,
            Guid.NewGuid(),
            false,
            CriadoEmUtc);

        Assert.False(primeiro.Principal);
        Assert.False(segundo.Principal);
        Assert.NotEqual(primeiro.UsuarioId, segundo.UsuarioId);
    }
}

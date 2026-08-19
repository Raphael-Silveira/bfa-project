using BFA.Domain.Usuarios;

namespace BFA.UnitTests.Usuarios;

public sealed class PerfilUsuarioTests
{
    [Fact]
    public void Atualizar_dados_normaliza_campos_e_preserva_identidade_do_perfil()
    {
        var criadoEmUtc = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        var atualizadoEmUtc = criadoEmUtc.AddHours(1);
        var perfil = new PerfilUsuario(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pessoa Inicial",
            null,
            criadoEmUtc);
        var id = perfil.Id;
        var usuarioId = perfil.UsuarioId;

        perfil.AtualizarDados(
            "  Pessoa Atualizada  ",
            "  (11) 99999-9999  ",
            atualizadoEmUtc);

        Assert.Equal(id, perfil.Id);
        Assert.Equal(usuarioId, perfil.UsuarioId);
        Assert.Equal("Pessoa Atualizada", perfil.NomeCompleto);
        Assert.Equal("(11) 99999-9999", perfil.Telefone);
        Assert.Equal(atualizadoEmUtc, perfil.AtualizadoEmUtc);
        Assert.Equal(criadoEmUtc, perfil.CriadoEmUtc);
    }

    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        18,
        12,
        30,
        0,
        DateTimeKind.Utc);

    [Fact]
    public void Criacao_normaliza_apenas_espacos_externos_e_inicia_ativa()
    {
        var id = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        var perfil = new PerfilUsuario(
            id,
            usuarioId,
            "  Maria  da Silva  ",
            "  (15) 99999-0000  ",
            CriadoEmUtc);

        Assert.Equal(id, perfil.Id);
        Assert.Equal(usuarioId, perfil.UsuarioId);
        Assert.Equal("Maria  da Silva", perfil.NomeCompleto);
        Assert.Equal("(15) 99999-0000", perfil.Telefone);
        Assert.True(perfil.Ativo);
        Assert.Equal(CriadoEmUtc, perfil.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, perfil.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_rejeita_nome_completo_vazio(string? nomeCompleto)
    {
        var exception = Assert.Throws<ArgumentException>(() => new PerfilUsuario(
            Guid.NewGuid(),
            Guid.NewGuid(),
            nomeCompleto!,
            null,
            CriadoEmUtc));

        Assert.Equal("nomeCompleto", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_usuario_vazio()
    {
        var exception = Assert.Throws<ArgumentException>(() => new PerfilUsuario(
            Guid.NewGuid(),
            Guid.Empty,
            "Maria da Silva",
            null,
            CriadoEmUtc));

        Assert.Equal("usuarioId", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_data_fora_de_utc()
    {
        var dataInvalida = DateTime.SpecifyKind(CriadoEmUtc, DateTimeKind.Unspecified);

        var exception = Assert.Throws<ArgumentException>(() => new PerfilUsuario(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Maria da Silva",
            null,
            dataInvalida));

        Assert.Equal("criadoEmUtc", exception.ParamName);
    }
}

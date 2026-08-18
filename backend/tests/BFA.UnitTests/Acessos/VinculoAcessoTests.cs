using BFA.Domain.Acessos;

namespace BFA.UnitTests.Acessos;

public sealed class VinculoAcessoTests
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
    public void Administrador_rede_sem_unidade_e_valido()
    {
        var id = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();

        var vinculo = new VinculoAcesso(
            id,
            usuarioId,
            organizacaoId,
            null,
            PerfilAcesso.AdministradorRede,
            CriadoEmUtc);

        Assert.Equal(id, vinculo.Id);
        Assert.Equal(usuarioId, vinculo.UsuarioId);
        Assert.Equal(organizacaoId, vinculo.OrganizacaoId);
        Assert.Null(vinculo.UnidadeId);
        Assert.Equal(PerfilAcesso.AdministradorRede, vinculo.Perfil);
        Assert.True(vinculo.Ativo);
        Assert.Equal(CriadoEmUtc, vinculo.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, vinculo.AtualizadoEmUtc);
        Assert.Equal(DateTimeKind.Utc, vinculo.CriadoEmUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, vinculo.AtualizadoEmUtc.Kind);
    }

    [Fact]
    public void Administrador_rede_com_unidade_e_invalido()
    {
        var exception = Assert.Throws<ArgumentException>(() => new VinculoAcesso(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PerfilAcesso.AdministradorRede,
            CriadoEmUtc));

        Assert.Equal("unidadeId", exception.ParamName);
    }

    [Theory]
    [InlineData(PerfilAcesso.AdministradorUnidade)]
    [InlineData(PerfilAcesso.Professor)]
    [InlineData(PerfilAcesso.Aluno)]
    [InlineData(PerfilAcesso.Responsavel)]
    public void Perfil_de_unidade_sem_unidade_e_invalido(PerfilAcesso perfil)
    {
        var exception = Assert.Throws<ArgumentException>(() => new VinculoAcesso(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            perfil,
            CriadoEmUtc));

        Assert.Equal("unidadeId", exception.ParamName);
    }

    [Fact]
    public void Perfil_de_unidade_rejeita_identificador_de_unidade_vazio()
    {
        var exception = Assert.Throws<ArgumentException>(() => new VinculoAcesso(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            PerfilAcesso.AdministradorUnidade,
            CriadoEmUtc));

        Assert.Equal("unidadeId", exception.ParamName);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("usuarioId")]
    [InlineData("organizacaoId")]
    public void Criacao_rejeita_identificador_obrigatorio_vazio(string parametro)
    {
        var id = parametro == "id" ? Guid.Empty : Guid.NewGuid();
        var usuarioId = parametro == "usuarioId" ? Guid.Empty : Guid.NewGuid();
        var organizacaoId = parametro == "organizacaoId" ? Guid.Empty : Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() => new VinculoAcesso(
            id,
            usuarioId,
            organizacaoId,
            null,
            PerfilAcesso.AdministradorRede,
            CriadoEmUtc));

        Assert.Equal(parametro, exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_perfil_invalido()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new VinculoAcesso(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            (PerfilAcesso)999,
            CriadoEmUtc));

        Assert.Equal("perfil", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_data_que_nao_esta_em_utc()
    {
        var dataSemFuso = DateTime.SpecifyKind(CriadoEmUtc, DateTimeKind.Unspecified);

        var exception = Assert.Throws<ArgumentException>(() => new VinculoAcesso(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            PerfilAcesso.AdministradorRede,
            dataSemFuso));

        Assert.Equal("criadoEmUtc", exception.ParamName);
    }

    [Fact]
    public void Mesmo_usuario_pode_possuir_multiplas_unidades()
    {
        var usuarioId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();

        var primeiro = new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            Guid.NewGuid(),
            PerfilAcesso.AdministradorUnidade,
            CriadoEmUtc);
        var segundo = new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            Guid.NewGuid(),
            PerfilAcesso.AdministradorUnidade,
            CriadoEmUtc);

        Assert.Equal(primeiro.UsuarioId, segundo.UsuarioId);
        Assert.NotEqual(primeiro.UnidadeId, segundo.UnidadeId);
    }

    [Fact]
    public void Mesmo_usuario_pode_possuir_multiplos_perfis()
    {
        var usuarioId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();

        var administradorRede = new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            null,
            PerfilAcesso.AdministradorRede,
            CriadoEmUtc);
        var professor = new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            Guid.NewGuid(),
            PerfilAcesso.Professor,
            CriadoEmUtc);

        Assert.Equal(administradorRede.UsuarioId, professor.UsuarioId);
        Assert.NotEqual(administradorRede.Perfil, professor.Perfil);
    }

    [Fact]
    public void Desativar_e_reativar_preserva_registro_e_atualiza_data_utc()
    {
        var vinculo = new VinculoAcesso(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PerfilAcesso.AdministradorUnidade,
            CriadoEmUtc);
        var desativadoEmUtc = CriadoEmUtc.AddHours(1);
        var reativadoEmUtc = CriadoEmUtc.AddHours(2);

        vinculo.Desativar(desativadoEmUtc);

        Assert.False(vinculo.Ativo);
        Assert.Equal(desativadoEmUtc, vinculo.AtualizadoEmUtc);

        vinculo.Ativar(reativadoEmUtc);

        Assert.True(vinculo.Ativo);
        Assert.Equal(reativadoEmUtc, vinculo.AtualizadoEmUtc);
        Assert.Equal(CriadoEmUtc, vinculo.CriadoEmUtc);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Alteracao_de_estado_rejeita_data_fora_de_utc(bool ativar)
    {
        var vinculo = new VinculoAcesso(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PerfilAcesso.AdministradorUnidade,
            CriadoEmUtc);
        var dataInvalida = DateTime.SpecifyKind(
            CriadoEmUtc.AddHours(1),
            DateTimeKind.Unspecified);

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            if (ativar)
            {
                vinculo.Ativar(dataInvalida);
            }
            else
            {
                vinculo.Desativar(dataInvalida);
            }
        });

        Assert.Equal("atualizadoEmUtc", exception.ParamName);
    }
}

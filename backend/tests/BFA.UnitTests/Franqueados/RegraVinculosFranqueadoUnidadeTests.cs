using BFA.Application.Franqueadora.Franqueados;
using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;

namespace BFA.UnitTests.Franqueados;

public sealed class RegraVinculosFranqueadoUnidadeTests
{
    private static readonly DateTime AgoraUtc =
        new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Nova_unidade_cria_um_vinculo_comercial_e_um_acesso_principal()
    {
        var franqueadoId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        var resultado = RegraVinculosFranqueadoUnidade.GarantirAtivos(
            franqueadoId,
            organizacaoId,
            unidadeId,
            usuarioId,
            vinculoComercial: null,
            acessoAdministradorUnidade: null,
            AgoraUtc);

        Assert.True(resultado.VinculoComercialCriado);
        Assert.True(resultado.AcessoCriado);
        Assert.True(resultado.VinculoComercial.Ativo);
        Assert.Equal(franqueadoId, resultado.VinculoComercial.FranqueadoId);
        Assert.Equal(unidadeId, resultado.VinculoComercial.UnidadeId);
        Assert.True(resultado.AcessoAdministradorUnidade.Ativo);
        Assert.Equal(usuarioId, resultado.AcessoAdministradorUnidade.UsuarioId);
        Assert.Equal(PerfilAcesso.AdministradorUnidade, resultado.AcessoAdministradorUnidade.Perfil);
        Assert.Equal(unidadeId, resultado.AcessoAdministradorUnidade.UnidadeId);
    }

    [Fact]
    public void Vinculos_ativos_equivalentes_sao_reutilizados_sem_duplicacao()
    {
        var ids = CriarIds();
        var vinculo = NovoVinculo(ids);
        var acesso = NovoAcesso(ids);

        var resultado = RegraVinculosFranqueadoUnidade.GarantirAtivos(
            ids.FranqueadoId,
            ids.OrganizacaoId,
            ids.UnidadeId,
            ids.UsuarioId,
            vinculo,
            acesso,
            AgoraUtc.AddHours(1));

        Assert.False(resultado.VinculoComercialCriado);
        Assert.False(resultado.AcessoCriado);
        Assert.Same(vinculo, resultado.VinculoComercial);
        Assert.Same(acesso, resultado.AcessoAdministradorUnidade);
    }

    [Fact]
    public void Vinculos_inativos_equivalentes_sao_reativados()
    {
        var ids = CriarIds();
        var vinculo = NovoVinculo(ids);
        var acesso = NovoAcesso(ids);
        var desativadoEmUtc = AgoraUtc.AddMinutes(10);
        var reativadoEmUtc = AgoraUtc.AddMinutes(20);
        vinculo.Desativar(desativadoEmUtc);
        acesso.Desativar(desativadoEmUtc);

        var resultado = RegraVinculosFranqueadoUnidade.GarantirAtivos(
            ids.FranqueadoId,
            ids.OrganizacaoId,
            ids.UnidadeId,
            ids.UsuarioId,
            vinculo,
            acesso,
            reativadoEmUtc);

        Assert.False(resultado.VinculoComercialCriado);
        Assert.False(resultado.AcessoCriado);
        Assert.True(vinculo.Ativo);
        Assert.True(acesso.Ativo);
        Assert.Equal(reativadoEmUtc, vinculo.AtualizadoEmUtc);
        Assert.Equal(reativadoEmUtc, acesso.AtualizadoEmUtc);
    }

    [Fact]
    public void Vinculo_de_outro_contexto_e_rejeitado()
    {
        var ids = CriarIds();
        var vinculoOutraUnidade = new FranqueadoUnidade(
            Guid.NewGuid(),
            ids.FranqueadoId,
            ids.OrganizacaoId,
            Guid.NewGuid(),
            AgoraUtc);

        Assert.Throws<ArgumentException>(() =>
            RegraVinculosFranqueadoUnidade.GarantirAtivos(
                ids.FranqueadoId,
                ids.OrganizacaoId,
                ids.UnidadeId,
                ids.UsuarioId,
                vinculoOutraUnidade,
                acessoAdministradorUnidade: null,
                AgoraUtc));
    }

    private static ContextoIds CriarIds() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static FranqueadoUnidade NovoVinculo(ContextoIds ids) =>
        new(
            Guid.NewGuid(),
            ids.FranqueadoId,
            ids.OrganizacaoId,
            ids.UnidadeId,
            AgoraUtc);

    private static VinculoAcesso NovoAcesso(ContextoIds ids) =>
        new(
            Guid.NewGuid(),
            ids.UsuarioId,
            ids.OrganizacaoId,
            ids.UnidadeId,
            PerfilAcesso.AdministradorUnidade,
            AgoraUtc);

    private sealed record ContextoIds(
        Guid FranqueadoId,
        Guid OrganizacaoId,
        Guid UnidadeId,
        Guid UsuarioId);
}

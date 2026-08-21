using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;

namespace BFA.Application.Franqueadora.Franqueados;

public sealed record ResultadoVinculosFranqueadoUnidade(
    FranqueadoUnidade VinculoComercial,
    VinculoAcesso AcessoAdministradorUnidade,
    bool VinculoComercialCriado,
    bool AcessoCriado);

public static class RegraVinculosFranqueadoUnidade
{
    public static ResultadoVinculosFranqueadoUnidade GarantirAtivos(
        Guid franqueadoId,
        Guid organizacaoId,
        Guid unidadeId,
        Guid usuarioPrincipalId,
        FranqueadoUnidade? vinculoComercial,
        VinculoAcesso? acessoAdministradorUnidade,
        DateTime agoraUtc)
    {
        ValidarVinculoComercial(
            vinculoComercial,
            franqueadoId,
            organizacaoId,
            unidadeId);
        ValidarAcesso(
            acessoAdministradorUnidade,
            usuarioPrincipalId,
            organizacaoId,
            unidadeId);

        var vinculoCriado = vinculoComercial is null;
        var acessoCriado = acessoAdministradorUnidade is null;

        vinculoComercial ??= new FranqueadoUnidade(
            Guid.NewGuid(),
            franqueadoId,
            organizacaoId,
            unidadeId,
            agoraUtc);
        acessoAdministradorUnidade ??= new VinculoAcesso(
            Guid.NewGuid(),
            usuarioPrincipalId,
            organizacaoId,
            unidadeId,
            PerfilAcesso.AdministradorUnidade,
            agoraUtc);

        if (!vinculoComercial.Ativo)
        {
            vinculoComercial.Ativar(agoraUtc);
        }

        if (!acessoAdministradorUnidade.Ativo)
        {
            acessoAdministradorUnidade.Ativar(agoraUtc);
        }

        return new(
            vinculoComercial,
            acessoAdministradorUnidade,
            vinculoCriado,
            acessoCriado);
    }

    private static void ValidarVinculoComercial(
        FranqueadoUnidade? vinculo,
        Guid franqueadoId,
        Guid organizacaoId,
        Guid unidadeId)
    {
        if (vinculo is not null
            && (vinculo.FranqueadoId != franqueadoId
                || vinculo.OrganizacaoId != organizacaoId
                || vinculo.UnidadeId != unidadeId))
        {
            throw new ArgumentException(
                "O vínculo comercial existente não corresponde ao franqueado e à unidade informados.",
                nameof(vinculo));
        }
    }

    private static void ValidarAcesso(
        VinculoAcesso? acesso,
        Guid usuarioPrincipalId,
        Guid organizacaoId,
        Guid unidadeId)
    {
        if (acesso is not null
            && (acesso.UsuarioId != usuarioPrincipalId
                || acesso.OrganizacaoId != organizacaoId
                || acesso.UnidadeId != unidadeId
                || acesso.Perfil != PerfilAcesso.AdministradorUnidade))
        {
            throw new ArgumentException(
                "O acesso existente não corresponde ao usuário principal e à unidade informados.",
                nameof(acesso));
        }
    }
}

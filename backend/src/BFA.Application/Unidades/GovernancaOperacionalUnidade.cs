using BFA.Application.Acessos;
using BFA.Domain.Acessos;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Unidades;

public sealed record GovernancaOperacionalUnidade(
    bool EhAdministradorRede,
    bool EhAdministradorUnidade,
    bool PossuiFranqueadoAtivo)
{
    public bool PodeAcessar => EhAdministradorRede || EhAdministradorUnidade;

    public bool PodeGerenciarTurmas =>
        EhAdministradorUnidade || EhAdministradorRede && !PossuiFranqueadoAtivo;

    public bool PodeGerenciarPlanoLocal =>
        EhAdministradorUnidade || EhAdministradorRede && !PossuiFranqueadoAtivo;

    public bool PodeGerenciarMatriculas =>
        EhAdministradorUnidade || EhAdministradorRede && !PossuiFranqueadoAtivo;

    public bool PodeGerenciarAlunos =>
        EhAdministradorUnidade || EhAdministradorRede && !PossuiFranqueadoAtivo;
}

public interface IEstadoOperacionalUnidadeConsulta
{
    Task<bool> PossuiFranqueadoAtivoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken);
}

public interface IGovernancaOperacionalUnidade
{
    Task<GovernancaOperacionalUnidade> ObterAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken);
}

public sealed class GovernancaOperacionalUnidadeServico(
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IEstadoOperacionalUnidadeConsulta estadoOperacionalConsulta,
    ILogger<GovernancaOperacionalUnidadeServico> logger)
    : IGovernancaOperacionalUnidade
{
    public async Task<GovernancaOperacionalUnidade> ObterAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var administradorUnidade =
            await acessoUsuarioConsulta.PossuiPerfilNaUnidadeAsync(
                usuarioId,
                organizacaoId,
                unidadeId,
                PerfilAcesso.AdministradorUnidade,
                cancellationToken);
        var administradorRede =
            await acessoUsuarioConsulta.EhAdministradorRedeNaOrganizacaoAsync(
                usuarioId,
                organizacaoId,
                cancellationToken);

        if (!administradorUnidade && !administradorRede)
        {
            return new(false, false, false);
        }

        var possuiFranqueadoAtivo =
            await estadoOperacionalConsulta.PossuiFranqueadoAtivoAsync(
                organizacaoId,
                unidadeId,
                cancellationToken);

        return new(administradorRede, administradorUnidade, possuiFranqueadoAtivo);
    }
}

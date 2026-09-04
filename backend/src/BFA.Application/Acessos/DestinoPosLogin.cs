using BFA.Application.Unidades;

namespace BFA.Application.Acessos;

public sealed class DestinoPosLogin(
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta)
    : IDestinoPosLogin
{
    public async Task<DestinoPosLoginResultado> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty)
        {
            return new(DestinoAcesso.SemAcesso);
        }

        if (await acessoUsuarioConsulta.EhAdministradorRedeAsync(
                usuarioId,
                cancellationToken))
        {
            return new(DestinoAcesso.AdministradorRede);
        }

        var unidades = await unidadesUsuarioConsulta.ListarAdministradasAsync(
            usuarioId,
            cancellationToken);

        if (unidades.Count > 0)
        {
            return unidades.Count == 1
                ? new(DestinoAcesso.Unidade, unidades[0].UnidadeId)
                : new(DestinoAcesso.SelecionarUnidade);
        }

        var unidadesProfessor = await unidadesUsuarioConsulta.ListarProfessorAsync(
            usuarioId, cancellationToken);
        if (unidadesProfessor.Count == 1)
        {
            return new(DestinoAcesso.ProfessorUnidade, unidadesProfessor[0].UnidadeId);
        }

        if (unidadesProfessor.Count > 1)
        {
            return new(DestinoAcesso.SelecionarUnidadeProfessor);
        }

        var unidadesAluno = await unidadesUsuarioConsulta.ListarAlunoAsync(
            usuarioId, cancellationToken);
        return unidadesAluno.Count switch
        {
            0 => new(DestinoAcesso.SemAcesso),
            1 => new(DestinoAcesso.AlunoUnidade, unidadesAluno[0].UnidadeId),
            _ => new(DestinoAcesso.SemAcesso)
        };
    }
}

using BFA.Application.Acessos;

namespace BFA.Web.Acessos;

public static class DestinoPosLoginUrl
{
    public static string Obter(DestinoPosLoginResultado resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return resultado.Destino switch
        {
            DestinoAcesso.AdministradorRede => "/franqueadora",
            DestinoAcesso.Unidade when resultado.UnidadeId is { } unidadeId =>
                $"/unidade/{unidadeId:D}",
            DestinoAcesso.SelecionarUnidade => "/selecionar-unidade",
            DestinoAcesso.ProfessorUnidade when resultado.UnidadeId is { } unidadeProfessorId =>
                $"/professor/unidade/{unidadeProfessorId:D}",
            DestinoAcesso.SelecionarUnidadeProfessor => "/professor/selecionar-unidade",
            DestinoAcesso.AlunoUnidade when resultado.UnidadeId is { } unidadeAlunoId =>
                $"/aluno/{unidadeAlunoId:D}",
            DestinoAcesso.SemAcesso => "/acesso-negado",
            DestinoAcesso.Padrao => "/",
            _ => throw new ArgumentOutOfRangeException(
                nameof(resultado),
                resultado,
                null)
        };
    }
}

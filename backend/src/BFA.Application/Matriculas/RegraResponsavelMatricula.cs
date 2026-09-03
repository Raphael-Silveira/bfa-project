using BFA.Domain.Alunos;

namespace BFA.Application.Matriculas;

public sealed record ResponsavelVinculadoMatricula(
    AlunoResponsavel Vinculo,
    Responsavel Responsavel);

public static class RegraResponsavelMatricula
{
    public static void Validar(
        Aluno aluno,
        DateOnly dataInicio,
        IEnumerable<ResponsavelVinculadoMatricula> responsaveis)
    {
        ArgumentNullException.ThrowIfNull(aluno);
        ArgumentNullException.ThrowIfNull(responsaveis);

        if (!aluno.Ativo)
        {
            throw new InvalidOperationException(
                "A matricula exige um aluno ativo.");
        }

        if (!aluno.EhMenorEm(dataInicio))
        {
            return;
        }

        var possuiResponsavelAtivo = responsaveis.Any(item =>
            item.Vinculo.OrganizacaoId == aluno.OrganizacaoId
            && item.Vinculo.AlunoId == aluno.Id
            && item.Vinculo.ResponsavelId == item.Responsavel.Id
            && item.Responsavel.OrganizacaoId == aluno.OrganizacaoId
            && item.Vinculo.Ativo
            && item.Responsavel.Ativo);

        if (!possuiResponsavelAtivo)
        {
            throw new InvalidOperationException(
                "O aluno menor deve possuir ao menos um responsavel ativo para ser matriculado.");
        }
    }
}

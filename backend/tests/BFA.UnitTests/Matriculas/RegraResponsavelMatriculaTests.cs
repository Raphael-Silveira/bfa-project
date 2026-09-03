using BFA.Application.Matriculas;
using BFA.Domain.Alunos;

namespace BFA.UnitTests.Matriculas;

public sealed class RegraResponsavelMatriculaTests
{
    private static readonly DateOnly Inicio = new(2026, 9, 1);
    private static readonly DateTime Agora = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Adulto_ativo_nao_exige_responsavel()
    {
        var aluno = CriarAluno(Inicio.AddYears(-18));

        RegraResponsavelMatricula.Validar(aluno, Inicio, []);
    }

    [Fact]
    public void Menor_sem_responsavel_ativo_e_rejeitado()
    {
        var aluno = CriarAluno(Inicio.AddYears(-17));

        Assert.Throws<InvalidOperationException>(() =>
            RegraResponsavelMatricula.Validar(aluno, Inicio, []));
    }

    [Fact]
    public void Menor_com_vinculo_e_responsavel_ativos_e_aceito()
    {
        var aluno = CriarAluno(Inicio.AddYears(-17));
        var responsavel = CriarResponsavel(aluno.OrganizacaoId);
        var vinculo = CriarVinculo(aluno, responsavel);

        RegraResponsavelMatricula.Validar(
            aluno, Inicio, [new(vinculo, responsavel)]);
    }

    [Fact]
    public void Menor_com_responsavel_inativo_e_rejeitado()
    {
        var aluno = CriarAluno(Inicio.AddYears(-17));
        var responsavel = CriarResponsavel(aluno.OrganizacaoId);
        var vinculo = CriarVinculo(aluno, responsavel);
        responsavel.Desativar(Agora.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            RegraResponsavelMatricula.Validar(
                aluno, Inicio, [new(vinculo, responsavel)]));
    }

    [Fact]
    public void Menor_com_vinculo_inativo_e_rejeitado()
    {
        var aluno = CriarAluno(Inicio.AddYears(-17));
        var responsavel = CriarResponsavel(aluno.OrganizacaoId);
        var vinculo = CriarVinculo(aluno, responsavel);
        vinculo.Desativar(Agora.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            RegraResponsavelMatricula.Validar(
                aluno, Inicio, [new(vinculo, responsavel)]));
    }

    [Fact]
    public void Aluno_inativo_e_rejeitado_mesmo_se_adulto()
    {
        var aluno = CriarAluno(Inicio.AddYears(-20));
        aluno.Desativar(Agora.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            RegraResponsavelMatricula.Validar(aluno, Inicio, []));
    }

    private static Aluno CriarAluno(DateOnly nascimento) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Aluno", nascimento, Inicio, Agora);

    private static Responsavel CriarResponsavel(Guid organizacaoId) => new(
        Guid.NewGuid(), organizacaoId, "Responsavel", Agora, telefone: "11999999999");

    private static AlunoResponsavel CriarVinculo(Aluno aluno, Responsavel responsavel) => new(
        Guid.NewGuid(), aluno.OrganizacaoId, aluno.Id, responsavel.Id,
        TipoRelacaoResponsavel.ResponsavelLegal, true, false, Agora);
}

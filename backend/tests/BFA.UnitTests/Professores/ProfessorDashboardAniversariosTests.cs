using BFA.Application.Professores.Turmas;

namespace BFA.UnitTests.Professores;

public sealed class ProfessorDashboardAniversariosTests
{
    [Fact]
    public void Inclui_aniversario_de_hoje_e_dos_proximos_trinta_dias()
    {
        var hoje = new DateOnly(2026, 9, 19);
        var alunos = new[]
        {
            new ProfessorDashboardAlunoResumo(Guid.NewGuid(), "Hoje", new DateOnly(2000, 9, 19)),
            new ProfessorDashboardAlunoResumo(Guid.NewGuid(), "Limite", new DateOnly(2000, 10, 19)),
            new ProfessorDashboardAlunoResumo(Guid.NewGuid(), "Fora", new DateOnly(2000, 10, 20))
        };

        var resultado = ProfessorDashboardAniversarios.Calcular(alunos, hoje);

        Assert.Equal(["Hoje", "Limite"], resultado.Select(item => item.Nome));
        Assert.Equal([0, 30], resultado.Select(item => item.DiasAteAniversario));
    }

    [Fact]
    public void Trata_a_virada_do_ano()
    {
        var hoje = new DateOnly(2026, 12, 28);
        var aluno = new ProfessorDashboardAlunoResumo(
            Guid.NewGuid(), "Janeiro", new DateOnly(2000, 1, 2));

        var resultado = ProfessorDashboardAniversarios.Calcular([aluno], hoje);

        var aniversario = Assert.Single(resultado);
        Assert.Equal(new DateOnly(2027, 1, 2), aniversario.Data);
        Assert.Equal(5, aniversario.DiasAteAniversario);
    }

    [Fact]
    public void Trata_29_de_fevereiro_em_ano_nao_bissexto()
    {
        var hoje = new DateOnly(2026, 2, 1);
        var aluno = new ProfessorDashboardAlunoResumo(
            Guid.NewGuid(), "Bissexto", new DateOnly(2000, 2, 29));

        var aniversario = Assert.Single(ProfessorDashboardAniversarios.Calcular([aluno], hoje));

        Assert.Equal(new DateOnly(2026, 2, 28), aniversario.Data);
    }

    [Fact]
    public void Ordena_por_proximidade()
    {
        var hoje = new DateOnly(2026, 9, 19);
        var alunos = new[]
        {
            new ProfessorDashboardAlunoResumo(Guid.NewGuid(), "Depois", new DateOnly(2000, 9, 24)),
            new ProfessorDashboardAlunoResumo(Guid.NewGuid(), "Antes", new DateOnly(2000, 9, 20))
        };

        var resultado = ProfessorDashboardAniversarios.Calcular(alunos, hoje);

        Assert.Equal(["Antes", "Depois"], resultado.Select(item => item.Nome));
    }
}

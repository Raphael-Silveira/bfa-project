using BFA.Application.Matriculas;
using BFA.Domain.Turmas;

namespace BFA.UnitTests.Matriculas;

public sealed class RegraGradeMatriculaTests
{
    [Fact]
    public void Horarios_adjacentes_nao_conflitam()
    {
        var horarios = new[]
        {
            new IntervaloHorarioGrade(
                DiaSemana.Segunda, new TimeOnly(19, 0), new TimeOnly(20, 0)),
            new IntervaloHorarioGrade(
                DiaSemana.Segunda, new TimeOnly(20, 0), new TimeOnly(21, 0))
        };

        Assert.False(RegraGradeMatricula.PossuiConflito(horarios));
    }

    [Fact]
    public void Sobreposicao_no_mesmo_dia_conflita()
    {
        var horarios = new[]
        {
            new IntervaloHorarioGrade(
                DiaSemana.Segunda, new TimeOnly(19, 0), new TimeOnly(20, 0)),
            new IntervaloHorarioGrade(
                DiaSemana.Segunda, new TimeOnly(19, 30), new TimeOnly(20, 30))
        };

        Assert.True(RegraGradeMatricula.PossuiConflito(horarios));
    }

    [Fact]
    public void Maximo_temporal_nao_soma_historicos_sucessivos()
    {
        var intervalos = new[]
        {
            new IntervaloVigenciaGrade(
                new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)),
            new IntervaloVigenciaGrade(
                new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)),
            new IntervaloVigenciaGrade(new DateOnly(2026, 7, 1), null)
        };

        Assert.Equal(1, RegraGradeMatricula.MaximoSimultaneo(
            intervalos, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void Maximo_temporal_conta_intervalos_inclusivos_sobrepostos()
    {
        var intervalos = new[]
        {
            new IntervaloVigenciaGrade(
                new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)),
            new IntervaloVigenciaGrade(new DateOnly(2026, 3, 31), null)
        };

        Assert.Equal(2, RegraGradeMatricula.MaximoSimultaneo(
            intervalos, new DateOnly(2026, 1, 1), null));
    }
}

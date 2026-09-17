using BFA.Application.AlunoArea;
using BFA.Web.ViewModels.AlunoArea;

namespace BFA.IntegrationTests;

public sealed class AlunoMatriculaFeatureTests
{
    [Fact]
    public void Dashboard_mapeia_matricula_ativa_com_nome_real_datas_e_frequencia()
    {
        var matricula = new MatriculaAlunoDto(
            Guid.NewGuid(),
            "Plano Performance",
            3,
            "Ativa",
            new DateOnly(2026, 9, 1),
            new DateOnly(2027, 9, 1),
            null,
            450m,
            [new("Segunda", "19:00", "20:00", "Turma Adulto")]);
        var dashboard = new DashboardAlunoDto(
            Guid.NewGuid(),
            new(Guid.NewGuid(), "Aluno", null, null, null, new DateOnly(2000, 1, 1), true),
            "BFA Unidade",
            null,
            "100%",
            "R$ 0,00",
            0,
            matricula);

        var viewModel = DashboardAlunoViewModel.Mapear(dashboard, dashboard.OrganizacaoId);

        Assert.NotNull(viewModel.MatriculaAtiva);
        Assert.Equal("Plano Performance", viewModel.MatriculaAtiva!.PlanoNome);
        Assert.Equal("01/09/2026", viewModel.MatriculaAtiva.DataInicio);
        Assert.Equal("01/09/2027", viewModel.MatriculaAtiva.DataFimPrevista);
        Assert.Equal(3, viewModel.MatriculaAtiva.FrequenciaSemanal);
        Assert.Single(viewModel.MatriculaAtiva.Horarios);
    }

    [Fact]
    public void Dashboard_mapeia_empty_state_quando_nao_ha_matricula_ativa()
    {
        var dashboard = new DashboardAlunoDto(
            Guid.NewGuid(),
            new(Guid.NewGuid(), "Aluno", null, null, null, new DateOnly(2000, 1, 1), true),
            "BFA Unidade",
            null,
            "0%",
            "R$ 0,00",
            0,
            null);

        var viewModel = DashboardAlunoViewModel.Mapear(dashboard, dashboard.OrganizacaoId);

        Assert.Null(viewModel.MatriculaAtiva);
    }

    [Fact]
    public void Consulta_de_matriculas_preserva_nome_real_e_horarios_do_DTO()
    {
        var matricula = new MatriculaAlunoDto(
            Guid.NewGuid(),
            "Plano Performance",
            2,
            "Encerrada",
            new DateOnly(2025, 1, 10),
            new DateOnly(2026, 1, 10),
            new DateOnly(2026, 1, 5),
            300m,
            [new("Quarta", "18:30", "19:30", "Turma Adulto")]);

        var viewModel = MatriculaAlunoViewModel.Mapear(matricula);

        Assert.Equal("Plano Performance", viewModel.PlanoNome);
        Assert.Equal(2, viewModel.FrequenciaSemanal);
        Assert.Equal("Quarta", Assert.Single(viewModel.Horarios).DiaSemana);
        Assert.Equal("18:30", viewModel.Horarios[0].HoraInicio);
    }

    [Fact]
    public void Implementacao_busca_plano_real_e_nao_fragmento_de_GUID()
    {
        var root = FindRepositoryRoot();
        var servico = File.ReadAllText(Path.Combine(
            root, "backend", "src", "BFA.Application", "AlunoArea", "AlunoAreaServico.cs"));
        var repositorio = File.ReadAllText(Path.Combine(
            root, "backend", "src", "BFA.Infrastructure", "AlunoArea", "AlunoAreaRepositorio.cs"));
        var dashboard = File.ReadAllText(Path.Combine(
            root, "backend", "src", "BFA.Web", "Areas", "Aluno", "Views", "Dashboard.cshtml"));

        Assert.DoesNotContain("PlanoVersaoId.ToString()[..8]", servico, StringComparison.Ordinal);
        Assert.Contains("PlanosVersoes", repositorio, StringComparison.Ordinal);
        Assert.Contains("Planos.AsNoTracking", repositorio, StringComparison.Ordinal);
        Assert.Contains("MatriculasHorarios", repositorio, StringComparison.Ordinal);
        Assert.Contains("StatusMatricula.Ativa", servico, StringComparison.Ordinal);
        Assert.Contains("Nenhuma matrícula ativa no momento.", dashboard, StringComparison.Ordinal);
        Assert.Contains("Ver matrícula", dashboard, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }
}

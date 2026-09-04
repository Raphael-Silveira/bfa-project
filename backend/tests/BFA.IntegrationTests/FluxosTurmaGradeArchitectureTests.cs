using System.Security.Cryptography;
using System.Text;

namespace BFA.IntegrationTests;

public sealed class FluxosTurmaGradeArchitectureTests
{
    [Fact]
    public void Prelock_da_troca_declara_ordem_matriculas_alunos_horarios()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryDirectory(), "backend", "src", "BFA.Infrastructure",
            "Unidades", "TrocaProfessorTurmaRepositorio.cs"));
        var matriculas = source.IndexOf(
            "GradeLoteLocks.BloquearMatriculasAsync", StringComparison.Ordinal);
        var alunos = source.IndexOf(
            "GradeLoteLocks.BloquearAlunosAsync", StringComparison.Ordinal);
        var horarios = source.IndexOf(
            "GradeLoteLocks.BloquearTurmasHorariosAsync", StringComparison.Ordinal);

        Assert.True(matriculas < alunos && alunos < horarios);
    }

    [Fact]
    public void Cada_conjunto_e_bloqueado_por_id_com_for_update()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryDirectory(), "backend", "src", "BFA.Infrastructure",
            "Unidades", "GradeLoteLocks.cs"));

        Assert.Equal(3, source.Split("ORDER BY id", StringSplitOptions.None).Length - 1);
        Assert.Equal(3, source.Split("FOR UPDATE", StringSplitOptions.None).Length - 1);
        Assert.Contains("FROM matriculas", source);
        Assert.Contains("FROM alunos", source);
        Assert.Contains("FROM turmas_horarios", source);
    }

    [Fact]
    public void V013_permanece_intacta_e_v014_e_somente_corretiva()
    {
        var migrations = Path.Combine(RepositoryDirectory(), "database", "migrations");
        var files = Directory.GetFiles(migrations, "V*.sql")
            .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.True(files.Length == 15, $"Esperadas 15 migrations, encontradas {files.Length}.");
        Assert.Contains("V014__corrigir_validacao_de_unidade_na_matricula.sql", files);

        var content = File.ReadAllText(Path.Combine(
            migrations, "V013__criar_grade_das_matriculas.sql"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(
            "973392C32ECFEAC651D99180459BA689AA06FA6A705166A3F8F677F9C650F5C4",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))));

        var correcao = File.ReadAllText(Path.Combine(
            migrations, "V014__corrigir_validacao_de_unidade_na_matricula.sql"));
        Assert.Contains("CREATE OR REPLACE FUNCTION proteger_matricula()", correcao);
        Assert.Contains("SELECT ativa", correcao);
        Assert.DoesNotContain("ALTER TABLE", correcao, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "database")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return directory.FullName;
    }
}

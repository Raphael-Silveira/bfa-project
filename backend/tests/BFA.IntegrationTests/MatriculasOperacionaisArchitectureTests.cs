using System.Security.Cryptography;
using System.Text;
using BFA.Application.Matriculas;
using BFA.Infrastructure.Matriculas;

namespace BFA.IntegrationTests;

public sealed class MatriculasOperacionaisArchitectureTests
{
    [Fact]
    public void Application_nao_depende_de_ef_infrastructure_mvc_ou_http()
    {
        var assembly = typeof(IMatriculasServico).Assembly;
        var referencias = assembly.GetReferencedAssemblies()
            .Select(item => item.Name).ToArray();

        Assert.DoesNotContain("BFA.Infrastructure", referencias);
        Assert.DoesNotContain(referencias, item =>
            item!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referencias, item =>
            item!.StartsWith("Microsoft.AspNetCore.Mvc", StringComparison.Ordinal));
    }

    [Fact]
    public void Alteracao_e_finalizacao_declaram_ordem_canonica_de_locks()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryDirectory(), "backend", "src", "BFA.Infrastructure",
            "Matriculas", "MatriculasRepositorio.cs"));
        var inicio = source.IndexOf(
            "public async Task<ResultadoMatriculas<ResultadoAlteracaoGrade>> AlterarGradeAsync",
            StringComparison.Ordinal);
        var fim = source.IndexOf(
            "public async Task<EstadoMatriculas> FinalizarAsync",
            inicio, StringComparison.Ordinal);
        var metodo = source[inicio..fim];

        var matriculas = metodo.IndexOf("BloquearMatriculasAsync", StringComparison.Ordinal);
        var alunos = metodo.IndexOf("BloquearAlunosAsync", StringComparison.Ordinal);
        var horarios = metodo.IndexOf("BloquearTurmasHorariosAsync", StringComparison.Ordinal);
        Assert.True(matriculas < alunos && alunos < horarios);
    }

    [Fact]
    public void Interface_web_reutiliza_application_e_v014_e_somente_corretiva()
    {
        var raiz = RepositoryDirectory();
        var web = Path.Combine(raiz, "backend", "src", "BFA.Web");
        var arquivosWeb = Directory.GetFiles(web, "*Matricula*", SearchOption.AllDirectories)
            .Where(item => item.Contains("Controllers", StringComparison.OrdinalIgnoreCase)
                || item.Contains("Views", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Contains(arquivosWeb, item => Path.GetFileName(item)
            .Equals("MatriculasController.cs", StringComparison.Ordinal));
        var controller = File.ReadAllText(arquivosWeb.Single(item => Path.GetFileName(item)
            .Equals("MatriculasController.cs", StringComparison.Ordinal)));
        Assert.Contains("IMatriculasServico", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("BfaDbContext", controller, StringComparison.Ordinal);

        var migrations = Path.Combine(raiz, "database", "migrations");
        var files = Directory.GetFiles(migrations, "V*.sql");
        Assert.True(files.Length == 14, $"Esperadas 14 migrations, encontradas {files.Length}.");
        Assert.Contains(files, item => Path.GetFileName(item)
            .Equals("V014__corrigir_validacao_de_unidade_na_matricula.sql",
                StringComparison.Ordinal));
        var content = File.ReadAllText(Path.Combine(
            migrations, "V013__criar_grade_das_matriculas.sql"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(
            "973392C32ECFEAC651D99180459BA689AA06FA6A705166A3F8F677F9C650F5C4",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
    }

    [Fact]
    public void Infrastructure_implementa_os_contratos_operacionais()
    {
        Assert.Contains(typeof(IMatriculasRepositorio),
            typeof(MatriculasRepositorio).GetInterfaces());
    }

    [Fact]
    public void Wizard_web_e_progressivo_seguro_e_chama_somente_application_no_post_final()
    {
        var raiz = RepositoryDirectory();
        var controller = File.ReadAllText(Path.Combine(
            raiz, "backend", "src", "BFA.Web", "Areas", "Unidade",
            "Controllers", "MatriculasController.cs"));
        var view = File.ReadAllText(Path.Combine(
            raiz, "backend", "src", "BFA.Web", "Areas", "Unidade",
            "Views", "Matriculas", "Nova.cshtml"));
        var script = File.ReadAllText(Path.Combine(
            raiz, "backend", "src", "BFA.Web", "wwwroot", "js",
            "bfa-matricula-wizard.js"));
        var css = File.ReadAllText(Path.Combine(
            raiz, "backend", "src", "BFA.Web", "wwwroot", "css",
            "unidade.css"));

        Assert.Contains("[HttpGet(\"nova\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"nova\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[ValidateAntiForgeryToken]", controller, StringComparison.Ordinal);
        Assert.Contains("matriculasServico.CriarAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("BfaDbContext", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("BeginTransaction", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("new Matricula(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("/unidade/unidade/", controller, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Um dos horários selecionados acabou de ficar sem vagas.",
            controller, StringComparison.Ordinal);
        Assert.Contains("O aluno possui outro horário conflitante",
            controller, StringComparison.Ordinal);
        Assert.Contains("O plano selecionado não está mais disponível.",
            controller, StringComparison.Ordinal);
        Assert.Contains("O aluno já possui uma matrícula ativa nesta unidade.",
            controller, StringComparison.Ordinal);
        Assert.Contains("Já existe um cadastro com o CPF informado.",
            controller, StringComparison.Ordinal);

        Assert.Contains("data-step=\"1\"", view, StringComparison.Ordinal);
        Assert.Contains("data-step=\"5\"", view, StringComparison.Ordinal);
        Assert.Contains("Confirmar matrícula", view, StringComparison.Ordinal);
        Assert.Contains("data-edit-step=\"1\"", view, StringComparison.Ordinal);
        Assert.Contains("data-edit-step=\"4\"", view, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", view, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionStorage", view, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("primeiro.dataset.start < segundo.dataset.end", script,
            StringComparison.Ordinal);
        Assert.Contains("segundo.dataset.start < primeiro.dataset.end", script,
            StringComparison.Ordinal);
        Assert.Contains("selecionados.length >= limite", script, StringComparison.Ordinal);
        Assert.Contains("item.checked = false", script, StringComparison.Ordinal);
        Assert.Contains("botao.disabled = true", script, StringComparison.Ordinal);
        Assert.Contains("Criando matrícula...", view, StringComparison.Ordinal);
        Assert.Contains("Os dados preenchidos serão descartados.", script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionStorage", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NovoAluno.Cpf", script, StringComparison.Ordinal);
        Assert.DoesNotContain("NovoAluno.Email", script, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(5, minmax(0, 1fr))", css,
            StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 58rem)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 30rem)", css, StringComparison.Ordinal);
        Assert.Contains(".bfa-matricula-step-actions .bfa-admin-button", css,
            StringComparison.Ordinal);
    }

    private static string RepositoryDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "database")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}

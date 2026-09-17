namespace BFA.IntegrationTests;

public sealed class ConfirmacaoAulaAlunoArchitectureTests
{
    [Fact]
    public void V022_define_confirmacao_sem_status_persistido_e_com_integridade_tenant_aware()
    {
        var caminho = Path.Combine(RepositoryDirectory(), "database", "migrations",
            "V022__criar_confirmacoes_de_aula_do_aluno.sql");
        var sql = File.ReadAllText(caminho);

        Assert.Contains("CREATE TABLE confirmacoes_aula_aluno", sql, StringComparison.Ordinal);
        Assert.Contains("ativa boolean NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("uq_confirmacoes_aula_aluno_identidade", sql, StringComparison.Ordinal);
        Assert.Contains("FOREIGN KEY (organizacao_id, unidade_id, aula_id)", sql, StringComparison.Ordinal);
        Assert.Contains("FOREIGN KEY (organizacao_id, aluno_id)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("status", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GRANT SELECT, INSERT, UPDATE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Mapeamento_de_aula_do_portal_preserva_o_identificador_real()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryDirectory(), "backend", "src",
            "BFA.Application", "AlunoArea", "AlunoAreaServico.cs"));

        Assert.Contains("a.AulaId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AulaAlunoDto(\n            Guid.Empty", source, StringComparison.Ordinal);
    }

    private static string RepositoryDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "database", "migrations")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}

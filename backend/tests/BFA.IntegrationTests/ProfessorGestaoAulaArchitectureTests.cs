using Xunit;

namespace BFA.IntegrationTests;

public sealed class ProfessorGestaoAulaArchitectureTests
{
    [Fact]
    public void V024_adiciona_auditoria_nullable_e_V025_preserva_escopo()
    {
        var raiz = RepositoryDirectory();
        var migrations = Path.Combine(raiz, "database", "migrations");
        var files = Directory.GetFiles(migrations, "V*.sql");
        Assert.Contains(files, item => Path.GetFileName(item)
            .Equals("V025__criar_day_use.sql", StringComparison.Ordinal));
        Assert.Contains(files, item => Path.GetFileName(item)
            .Equals("V026__permitir_exclusao_day_use.sql", StringComparison.Ordinal));

        var sql = File.ReadAllText(Path.Combine(
            migrations, "V024__adicionar_auditoria_cancelamento_aula.sql"));
        Assert.Contains("motivo_cancelamento varchar(500) NULL", sql, StringComparison.Ordinal);
        Assert.Contains("cancelada_em_utc timestamptz NULL", sql, StringComparison.Ordinal);
        Assert.Contains("cancelada_por_usuario_id uuid NULL", sql, StringComparison.Ordinal);
        Assert.Contains("VALUES ('V024'", sql, StringComparison.Ordinal);
    }

    private static string RepositoryDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "database")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}

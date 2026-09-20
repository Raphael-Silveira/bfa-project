namespace BFA.IntegrationTests;

public sealed class DayUseMigrationTests
{
    [Fact]
    public void V025_cria_day_use_isolado_sem_relacoes_operacionais()
    {
        var sql = File.ReadAllText(Migration("V025__criar_day_use.sql"));

        Assert.Contains("ALTER TABLE unidades", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("valor_day_use_sugerido", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE day_uses", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ck_day_uses_participante_valido", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uq_day_uses_aluno_data", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aula_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("matricula_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("presenca", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cobranca", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VALUES ('V025'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V001_a_V026_continuam_presentes_com_v025_e_v026()
    {
        var directory = MigrationDirectory();
        for (var version = 1; version <= 24; version++)
        {
            Assert.Contains(Directory.GetFiles(directory), path =>
                Path.GetFileName(path).StartsWith($"V{version:000}_", StringComparison.Ordinal));
        }

        Assert.Single(Directory.GetFiles(directory), path =>
            Path.GetFileName(path).StartsWith("V025_", StringComparison.Ordinal));
        Assert.Contains(Directory.GetFiles(directory), path =>
            Path.GetFileName(path).StartsWith("V026_", StringComparison.Ordinal));
    }

    [Fact]
    public void V026_concede_delete_sem_alterar_a_v025()
    {
        var v025 = File.ReadAllText(Migration("V025__criar_day_use.sql"));
        var v026 = File.ReadAllText(Migration("V026__permitir_exclusao_day_use.sql"));

        Assert.DoesNotContain("GRANT DELETE", v025, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GRANT DELETE ON TABLE day_uses TO bfa_app_role", v026, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VALUES ('V026'", v026, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP", v026, StringComparison.OrdinalIgnoreCase);
    }

    private static string Migration(string file) => Path.Combine(MigrationDirectory(), file);

    private static string MigrationDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var index = 0; index < 6; index++) directory = directory.Parent!;
        return Path.Combine(directory.FullName, "database", "migrations");
    }
}

using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class LocalidadesMigrationTests
{
    [Fact]
    public void V006_cria_catalogo_global_exato_sem_dados_em_massa()
    {
        var sql = ReadMigration();
        var normalized = Regex.Replace(sql, @"\s+", " ").Trim();

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("organizacao_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unidade_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO estados", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO municipios", sql, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("codigo_ibge integer NOT NULL", normalized);
        Assert.Contains("sigla varchar(2) NOT NULL", normalized);
        Assert.Contains("nome varchar(100) NOT NULL", normalized);
        Assert.Contains("nome varchar(150) NOT NULL", normalized);
        Assert.Contains("CONSTRAINT pk_estados PRIMARY KEY (codigo_ibge)", normalized);
        Assert.Contains("CONSTRAINT uq_estados_sigla UNIQUE (sigla)", normalized);
        Assert.Contains("CONSTRAINT pk_municipios PRIMARY KEY (codigo_ibge)", normalized);
        Assert.Contains("codigo_ibge > 0", normalized);
        Assert.Contains("sigla ~ '^[A-Z]{2}$'", normalized);
        Assert.Contains("btrim(nome) <> ''", normalized);
        Assert.Contains(
            "FOREIGN KEY (estado_codigo_ibge) REFERENCES estados (codigo_ibge) ON DELETE RESTRICT",
            normalized);
        Assert.Contains(
            "CREATE INDEX ix_municipios_estado_ativo_nome ON municipios (estado_codigo_ibge, ativo, nome);",
            normalized);
        Assert.Contains(
            "GRANT SELECT, INSERT, UPDATE ON TABLE estados, municipios TO bfa_app_role;",
            normalized);
        Assert.Contains("VALUES ('V006', 'criar catalogo de localidades');", normalized);
    }

    [Fact]
    public void V001_a_v005_permanecem_sem_referencia_a_v006()
    {
        for (var version = 1; version <= 5; version++)
        {
            var migration = Directory.GetFiles(
                GetMigrationsDirectory(),
                $"V{version:000}__*.sql").Single();
            Assert.DoesNotContain("estados", File.ReadAllText(migration), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("municipios", File.ReadAllText(migration), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadMigration() =>
        File.ReadAllText(Path.Combine(
            GetMigrationsDirectory(),
            "V006__criar_catalogo_localidades.sql"));

    private static string GetMigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "database")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "database", "migrations");
    }
}

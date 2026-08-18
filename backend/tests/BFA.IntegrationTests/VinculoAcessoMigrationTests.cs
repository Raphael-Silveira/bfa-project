using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class VinculoAcessoMigrationTests
{
    [Fact]
    public void V003_possui_colunas_constraints_indices_e_permissoes_esperados()
    {
        var sql = ReadMigration("V003__criar_vinculos_acesso.sql");
        var normalizedSql = NormalizeWhitespace(sql);

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "usuario_id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "unidade_id uuid NULL",
                "perfil varchar(50) NOT NULL",
                "ativo boolean NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "vinculos_acesso"));

        Assert.Contains(
            "ALTER TABLE unidades ADD CONSTRAINT uq_unidades_organizacao_id_id "
            + "UNIQUE (organizacao_id, id);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains("CONSTRAINT pk_vinculos_acesso PRIMARY KEY (id)", normalizedSql);
        Assert.Contains(
            "CONSTRAINT fk_vinculos_acesso_usuarios_usuario_id "
            + "FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT fk_vinculos_acesso_organizacoes_organizacao_id "
            + "FOREIGN KEY (organizacao_id) REFERENCES organizacoes (id) ON DELETE RESTRICT",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT fk_vinculos_acesso_unidades_organizacao_id_unidade_id "
            + "FOREIGN KEY (organizacao_id, unidade_id) "
            + "REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Equal(
            3,
            Regex.Matches(
                sql,
                "ON DELETE RESTRICT",
                RegexOptions.CultureInvariant).Count);

        Assert.Contains(
            "CONSTRAINT ck_vinculos_acesso_perfil_valido CHECK "
            + "( perfil IN ( 'AdministradorRede', 'AdministradorUnidade', "
            + "'Professor', 'Aluno', 'Responsavel' ) )",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT ck_vinculos_acesso_escopo_perfil CHECK "
            + "( (perfil = 'AdministradorRede' AND unidade_id IS NULL) OR "
            + "(perfil <> 'AdministradorRede' AND unidade_id IS NOT NULL) )",
            normalizedSql,
            StringComparison.Ordinal);

        Assert.Contains(
            "CREATE INDEX ix_vinculos_acesso_usuario_id_ativo "
            + "ON vinculos_acesso (usuario_id, ativo);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE INDEX ix_vinculos_acesso_organizacao_id_unidade_id "
            + "ON vinculos_acesso (organizacao_id, unidade_id);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE INDEX ix_vinculos_acesso_unidade_id "
            + "ON vinculos_acesso (unidade_id);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_vinculos_acesso_usuario_organizacao_unidade_perfil "
            + "ON vinculos_acesso (usuario_id, organizacao_id, unidade_id, perfil) "
            + "NULLS NOT DISTINCT;",
            normalizedSql,
            StringComparison.Ordinal);

        Assert.Contains(
            "GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE vinculos_acesso TO bfa_app_role;",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("bfa_dev_app", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("bfa_staging_app", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("bfa_prod_app", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ON TABLE bfa_schema_history",
            normalizedSql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "INSERT INTO bfa_schema_history (version, descricao) "
            + "VALUES ('V003', 'criar vinculos de acesso');",
            normalizedSql,
            StringComparison.Ordinal);
    }

    private static string[] GetSqlColumns(string sql, string tableName)
    {
        var tableMatch = Regex.Match(
            sql,
            $@"CREATE TABLE {Regex.Escape(tableName)} \((?<body>.*?)\r?\n\);",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        Assert.True(tableMatch.Success, $"Tabela {tableName} nao encontrada na V003.");

        return tableMatch.Groups["body"].Value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimEnd(','))
            .TakeWhile(line => !line.StartsWith("CONSTRAINT ", StringComparison.Ordinal))
            .ToArray();
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(
            value,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant).Trim();
    }

    private static string ReadMigration(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "database",
            "migrations",
            fileName));
    }
}

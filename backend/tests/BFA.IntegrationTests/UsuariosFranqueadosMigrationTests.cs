using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class UsuariosFranqueadosMigrationTests
{
    [Fact]
    public void V004_possui_tabelas_colunas_e_limites_esperados()
    {
        var sql = ReadV004();

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            ["franqueados", "franqueados_unidades", "franqueados_usuarios", "perfis_usuario"],
            Regex.Matches(
                    sql,
                    @"CREATE TABLE (?<table>[a-z0-9_]+) \(",
                    RegexOptions.CultureInvariant)
                .Select(match => match.Groups["table"].Value)
                .OrderBy(tableName => tableName));

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "usuario_id uuid NOT NULL",
                "nome_completo varchar(150) NOT NULL",
                "telefone varchar(30) NULL",
                "ativo boolean NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "perfis_usuario"));

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "tipo_pessoa varchar(30) NOT NULL",
                "nome_razao_social varchar(200) NOT NULL",
                "nome_fantasia varchar(200) NULL",
                "documento varchar(14) NOT NULL",
                "telefone varchar(30) NULL",
                "email varchar(256) NOT NULL",
                "email_financeiro varchar(256) NULL",
                "responsavel_legal varchar(150) NULL",
                "logradouro varchar(200) NULL",
                "numero varchar(30) NULL",
                "complemento varchar(100) NULL",
                "bairro varchar(100) NULL",
                "cidade varchar(100) NULL",
                "estado varchar(2) NULL",
                "cep varchar(8) NULL",
                "observacoes varchar(2000) NULL",
                "ativo boolean NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "franqueados"));

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "franqueado_id uuid NOT NULL",
                "usuario_id uuid NOT NULL",
                "principal boolean NOT NULL",
                "ativo boolean NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "franqueados_usuarios"));

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "franqueado_id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "unidade_id uuid NOT NULL",
                "ativo boolean NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "franqueados_unidades"));
    }

    [Fact]
    public void V004_possui_constraints_multi_tenant_e_delete_restritivo()
    {
        var sql = ReadV004();
        var normalizedSql = NormalizeWhitespace(sql);

        Assert.Contains("CONSTRAINT pk_perfis_usuario PRIMARY KEY (id)", normalizedSql);
        Assert.Contains("CONSTRAINT pk_franqueados PRIMARY KEY (id)", normalizedSql);
        Assert.Contains("CONSTRAINT pk_franqueados_usuarios PRIMARY KEY (id)", normalizedSql);
        Assert.Contains("CONSTRAINT pk_franqueados_unidades PRIMARY KEY (id)", normalizedSql);

        Assert.Contains(
            "CONSTRAINT uq_franqueados_organizacao_id_id UNIQUE (organizacao_id, id)",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT fk_perfis_usuario_usuarios_usuario_id FOREIGN KEY (usuario_id) "
            + "REFERENCES usuarios (id) ON DELETE RESTRICT",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT fk_franqueados_organizacoes_organizacao_id "
            + "FOREIGN KEY (organizacao_id) REFERENCES organizacoes (id) ON DELETE RESTRICT",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT fk_franqueados_usuarios_franqueado_id "
            + "FOREIGN KEY (franqueado_id) REFERENCES franqueados (id) ON DELETE RESTRICT",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT fk_franqueados_usuarios_usuario_id "
            + "FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT fk_franqueados_unidades_franqueado "
            + "FOREIGN KEY (organizacao_id, franqueado_id) "
            + "REFERENCES franqueados (organizacao_id, id) ON DELETE RESTRICT",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT fk_franqueados_unidades_organizacao "
            + "FOREIGN KEY (organizacao_id) REFERENCES organizacoes (id) ON DELETE RESTRICT",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT fk_franqueados_unidades_unidade "
            + "FOREIGN KEY (organizacao_id, unidade_id) "
            + "REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Equal(
            7,
            Regex.Matches(sql, "ON DELETE RESTRICT", RegexOptions.CultureInvariant).Count);
        Assert.DoesNotContain("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "CONSTRAINT ck_franqueados_tipo_pessoa_valido "
            + "CHECK (tipo_pessoa IN ('PessoaFisica', 'PessoaJuridica'))",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "(tipo_pessoa = 'PessoaFisica' AND documento ~ '^[0-9]{11}$')",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "(tipo_pessoa = 'PessoaJuridica' AND documento ~ '^[0-9]{14}$')",
            normalizedSql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V004_possui_uniques_indices_historico_e_unico_vinculo_ativo()
    {
        var normalizedSql = NormalizeWhitespace(ReadV004());

        Assert.Contains(
            "CREATE UNIQUE INDEX uq_perfis_usuario_usuario_id "
            + "ON perfis_usuario (usuario_id);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_franqueados_organizacao_id_documento "
            + "ON franqueados (organizacao_id, documento);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_franqueados_usuarios_franqueado_id_usuario_id "
            + "ON franqueados_usuarios (franqueado_id, usuario_id);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_franqueados_usuarios_principal_ativo "
            + "ON franqueados_usuarios (franqueado_id) "
            + "WHERE principal = true AND ativo = true;",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE INDEX ix_franqueados_usuarios_usuario_id "
            + "ON franqueados_usuarios (usuario_id);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE INDEX ix_franqueados_unidades_franqueado_id "
            + "ON franqueados_unidades (franqueado_id);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_franqueados_unidades_franqueado_unidade "
            + "ON franqueados_unidades (organizacao_id, franqueado_id, unidade_id);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ix_franqueados_unidades_organizacao_franqueado",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE INDEX ix_franqueados_unidades_organizacao_unidade_ativo "
            + "ON franqueados_unidades (organizacao_id, unidade_id, ativo);",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_franqueados_unidades_unidade_ativa "
            + "ON franqueados_unidades (organizacao_id, unidade_id) WHERE ativo = true;",
            normalizedSql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V004_e_portavel_limitada_ao_escopo_e_registra_historico()
    {
        var sql = ReadV004();
        var normalizedSql = NormalizeWhitespace(sql);

        Assert.Contains(
            "GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE perfis_usuario, franqueados, "
            + "franqueados_usuarios, franqueados_unidades TO bfa_app_role;",
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
            + "VALUES ('V004', 'criar usuarios e franqueados');",
            normalizedSql,
            StringComparison.Ordinal);

        Assert.DoesNotContain("tipo_usuario", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("identityrole", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contrato", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("royalt", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mensalidade", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("documento_contrato", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V005_altera_somente_a_constraint_do_cnpj_e_registra_historico()
    {
        var sql = ReadV005();
        var normalizedSql = NormalizeWhitespace(sql);

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            2,
            Regex.Matches(
                sql,
                @"\bALTER\s+TABLE\s+franqueados\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count);
        Assert.Contains(
            "ALTER TABLE franqueados DROP CONSTRAINT ck_franqueados_documento_tipo_pessoa;",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "ALTER TABLE franqueados ADD CONSTRAINT ck_franqueados_documento_tipo_pessoa CHECK ( "
            + "(tipo_pessoa = 'PessoaFisica' AND documento ~ '^[0-9]{11}$') OR "
            + "(tipo_pessoa = 'PessoaJuridica' AND documento ~ '^[A-Z0-9]{12}[0-9]{2}$') );",
            normalizedSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "INSERT INTO bfa_schema_history (version, descricao) "
            + "VALUES ('V005', 'adequar cnpj alfanumerico');",
            normalizedSql,
            StringComparison.Ordinal);

        Assert.DoesNotContain("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE INDEX", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GRANT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_dev_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_staging_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_prod_app", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetSqlColumns(string sql, string tableName)
    {
        var tableMatch = Regex.Match(
            sql,
            $@"CREATE TABLE {Regex.Escape(tableName)} \((?<body>.*?)\r?\n\);",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        Assert.True(tableMatch.Success, $"Tabela {tableName} nao encontrada na V004.");

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

    private static string ReadV004()
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
            "V004__criar_usuarios_e_franqueados.sql"));
    }

    private static string ReadV005()
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
            "V005__adequar_cnpj_alfanumerico.sql"));
    }
}

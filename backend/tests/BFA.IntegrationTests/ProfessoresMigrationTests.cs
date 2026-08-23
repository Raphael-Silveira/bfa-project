using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class ProfessoresMigrationTests
{
    [Fact]
    public void V008_cria_somente_as_tres_tabelas_com_colunas_e_tipos_exatos()
    {
        var sql = ReadV008();

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            ["professores", "professores_unidades", "professores_remuneracoes"],
            Regex.Matches(
                    sql,
                    @"CREATE TABLE (?<table>[a-z0-9_]+) \(",
                    RegexOptions.CultureInvariant)
                .Select(match => match.Groups["table"].Value));

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "usuario_id uuid NULL",
                "nome_completo varchar(150) NOT NULL",
                "cpf varchar(11) NULL",
                "telefone varchar(30) NULL",
                "email varchar(256) NULL",
                "ativo boolean NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "professores"));
        Assert.Equal(
            [
                "id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "professor_id uuid NOT NULL",
                "unidade_id uuid NOT NULL",
                "ativo boolean NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "professores_unidades"));
        Assert.Equal(
            [
                "id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "professor_unidade_id uuid NOT NULL",
                "modalidade varchar(30) NOT NULL",
                "valor numeric(12,2) NOT NULL",
                "vigencia_inicio date NOT NULL",
                "vigencia_fim date NULL",
                "observacao varchar(1000) NULL",
                "criado_por_usuario_id uuid NOT NULL",
                "criado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "professores_remuneracoes"));

        Assert.DoesNotContain("atualizado_em_utc", string.Join(
            ' ',
            GetSqlColumns(sql, "professores_remuneracoes")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void V008_possui_constraints_de_professor_e_vinculo_multi_tenant()
    {
        var sql = NormalizeWhitespace(ReadV008());

        Assert.Contains(
            "CONSTRAINT uq_professores_organizacao_id_id UNIQUE (organizacao_id, id)",
            sql,
            StringComparison.Ordinal);
        Assert.Contains("CHECK (btrim(nome_completo) <> '')", sql, StringComparison.Ordinal);
        Assert.Contains("CHECK (cpf IS NULL OR cpf ~ '^[0-9]{11}$')", sql, StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_professores_organizacao_cpf "
            + "ON professores (organizacao_id, cpf) WHERE cpf IS NOT NULL;",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_professores_organizacao_usuario "
            + "ON professores (organizacao_id, usuario_id) WHERE usuario_id IS NOT NULL;",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT uq_professores_unidades_organizacao_id_id "
            + "UNIQUE (organizacao_id, id)",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (organizacao_id, professor_id) "
            + "REFERENCES professores (organizacao_id, id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (organizacao_id, unidade_id) "
            + "REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_professores_unidades_professor_unidade "
            + "ON professores_unidades (organizacao_id, professor_id, unidade_id);",
            sql,
            StringComparison.Ordinal);
        Assert.Contains("ix_professores_unidades_organizacao_unidade_ativo", sql);
        Assert.Contains("ix_professores_unidades_organizacao_professor_ativo", sql);
    }

    [Fact]
    public void V008_protege_tenant_modalidades_valor_e_vigencia_da_remuneracao()
    {
        var sql = NormalizeWhitespace(ReadV008());

        Assert.Contains(
            "FOREIGN KEY (organizacao_id, professor_unidade_id) "
            + "REFERENCES professores_unidades (organizacao_id, id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (criado_por_usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "modalidade IN ('Mensal', 'PorAula', 'PorHora')",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Percentual", sql, StringComparison.Ordinal);
        Assert.Contains("CHECK (valor >= 0)", sql, StringComparison.Ordinal);
        Assert.Contains(
            "CHECK (vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio)",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_professores_remuneracoes_aberta "
            + "ON professores_remuneracoes (professor_unidade_id) "
            + "WHERE vigencia_fim IS NULL;",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_professores_remuneracoes_vigencia_inicio "
            + "ON professores_remuneracoes "
            + "(organizacao_id, professor_unidade_id, vigencia_inicio);",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V008_impede_inativar_vinculo_com_remuneracao_aberta_sem_alterar_historico()
    {
        var sql = NormalizeWhitespace(ReadV008());

        Assert.Contains(
            "CREATE TRIGGER trg_proteger_estado_professor_unidade "
            + "BEFORE INSERT OR UPDATE ON professores_unidades FOR EACH ROW "
            + "EXECUTE FUNCTION proteger_estado_professor_unidade();",
            sql,
            StringComparison.Ordinal);
        Assert.Contains("IF TG_OP = 'UPDATE' THEN", sql, StringComparison.Ordinal);
        Assert.Contains(
            "IF OLD.ativo = true AND NEW.ativo = false",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FROM professores_remuneracoes "
            + "WHERE organizacao_id = OLD.organizacao_id "
            + "AND professor_unidade_id = OLD.id AND vigencia_fim IS NULL",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "O vinculo profissional nao pode ser inativado enquanto possuir remuneracao aberta.",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "UPDATE professores_remuneracoes",
            sql,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V008_protege_identidade_historica_do_professor_e_mantem_cadastro_editavel()
    {
        var sql = NormalizeWhitespace(ReadV008());
        var function = Regex.Match(
            sql,
            @"CREATE FUNCTION proteger_inativacao_professor\(\).*?\$\$;",
            RegexOptions.CultureInvariant);

        Assert.True(function.Success);

        foreach (var field in new[] { "id", "organizacao_id", "criado_em_utc" })
        {
            Assert.Contains(
                $"NEW.{field} IS DISTINCT FROM OLD.{field}",
                function.Value,
                StringComparison.Ordinal);
        }

        foreach (var editableField in new[]
        {
            "usuario_id",
            "nome_completo",
            "cpf",
            "telefone",
            "email",
            "atualizado_em_utc"
        })
        {
            Assert.DoesNotContain(
                $"NEW.{editableField} IS DISTINCT FROM OLD.{editableField}",
                function.Value,
                StringComparison.Ordinal);
        }

        Assert.Contains("USING ERRCODE = '23514'", function.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void V008_protege_identidade_historica_do_vinculo_profissional()
    {
        var sql = NormalizeWhitespace(ReadV008());
        var function = Regex.Match(
            sql,
            @"CREATE FUNCTION proteger_estado_professor_unidade\(\).*?\$\$;",
            RegexOptions.CultureInvariant);

        Assert.True(function.Success);

        foreach (var field in new[]
        {
            "id",
            "organizacao_id",
            "professor_id",
            "unidade_id",
            "criado_em_utc"
        })
        {
            Assert.Contains(
                $"NEW.{field} IS DISTINCT FROM OLD.{field}",
                function.Value,
                StringComparison.Ordinal);
        }

        Assert.DoesNotContain(
            "NEW.ativo IS DISTINCT FROM OLD.ativo",
            function.Value,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "NEW.atualizado_em_utc IS DISTINCT FROM OLD.atualizado_em_utc",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains("USING ERRCODE = '23514'", function.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void V008_permite_inativar_vinculo_apos_encerrar_remuneracao()
    {
        var sql = NormalizeWhitespace(ReadV008());
        var function = Regex.Match(
            sql,
            @"CREATE FUNCTION proteger_estado_professor_unidade\(\).*?\$\$;",
            RegexOptions.CultureInvariant);

        Assert.True(function.Success);
        Assert.Contains("AND vigencia_fim IS NULL", function.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "vigencia_fim IS NOT NULL",
            function.Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V008_impede_inativar_professor_com_vinculo_ativo_sem_inativacao_automatica()
    {
        var sql = NormalizeWhitespace(ReadV008());

        Assert.Contains(
            "CREATE TRIGGER trg_proteger_inativacao_professor BEFORE UPDATE "
            + "ON professores FOR EACH ROW "
            + "EXECUTE FUNCTION proteger_inativacao_professor();",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "OLD.ativo = true AND NEW.ativo = false AND EXISTS",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FROM professores_unidades "
            + "WHERE organizacao_id = OLD.organizacao_id "
            + "AND professor_id = OLD.id AND ativo = true",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "O professor nao pode ser inativado enquanto possuir vinculo profissional ativo.",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "UPDATE professores_unidades",
            sql,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V008_permite_inativar_professor_sem_vinculo_profissional_ativo()
    {
        var sql = NormalizeWhitespace(ReadV008());
        var function = Regex.Match(
            sql,
            @"CREATE FUNCTION proteger_inativacao_professor\(\).*?\$\$;",
            RegexOptions.CultureInvariant);

        Assert.True(function.Success);
        Assert.Contains("AND ativo = true", function.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AND professor_id = OLD.id AND ativo = false",
            function.Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V008_reativacao_exige_pais_ativos_e_nao_cria_remuneracao_automaticamente()
    {
        var sql = NormalizeWhitespace(ReadV008());

        Assert.Contains(
            "SELECT ativo INTO professor_ativo FROM professores "
            + "WHERE organizacao_id = NEW.organizacao_id "
            + "AND id = NEW.professor_id FOR UPDATE;",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "IF NEW.ativo = true AND professor_ativo = false THEN",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "IF NEW.vigencia_fim IS NULL AND EXISTS ( "
            + "SELECT 1 FROM professores_unidades "
            + "WHERE organizacao_id = NEW.organizacao_id "
            + "AND id = NEW.professor_unidade_id AND ativo = false ) THEN",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "INSERT INTO professores_remuneracoes",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_professores_unidades_professor_unidade "
            + "ON professores_unidades (organizacao_id, professor_id, unidade_id);",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V008_torna_termos_historicos_imutaveis_e_permite_encerrar_uma_vez()
    {
        var sql = NormalizeWhitespace(ReadV008());

        foreach (var field in new[]
        {
            "id",
            "organizacao_id",
            "professor_unidade_id",
            "modalidade",
            "valor",
            "vigencia_inicio",
            "observacao",
            "criado_por_usuario_id",
            "criado_em_utc"
        })
        {
            Assert.Contains(
                $"NEW.{field} IS DISTINCT FROM OLD.{field}",
                sql,
                StringComparison.Ordinal);
        }

        Assert.Contains(
            "OLD.vigencia_fim IS NOT NULL "
            + "AND NEW.vigencia_fim IS DISTINCT FROM OLD.vigencia_fim",
            sql,
            StringComparison.Ordinal);
        Assert.Contains("USING ERRCODE = '23514'", sql, StringComparison.Ordinal);
        Assert.Contains(
            "CREATE TRIGGER trg_proteger_remuneracao_professor BEFORE INSERT OR UPDATE "
            + "ON professores_remuneracoes FOR EACH ROW "
            + "EXECUTE FUNCTION proteger_remuneracao_professor();",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V008_serializa_por_vinculo_e_rejeita_periodos_sobrepostos()
    {
        var sql = NormalizeWhitespace(ReadV008());

        Assert.Contains(
            "FROM professores_unidades "
            + "WHERE organizacao_id = NEW.organizacao_id "
            + "AND id = NEW.professor_unidade_id FOR UPDATE;",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "existente.organizacao_id = NEW.organizacao_id",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "existente.professor_unidade_id = NEW.professor_unidade_id",
            sql,
            StringComparison.Ordinal);
        Assert.Contains("existente.id <> NEW.id", sql, StringComparison.Ordinal);
        Assert.Contains(
            "daterange( existente.vigencia_inicio, existente.vigencia_fim, '[]') "
            + "&& daterange(NEW.vigencia_inicio, NEW.vigencia_fim, '[]')",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE EXTENSION", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V008_usa_delete_restritivo_e_grants_sem_delete()
    {
        var sql = ReadV008();
        var normalizedSql = NormalizeWhitespace(sql);

        Assert.Equal(8, Regex.Matches(sql, "ON DELETE RESTRICT").Count);
        Assert.DoesNotContain("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);

        var grants = Regex.Matches(
                normalizedSql,
                @"GRANT .*? TO bfa_app_role;",
                RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .ToArray();

        Assert.Equal(
            [
                "GRANT SELECT, INSERT, UPDATE ON TABLE professores TO bfa_app_role;",
                "GRANT SELECT, INSERT, UPDATE ON TABLE professores_unidades TO bfa_app_role;",
                "GRANT SELECT, INSERT, UPDATE ON TABLE professores_remuneracoes TO bfa_app_role;"
            ],
            grants);
        Assert.DoesNotContain(
            grants,
            grant => grant.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("bfa_dev_app", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("bfa_staging_app", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("bfa_prod_app", sql, StringComparison.Ordinal);
        Assert.Contains(
            "INSERT INTO bfa_schema_history (version, descricao) "
            + "VALUES ('V008', 'criar professores e historico de remuneracoes');",
            normalizedSql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V001_a_v007_permanecem_exatamente_inalteradas()
    {
        var expectedHashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["V001__criar_organizacoes_e_unidades.sql"] =
                "8D458CEFD177E176D4FFB1C3D6D07AB2FFE2C6BB0C6FA97E568470DB54D697B3",
            ["V002__criar_identidade.sql"] =
                "3819E7472B1E75B711EBA36900BE816B7F8B527354DB7BECD0FB11685A8D4B15",
            ["V003__criar_vinculos_acesso.sql"] =
                "4B347730B498F0A449CB8EE57BA1752A6350E9C884F485223858F51B1D5CACF9",
            ["V004__criar_usuarios_e_franqueados.sql"] =
                "AA42F834A90BA7777F27D1AB87E208C66DC6D205EA2AB3AACD888A4F4ED3AFA7",
            ["V005__adequar_cnpj_alfanumerico.sql"] =
                "38585E7CBA436756115DD612291B9919C1F9B18E525D65F9CD53E575124707E4",
            ["V006__criar_catalogo_localidades.sql"] =
                "26DAD5CCDFC483910F91F9B0D125BB2A48FA2C9F8EBBD547234C535F28E2B31B",
            ["V007__criar_contratos_franquia.sql"] =
                "33C0F893515897D4C7CC198C26515BEC9EF9370D589E86103B8B1946A7B56B46"
        };

        foreach (var (fileName, expectedHash) in expectedHashes)
        {
            var content = File.ReadAllText(Path.Combine(GetMigrationsDirectory(), fileName))
                .Replace("\r\n", "\n", StringComparison.Ordinal);
            var actualHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(content)));

            Assert.Equal(expectedHash, actualHash);
        }
    }

    private static string[] GetSqlColumns(string sql, string tableName)
    {
        var tableMatch = Regex.Match(
            sql,
            $@"CREATE TABLE {Regex.Escape(tableName)} \((?<body>.*?)\r?\n\);",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        Assert.True(tableMatch.Success, $"Tabela {tableName} nao encontrada na V008.");

        return tableMatch.Groups["body"].Value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimEnd(','))
            .TakeWhile(line => !line.StartsWith("CONSTRAINT ", StringComparison.Ordinal))
            .ToArray();
    }

    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

    private static string ReadV008() => File.ReadAllText(Path.Combine(
        GetMigrationsDirectory(),
        "V008__criar_professores_e_remuneracoes.sql"));

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

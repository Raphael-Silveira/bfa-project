using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class PlanosMigrationTests
{
    [Fact]
    public void V010_cria_somente_planos_e_versoes_com_colunas_exatas()
    {
        var sql = ReadV010();

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE EXTENSION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            ["planos", "planos_versoes"],
            Regex.Matches(sql, @"CREATE TABLE (?<table>[a-z0-9_]+) \(")
                .Select(match => match.Groups["table"].Value));

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "unidade_id uuid NULL",
                "nome varchar(150) NOT NULL",
                "ativo boolean NOT NULL",
                "criado_por_usuario_id uuid NOT NULL",
                "atualizado_por_usuario_id uuid NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "planos"));
        Assert.Equal(
            [
                "id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "plano_id uuid NOT NULL",
                "numero_versao integer NOT NULL",
                "duracao_meses smallint NOT NULL",
                "frequencia_semanal smallint NOT NULL",
                "valor_mensal numeric(12,2) NOT NULL",
                "cobra_matricula boolean NOT NULL",
                "valor_matricula numeric(12,2) NULL",
                "vigencia_inicio date NOT NULL",
                "vigencia_fim date NULL",
                "criado_por_usuario_id uuid NOT NULL",
                "criado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "planos_versoes"));
    }

    [Fact]
    public void V010_protege_escopo_rede_unidade_e_tenant_com_fks_compostas()
    {
        var sql = NormalizeWhitespace(ReadV010());

        Assert.Contains(
            "CONSTRAINT uq_planos_organizacao_id_id UNIQUE (organizacao_id, id)",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (organizacao_id, unidade_id) "
            + "REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (organizacao_id, plano_id) "
            + "REFERENCES planos (organizacao_id, id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT uq_planos_versoes_organizacao_id_id "
            + "UNIQUE (organizacao_id, id)",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V010_valida_nome_duracao_frequencia_valores_matricula_e_vigencia()
    {
        var sql = NormalizeWhitespace(ReadV010());

        foreach (var check in new[]
        {
            "CHECK (btrim(nome) <> '')",
            "CHECK (numero_versao > 0)",
            "CHECK (duracao_meses > 0)",
            "CHECK (frequencia_semanal BETWEEN 1 AND 7)",
            "CHECK (valor_mensal > 0)",
            "( cobra_matricula = true AND valor_matricula IS NOT NULL "
                + "AND valor_matricula > 0 ) "
                + "OR (cobra_matricula = false AND valor_matricula IS NULL)",
            "CHECK (vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio)"
        })
        {
            Assert.Contains(check, sql, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("UNIQUE (nome", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V010_garante_numero_unico_e_apenas_uma_versao_aberta()
    {
        var sql = NormalizeWhitespace(ReadV010());

        Assert.Contains(
            "CREATE UNIQUE INDEX uq_planos_versoes_plano_numero "
            + "ON planos_versoes (plano_id, numero_versao);",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_planos_versoes_aberta "
            + "ON planos_versoes (plano_id) WHERE vigencia_fim IS NULL;",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V010_serializa_por_plano_e_rejeita_sobreposicao_inclusiva()
    {
        var function = Regex.Match(
            NormalizeWhitespace(ReadV010()),
            @"CREATE FUNCTION proteger_plano_versao\(\).*?\$\$;",
            RegexOptions.CultureInvariant);

        Assert.True(function.Success);
        Assert.Contains(
            "FROM planos WHERE organizacao_id = NEW.organizacao_id "
            + "AND id = NEW.plano_id FOR UPDATE;",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "daterange( existente.vigencia_inicio, existente.vigencia_fim, '[]') "
            + "&& daterange(NEW.vigencia_inicio, NEW.vigencia_fim, '[]')",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains("existente.id <> NEW.id", function.Value, StringComparison.Ordinal);
        Assert.Contains(
            "As vigencias das versoes comerciais do plano nao podem se sobrepor.",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains("USING ERRCODE = '23514'", function.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void V010_permite_periodos_adjacentes_por_usar_datas_inclusivas_sem_sobreposicao()
    {
        var sql = NormalizeWhitespace(ReadV010());

        Assert.Contains("daterange(", sql, StringComparison.Ordinal);
        Assert.Contains("'[]'", sql, StringComparison.Ordinal);
        Assert.Contains(
            "vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "vigencia_inicio <= existente.vigencia_fim + 1",
            sql,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V010_preserva_identidade_e_escopo_do_plano()
    {
        var function = Regex.Match(
            NormalizeWhitespace(ReadV010()),
            @"CREATE FUNCTION proteger_plano\(\).*?\$\$;",
            RegexOptions.CultureInvariant);

        Assert.True(function.Success);
        foreach (var field in new[]
        {
            "id",
            "organizacao_id",
            "unidade_id",
            "nome",
            "criado_por_usuario_id",
            "criado_em_utc"
        })
        {
            Assert.Contains(
                $"NEW.{field} IS DISTINCT FROM OLD.{field}",
                function.Value,
                StringComparison.Ordinal);
        }

        Assert.DoesNotContain("NEW.ativo IS DISTINCT", function.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "NEW.atualizado_por_usuario_id IS DISTINCT",
            function.Value,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "NEW.atualizado_em_utc IS DISTINCT",
            function.Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V010_preserva_termos_historicos_e_encerra_vigencia_uma_vez()
    {
        var function = Regex.Match(
            NormalizeWhitespace(ReadV010()),
            @"CREATE FUNCTION proteger_plano_versao\(\).*?\$\$;",
            RegexOptions.CultureInvariant);

        Assert.True(function.Success);
        foreach (var field in new[]
        {
            "id",
            "organizacao_id",
            "plano_id",
            "numero_versao",
            "duracao_meses",
            "frequencia_semanal",
            "valor_mensal",
            "cobra_matricula",
            "valor_matricula",
            "vigencia_inicio",
            "criado_por_usuario_id",
            "criado_em_utc"
        })
        {
            Assert.Contains(
                $"NEW.{field} IS DISTINCT FROM OLD.{field}",
                function.Value,
                StringComparison.Ordinal);
        }

        Assert.Contains(
            "OLD.vigencia_fim IS NOT NULL "
            + "AND NEW.vigencia_fim IS DISTINCT FROM OLD.vigencia_fim",
            function.Value,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "NEW.vigencia_fim IS DISTINCT FROM OLD.vigencia_fim "
            + "OR OLD.vigencia_fim IS NULL",
            function.Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V010_cria_indices_de_consulta_e_auditoria()
    {
        var sql = ReadV010();

        foreach (var index in new[]
        {
            "ix_planos_organizacao_unidade_ativo",
            "ix_planos_criado_por_usuario_id",
            "ix_planos_atualizado_por_usuario_id",
            "ix_planos_versoes_organizacao_plano_vigencia",
            "ix_planos_versoes_criado_por_usuario_id"
        })
        {
            Assert.Contains(index, sql, StringComparison.Ordinal);
        }

        Assert.Equal(3, Regex.Matches(sql, @"REFERENCES usuarios \(id\)").Count);
    }

    [Fact]
    public void V010_concede_somente_dml_necessario_sem_delete_ou_usuario_de_ambiente()
    {
        var sql = NormalizeWhitespace(ReadV010());
        var grants = Regex.Matches(sql, @"GRANT .*? TO bfa_app_role;")
            .Select(match => match.Value)
            .ToArray();

        Assert.Equal(
            [
                "GRANT SELECT, INSERT, UPDATE ON TABLE planos TO bfa_app_role;",
                "GRANT SELECT, INSERT, UPDATE ON TABLE planos_versoes TO bfa_app_role;"
            ],
            grants);
        Assert.DoesNotContain(grants, grant =>
            grant.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("bfa_dev_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_staging_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_prod_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "INSERT INTO bfa_schema_history (version, descricao) "
            + "VALUES ('V010', 'criar planos e versoes comerciais');",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V010_nao_mistura_planos_com_operacao_academica_ou_financeira()
    {
        var sql = ReadV010();

        foreach (var term in new[]
        {
            "turma_id",
            "professor_id",
            "aluno_id",
            "matricula_id",
            "cobranca",
            "pagamento",
            "gateway",
            "split",
            "parcela",
            "desconto",
            "cupom"
        })
        {
            Assert.DoesNotContain(term, sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void V001_a_v009_permanecem_exatamente_inalteradas()
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
                "33C0F893515897D4C7CC198C26515BEC9EF9370D589E86103B8B1946A7B56B46",
            ["V008__criar_professores_e_remuneracoes.sql"] =
                "F023208895448525133F952D047B17EDDFF5107BC5AA32B44AE3C100033347AA",
            ["V009__criar_turmas_e_horarios.sql"] =
                "DE1F630CB97CC0C9D8B1A5C04A0C376714DC2B6BAE03743529E74D1C6E6B751F"
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
        Assert.True(tableMatch.Success, $"Tabela {tableName} nao encontrada na V010.");

        return tableMatch.Groups["body"].Value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimEnd(','))
            .TakeWhile(line => !line.StartsWith("CONSTRAINT ", StringComparison.Ordinal))
            .ToArray();
    }

    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

    private static string ReadV010() => File.ReadAllText(Path.Combine(
        GetMigrationsDirectory(),
        "V010__criar_planos.sql"));

    private static string GetMigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "database")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "database", "migrations");
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class TurmasMigrationTests
{
    [Fact]
    public void V009_cria_somente_turmas_e_horarios_com_colunas_exatas()
    {
        var sql = ReadV009();

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE EXTENSION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            ["turmas", "turmas_horarios"],
            Regex.Matches(sql, @"CREATE TABLE (?<table>[a-z0-9_]+) \(")
                .Select(match => match.Groups["table"].Value));

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "unidade_id uuid NOT NULL",
                "professor_unidade_id uuid NOT NULL",
                "nome varchar(150) NOT NULL",
                "capacidade integer NOT NULL",
                "ativo boolean NOT NULL",
                "criado_por_usuario_id uuid NOT NULL",
                "atualizado_por_usuario_id uuid NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "turmas"));
        Assert.Equal(
            [
                "id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "unidade_id uuid NOT NULL",
                "turma_id uuid NOT NULL",
                "professor_unidade_id uuid NOT NULL",
                "dia_semana smallint NOT NULL",
                "hora_inicio time without time zone NOT NULL",
                "hora_fim time without time zone NOT NULL",
                "vigencia_inicio date NOT NULL",
                "vigencia_fim date NULL",
                "ativo boolean NOT NULL",
                "criado_por_usuario_id uuid NOT NULL",
                "atualizado_por_usuario_id uuid NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "turmas_horarios"));
    }

    [Fact]
    public void V009_adiciona_somente_chave_candidata_autorizada_em_professores_unidades()
    {
        var sql = NormalizeWhitespace(ReadV009());

        Assert.Contains(
            "ALTER TABLE professores_unidades "
            + "ADD CONSTRAINT uq_professores_unidades_organizacao_unidade_id "
            + "UNIQUE (organizacao_id, unidade_id, id);",
            sql,
            StringComparison.Ordinal);
        Assert.Single(Regex.Matches(sql, @"ALTER TABLE professores_unidades").Cast<Match>());
        Assert.DoesNotContain("ALTER TABLE professores ", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE professores_remuneracoes", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void V009_protege_tenant_com_fks_compostas_e_delete_restritivo()
    {
        var sql = NormalizeWhitespace(ReadV009());

        Assert.Contains(
            "FOREIGN KEY (organizacao_id, unidade_id, professor_unidade_id) "
            + "REFERENCES professores_unidades (organizacao_id, unidade_id, id) "
            + "ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (organizacao_id, unidade_id, turma_id) "
            + "REFERENCES turmas (organizacao_id, unidade_id, id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT fk_turmas_horarios_professor_unidade "
            + "FOREIGN KEY (organizacao_id, unidade_id, professor_unidade_id) "
            + "REFERENCES professores_unidades (organizacao_id, unidade_id, id) "
            + "ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT uq_turmas_organizacao_unidade_id "
            + "UNIQUE (organizacao_id, unidade_id, id)",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V009_valida_nome_capacidade_dia_hora_e_vigencia()
    {
        var sql = NormalizeWhitespace(ReadV009());

        Assert.Contains("CHECK (btrim(nome) <> '')", sql, StringComparison.Ordinal);
        Assert.Contains("CHECK (capacidade > 0)", sql, StringComparison.Ordinal);
        Assert.Contains("CHECK (dia_semana BETWEEN 1 AND 7)", sql, StringComparison.Ordinal);
        Assert.Contains("CHECK (hora_inicio < hora_fim)", sql, StringComparison.Ordinal);
        Assert.Contains(
            "CHECK (vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio)",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V009_exige_vinculo_e_turma_ativos_sem_alteracao_automatica()
    {
        var sql = NormalizeWhitespace(ReadV009());

        Assert.Contains(
            "IF NEW.ativo = true AND vinculo_ativo IS DISTINCT FROM true THEN",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "Uma turma ativa exige um vinculo profissional ativo na mesma unidade.",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "Um horario recorrente ativo exige uma turma ativa.",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "professor_unidade_id_turma IS DISTINCT FROM p_professor_unidade_id",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "Um horario recorrente ativo deve registrar o professor responsavel atual da turma.",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "Um horario recorrente ativo exige um vinculo profissional ativo.",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "O vinculo profissional nao pode ser inativado enquanto for responsavel "
            + "por turma ativa.",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "A turma nao pode ser inativada enquanto possuir horario recorrente ativo.",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE turmas_horarios", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE turmas SET", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V009_protege_conflito_do_mesmo_professor_na_mesma_ou_em_outra_unidade()
    {
        var sql = NormalizeWhitespace(ReadV009());

        Assert.Contains(
            "vinculo_existente.professor_id = professor_id_atual",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "vinculo_existente.id = existente.professor_unidade_id",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "vinculo_existente.id = turma_existente.professor_unidade_id",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "vinculo_existente.unidade_id = p_unidade_id",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "p_hora_inicio < existente.hora_fim "
            + "AND existente.hora_inicio < p_hora_fim",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "daterange( existente.vigencia_inicio, existente.vigencia_fim, '[]') "
            + "&& daterange(p_vigencia_inicio, p_vigencia_fim, '[]')",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "O professor responsavel possui horario recorrente conflitante.",
            sql,
            StringComparison.Ordinal);
        Assert.Contains("USING ERRCODE = '23514'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void V009_permite_horarios_adjacentes_e_vigencias_nao_sobrepostas()
    {
        var sql = NormalizeWhitespace(ReadV009());

        Assert.Contains("p_hora_inicio < existente.hora_fim", sql, StringComparison.Ordinal);
        Assert.Contains("existente.hora_inicio < p_hora_fim", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("p_hora_inicio <= existente.hora_fim", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("existente.hora_inicio <= p_hora_fim", sql, StringComparison.Ordinal);
        Assert.Contains("daterange(", sql, StringComparison.Ordinal);
        Assert.Contains("&& daterange(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void V009_serializa_concorrencia_pela_linha_global_do_professor()
    {
        var sql = NormalizeWhitespace(ReadV009());
        var function = Regex.Match(
            sql,
            @"CREATE FUNCTION validar_conflito_horario_professor\(.*?\$\$;",
            RegexOptions.CultureInvariant);

        Assert.True(function.Success);
        Assert.Contains(
            "FROM professores WHERE organizacao_id = p_organizacao_id "
            + "AND id = professor_id_atual FOR UPDATE;",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains("vinculo_existente.professor_id", function.Value, StringComparison.Ordinal);
        Assert.Contains(
            "id = p_professor_unidade_id FOR UPDATE;",
            function.Value,
            StringComparison.Ordinal);
        Assert.DoesNotContain("LOCK TABLE", function.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V009_preserva_identidade_historica_e_permite_encerrar_vigencia_uma_vez()
    {
        var sql = NormalizeWhitespace(ReadV009());
        var function = Regex.Match(
            sql,
            @"CREATE FUNCTION proteger_turma_horario\(\).*?\$\$;",
            RegexOptions.CultureInvariant);

        Assert.True(function.Success);
        foreach (var field in new[]
        {
            "id",
            "organizacao_id",
            "unidade_id",
            "turma_id",
            "professor_unidade_id",
            "dia_semana",
            "hora_inicio",
            "hora_fim",
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
            "NEW.ativo IS DISTINCT FROM OLD.ativo",
            function.Value,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "NEW.atualizado_em_utc IS DISTINCT FROM OLD.atualizado_em_utc",
            function.Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V009_bloqueia_troca_de_professor_enquanto_regra_anterior_esta_aberta()
    {
        var sql = NormalizeWhitespace(ReadV009());

        Assert.Contains(
            "NEW.professor_unidade_id IS DISTINCT FROM OLD.professor_unidade_id",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "professor_unidade_id = OLD.professor_unidade_id "
            + "AND ativo = true AND vigencia_fim IS NULL",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "O professor responsavel nao pode ser trocado enquanto possuir "
            + "horario recorrente ativo e aberto.",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "validar_conflitos_turma_professor",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V009_apos_encerrar_regras_permite_troca_e_nova_regra_do_responsavel_atual()
    {
        var sql = NormalizeWhitespace(ReadV009());

        Assert.Contains(
            "ativo = true AND vigencia_fim IS NULL",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AND vigencia_fim IS NOT NULL",
            Regex.Match(
                sql,
                @"IF NEW\.professor_unidade_id IS DISTINCT.*?END IF;",
                RegexOptions.CultureInvariant).Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "IF professor_unidade_id_turma IS DISTINCT FROM p_professor_unidade_id THEN",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "NEW.professor_unidade_id IS DISTINCT FROM OLD.professor_unidade_id",
            Regex.Match(
                sql,
                @"CREATE FUNCTION proteger_turma_horario\(\).*?\$\$;",
                RegexOptions.CultureInvariant).Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V009_possui_indices_auditoria_grants_sem_delete_e_historico()
    {
        var sql = ReadV009();
        var normalizedSql = NormalizeWhitespace(sql);

        foreach (var index in new[]
        {
            "ix_turmas_organizacao_unidade_ativo",
            "ix_turmas_organizacao_professor_unidade_ativo",
            "ix_turmas_horarios_organizacao_unidade_dia_ativo",
            "ix_turmas_horarios_organizacao_turma_ativo",
            "ix_turmas_horarios_conflito_professor"
        })
        {
            Assert.Contains(index, sql, StringComparison.Ordinal);
        }

        Assert.Equal(4, Regex.Matches(sql, @"REFERENCES usuarios \(id\)").Count);
        var grants = Regex.Matches(normalizedSql, @"GRANT .*? TO bfa_app_role;")
            .Select(match => match.Value)
            .ToArray();
        Assert.Equal(
            [
                "GRANT SELECT, INSERT, UPDATE ON TABLE turmas TO bfa_app_role;",
                "GRANT SELECT, INSERT, UPDATE ON TABLE turmas_horarios TO bfa_app_role;"
            ],
            grants);
        Assert.DoesNotContain(
            grants,
            grant => grant.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            "INSERT INTO bfa_schema_history (version, descricao) "
            + "VALUES ('V009', 'criar turmas e horarios recorrentes');",
            normalizedSql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V001_a_v008_permanecem_exatamente_inalteradas()
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
                "F023208895448525133F952D047B17EDDFF5107BC5AA32B44AE3C100033347AA"
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
        Assert.True(tableMatch.Success, $"Tabela {tableName} nao encontrada na V009.");

        return tableMatch.Groups["body"].Value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimEnd(','))
            .TakeWhile(line => !line.StartsWith("CONSTRAINT ", StringComparison.Ordinal))
            .ToArray();
    }

    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

    private static string ReadV009() => File.ReadAllText(Path.Combine(
        GetMigrationsDirectory(),
        "V009__criar_turmas_e_horarios.sql"));

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

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class AlunosMigrationTests
{
    [Fact]
    public void V011_cria_somente_alunos_responsaveis_e_vinculos_com_colunas_exatas()
    {
        var sql = ReadV011();

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            ["alunos", "responsaveis", "alunos_responsaveis"],
            Regex.Matches(sql, @"CREATE TABLE (?<table>[a-z0-9_]+) \(")
                .Select(match => match.Groups["table"].Value));

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "usuario_id uuid NULL",
                "nome_completo varchar(150) NOT NULL",
                "data_nascimento date NOT NULL",
                "cpf varchar(11) NULL",
                "telefone varchar(30) NULL",
                "email varchar(256) NULL",
                "ativo boolean NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "alunos"));
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
            GetSqlColumns(sql, "responsaveis"));
        Assert.Equal(
            [
                "id uuid NOT NULL",
                "organizacao_id uuid NOT NULL",
                "aluno_id uuid NOT NULL",
                "responsavel_id uuid NOT NULL",
                "tipo_relacao varchar(30) NOT NULL",
                "descricao_relacao varchar(100) NULL",
                "principal_contato boolean NOT NULL",
                "responsavel_financeiro boolean NOT NULL",
                "ativo boolean NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "alunos_responsaveis"));
    }

    [Fact]
    public void V011_nao_coloca_unidade_matricula_ou_grade_no_cadastro_pessoal()
    {
        var sql = ReadV011();

        foreach (var term in new[]
        {
            "unidade_id",
            "matricula_id",
            "plano_versao_id",
            "grade_id",
            "turma_horario_id"
        })
        {
            Assert.DoesNotContain(term, sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void V011_define_unicidade_parcial_de_cpf_e_usuario_por_organizacao()
    {
        var sql = NormalizeWhitespace(ReadV011());

        foreach (var index in new[]
        {
            "CREATE UNIQUE INDEX uq_alunos_organizacao_cpf "
                + "ON alunos (organizacao_id, cpf) WHERE cpf IS NOT NULL;",
            "CREATE UNIQUE INDEX uq_alunos_organizacao_usuario "
                + "ON alunos (organizacao_id, usuario_id) WHERE usuario_id IS NOT NULL;",
            "CREATE UNIQUE INDEX uq_responsaveis_organizacao_cpf "
                + "ON responsaveis (organizacao_id, cpf) WHERE cpf IS NOT NULL;",
            "CREATE UNIQUE INDEX uq_responsaveis_organizacao_usuario "
                + "ON responsaveis (organizacao_id, usuario_id) WHERE usuario_id IS NOT NULL;"
        })
        {
            Assert.Contains(index, sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void V011_valida_dados_pessoais_e_contato_do_responsavel()
    {
        var sql = NormalizeWhitespace(ReadV011());

        Assert.Contains("CHECK (data_nascimento <= CURRENT_DATE)", sql, StringComparison.Ordinal);
        Assert.Contains("cpf IS NULL OR cpf ~ '^[0-9]{11}$'", sql, StringComparison.Ordinal);
        Assert.Contains(
            "CHECK (telefone IS NOT NULL OR email IS NOT NULL)",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "COMMENT ON CONSTRAINT ck_alunos_data_nascimento_nao_futura ON alunos",
            sql,
            StringComparison.Ordinal);
        Assert.Contains("data civil do contexto BFA", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void V011_protege_vinculo_com_fks_compostas_e_unicidade_para_reativacao()
    {
        var sql = NormalizeWhitespace(ReadV011());

        Assert.Contains(
            "FOREIGN KEY (organizacao_id, aluno_id) "
            + "REFERENCES alunos (organizacao_id, id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (organizacao_id, responsavel_id) "
            + "REFERENCES responsaveis (organizacao_id, id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_alunos_responsaveis_aluno_responsavel "
            + "ON alunos_responsaveis (organizacao_id, aluno_id, responsavel_id);",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V011_persiste_tipo_como_string_e_alinha_descricao_de_Outro()
    {
        var sql = NormalizeWhitespace(ReadV011());

        foreach (var value in new[]
        {
            "'Pai'",
            "'Mae'",
            "'ResponsavelLegal'",
            "'Tutor'",
            "'Avo'",
            "'Outro'"
        })
        {
            Assert.Contains(value, sql, StringComparison.Ordinal);
        }

        Assert.Contains(
            "tipo_relacao = 'Outro' AND descricao_relacao IS NOT NULL "
            + "AND btrim(descricao_relacao) <> ''",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "tipo_relacao <> 'Outro' AND descricao_relacao IS NULL",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V011_limita_somente_principal_ativo_e_permite_multiplos_financeiros()
    {
        var sql = NormalizeWhitespace(ReadV011());

        Assert.Contains(
            "CREATE UNIQUE INDEX uq_alunos_responsaveis_principal_ativo "
            + "ON alunos_responsaveis (organizacao_id, aluno_id) "
            + "WHERE principal_contato = true AND ativo = true;",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new Regex(@"CREATE UNIQUE INDEX[^;]+responsavel_financeiro", RegexOptions.IgnoreCase),
            sql);
    }

    [Fact]
    public void V011_cria_indices_operacionais_solicitados()
    {
        var sql = ReadV011();

        foreach (var index in new[]
        {
            "ix_alunos_organizacao_ativo",
            "ix_responsaveis_organizacao_ativo",
            "ix_alunos_responsaveis_organizacao_aluno_ativo",
            "ix_alunos_responsaveis_organizacao_responsavel_ativo"
        })
        {
            Assert.Contains(index, sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void V011_preserva_identidades_e_permite_usuario_mutavel()
    {
        var sql = NormalizeWhitespace(ReadV011());

        AssertIdentityFields(sql, "proteger_aluno", ["id", "organizacao_id", "criado_em_utc"]);
        AssertIdentityFields(
            sql,
            "proteger_responsavel",
            ["id", "organizacao_id", "criado_em_utc"]);
        AssertIdentityFields(
            sql,
            "proteger_aluno_responsavel",
            ["id", "organizacao_id", "aluno_id", "responsavel_id", "criado_em_utc"]);

        var alunoFunction = GetFunction(sql, "proteger_aluno");
        var responsavelFunction = GetFunction(sql, "proteger_responsavel");
        Assert.DoesNotContain("NEW.usuario_id IS DISTINCT", alunoFunction, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "NEW.usuario_id IS DISTINCT",
            responsavelFunction,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V011_bloqueia_inativacao_com_vinculo_e_exige_entidades_ativas()
    {
        var sql = NormalizeWhitespace(ReadV011());

        Assert.Contains(
            "FROM alunos_responsaveis WHERE organizacao_id = OLD.organizacao_id "
            + "AND aluno_id = OLD.id AND ativo = true",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FROM alunos_responsaveis WHERE organizacao_id = OLD.organizacao_id "
            + "AND responsavel_id = OLD.id AND ativo = true",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "NEW.ativo = true AND aluno_ativo IS DISTINCT FROM true",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "NEW.ativo = true AND responsavel_ativo IS DISTINCT FROM true",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V011_concede_somente_dml_sem_delete_ddl_ou_usuarios_de_ambiente()
    {
        var sql = NormalizeWhitespace(ReadV011());
        var grants = Regex.Matches(sql, @"GRANT .*? TO bfa_app_role;")
            .Select(match => match.Value)
            .ToArray();

        Assert.Equal(
            [
                "GRANT SELECT, INSERT, UPDATE ON TABLE alunos TO bfa_app_role;",
                "GRANT SELECT, INSERT, UPDATE ON TABLE responsaveis TO bfa_app_role;",
                "GRANT SELECT, INSERT, UPDATE ON TABLE alunos_responsaveis TO bfa_app_role;"
            ],
            grants);
        Assert.DoesNotContain(grants, grant =>
            grant.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("GRANT CREATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_dev_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_staging_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_prod_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "INSERT INTO bfa_schema_history (version, descricao) "
            + "VALUES ('V011', 'criar alunos, responsaveis e seus vinculos');",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V001_a_v010_permanecem_exatamente_inalteradas()
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
                "DE1F630CB97CC0C9D8B1A5C04A0C376714DC2B6BAE03743529E74D1C6E6B751F",
            ["V010__criar_planos.sql"] =
                "A3E43CF8DF4256A72D924F61EE8E6C7D030C925C0D62DB2BB9A72BAE2AADB5CB"
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

    private static void AssertIdentityFields(
        string sql,
        string functionName,
        string[] fields)
    {
        var function = GetFunction(sql, functionName);

        foreach (var field in fields)
        {
            Assert.Contains(
                $"NEW.{field} IS DISTINCT FROM OLD.{field}",
                function,
                StringComparison.Ordinal);
        }
    }

    private static string GetFunction(string normalizedSql, string functionName)
    {
        var function = Regex.Match(
            normalizedSql,
            $@"CREATE FUNCTION {Regex.Escape(functionName)}\(\).*?\$\$;",
            RegexOptions.CultureInvariant);
        Assert.True(function.Success, $"Funcao {functionName} nao encontrada na V011.");
        return function.Value;
    }

    private static string[] GetSqlColumns(string sql, string tableName)
    {
        var tableMatch = Regex.Match(
            sql,
            $@"CREATE TABLE {Regex.Escape(tableName)} \((?<body>.*?)\r?\n\);",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(tableMatch.Success, $"Tabela {tableName} nao encontrada na V011.");

        return tableMatch.Groups["body"].Value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimEnd(','))
            .TakeWhile(line => !line.StartsWith("CONSTRAINT ", StringComparison.Ordinal))
            .ToArray();
    }

    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

    private static string ReadV011() => File.ReadAllText(Path.Combine(
        GetMigrationsDirectory(),
        "V011__criar_alunos_e_responsaveis.sql"));

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

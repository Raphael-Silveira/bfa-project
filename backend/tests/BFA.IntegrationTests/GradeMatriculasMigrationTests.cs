using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class GradeMatriculasMigrationTests
{
    [Fact]
    public void V013_cria_somente_grade_em_transacao_manual()
    {
        var sql = ReadV013();

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.Equal(["matriculas_horarios"], Regex.Matches(
            sql, @"CREATE TABLE (?<table>[a-z0-9_]+) \(")
            .Select(match => match.Groups["table"].Value));
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VALUES ('V013', 'criar grade das matriculas');", sql);
    }

    [Fact]
    public void Turmas_horarios_recebe_chave_candidata_tenant_safe()
    {
        Assert.Contains(
            "ALTER TABLE turmas_horarios ADD CONSTRAINT "
            + "uq_turmas_horarios_organizacao_unidade_id "
            + "UNIQUE (organizacao_id, unidade_id, id);",
            Normalize(ReadV013()));
    }

    [Fact]
    public void Grade_possui_colunas_exatas_sem_ativo()
    {
        Assert.Equal(
            [
                "id uuid NOT NULL", "organizacao_id uuid NOT NULL",
                "unidade_id uuid NOT NULL", "matricula_id uuid NOT NULL",
                "turma_horario_id uuid NOT NULL", "vigencia_inicio date NOT NULL",
                "vigencia_fim date NULL", "criado_por_usuario_id uuid NOT NULL",
                "atualizado_por_usuario_id uuid NOT NULL", "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetColumns(ReadV013()));
        Assert.DoesNotContain("ativo boolean", TableBody(ReadV013()),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Grade_usa_fks_tenant_safe_e_delete_restritivo()
    {
        var sql = Normalize(ReadV013());
        Assert.Contains(
            "FOREIGN KEY (organizacao_id, unidade_id, matricula_id) "
            + "REFERENCES matriculas (organizacao_id, unidade_id, id) ON DELETE RESTRICT",
            sql);
        Assert.Contains(
            "FOREIGN KEY (organizacao_id, unidade_id, turma_horario_id) "
            + "REFERENCES turmas_horarios (organizacao_id, unidade_id, id) ON DELETE RESTRICT",
            sql);
        Assert.Equal(4, Regex.Matches(TableBody(ReadV013()), "ON DELETE RESTRICT").Count);
    }

    [Fact]
    public void Grade_impede_duplicidade_aberta_e_historica_sem_unique_por_dia()
    {
        var sql = Normalize(ReadV013());
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_matriculas_horarios_aberto ON matriculas_horarios "
            + "(organizacao_id, unidade_id, matricula_id, turma_horario_id) "
            + "WHERE vigencia_fim IS NULL;",
            sql);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_matriculas_horarios_historico ON matriculas_horarios "
            + "(organizacao_id, matricula_id, turma_horario_id, vigencia_inicio);",
            sql);
        Assert.DoesNotContain("dia_semana)", Regex.Match(
            sql, @"CREATE UNIQUE INDEX.*?;", RegexOptions.IgnoreCase).Value,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Grade_preserva_identidade_fechamento_unico_e_historico()
    {
        var function = Function("proteger_matricula_horario");
        foreach (var field in new[]
        {
            "id", "organizacao_id", "unidade_id", "matricula_id", "turma_horario_id",
            "vigencia_inicio", "criado_por_usuario_id", "criado_em_utc"
        })
        {
            Assert.Contains($"NEW.{field} IS DISTINCT FROM OLD.{field}", function);
        }
        Assert.Contains("OLD.vigencia_fim IS NOT NULL", function);
        Assert.Contains("TG_OP = 'DELETE'", function);
        Assert.DoesNotContain("NEW.atualizado_por_usuario_id IS DISTINCT", function);
    }

    [Fact]
    public void Grade_deve_nascer_aberta_sem_bypass_operacional()
    {
        var function = Function("proteger_matricula_horario");

        Assert.Contains("TG_OP = 'INSERT' AND NEW.vigencia_fim IS NOT NULL", function);
        Assert.Contains("deve ser criado aberto", function);
        Assert.Contains("IF NEW.vigencia_fim IS NULL", function);
    }

    [Fact]
    public void Grade_valida_vigencia_da_matricula_e_do_horario()
    {
        var function = Function("proteger_matricula_horario");
        Assert.Contains("NEW.vigencia_inicio < data_inicio_matricula", function);
        Assert.Contains("NEW.vigencia_inicio > data_fim_prevista_matricula", function);
        Assert.Contains("NEW.vigencia_fim > data_fim_prevista_matricula", function);
        Assert.Contains("NEW.vigencia_fim > data_fim_real_matricula", function);
        Assert.Contains("NEW.vigencia_inicio < vigencia_inicio_horario", function);
        Assert.Contains("NEW.vigencia_fim > vigencia_fim_horario", function);
    }

    [Fact]
    public void Grade_aberta_exige_participantes_operacionais_ativos()
    {
        var function = Function("proteger_matricula_horario");
        Assert.Contains("status_matricula <> 'Ativa'", function);
        Assert.Contains("aluno_ativo IS DISTINCT FROM true", function);
        Assert.Contains("turma_ativa IS DISTINCT FROM true", function);
        Assert.Contains("horario_ativo IS DISTINCT FROM true", function);
        Assert.Contains("vigencia_fim_horario IS NOT NULL", function);
    }

    [Fact]
    public void Frequencia_calcula_maximo_nos_pontos_de_inicio_sem_count_historico_ingenuo()
    {
        var function = Function("proteger_matricula_horario");
        Assert.Contains("SELECT frequencia_semanal", function);
        Assert.Contains("SELECT DISTINCT vigencia_inicio AS data_referencia", function);
        Assert.Contains("intervalo.vigencia_inicio <= ponto.data_referencia", function);
        Assert.Contains("intervalo.vigencia_fim >= ponto.data_referencia", function);
        Assert.Contains("maximo_simultaneo > frequencia_semanal_plano", function);
    }

    [Fact]
    public void Conflito_do_aluno_e_global_entre_unidades_e_aceita_adjacencia()
    {
        var function = Function("proteger_matricula_horario");
        Assert.Contains("matricula_existente.aluno_id = aluno_id_matricula", function);
        Assert.DoesNotContain("matricula_existente.unidade_id = NEW.unidade_id", function);
        Assert.Contains("hora_inicio_horario < horario_existente.hora_fim", function);
        Assert.Contains("horario_existente.hora_inicio < hora_fim_horario", function);
        Assert.Contains("COALESCE(NEW.vigencia_fim, 'infinity'::date)", function);
    }

    [Fact]
    public void Capacidade_e_por_horario_e_por_maximo_temporal()
    {
        var function = Function("proteger_matricula_horario");
        Assert.Contains("existente.turma_horario_id = NEW.turma_horario_id", function);
        Assert.Contains("maximo_simultaneo > capacidade_turma", function);
        Assert.DoesNotContain("COUNT(*) FROM matriculas_horarios WHERE vigencia_fim IS NULL", function);
    }

    [Fact]
    public void Grade_serializa_matricula_aluno_e_horario_nessa_ordem()
    {
        var function = Function("proteger_matricula_horario");
        var matricula = function.IndexOf("FROM matriculas WHERE", StringComparison.Ordinal);
        var aluno = function.IndexOf("FROM alunos WHERE", StringComparison.Ordinal);
        var horario = function.IndexOf("FROM turmas_horarios WHERE", StringComparison.Ordinal);

        Assert.True(matricula < aluno && aluno < horario);
        Assert.Equal(3, Regex.Matches(function[..function.IndexOf(
            "SELECT ativo, capacidade", StringComparison.Ordinal)], "FOR UPDATE").Count);
        Assert.Contains("ORDER BY id", Function("proteger_capacidade_turma_grade"));
    }

    [Fact]
    public void Matricula_terminal_exige_grade_fechada()
    {
        var function = Function("proteger_matricula_grade_aberta");
        Assert.Contains("OLD.status = 'Ativa'", function);
        Assert.Contains("NEW.status IN ('Encerrada', 'Cancelada')", function);
        Assert.Contains("vigencia_fim IS NULL", function);
        Assert.Contains("vigencia_fim > NEW.data_fim_real", function);
        Assert.Contains("antes do encerramento de toda a sua Grade", function);
    }

    [Fact]
    public void Horario_com_grade_aberta_nao_pode_encerrar_nem_inativar()
    {
        var function = Function("proteger_turma_horario_grade_aberta");
        Assert.Contains("OLD.vigencia_fim IS NULL", function);
        Assert.Contains("vigencia_fim > NEW.vigencia_fim", function);
        Assert.Contains("OLD.ativo = true", function);
        Assert.Contains("NEW.ativo = false", function);
        Assert.Contains("vigencia_fim IS NULL", function);
        Assert.Contains("vigencia_fim >= CURRENT_DATE", function);
        Assert.Single(Regex.Matches(function, "CURRENT_DATE").Cast<Match>());
    }

    [Fact]
    public void Reducao_de_capacidade_considera_maximo_atual_e_futuro_sem_historico_antigo()
    {
        var function = Function("proteger_capacidade_turma_grade");
        Assert.Contains("NEW.capacidade >= OLD.capacidade", function);
        Assert.Contains("grade.vigencia_fim >= CURRENT_DATE", function);
        Assert.Contains("CURRENT_DATE AS data_referencia", function);
        Assert.Contains("vigencia_inicio > CURRENT_DATE", function);
        Assert.Contains("NEW.capacidade < maximo_simultaneo", function);
    }

    [Fact]
    public void V013_cria_indices_operacionais_e_de_auditoria()
    {
        var sql = ReadV013();
        foreach (var index in new[]
        {
            "ix_matriculas_horarios_organizacao_unidade_matricula",
            "ix_matriculas_horarios_organizacao_unidade_turma_horario",
            "ix_matriculas_horarios_abertos_matricula",
            "ix_matriculas_horarios_abertos_turma_horario",
            "ix_matriculas_horarios_criado_por_usuario_id",
            "ix_matriculas_horarios_atualizado_por_usuario_id"
        })
        {
            Assert.Contains(index, sql);
        }
    }

    [Fact]
    public void Runtime_recebe_dml_sem_delete_ou_ddl()
    {
        var sql = Normalize(ReadV013());
        var grants = Regex.Matches(sql, @"GRANT .*? TO bfa_app_role;")
            .Select(match => match.Value).ToArray();
        Assert.Equal(
            ["GRANT SELECT, INSERT, UPDATE ON TABLE matriculas_horarios TO bfa_app_role;"],
            grants);
        Assert.DoesNotContain("DELETE", grants[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER", grants[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V001_a_V012_permanecem_exatamente_inalteradas()
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["V001__criar_organizacoes_e_unidades.sql"] = "8D458CEFD177E176D4FFB1C3D6D07AB2FFE2C6BB0C6FA97E568470DB54D697B3",
            ["V002__criar_identidade.sql"] = "3819E7472B1E75B711EBA36900BE816B7F8B527354DB7BECD0FB11685A8D4B15",
            ["V003__criar_vinculos_acesso.sql"] = "4B347730B498F0A449CB8EE57BA1752A6350E9C884F485223858F51B1D5CACF9",
            ["V004__criar_usuarios_e_franqueados.sql"] = "AA42F834A90BA7777F27D1AB87E208C66DC6D205EA2AB3AACD888A4F4ED3AFA7",
            ["V005__adequar_cnpj_alfanumerico.sql"] = "38585E7CBA436756115DD612291B9919C1F9B18E525D65F9CD53E575124707E4",
            ["V006__criar_catalogo_localidades.sql"] = "26DAD5CCDFC483910F91F9B0D125BB2A48FA2C9F8EBBD547234C535F28E2B31B",
            ["V007__criar_contratos_franquia.sql"] = "33C0F893515897D4C7CC198C26515BEC9EF9370D589E86103B8B1946A7B56B46",
            ["V008__criar_professores_e_remuneracoes.sql"] = "F023208895448525133F952D047B17EDDFF5107BC5AA32B44AE3C100033347AA",
            ["V009__criar_turmas_e_horarios.sql"] = "DE1F630CB97CC0C9D8B1A5C04A0C376714DC2B6BAE03743529E74D1C6E6B751F",
            ["V010__criar_planos.sql"] = "A3E43CF8DF4256A72D924F61EE8E6C7D030C925C0D62DB2BB9A72BAE2AADB5CB",
            ["V011__criar_alunos_e_responsaveis.sql"] = "E9B19B01AC1DF1D0BC5ECF28988595246AC03C14F61BCD61915508DDFA0CFCC2",
            ["V012__criar_disponibilidades_de_planos_e_matriculas.sql"] = "F673BCE15828B81C6CD6EFE6A7215A523CA3F05FFB4D387DC8190A4A6E233DE5"
        };

        foreach (var (file, expected) in hashes)
        {
            var content = File.ReadAllText(Path.Combine(MigrationDirectory(), file))
                .Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Equal(expected,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
        }
    }

    private static string Function(string name)
    {
        var match = Regex.Match(ReadV013(),
            $@"CREATE FUNCTION {Regex.Escape(name)}\(\).*?\$\$;",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Funcao {name} nao encontrada.");
        return Normalize(match.Value);
    }

    private static string TableBody(string sql)
    {
        var match = Regex.Match(sql,
            @"CREATE TABLE matriculas_horarios \((?<body>.*?)\r?\n\);",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(match.Success);
        return match.Groups["body"].Value;
    }

    private static string[] GetColumns(string sql) => TableBody(sql)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim().TrimEnd(','))
        .TakeWhile(line => !line.StartsWith("CONSTRAINT ", StringComparison.Ordinal))
        .ToArray();

    private static string Normalize(string value) =>
        Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

    private static string ReadV013() => File.ReadAllText(Path.Combine(
        MigrationDirectory(), "V013__criar_grade_das_matriculas.sql"));

    private static string MigrationDirectory()
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

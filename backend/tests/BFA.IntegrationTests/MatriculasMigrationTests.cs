using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class MatriculasMigrationTests
{
    [Fact]
    public void V012_cria_somente_disponibilidades_e_matriculas_em_transacao()
    {
        var sql = ReadV012();

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.Equal(
            ["planos_disponibilidades_unidades", "matriculas"],
            Regex.Matches(sql, @"CREATE TABLE (?<table>[a-z0-9_]+) \(")
                .Select(match => match.Groups["table"].Value));
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE EXTENSION", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Disponibilidade_possui_colunas_exatas()
    {
        Assert.Equal(
            [
                "id uuid NOT NULL", "organizacao_id uuid NOT NULL",
                "plano_id uuid NOT NULL", "unidade_id uuid NOT NULL",
                "ativo boolean NOT NULL", "criado_por_usuario_id uuid NOT NULL",
                "atualizado_por_usuario_id uuid NOT NULL", "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(ReadV012(), "planos_disponibilidades_unidades"));
    }

    [Fact]
    public void Matricula_possui_colunas_exatas_sem_pagador()
    {
        var sql = ReadV012();
        Assert.Equal(
            [
                "id uuid NOT NULL", "organizacao_id uuid NOT NULL", "unidade_id uuid NOT NULL",
                "aluno_id uuid NOT NULL", "plano_versao_id uuid NOT NULL",
                "data_inicio date NOT NULL", "data_fim_prevista date NOT NULL",
                "data_fim_real date NULL", "status varchar(20) NOT NULL",
                "valor_mensal_contratado numeric(12,2) NOT NULL",
                "cobra_taxa_matricula boolean NOT NULL", "valor_taxa_matricula numeric(12,2) NULL",
                "criado_por_usuario_id uuid NOT NULL", "atualizado_por_usuario_id uuid NOT NULL",
                "criado_em_utc timestamptz NOT NULL", "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "matriculas"));
        Assert.DoesNotContain("pagador", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("responsavel_financeiro_id", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Disponibilidade_usa_fks_tenant_safe_restritivas_e_chave_reativavel()
    {
        var sql = NormalizeWhitespace(ReadV012());

        Assert.Contains("UNIQUE (organizacao_id, plano_id, unidade_id)", sql);
        Assert.Contains("FOREIGN KEY (organizacao_id, plano_id) REFERENCES planos (organizacao_id, id) ON DELETE RESTRICT", sql);
        Assert.Contains("FOREIGN KEY (organizacao_id, unidade_id) REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT", sql);
        Assert.DoesNotContain("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WHERE ativo = true", Regex.Match(sql,
            @"CREATE UNIQUE INDEX.*?;", RegexOptions.IgnoreCase).Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Disponibilidade_ativa_exige_plano_de_rede_e_unidade_ativos()
    {
        var function = Function("proteger_plano_disponibilidade_unidade");

        Assert.Contains("plano_unidade_id IS NOT NULL", function);
        Assert.Contains("Somente um plano da rede", function);
        Assert.Contains("IF NEW.ativo = true", function);
        Assert.Contains("plano_ativo IS DISTINCT FROM true", function);
        Assert.Contains("unidade_ativa IS DISTINCT FROM true", function);
        Assert.Contains("FROM planos", function);
        Assert.Contains("FROM unidades", function);
        Assert.DoesNotContain("FOR UPDATE", function, StringComparison.Ordinal);
    }

    [Fact]
    public void Disponibilidade_preserva_identidade_e_permite_alterar_estado_e_auditoria_de_atualizacao()
    {
        var function = Function("proteger_plano_disponibilidade_unidade");
        foreach (var field in new[]
        {
            "id", "organizacao_id", "plano_id", "unidade_id",
            "criado_por_usuario_id", "criado_em_utc"
        })
        {
            Assert.Contains($"NEW.{field} IS DISTINCT FROM OLD.{field}", function);
        }
        Assert.DoesNotContain("NEW.ativo IS DISTINCT FROM OLD.ativo", function);
        Assert.DoesNotContain("NEW.atualizado_por_usuario_id IS DISTINCT", function);
        Assert.DoesNotContain("NEW.atualizado_em_utc IS DISTINCT", function);
    }

    [Fact]
    public void Matricula_usa_fks_tenant_safe_e_chave_candidata_local()
    {
        var sql = NormalizeWhitespace(ReadV012());
        Assert.Contains("UNIQUE (organizacao_id, unidade_id, id)", sql);
        Assert.Contains("FOREIGN KEY (organizacao_id, aluno_id) REFERENCES alunos (organizacao_id, id) ON DELETE RESTRICT", sql);
        Assert.Contains("FOREIGN KEY (organizacao_id, unidade_id) REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT", sql);
        Assert.Contains("FOREIGN KEY (organizacao_id, plano_versao_id) REFERENCES planos_versoes (organizacao_id, id) ON DELETE RESTRICT", sql);
    }

    [Fact]
    public void Matricula_restringe_status_datas_preco_e_taxa_null_safe()
    {
        var sql = NormalizeWhitespace(ReadV012());
        Assert.Contains("status IN ('Ativa', 'Encerrada', 'Cancelada')", sql);
        Assert.DoesNotContain("Agendada", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data_fim_prevista >= data_inicio", sql);
        Assert.Contains("valor_mensal_contratado > 0", sql);
        Assert.Contains("cobra_taxa_matricula = true AND valor_taxa_matricula IS NOT NULL AND valor_taxa_matricula > 0", sql);
        Assert.Contains("cobra_taxa_matricula = false AND valor_taxa_matricula IS NULL", sql);
        Assert.Contains("status = 'Ativa' AND data_fim_real IS NULL", sql);
        Assert.Contains("data_fim_real >= data_inicio", sql);
    }

    [Fact]
    public void Matricula_limita_uma_ativa_por_unidade_mas_nao_entre_unidades()
    {
        var sql = NormalizeWhitespace(ReadV012());
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_matriculas_ativa_organizacao_unidade_aluno "
            + "ON matriculas (organizacao_id, unidade_id, aluno_id) WHERE status = 'Ativa';",
            sql);
        Assert.DoesNotContain(
            "ON matriculas (organizacao_id, aluno_id) WHERE status = 'Ativa'",
            sql);
    }

    [Fact]
    public void Matricula_preserva_snapshot_e_plano_versao()
    {
        var function = Function("proteger_matricula");
        foreach (var field in new[]
        {
            "id", "organizacao_id", "unidade_id", "aluno_id", "plano_versao_id",
            "data_inicio", "data_fim_prevista", "valor_mensal_contratado",
            "cobra_taxa_matricula", "valor_taxa_matricula",
            "criado_por_usuario_id", "criado_em_utc"
        })
        {
            Assert.Contains($"NEW.{field} IS DISTINCT FROM OLD.{field}", function);
        }
        Assert.DoesNotContain("NEW.atualizado_por_usuario_id IS DISTINCT", function);
        Assert.DoesNotContain("NEW.atualizado_em_utc IS DISTINCT", function);
    }

    [Fact]
    public void Matricula_aceita_apenas_transicoes_terminais_e_data_real_uma_vez()
    {
        var function = Function("proteger_matricula");
        Assert.Contains("OLD.status IN ('Encerrada', 'Cancelada')", function);
        Assert.Contains("NEW.status NOT IN ('Encerrada', 'Cancelada')", function);
        Assert.Contains("OLD.data_fim_real IS NOT NULL", function);
        Assert.Contains("Uma matricula em estado terminal nao pode ser alterada", function);
        Assert.Contains("Uma nova matricula deve ser criada ativa", function);
    }

    [Fact]
    public void Matricula_valida_vigencia_inclusiva_e_escopo_de_plano_local_ou_rede()
    {
        var function = Function("proteger_matricula");
        Assert.Contains("NEW.data_inicio < vigencia_inicio_da_versao", function);
        Assert.Contains("NEW.data_inicio > vigencia_fim_da_versao", function);
        Assert.Contains("plano_unidade_id IS NULL", function);
        Assert.Contains("disponibilidade_ativa IS DISTINCT FROM true", function);
        Assert.Contains("plano_unidade_id <> NEW.unidade_id", function);
        Assert.Contains("plano_ativo IS DISTINCT FROM true", function);
        Assert.Contains("unidade_ativa IS DISTINCT FROM true", function);
        Assert.Contains("aluno_ativo IS DISTINCT FROM true", function);
    }

    [Fact]
    public void Matricula_serializa_elegibilidade_na_ordem_comercial_documentada()
    {
        var function = Function("proteger_matricula");
        var versao = function.IndexOf("FROM planos_versoes", StringComparison.Ordinal);
        var plano = function.IndexOf("FROM planos WHERE", StringComparison.Ordinal);
        var disponibilidade = function.IndexOf("FROM planos_disponibilidades_unidades", StringComparison.Ordinal);
        var unidade = function.IndexOf("FROM unidades", StringComparison.Ordinal);
        var aluno = function.IndexOf("FROM alunos", StringComparison.Ordinal);

        Assert.True(versao < plano && plano < disponibilidade && disponibilidade < unidade && unidade < aluno);
        Assert.Equal(5, Regex.Matches(function, "FOR UPDATE").Count);
    }

    [Fact]
    public void Corrida_com_desativacao_da_disponibilidade_revalida_estado_sob_lock()
    {
        var matricula = Function("proteger_matricula");
        var disponibilidade = Function("proteger_plano_disponibilidade_unidade");

        Assert.Contains(
            "FROM planos_disponibilidades_unidades WHERE organizacao_id = NEW.organizacao_id "
            + "AND plano_id = plano_id_da_versao AND unidade_id = NEW.unidade_id FOR UPDATE",
            matricula);
        Assert.Contains("disponibilidade_ativa IS DISTINCT FROM true", matricula);
        Assert.DoesNotContain("FOR UPDATE", disponibilidade, StringComparison.Ordinal);
    }

    [Fact]
    public void Encerramento_da_versao_preserva_inicio_de_todas_as_matriculas_historicas()
    {
        var function = Function("proteger_plano_versao_matriculas");
        var sql = ReadV012();

        Assert.Contains("OLD.vigencia_fim IS NULL", function);
        Assert.Contains("NEW.vigencia_fim IS NOT NULL", function);
        Assert.Contains("FROM matriculas", function);
        Assert.Contains("organizacao_id = NEW.organizacao_id", function);
        Assert.Contains("plano_versao_id = NEW.id", function);
        Assert.Contains("data_inicio > NEW.vigencia_fim", function);
        Assert.DoesNotContain("status =", function, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("USING ERRCODE = '23514'", function);
        Assert.Contains(
            "CREATE TRIGGER trg_proteger_plano_versao_matriculas",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Corrida_com_inativacao_do_plano_revalida_estado_sob_lock()
    {
        var function = Function("proteger_matricula");

        Assert.Contains(
            "FROM planos WHERE organizacao_id = NEW.organizacao_id "
            + "AND id = plano_id_da_versao FOR UPDATE",
            function);
        Assert.Contains("plano_ativo IS DISTINCT FROM true", function);
    }

    [Fact]
    public void Concorrencia_de_duas_matriculas_ativas_e_serializada_por_indice_unico()
    {
        var sql = NormalizeWhitespace(ReadV012());

        Assert.Contains(
            "CREATE UNIQUE INDEX uq_matriculas_ativa_organizacao_unidade_aluno "
            + "ON matriculas (organizacao_id, unidade_id, aluno_id) WHERE status = 'Ativa';",
            sql);
    }

    [Fact]
    public void V012_bloqueia_inativacao_de_aluno_com_matricula_ativa_sem_alterar_V011()
    {
        var function = Function("proteger_aluno_matriculas");
        Assert.Contains("OLD.ativo = true", function);
        Assert.Contains("NEW.ativo = false", function);
        Assert.Contains("FROM matriculas", function);
        Assert.Contains("status = 'Ativa'", function);
        Assert.Contains("trg_proteger_aluno_matriculas", ReadV012());
    }

    [Fact]
    public void V012_cria_indices_operacionais_e_de_auditoria_sem_grade()
    {
        var sql = ReadV012();
        foreach (var index in new[]
        {
            "ix_planos_disponibilidades_unidades_organizacao_unidade_ativo",
            "ix_planos_disponibilidades_unidades_organizacao_plano_ativo",
            "ix_matriculas_organizacao_unidade_status",
            "ix_matriculas_organizacao_aluno_status",
            "ix_matriculas_organizacao_unidade_aluno",
            "ix_matriculas_organizacao_plano_versao",
            "ix_matriculas_criado_por_usuario_id",
            "ix_matriculas_atualizado_por_usuario_id"
        })
        {
            Assert.Contains(index, sql);
        }
        Assert.DoesNotContain("matriculas_horarios", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("turma_horario_id", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V012_concede_somente_select_insert_update_ao_papel_portatil()
    {
        var sql = NormalizeWhitespace(ReadV012());
        var grants = Regex.Matches(sql, @"GRANT .*? TO bfa_app_role;")
            .Select(match => match.Value).ToArray();
        Assert.Equal(
            [
                "GRANT SELECT, INSERT, UPDATE ON TABLE planos_disponibilidades_unidades TO bfa_app_role;",
                "GRANT SELECT, INSERT, UPDATE ON TABLE matriculas TO bfa_app_role;"
            ],
            grants);
        Assert.DoesNotContain(grants, grant => grant.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("bfa_dev_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_staging_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_prod_app", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VALUES ('V012', 'criar disponibilidades de planos e matriculas');", sql);
    }

    [Fact]
    public void V001_a_V011_permanecem_exatamente_inalteradas()
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
            ["V011__criar_alunos_e_responsaveis.sql"] = "E9B19B01AC1DF1D0BC5ECF28988595246AC03C14F61BCD61915508DDFA0CFCC2"
        };

        foreach (var (file, expected) in hashes)
        {
            var content = File.ReadAllText(Path.Combine(MigrationDirectory(), file))
                .Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
        }
    }

    private static string Function(string name)
    {
        var match = Regex.Match(
            ReadV012(),
            $@"CREATE FUNCTION {Regex.Escape(name)}\(\).*?\$\$;",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Funcao {name} nao encontrada.");
        return NormalizeWhitespace(match.Value);
    }

    private static string[] GetSqlColumns(string sql, string tableName)
    {
        var match = Regex.Match(
            sql,
            $@"CREATE TABLE {Regex.Escape(tableName)} \((?<body>.*?)\r?\n\);",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Tabela {tableName} nao encontrada.");
        return match.Groups["body"].Value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimEnd(','))
            .TakeWhile(line => !line.StartsWith("CONSTRAINT ", StringComparison.Ordinal))
            .ToArray();
    }

    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

    private static string ReadV012() => File.ReadAllText(Path.Combine(
        MigrationDirectory(), "V012__criar_disponibilidades_de_planos_e_matriculas.sql"));

    private static string MigrationDirectory()
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

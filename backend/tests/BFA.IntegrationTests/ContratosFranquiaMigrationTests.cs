using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class ContratosFranquiaMigrationTests
{
    [Fact]
    public void V007_cria_as_tres_tabelas_com_colunas_e_tipos_exatos()
    {
        var sql = ReadV007();

        Assert.StartsWith("BEGIN;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            [
                "contratos_franquia",
                "contratos_franquia_versoes",
                "documentos_contrato_franquia"
            ],
            Regex.Matches(
                    sql,
                    @"CREATE TABLE (?<table>[a-z0-9_]+) \(",
                    RegexOptions.CultureInvariant)
                .Select(match => match.Groups["table"].Value));

        Assert.Equal(
            [
                "id uuid NOT NULL",
                "franqueado_unidade_id uuid NOT NULL",
                "numero varchar(100) NULL",
                "status varchar(30) NOT NULL",
                "criado_em_utc timestamptz NOT NULL",
                "atualizado_em_utc timestamptz NOT NULL"
            ],
            GetSqlColumns(sql, "contratos_franquia"));
        Assert.Equal(
            [
                "id uuid NOT NULL",
                "contrato_franquia_id uuid NOT NULL",
                "numero_versao integer NOT NULL",
                "data_inicio date NOT NULL",
                "data_fim date NULL",
                "percentual_royalties numeric(5,2) NOT NULL",
                "mensalidade_fixa numeric(12,2) NOT NULL",
                "taxa_adesao numeric(12,2) NULL",
                "dia_vencimento smallint NULL",
                "status varchar(30) NOT NULL",
                "motivo_alteracao varchar(1000) NULL",
                "observacoes varchar(4000) NULL",
                "criado_em_utc timestamptz NOT NULL",
                "criado_por_usuario_id uuid NOT NULL"
            ],
            GetSqlColumns(sql, "contratos_franquia_versoes"));
        Assert.Equal(
            [
                "id uuid NOT NULL",
                "contrato_franquia_versao_id uuid NOT NULL",
                "tipo_documento varchar(30) NOT NULL",
                "nome_original varchar(255) NOT NULL",
                "chave_armazenamento varchar(500) NOT NULL",
                "content_type varchar(100) NOT NULL",
                "tamanho_bytes bigint NOT NULL",
                "hash_sha256 varchar(64) NULL",
                "criado_em_utc timestamptz NOT NULL",
                "enviado_por_usuario_id uuid NOT NULL"
            ],
            GetSqlColumns(sql, "documentos_contrato_franquia"));

        Assert.DoesNotContain("bytea", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base64", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("large object", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V007_possui_checks_e_fks_restritivas_sem_cascade()
    {
        var sql = NormalizeWhitespace(ReadV007());

        Assert.Contains(
            "FOREIGN KEY (franqueado_unidade_id) REFERENCES franqueados_unidades (id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (contrato_franquia_id) REFERENCES contratos_franquia (id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (criado_por_usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (contrato_franquia_versao_id) REFERENCES contratos_franquia_versoes (id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FOREIGN KEY (enviado_por_usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT",
            sql,
            StringComparison.Ordinal);
        Assert.Equal(5, Regex.Matches(sql, "ON DELETE RESTRICT").Count);
        Assert.DoesNotContain("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("status IN ('Rascunho', 'Ativo', 'Encerrado', 'Cancelado')", sql);
        Assert.Contains("numero IS NULL OR btrim(numero) <> ''", sql);
        Assert.Contains("numero_versao >= 1", sql);
        Assert.Contains("data_fim IS NULL OR data_fim >= data_inicio", sql);
        Assert.Contains("percentual_royalties >= 0 AND percentual_royalties <= 100", sql);
        Assert.Contains("mensalidade_fixa >= 0", sql);
        Assert.Contains("taxa_adesao IS NULL OR taxa_adesao >= 0", sql);
        Assert.Contains("dia_vencimento IS NULL OR dia_vencimento BETWEEN 1 AND 31", sql);
        Assert.Contains("status IN ('Rascunho', 'Vigente', 'Substituida', 'Cancelada')", sql);
        Assert.Contains("motivo_alteracao IS NULL OR btrim(motivo_alteracao) <> ''", sql);
        Assert.Contains("observacoes IS NULL OR btrim(observacoes) <> ''", sql);
        Assert.Contains("tipo_documento IN ('Contrato', 'Aditivo', 'Anexo', 'Outro')", sql);
        Assert.Contains("btrim(nome_original) <> ''", sql);
        Assert.Contains("btrim(chave_armazenamento) <> ''", sql);
        Assert.Contains("btrim(content_type) <> ''", sql);
        Assert.Contains("tamanho_bytes > 0", sql);
        Assert.Contains("hash_sha256 IS NULL OR hash_sha256 ~ '^[0-9a-f]{64}$'", sql);
    }

    [Fact]
    public void V007_protege_identidade_numero_e_transicoes_do_contrato_principal()
    {
        var sql = NormalizeWhitespace(ReadV007());
        var function = Regex.Match(
            sql,
            @"CREATE FUNCTION proteger_contrato_franquia\(\).*?\$\$;",
            RegexOptions.CultureInvariant);

        Assert.True(function.Success);
        Assert.Contains("RETURNS trigger LANGUAGE plpgsql", function.Value, StringComparison.Ordinal);
        Assert.Contains("NEW.id IS DISTINCT FROM OLD.id", function.Value, StringComparison.Ordinal);
        Assert.Contains(
            "NEW.franqueado_unidade_id IS DISTINCT FROM OLD.franqueado_unidade_id",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "OLD.status <> 'Rascunho' AND NEW.numero IS DISTINCT FROM OLD.numero",
            function.Value,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "NEW.atualizado_em_utc IS DISTINCT FROM OLD.atualizado_em_utc",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "OLD.status = 'Rascunho' AND NEW.status IN ('Rascunho', 'Ativo', 'Cancelado')",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "OLD.status = 'Ativo' AND NEW.status IN ('Ativo', 'Encerrado', 'Cancelado')",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "OLD.status = 'Encerrado' AND NEW.status = 'Encerrado'",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "OLD.status = 'Cancelado' AND NEW.status = 'Cancelado'",
            function.Value,
            StringComparison.Ordinal);
        Assert.Contains("USING ERRCODE = '23514'", function.Value, StringComparison.Ordinal);
        Assert.Contains(
            "CREATE TRIGGER trg_proteger_contrato_franquia BEFORE UPDATE "
            + "ON contratos_franquia FOR EACH ROW "
            + "EXECUTE FUNCTION proteger_contrato_franquia();",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V007_protege_identidade_auditoria_termos_formalizados_e_transicoes()
    {
        var sql = NormalizeWhitespace(ReadV007());

        Assert.Contains(
            "CREATE FUNCTION proteger_versao_contrato_formalizada() RETURNS trigger "
            + "LANGUAGE plpgsql",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE TRIGGER trg_proteger_versao_contrato_formalizada BEFORE UPDATE "
            + "ON contratos_franquia_versoes FOR EACH ROW "
            + "EXECUTE FUNCTION proteger_versao_contrato_formalizada();",
            sql,
            StringComparison.Ordinal);

        foreach (var field in new[]
        {
            "id",
            "contrato_franquia_id",
            "numero_versao",
            "criado_em_utc",
            "criado_por_usuario_id"
        })
        {
            Assert.Contains(
                $"NEW.{field} IS DISTINCT FROM OLD.{field}",
                sql,
                StringComparison.Ordinal);
        }

        foreach (var field in new[]
        {
            "data_inicio",
            "data_fim",
            "percentual_royalties",
            "mensalidade_fixa",
            "taxa_adesao",
            "dia_vencimento",
            "motivo_alteracao",
            "observacoes"
        })
        {
            Assert.Contains(
                $"NEW.{field} IS DISTINCT FROM OLD.{field}",
                sql,
                StringComparison.Ordinal);
        }

        Assert.Contains("IF OLD.status <> 'Rascunho'", sql, StringComparison.Ordinal);
        Assert.Contains(
            "OLD.status = 'Rascunho' AND NEW.status IN ('Rascunho', 'Vigente', 'Cancelada')",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "OLD.status = 'Vigente' AND NEW.status IN ('Vigente', 'Substituida', 'Cancelada')",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "OLD.status = 'Substituida' AND NEW.status = 'Substituida'",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "OLD.status = 'Cancelada' AND NEW.status = 'Cancelada'",
            sql,
            StringComparison.Ordinal);
        Assert.Contains("USING ERRCODE = '23514'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void V007_possui_indices_unicos_parciais_e_evitar_indice_redundante_de_versao()
    {
        var sql = NormalizeWhitespace(ReadV007());

        Assert.Contains(
            "CREATE INDEX ix_contratos_franquia_franqueado_unidade_id "
            + "ON contratos_franquia (franqueado_unidade_id);",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_contratos_franquia_franqueado_unidade_ativo "
            + "ON contratos_franquia (franqueado_unidade_id) WHERE status = 'Ativo';",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_contratos_franquia_versoes_contrato_numero "
            + "ON contratos_franquia_versoes (contrato_franquia_id, numero_versao);",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_contratos_franquia_versoes_vigente "
            + "ON contratos_franquia_versoes (contrato_franquia_id) WHERE status = 'Vigente';",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE INDEX ix_contratos_franquia_versoes_criado_por_usuario_id "
            + "ON contratos_franquia_versoes (criado_por_usuario_id);",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ix_contratos_franquia_versoes_contrato_id",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE INDEX ix_documentos_contrato_franquia_versao_id "
            + "ON documentos_contrato_franquia (contrato_franquia_versao_id);",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX uq_documentos_contrato_franquia_chave_armazenamento "
            + "ON documentos_contrato_franquia (chave_armazenamento);",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE INDEX ix_documentos_contrato_franquia_enviado_por_usuario_id "
            + "ON documentos_contrato_franquia (enviado_por_usuario_id);",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V007_concede_dml_sem_delete_e_registra_historico_portavel()
    {
        var sql = ReadV007();
        var normalized = NormalizeWhitespace(sql);
        Assert.Equal(
            [
                "GRANT SELECT, INSERT, UPDATE ON TABLE contratos_franquia TO bfa_app_role;",
                "GRANT SELECT, INSERT, UPDATE ON TABLE contratos_franquia_versoes TO bfa_app_role;",
                "GRANT SELECT, INSERT ON TABLE documentos_contrato_franquia TO bfa_app_role;"
            ],
            Regex.Matches(
                    normalized,
                    @"GRANT .*? TO bfa_app_role;",
                    RegexOptions.CultureInvariant)
                .Select(match => match.Value));
        var grants = string.Join(
            ' ',
            Regex.Matches(
                    normalized,
                    @"GRANT .*? TO bfa_app_role;",
                    RegexOptions.CultureInvariant)
                .Select(match => match.Value));
        Assert.DoesNotContain("DELETE", grants, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "GRANT SELECT, INSERT, UPDATE ON TABLE documentos_contrato_franquia",
            grants,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_schema_history", grants, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bfa_dev_app", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("bfa_staging_app", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("bfa_prod_app", sql, StringComparison.Ordinal);
        Assert.Contains(
            "INSERT INTO bfa_schema_history (version, descricao) "
            + "VALUES ('V007', 'criar contratos de franquia versionados');",
            normalized,
            StringComparison.Ordinal);
    }

    [Fact]
    public void V001_a_v006_permanecem_exatamente_inalteradas()
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
                "26DAD5CCDFC483910F91F9B0D125BB2A48FA2C9F8EBBD547234C535F28E2B31B"
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

        Assert.True(tableMatch.Success, $"Tabela {tableName} nao encontrada na V007.");

        return tableMatch.Groups["body"].Value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimEnd(','))
            .TakeWhile(line => !line.StartsWith("CONSTRAINT ", StringComparison.Ordinal))
            .ToArray();
    }

    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

    private static string ReadV007() => File.ReadAllText(Path.Combine(
        GetMigrationsDirectory(),
        "V007__criar_contratos_franquia.sql"));

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

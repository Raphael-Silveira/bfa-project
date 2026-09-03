using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BFA.IntegrationTests;

public sealed class CorrecaoMatriculaMigrationTests
{
    [Fact]
    public void V014_corrige_somente_o_nome_da_coluna_de_atividade_da_unidade()
    {
        var v012 = ReadMigration(
            "V012__criar_disponibilidades_de_planos_e_matriculas.sql");
        var v014 = ReadMigration(
            "V014__corrigir_validacao_de_unidade_na_matricula.sql");

        foreach (var function in new[]
        {
            "proteger_plano_disponibilidade_unidade",
            "proteger_matricula"
        })
        {
            var original = CorpoFuncao(v012, function);
            var corrigida = CorpoFuncao(v014, function);
            var originalEsperada = original.Replace(
                "SELECT ativo\n        INTO unidade_ativa\n        FROM unidades",
                "SELECT ativa\n        INTO unidade_ativa\n        FROM unidades",
                StringComparison.Ordinal).Replace(
                "SELECT ativo\n    INTO unidade_ativa\n    FROM unidades",
                "SELECT ativa\n    INTO unidade_ativa\n    FROM unidades",
                StringComparison.Ordinal);

            Assert.NotEqual(original, originalEsperada);
            Assert.Equal(originalEsperada, corrigida);
        }
    }

    [Fact]
    public void V014_substitui_a_funcao_sem_alterar_tabelas_ou_triggers()
    {
        var sql = Normalize(ReadMigration(
            "V014__corrigir_validacao_de_unidade_na_matricula.sql"));

        Assert.StartsWith("BEGIN;", sql, StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE OR REPLACE FUNCTION proteger_matricula()", sql);
        Assert.Contains("SELECT ativa\n    INTO unidade_ativa\n    FROM unidades", sql);
        Assert.Contains("VALUES ('V014', 'corrigir validacao de unidade na matricula');", sql);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TRIGGER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V001_a_V013_permanecem_intactas()
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
            ["V012__criar_disponibilidades_de_planos_e_matriculas.sql"] = "F673BCE15828B81C6CD6EFE6A7215A523CA3F05FFB4D387DC8190A4A6E233DE5",
            ["V013__criar_grade_das_matriculas.sql"] = "973392C32ECFEAC651D99180459BA689AA06FA6A705166A3F8F677F9C650F5C4"
        };

        foreach (var (file, expected) in hashes)
        {
            var content = ReadMigration(file)
                .Replace("\r\n", "\n", StringComparison.Ordinal);
            var actual = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(content)));
            Assert.Equal(expected, actual);
        }
    }

    private static string CorpoFuncao(string sql, string function)
    {
        var match = Regex.Match(Normalize(sql),
            $@"CREATE(?: OR REPLACE)? FUNCTION {function}\(\).*?\$\$;",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Função {function} não encontrada.");
        return match.Value.Replace("CREATE OR REPLACE FUNCTION", "CREATE FUNCTION",
            StringComparison.Ordinal);
    }

    private static string ReadMigration(string file) => File.ReadAllText(
        Path.Combine(MigrationDirectory(), file));

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

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

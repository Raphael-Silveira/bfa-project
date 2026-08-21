using System.Reflection;
using BFA.Application.Unidades;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Unidades;
using Microsoft.EntityFrameworkCore;

namespace BFA.IntegrationTests;

public sealed class UnidadesUsuarioConsultaNpgsqlTranslationTests
{
    // Exercita o tradutor relacional real sem abrir conexão. A execução funcional
    // permanece coberta separadamente, pois a suíte não exige PostgreSQL local.
    [Fact]
    public void Listagem_e_traduzida_integralmente_pelo_provider_Npgsql()
    {
        using var context = CreateNpgsqlContext();
        var consulta = new UnidadesUsuarioConsulta(context);
        var query = ObterConsulta(
            consulta,
            "ConsultaListagemAdministradas",
            Guid.NewGuid());

        var sql = query.ToQueryString();

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vinculos_acesso", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unidades", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("organizacoes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "ON v.organizacao_id = u.organizacao_id AND v.unidade_id = u.id",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "v.perfil = 'AdministradorUnidade'",
            sql,
            StringComparison.Ordinal);
        Assert.Equal(
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            context.Database.ProviderName);
    }

    [Fact]
    public void Obtencao_e_traduzida_integralmente_pelo_provider_Npgsql()
    {
        using var context = CreateNpgsqlContext();
        var consulta = new UnidadesUsuarioConsulta(context);
        var query = ObterConsulta(
            consulta,
            "ConsultaAdministrada",
            Guid.NewGuid(),
            Guid.NewGuid());

        var sql = query.ToQueryString();

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vinculos_acesso", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unidades", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("organizacoes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("u.id = @unidadeId", sql, StringComparison.Ordinal);
        Assert.Equal(
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            context.Database.ProviderName);
    }

    private static IQueryable<UnidadeAcessoResumo> ObterConsulta(
        UnidadesUsuarioConsulta consulta,
        string methodName,
        params object[] arguments)
    {
        var method = typeof(UnidadesUsuarioConsulta).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return Assert.IsAssignableFrom<IQueryable<UnidadeAcessoResumo>>(
            method.Invoke(consulta, arguments));
    }

    private static BfaDbContext CreateNpgsqlContext()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql()
            .Options;
        return new BfaDbContext(options);
    }
}

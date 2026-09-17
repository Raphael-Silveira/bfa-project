using System.Reflection;
using BFA.Infrastructure.AlunoArea;
using BFA.Infrastructure.Persistence;
using BFA.Web.Areas.Aluno.Controllers;
using BFA.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BFA.IntegrationTests;

public sealed class AlunoAreaSegurancaTests
{
    [Fact]
    public void Controller_exige_policy_de_aluno()
    {
        var atributo = typeof(AlunoController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Equal(PoliticasAcesso.Aluno, atributo.Policy);
    }

    [Fact]
    public void Consulta_de_aulas_exige_vinculo_com_matricula_e_horario()
    {
        using var context = CreateNpgsqlContext();
        var repositorio = new AlunoAreaRepositorio(context);
        var query = ObterConsultaPrivada(
            repositorio,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));

        var sql = query.ToQueryString();

        Assert.Contains("aulas", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("matriculas_horarios", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("matriculas", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("turma_horario_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aluno_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status = 'Ativa'", sql, StringComparison.Ordinal);
        Assert.Contains("vigencia_inicio", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vigencia_fim", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            context.Database.ProviderName);
    }

    private static IQueryable ObterConsultaPrivada(
        AlunoAreaRepositorio repositorio,
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim)
    {
        var metodo = typeof(AlunoAreaRepositorio).GetMethod(
            "ConsultaAulasRelacionadas",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(metodo);

        var consulta = metodo.Invoke(
            repositorio,
            [organizacaoId, unidadeId, alunoId, dataInicio, dataFim, false]);

        return Assert.IsAssignableFrom<IQueryable>(consulta);
    }

    private static BfaDbContext CreateNpgsqlContext()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql()
            .Options;
        return new BfaDbContext(options);
    }
}

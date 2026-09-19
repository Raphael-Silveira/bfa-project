using System.Net;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AreaUnidadeEndpointTests
{
    [Fact]
    public async Task Edicao_administrativa_persiste_CPF_mascarado_normalizado_no_banco()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        var aluno = new Aluno(
            Guid.NewGuid(),
            cenario.OrganizacaoId,
            "Aluno CPF Editavel",
            new DateOnly(2000, 1, 1),
            new DateOnly(2026, 9, 18),
            CriadoEmUtc,
            cpf: "73865928099");
        var matricula = new Matricula(
            Guid.NewGuid(),
            cenario.OrganizacaoId,
            cenario.UnidadeId,
            aluno.Id,
            cenario.PlanoVersaoId,
            new DateOnly(2026, 9, 1),
            12,
            300m,
            true,
            100m,
            application.UsuarioStore.Usuario.Id,
            CriadoEmUtc);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            db.AddRange(aluno, matricula);
            await db.SaveChangesAsync();
            db.Entry(aluno).Property(item => item.Telefone).CurrentValue = "11912345678";
            await db.SaveChangesAsync();
        }

        using var client = CreateClient(application);
        await LoginAsync(client, application);
        using var pagina = await client.GetAsync(
            $"/unidade/{cenario.UnidadeId:D}/alunos/{aluno.Id:D}/editar");
        var token = ObterAntiforgery(await pagina.Content.ReadAsStringAsync());

        using var resposta = await client.PostAsync(
            $"/unidade/{cenario.UnidadeId:D}/alunos/{aluno.Id:D}/editar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["NomeCompleto"] = aluno.NomeCompleto,
                ["DataNascimento"] = "2000-01-01",
                ["Cpf"] = "526.904.950-31",
                ["Telefone"] = "(11) 91234-5678",
                ["Email"] = "aluno-cpf@bfa.test"
            }));

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);

        await using var leitura = application.Services.CreateAsyncScope();
        var dbRecarregado = leitura.ServiceProvider.GetRequiredService<BfaDbContext>();
        var persistido = await dbRecarregado.Alunos
            .AsNoTracking()
            .SingleAsync(item => item.Id == aluno.Id);

        Assert.Equal("52690495031", persistido.Cpf);
        Assert.Equal("5511912345678", persistido.Telefone);
    }
}

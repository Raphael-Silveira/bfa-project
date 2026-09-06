using System.Net;
using BFA.Domain.Acessos;
using BFA.Domain.Aulas;
using BFA.Domain.Professores;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AreaUnidadeEndpointTests
{
    private async Task<Aula> AdicionarAulaDiretoNoBancoAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid organizacaoId,
        Guid unidadeId,
        StatusAula status,
        string? observacoes = null)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();

        var aula = Aula.Reconstituir(
            Guid.NewGuid(),
            organizacaoId,
            unidadeId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 7),
            new TimeOnly(8, 0),
            new TimeOnly(10, 0),
            status,
            20,
            observacoes,
            application.UsuarioStore.Usuario.Id,
            DateTime.UtcNow,
            application.UsuarioStore.Usuario.Id,
            DateTime.UtcNow);

        db.Set<Aula>().Add(aula);
        await db.SaveChangesAsync();
        return aula;
    }

    [Fact]
    public async Task Administrador_unidade_edita_observacoes_de_aula_com_sucesso()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-aulas-teste");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Centro");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);

        var aula = await AdicionarAulaDiretoNoBancoAsync(
            application, organizacao.Id, unidade.Id, StatusAula.Concluida, "Obs inicial");

        using var client = CreateClient(application);
        await LoginAsync(client, application);

        // 1. GET Editar mostra Status atual
        var getResponse = await client.GetAsync($"/unidade/{unidade.Id:D}/aulas/{aula.Id:D}/editar");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var html = WebUtility.HtmlDecode(await getResponse.Content.ReadAsStringAsync());
        
        Assert.Contains("Concluida", html, StringComparison.Ordinal);
        Assert.Contains("Observações", html, StringComparison.Ordinal);
        Assert.Contains("Obs inicial", html, StringComparison.Ordinal);

        var token = ObterAntiforgery(html);

        // 2. Salvar Observações persiste (POST)
        using var postResponse = await client.PostAsync($"/unidade/{unidade.Id:D}/aulas/{aula.Id:D}/editar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Observacoes"] = "Obs modificada pelo teste"
            }));

        // PRG Redirect
        Assert.Equal(HttpStatusCode.Found, postResponse.StatusCode);
        Assert.EndsWith($"/unidade/{unidade.Id:D}/aulas/{aula.Id:D}", postResponse.Headers.Location!.ToString());

        // 3. Detalhes exibe Observações quando preenchidas
        var detalhesResponse = await client.GetAsync($"/unidade/{unidade.Id:D}/aulas/{aula.Id:D}");
        var htmlDetalhes = WebUtility.HtmlDecode(await detalhesResponse.Content.ReadAsStringAsync());
        Assert.Contains("Obs modificada pelo teste", htmlDetalhes, StringComparison.Ordinal);

        // 4. Banco de dados manteve o Status intacto
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var aulaNoBanco = await db.Set<Aula>().SingleAsync(a => a.Id == aula.Id);
        Assert.Equal(StatusAula.Concluida, aulaNoBanco.Status);
        Assert.Equal("Obs modificada pelo teste", aulaNoBanco.Observacoes);
    }
}

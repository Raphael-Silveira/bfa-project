using System.Net;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
using BFA.Domain.Alunos;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AlunoDashboardEndpointTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Login_de_aluno_renderiza_dashboard_canônico_da_area_aluno()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = new Organizacao(
            Guid.NewGuid(), "BFA", $"bfa-aluno-{Guid.NewGuid():N}", CriadoEmUtc);
        var unidade = new Unidade(
            Guid.NewGuid(), organizacao.Id, "BFA Aluno", $"aluno-{Guid.NewGuid():N}", CriadoEmUtc);
        var aluno = new Aluno(
            Guid.NewGuid(), organizacao.Id, "Aluno Dashboard",
            new DateOnly(2005, 5, 10), new DateOnly(2026, 9, 1), CriadoEmUtc,
            cpf: "10966666557");
        aluno.AlterarUsuario(application.UsuarioStore.Usuario.Id, CriadoEmUtc);
        var vinculo = new VinculoAcesso(
            Guid.NewGuid(), application.UsuarioStore.Usuario.Id, organizacao.Id,
            unidade.Id, PerfilAcesso.Aluno, CriadoEmUtc);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            db.AddRange(organizacao, unidade, aluno, vinculo);
            await db.SaveChangesAsync();
        }

        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        await LoginAsync(client, application);

        using var response = await client.GetAsync($"/aluno/{unidade.Id:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Painel do Aluno", html, StringComparison.Ordinal);
        Assert.Contains("Aluno Dashboard", html, StringComparison.Ordinal);
        Assert.DoesNotContain("The view 'Dashboard' was not found", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_de_aluno_renderiza_todos_os_menus_da_area_aluno()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = new Organizacao(
            Guid.NewGuid(), "BFA", $"bfa-menus-{Guid.NewGuid():N}", CriadoEmUtc);
        var unidade = new Unidade(
            Guid.NewGuid(), organizacao.Id, "BFA Menus", $"menus-{Guid.NewGuid():N}", CriadoEmUtc);
        var aluno = new Aluno(
            Guid.NewGuid(), organizacao.Id, "Aluno Menus",
            new DateOnly(2005, 5, 10), new DateOnly(2026, 9, 1), CriadoEmUtc,
            cpf: "10966666557");
        aluno.AlterarUsuario(application.UsuarioStore.Usuario.Id, CriadoEmUtc);
        var vinculo = new VinculoAcesso(
            Guid.NewGuid(), application.UsuarioStore.Usuario.Id, organizacao.Id,
            unidade.Id, PerfilAcesso.Aluno, CriadoEmUtc);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            db.AddRange(organizacao, unidade, aluno, vinculo);
            await db.SaveChangesAsync();
        }

        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        await LoginAsync(client, application);

        var rotas = new[] { "", "/perfil", "/matriculas", "/agenda", "/frequencia", "/financeiro" };
        foreach (var sufixo in rotas)
        {
            using var response = await client.GetAsync($"/aluno/{unidade.Id:D}{sufixo}");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("ViewNotFound", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("The view", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task LoginAsync(
        HttpClient client,
        AreaUnidadeWebApplicationFactory application)
    {
        using var loginPage = await client.GetAsync("/login");
        var loginHtml = await loginPage.Content.ReadAsStringAsync();
        var token = AntiforgeryToken().Match(loginHtml).Groups["token"].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = application.UsuarioStore.Email,
            ["Senha"] = application.UsuarioStore.Senha,
            ["LembrarMe"] = "false",
            ["ReturnUrl"] = string.Empty,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        });
        using var response = await client.PostAsync("/login", form);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)",
        RegexOptions.Compiled)]
    private static partial Regex AntiforgeryToken();
}

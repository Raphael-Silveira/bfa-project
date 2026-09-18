using System.Net;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
using BFA.Domain.Alunos;
using BFA.Domain.Organizacoes;
using BFA.Domain.Localidades;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

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

        using var filtroValido = await client.GetAsync(
            $"/aluno/{unidade.Id:D}/financeiro?periodo=personalizado&dataInicio=01%2F09%2F2026&dataFim=30%2F09%2F2026");
        Assert.Equal(HttpStatusCode.OK, filtroValido.StatusCode);

        using var filtroInvalido = await client.GetAsync(
            $"/aluno/{unidade.Id:D}/financeiro?periodo=personalizado&dataInicio=30%2F09%2F2026&dataFim=01%2F09%2F2026");
        Assert.Equal(HttpStatusCode.BadRequest, filtroInvalido.StatusCode);
    }

    [Fact]
    public async Task Aluno_pode_renderizar_e_atualizar_apenas_seus_dados_de_contato()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = new Organizacao(
            Guid.NewGuid(), "BFA", $"bfa-perfil-{Guid.NewGuid():N}", CriadoEmUtc);
        var unidade = new Unidade(
            Guid.NewGuid(), organizacao.Id, "BFA Perfil", $"perfil-{Guid.NewGuid():N}", CriadoEmUtc);
        var aluno = new Aluno(
            Guid.NewGuid(), organizacao.Id, "Aluno Perfil",
            new DateOnly(2005, 5, 10), new DateOnly(2026, 9, 1), CriadoEmUtc,
            cpf: "10966666557", telefone: "5511992682235", email: "antes@exemplo.com");
        aluno.AlterarUsuario(application.UsuarioStore.Usuario.Id, CriadoEmUtc);
        var vinculo = new VinculoAcesso(
            Guid.NewGuid(), application.UsuarioStore.Usuario.Id, organizacao.Id,
            unidade.Id, PerfilAcesso.Aluno, CriadoEmUtc);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            db.AddRange(
                organizacao,
                unidade,
                aluno,
                vinculo,
                new Estado(35, "SP", "São Paulo", CriadoEmUtc),
                new Municipio(3554508, 35, "Tietê", CriadoEmUtc));
            await db.SaveChangesAsync();
        }

        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        await LoginAsync(client, application);

        using var perfilResponse = await client.GetAsync($"/aluno/{unidade.Id:D}/perfil");
        var perfilHtml = await perfilResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, perfilResponse.StatusCode);
        Assert.Contains("Dados Cadastrais", perfilHtml, StringComparison.Ordinal);
        Assert.Contains("Atualizar cadastro", perfilHtml, StringComparison.Ordinal);
        Assert.Contains("+55 (11) 99268-2235", WebUtility.HtmlDecode(perfilHtml), StringComparison.Ordinal);

        using var editResponse = await client.GetAsync($"/aluno/{unidade.Id:D}/perfil/editar");
        var editHtml = await editResponse.Content.ReadAsStringAsync();
        var token = AntiforgeryToken().Match(editHtml).Groups["token"].Value;
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        Assert.Contains("Aluno Perfil", editHtml, StringComparison.Ordinal);
        Assert.Contains("109.666.665-57", editHtml, StringComparison.Ordinal);
        Assert.Contains("(11) 99268-2235", WebUtility.HtmlDecode(editHtml), StringComparison.Ordinal);
        Assert.Contains("data-bfa-combobox", editHtml, StringComparison.Ordinal);
        Assert.Contains("São Paulo (SP)", WebUtility.HtmlDecode(editHtml), StringComparison.Ordinal);
        Assert.Contains("data-placeholder=\"Pesquise ou selecione um Estado\"", editHtml, StringComparison.Ordinal);
        Assert.Contains("bfa-searchable-combobox.js", editHtml, StringComparison.Ordinal);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Apelido"] = "Aluno atualizado",
            ["Email"] = "depois@exemplo.com",
            ["Telefone"] = "(11) 99999-0000",
            ["Cep"] = "18530-000",
            ["EstadoCodigoIbge"] = "35",
            ["MunicipioCodigoIbge"] = "3554508",
            ["Bairro"] = "Centro",
            ["Logradouro"] = "Rua Principal",
            ["Numero"] = "123",
            ["Complemento"] = "Apto 4",
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        });
        using var postResponse = await client.PostAsync($"/aluno/{unidade.Id:D}/perfil/editar", form);

        Assert.Equal(HttpStatusCode.Found, postResponse.StatusCode);
        Assert.Equal($"/aluno/{unidade.Id:D}/perfil", postResponse.Headers.Location?.OriginalString);

        await using var verifyScope = application.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var persisted = await verifyDb.Alunos.SingleAsync(item => item.Id == aluno.Id);
        Assert.Equal("depois@exemplo.com", persisted.Email);
        Assert.Equal("5511999990000", persisted.Telefone);
        Assert.Equal("Aluno atualizado", persisted.Apelido);
        Assert.Equal("18530000", persisted.Cep);
        Assert.Equal(35, persisted.EstadoCodigoIbge);
        Assert.Equal(3554508, persisted.MunicipioCodigoIbge);
        Assert.Equal("Centro", persisted.Bairro);
        Assert.Equal("Rua Principal", persisted.Logradouro);
        Assert.Equal("123", persisted.Numero);
        Assert.Equal("Apto 4", persisted.Complemento);
        Assert.Equal("Aluno Perfil", persisted.NomeCompleto);
        Assert.Equal("10966666557", persisted.Cpf);
        Assert.Equal(new DateOnly(2005, 5, 10), persisted.DataNascimento);
    }

    [Fact]
    public async Task POST_de_perfil_com_foto_persiste_dados_cadastrais_e_referencia_da_foto()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = new Organizacao(
            Guid.NewGuid(), "BFA", $"bfa-perfil-foto-{Guid.NewGuid():N}", CriadoEmUtc);
        var unidade = new Unidade(
            Guid.NewGuid(), organizacao.Id, "BFA Perfil Foto", $"foto-{Guid.NewGuid():N}", CriadoEmUtc);
        var aluno = new Aluno(
            Guid.NewGuid(), organizacao.Id, "Aluno Foto",
            new DateOnly(2005, 5, 10), new DateOnly(2026, 9, 1), CriadoEmUtc,
            cpf: "10966666557", telefone: "5511992682235", email: "antes@exemplo.com");
        aluno.AlterarUsuario(application.UsuarioStore.Usuario.Id, CriadoEmUtc);
        var vinculo = new VinculoAcesso(
            Guid.NewGuid(), application.UsuarioStore.Usuario.Id, organizacao.Id,
            unidade.Id, PerfilAcesso.Aluno, CriadoEmUtc);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            db.AddRange(
                organizacao,
                unidade,
                aluno,
                vinculo,
                new Estado(35, "SP", "São Paulo", CriadoEmUtc),
                new Municipio(3554508, 35, "Tietê", CriadoEmUtc));
            await db.SaveChangesAsync();
        }

        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        await LoginAsync(client, application);

        var editHtml = await client.GetStringAsync($"/aluno/{unidade.Id:D}/perfil/editar");
        var token = AntiforgeryToken().Match(editHtml).Groups["token"].Value;
        using var form = new MultipartFormDataContent();
        AdicionarCampo(form, "Apelido", "Aluno com foto");
        AdicionarCampo(form, "Email", "foto@exemplo.com");
        AdicionarCampo(form, "Telefone", "(11) 98888-0000");
        AdicionarCampo(form, "Cep", "18530-000");
        AdicionarCampo(form, "EstadoCodigoIbge", "35");
        AdicionarCampo(form, "MunicipioCodigoIbge", "3554508");
        AdicionarCampo(form, "Bairro", "Centro");
        AdicionarCampo(form, "Logradouro", "Rua da Foto");
        AdicionarCampo(form, "Numero", "45");
        AdicionarCampo(form, "Complemento", "Casa");
        AdicionarCampo(form, "__RequestVerificationToken", WebUtility.HtmlDecode(token));
        var arquivo = new ByteArrayContent(CriarJpeg());
        arquivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(arquivo, "FotoPerfil", "perfil.jpg");

        using var response = await client.PostAsync($"/aluno/{unidade.Id:D}/perfil/editar", form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var verifyScope = application.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var persisted = await verifyDb.Alunos.SingleAsync(item => item.Id == aluno.Id);
        Assert.Equal("Aluno com foto", persisted.Apelido);
        Assert.Equal("foto@exemplo.com", persisted.Email);
        Assert.Equal("5511988880000", persisted.Telefone);
        Assert.Equal("18530000", persisted.Cep);
        Assert.Equal(35, persisted.EstadoCodigoIbge);
        Assert.Equal(3554508, persisted.MunicipioCodigoIbge);
        Assert.Equal("Centro", persisted.Bairro);
        Assert.Equal("Rua da Foto", persisted.Logradouro);
        Assert.Equal("45", persisted.Numero);
        Assert.Equal("Casa", persisted.Complemento);
        Assert.NotNull(persisted.FotoPerfilChave);
        Assert.EndsWith(".webp", persisted.FotoPerfilChave, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            application.DiretorioFotos,
            persisted.FotoPerfilChave!.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static void AdicionarCampo(MultipartFormDataContent form, string nome, string valor) =>
        form.Add(new StringContent(valor), nome);

    private static byte[] CriarJpeg()
    {
        using var bitmap = new SKBitmap(320, 180);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Gold);
        using var imagem = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
        return imagem!.ToArray();
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

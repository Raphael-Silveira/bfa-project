using System.Net;
using System.Text.RegularExpressions;
using BFA.Domain.Acessos;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AreaUnidadeEndpointTests
{
    [Theory]
    [InlineData(PerfilAcesso.AdministradorUnidade)]
    [InlineData(PerfilAcesso.AdministradorRede)]
    public async Task Administrador_ajusta_programacao_preservando_historico(
        PerfilAcesso perfil)
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"ajuste-{perfil}-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, perfil == PerfilAcesso.AdministradorRede ? null : unidade.Id,
            perfil);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professora Ana");
        await AdicionarTurmaHorarioAsync(application, organizacao.Id, unidade.Id,
            professor.Vinculo.Id, "Turma Noite", new TimeOnly(19, 0), new TimeOnly(20, 0));
        Guid turmaId;
        Guid horarioAntigoId;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            turmaId = (await db.Turmas.SingleAsync()).Id;
            horarioAntigoId = (await db.TurmasHorarios.SingleAsync()).Id;
        }

        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var url = $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/horarios";
        var pagina = await client.GetStringAsync(url);
        Assert.Contains("Programação atual", WebUtility.HtmlDecode(pagina),
            StringComparison.Ordinal);
        Assert.Contains("Os horários vigentes já estão carregados abaixo", pagina,
            StringComparison.Ordinal);
        Assert.Contains("value=\"19:00\"", pagina, StringComparison.Ordinal);
        Assert.Contains("value=\"20:00\"", pagina, StringComparison.Ordinal);
        Assert.Contains("value=\"1\" selected=\"selected\"", pagina,
            StringComparison.Ordinal);
        Assert.Equal(4, Regex.Matches(pagina, "data-bfa-time-input").Count);
        Assert.Contains("inputmode=\"numeric\"", pagina, StringComparison.Ordinal);
        Assert.Contains("pattern=\"(?:[01][0-9]|2[0-3]):[0-5][0-9]\"", pagina,
            StringComparison.Ordinal);
        var action = ObterActionFormularioHorarios(pagina);
        Assert.Equal(url, action);
        Assert.DoesNotContain("/unidade/unidade/", pagina,
            StringComparison.OrdinalIgnoreCase);
        var token = ObterAntiforgery(pagina);
        using var response = await client.PostAsync(action,
            FormAjuste(token, "01/09/2026",
                [(DiaSemana.Segunda, "20:00", "21:00"),
                 (DiaSemana.Quarta, "19:00", "20:00")]));

        Assert.NotEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var verificacao = application.Services.CreateAsyncScope();
        var contexto = verificacao.ServiceProvider.GetRequiredService<BfaDbContext>();
        var horarios = await contexto.TurmasHorarios.OrderBy(item => item.VigenciaInicio)
            .ThenBy(item => item.DiaSemana).ToArrayAsync();
        Assert.Equal(3, horarios.Length);
        var antigo = Assert.Single(horarios, item => item.Id == horarioAntigoId);
        Assert.Equal(new DateOnly(2026, 8, 31), antigo.VigenciaFim);
        Assert.Equal(professor.Vinculo.Id, antigo.ProfessorUnidadeId);
        Assert.All(horarios.Where(item => item.Id != horarioAntigoId), item =>
        {
            Assert.Equal(new DateOnly(2026, 9, 1), item.VigenciaInicio);
            Assert.Null(item.VigenciaFim);
            Assert.Equal(professor.Vinculo.Id, item.ProfessorUnidadeId);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Conflito_na_mesma_ou_outra_unidade_rejeita_ajuste_sem_encerrar_historico(
        bool outraUnidade)
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"ajuste-conflito-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA A");
        var outra = outraUnidade
            ? await AdicionarUnidadeAsync(application, organizacao.Id, "BFA B")
            : unidade;
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor");
        var outroVinculo = outraUnidade
            ? await AdicionarVinculoProfessorTurmaAsync(
                application, professor.Professor, outra.Id)
            : professor.Vinculo;
        await AdicionarTurmaHorarioAsync(application, organizacao.Id, unidade.Id,
            professor.Vinculo.Id, "Turma ajustada", new TimeOnly(17, 0), new TimeOnly(18, 0));
        await AdicionarTurmaHorarioAsync(application, organizacao.Id, outra.Id,
            outroVinculo.Id, "Turma conflitante", new TimeOnly(19, 0), new TimeOnly(20, 0));
        Guid turmaId;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            turmaId = await db.Turmas.Where(item => item.UnidadeId == unidade.Id
                    && item.Nome == "Turma ajustada")
                .Select(item => item.Id).SingleAsync();
        }
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/horarios");
        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/horarios",
            FormAjuste(ObterAntiforgery(pagina), "01/09/2026",
                [(DiaSemana.Segunda, "19:30", "20:30")]));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("já possui outra turma nesse horário", html, StringComparison.Ordinal);
        await using var verificacao = application.Services.CreateAsyncScope();
        var horarios = await verificacao.ServiceProvider.GetRequiredService<BfaDbContext>()
            .TurmasHorarios.Where(item => item.TurmaId == turmaId).ToArrayAsync();
        Assert.Single(horarios);
        Assert.Null(horarios[0].VigenciaFim);
    }

    [Fact]
    public async Task Horarios_adjacentes_sao_aceitos_no_ajuste()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"ajuste-adjacente-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor");
        await AdicionarTurmaHorarioAsync(application, organizacao.Id, unidade.Id,
            professor.Vinculo.Id, "Turma", new TimeOnly(19, 0), new TimeOnly(20, 0));
        Guid turmaId;
        await using (var scope = application.Services.CreateAsyncScope())
            turmaId = await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
                .Turmas.Select(item => item.Id).SingleAsync();
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/horarios");

        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/horarios",
            FormAjuste(ObterAntiforgery(pagina), "01/09/2026",
                [(DiaSemana.Segunda, "19:00", "20:00"),
                 (DiaSemana.Segunda, "20:00", "21:00")]));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact]
    public async Task Vigencia_igual_ao_inicio_anterior_e_rejeitada_amigavelmente()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"ajuste-data-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor");
        await AdicionarTurmaHorarioAsync(application, organizacao.Id, unidade.Id,
            professor.Vinculo.Id, "Turma", new TimeOnly(19, 0), new TimeOnly(20, 0));
        Guid turmaId;
        await using (var scope = application.Services.CreateAsyncScope())
            turmaId = await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
                .Turmas.Select(item => item.Id).SingleAsync();
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/horarios");
        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/horarios",
            FormAjuste(ObterAntiforgery(pagina), "01/01/2026",
                [(DiaSemana.Segunda, "20:00", "21:00")]));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("02/01/2026 ou depois", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Outro_tenant_nao_acessa_ajuste_de_horarios()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var permitida = await AdicionarOrganizacaoAsync(
            application, "BFA", $"ajuste-permitida-{Guid.NewGuid():N}");
        var externa = await AdicionarOrganizacaoAsync(
            application, "Outra", $"ajuste-externa-{Guid.NewGuid():N}");
        var unidadePermitida = await AdicionarUnidadeAsync(
            application, permitida.Id, "Permitida");
        var unidadeExterna = await AdicionarUnidadeAsync(
            application, externa.Id, "Externa");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            permitida.Id, unidadePermitida.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(
            application, externa.Id, unidadeExterna.Id, "Professor externo");
        await AdicionarTurmaHorarioAsync(application, externa.Id, unidadeExterna.Id,
            professor.Vinculo.Id, "Turma externa", new TimeOnly(19, 0),
            new TimeOnly(20, 0));
        Guid turmaId;
        await using (var scope = application.Services.CreateAsyncScope())
            turmaId = await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
                .Turmas.Where(item => item.UnidadeId == unidadeExterna.Id)
                .Select(item => item.Id).SingleAsync();
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync(
            $"/unidade/{unidadeExterna.Id:D}/turmas/{turmaId:D}/horarios");

        AssertAcessoNegado(response);
    }

    private static FormUrlEncodedContent FormAjuste(
        string token, string vigencia,
        IReadOnlyList<(DiaSemana Dia, string Inicio, string Fim)> horarios)
    {
        var dados = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("NovaVigenciaInicioTexto", vigencia)
        };
        for (var indice = 0; indice < horarios.Count; indice++)
        {
            dados.Add(new($"Horarios[{indice}].DiaSemana",
                ((short)horarios[indice].Dia).ToString()));
            dados.Add(new($"Horarios[{indice}].HoraInicio", horarios[indice].Inicio));
            dados.Add(new($"Horarios[{indice}].HoraFim", horarios[indice].Fim));
        }
        return new(dados);
    }

    private static string ObterActionFormularioHorarios(string html)
    {
        var match = Regex.Match(
            html,
            "<form(?=[^>]*class=\"[^\"]*bfa-turma-form[^\"]*\")[^>]*action=\"(?<action>[^\"]+)\"[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(match.Success, "O formulário de ajuste de horários não foi renderizado.");
        return WebUtility.HtmlDecode(match.Groups["action"].Value);
    }
}

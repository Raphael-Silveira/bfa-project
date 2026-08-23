using System.Net;
using BFA.Domain.Acessos;
using BFA.Domain.Professores;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AreaUnidadeEndpointTests
{
    [Fact]
    public async Task Alteracao_cria_nova_remuneracao_e_preserva_historico_no_mesmo_vinculo()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-remuneracao-alterar");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Cerquilho");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var cadastro = await AdicionarProfessorAsync(
            application, organizacao.Id, unidade.Id, "12345678901", 1500m);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var pagina = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/remuneracao"));
        Assert.Contains("Remuneração atual", pagina, StringComparison.Ordinal);
        Assert.Contains("Histórico de remuneração", pagina, StringComparison.Ordinal);
        Assert.Contains("R$ 1.500,00", pagina, StringComparison.Ordinal);
        var token = ObterAntiforgery(pagina);

        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/remuneracao",
            FormAlteracaoRemuneracao(token, ModalidadeRemuneracaoProfessor.Mensal,
                "2000,00", "01/09/2026", "Reajuste anual"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/remuneracao",
            response.Headers.Location?.OriginalString);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var professor = await db.Professores.SingleAsync();
        var vinculo = await db.ProfessoresUnidades.SingleAsync();
        var remuneracoes = await db.ProfessoresRemuneracoes
            .OrderBy(item => item.VigenciaInicio)
            .ToArrayAsync();
        Assert.Equal(cadastro.Professor.Id, professor.Id);
        Assert.Equal(cadastro.Vinculo.Id, vinculo.Id);
        Assert.True(vinculo.Ativo);
        Assert.Equal(2, remuneracoes.Length);
        Assert.Equal(ModalidadeRemuneracaoProfessor.Mensal, remuneracoes[0].Modalidade);
        Assert.Equal(1500m, remuneracoes[0].Valor);
        Assert.Equal(new DateOnly(2026, 1, 1), remuneracoes[0].VigenciaInicio);
        Assert.Equal(new DateOnly(2026, 8, 31), remuneracoes[0].VigenciaFim);
        Assert.Equal(ModalidadeRemuneracaoProfessor.Mensal, remuneracoes[1].Modalidade);
        Assert.Equal(2000m, remuneracoes[1].Valor);
        Assert.Equal(new DateOnly(2026, 9, 1), remuneracoes[1].VigenciaInicio);
        Assert.Null(remuneracoes[1].VigenciaFim);
        Assert.Equal("Reajuste anual", remuneracoes[1].Observacao);
        Assert.Single(remuneracoes, item => item.VigenciaFim == null);

        var historico = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/remuneracao"));
        Assert.True(
            historico.IndexOf("01/09/2026 — Atual", StringComparison.Ordinal)
            < historico.IndexOf("01/01/2026 — 31/08/2026", StringComparison.Ordinal));
        Assert.Contains("R$ 2.000,00", historico, StringComparison.Ordinal);
        Assert.Contains("R$ 1.500,00", historico, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vigencia_igual_ou_anterior_a_atual_e_rejeitada_sem_alterar_registros()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-remuneracao-data");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var cadastro = await AdicionarProfessorAsync(
            application, organizacao.Id, unidade.Id, "12345678901", 1500m);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/remuneracao");
        var token = ObterAntiforgery(pagina);

        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/remuneracao",
            FormAlteracaoRemuneracao(token, ModalidadeRemuneracaoProfessor.PorAula,
                "100,00", "01/01/2026"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("A nova remuneração deve iniciar após 01/01/2026.", html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var remuneracao = await db.ProfessoresRemuneracoes.SingleAsync();
        Assert.Equal(1500m, remuneracao.Valor);
        Assert.Equal(ModalidadeRemuneracaoProfessor.Mensal, remuneracao.Modalidade);
        Assert.Equal(new DateOnly(2026, 1, 1), remuneracao.VigenciaInicio);
        Assert.Null(remuneracao.VigenciaFim);
        Assert.True((await db.ProfessoresUnidades.SingleAsync()).Ativo);
    }

    [Fact]
    public async Task Outro_tenant_e_administrador_sem_acesso_nao_alteram_remuneracao()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-remuneracao-auth");
        var outraOrganizacao = await AdicionarOrganizacaoAsync(application, "Outra", "outra-remuneracao-auth");
        var permitida = await AdicionarUnidadeAsync(application, organizacao.Id, "Permitida");
        var proibida = await AdicionarUnidadeAsync(application, organizacao.Id, "Proibida");
        var externa = await AdicionarUnidadeAsync(application, outraOrganizacao.Id, "Externa");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, permitida.Id, PerfilAcesso.AdministradorUnidade);
        var semAcesso = await AdicionarProfessorAsync(
            application, organizacao.Id, proibida.Id, "12345678901");
        var outroTenant = await AdicionarProfessorAsync(
            application, outraOrganizacao.Id, externa.Id, "98765432100");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{permitida.Id:D}/professores/novo"));

        using var getSemAcesso = await client.GetAsync(
            $"/unidade/{proibida.Id:D}/professores/{semAcesso.Professor.Id:D}/remuneracao");
        using var getOutroTenant = await client.GetAsync(
            $"/unidade/{permitida.Id:D}/professores/{outroTenant.Professor.Id:D}/remuneracao");
        using var postSemAcesso = await client.PostAsync(
            $"/unidade/{proibida.Id:D}/professores/{semAcesso.Professor.Id:D}/remuneracao",
            FormAlteracaoRemuneracao(token, ModalidadeRemuneracaoProfessor.Mensal,
                "2000,00", "01/09/2026"));
        using var postOutroTenant = await client.PostAsync(
            $"/unidade/{permitida.Id:D}/professores/{outroTenant.Professor.Id:D}/remuneracao",
            FormAlteracaoRemuneracao(token, ModalidadeRemuneracaoProfessor.Mensal,
                "2000,00", "01/09/2026"));

        AssertAcessoNegado(getSemAcesso);
        Assert.Equal(HttpStatusCode.NotFound, getOutroTenant.StatusCode);
        AssertAcessoNegado(postSemAcesso);
        Assert.Equal(HttpStatusCode.NotFound, postOutroTenant.StatusCode);
    }

    [Fact]
    public async Task Acao_de_remuneracao_aparece_somente_para_vinculo_ativo()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-remuneracao-acao");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var cadastro = await AdicionarProfessorAsync(application, organizacao.Id, unidade.Id, "12345678901");
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var ativos = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores"));
        Assert.Contains(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/remuneracao",
            ativos,
            StringComparison.Ordinal);

        await InativarVinculoProfessorAsync(application, cadastro.Vinculo.Id);
        var encerrados = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores?filtro=encerrados"));
        Assert.DoesNotContain(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/remuneracao",
            encerrados,
            StringComparison.Ordinal);
    }

    private static FormUrlEncodedContent FormAlteracaoRemuneracao(
        string token,
        ModalidadeRemuneracaoProfessor modalidade,
        string valor,
        string vigencia,
        string observacao = "Nova condição") => new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Modalidade"] = modalidade.ToString(),
            ["ValorTexto"] = valor,
            ["VigenciaInicioTexto"] = vigencia,
            ["Observacao"] = observacao
        });
}

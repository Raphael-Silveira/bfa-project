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
    public async Task Administrador_unidade_visualiza_estado_vazio_de_professores()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-prof-vazio");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Professores");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync($"/unidade/{unidade.Id:D}/professores");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nenhum professor encontrado", html, StringComparison.Ordinal);
        Assert.Contains("Novo professor", html, StringComparison.Ordinal);
        Assert.Contains("Professores", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ModalidadeRemuneracaoProfessor.Mensal)]
    [InlineData(ModalidadeRemuneracaoProfessor.PorAula)]
    [InlineData(ModalidadeRemuneracaoProfessor.PorHora)]
    public async Task Cadastro_cria_professor_vinculo_e_remuneracao_inicial_na_modalidade(
        ModalidadeRemuneracaoProfessor modalidade)
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", $"bfa-{modalidade}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/novo"));

        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/professores/novo",
            FormProfessor(token, modalidade, "123.456.789-01", "1500,50", "22/08/2026"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal($"/unidade/{unidade.Id:D}/professores", response.Headers.Location?.OriginalString);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var professor = await db.Professores.SingleAsync();
        var vinculo = await db.ProfessoresUnidades.SingleAsync();
        var remuneracao = await db.ProfessoresRemuneracoes.SingleAsync();
        Assert.Equal(organizacao.Id, professor.OrganizacaoId);
        Assert.Equal("12345678901", professor.Cpf);
        Assert.Equal(professor.Id, vinculo.ProfessorId);
        Assert.Equal(unidade.Id, vinculo.UnidadeId);
        Assert.Equal(vinculo.Id, remuneracao.ProfessorUnidadeId);
        Assert.Equal(modalidade, remuneracao.Modalidade);
        Assert.Equal(1500.50m, remuneracao.Valor);
        Assert.Equal(new DateOnly(2026, 8, 22), remuneracao.VigenciaInicio);
        Assert.Null(remuneracao.VigenciaFim);

        var listagem = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores"));
        Assert.Contains("Professora Ana Silva", listagem, StringComparison.Ordinal);
        Assert.Contains(NomeModalidadeTeste(modalidade), listagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cpf_duplicado_na_organizacao_retorna_mensagem_amigavel_sem_criar_registros()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-cpf-duplicado");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        await AdicionarProfessorAsync(application, organizacao.Id, unidade.Id, "12345678901");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/novo"));

        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/professores/novo",
            FormProfessor(token, ModalidadeRemuneracaoProfessor.Mensal,
                "123.456.789-01", "1000,00", "22/08/2026"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Este professor já está cadastrado na rede", html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(1, await db.Professores.CountAsync());
        Assert.Equal(1, await db.ProfessoresUnidades.CountAsync());
        Assert.Equal(1, await db.ProfessoresRemuneracoes.CountAsync());
    }

    [Theory]
    [InlineData("-1,00", "22/08/2026")]
    [InlineData("100,00", "data inválida")]
    public async Task Remuneracao_negativa_ou_vigencia_invalida_e_rejeitada(
        string valor, string data)
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", $"bfa-invalido-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync($"/unidade/{unidade.Id:D}/professores/novo"));

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/professores/novo",
            FormProfessor(token, ModalidadeRemuneracaoProfessor.Mensal, null, valor, data));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Empty(await db.Professores.ToArrayAsync());
        Assert.Empty(await db.ProfessoresUnidades.ToArrayAsync());
        Assert.Empty(await db.ProfessoresRemuneracoes.ToArrayAsync());
    }

    [Fact]
    public async Task Administrador_nao_acessa_professores_de_outra_unidade_ou_tenant_pela_url()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-prof-auth");
        var externa = await AdicionarOrganizacaoAsync(application, "Outra", "outra-prof-auth");
        var permitida = await AdicionarUnidadeAsync(application, organizacao.Id, "Permitida");
        var proibida = await AdicionarUnidadeAsync(application, organizacao.Id, "Proibida");
        var outroTenant = await AdicionarUnidadeAsync(application, externa.Id, "Externa");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, permitida.Id, PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{permitida.Id:D}/professores/novo"));

        using var mesmaOrg = await client.GetAsync($"/unidade/{proibida.Id:D}/professores");
        using var outraOrg = await client.GetAsync($"/unidade/{outroTenant.Id:D}/professores/novo");
        using var postOutraUnidade = await client.PostAsync(
            $"/unidade/{proibida.Id:D}/professores/novo",
            FormProfessor(token, ModalidadeRemuneracaoProfessor.Mensal,
                null, "100,00", "22/08/2026"));
        using var postVinculoOutraUnidade = await client.PostAsync(
            $"/unidade/{proibida.Id:D}/professores/vincular",
            FormVinculoProfessor(token, Guid.NewGuid(),
                ModalidadeRemuneracaoProfessor.Mensal, "100,00"));
        AssertAcessoNegado(mesmaOrg);
        AssertAcessoNegado(outraOrg);
        AssertAcessoNegado(postOutraUnidade);
        AssertAcessoNegado(postVinculoOutraUnidade);
    }

    [Fact]
    public async Task Professor_existente_e_vinculado_a_segunda_unidade_sem_duplicar_professor()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-segunda-unidade");
        var cerquilho = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Cerquilho");
        var tiete = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Tietê");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, tiete.Id, PerfilAcesso.AdministradorUnidade);
        var existente = await AdicionarProfessorAsync(
            application, organizacao.Id, cerquilho.Id, "12345678901", 3000m);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var busca = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{tiete.Id:D}/professores/vincular?termo=Professor"));
        Assert.Contains("Professor existente", busca, StringComparison.Ordinal);
        Assert.Contains("CPF ***.***.***-01", busca, StringComparison.Ordinal);
        Assert.DoesNotContain("3.000", busca, StringComparison.Ordinal);
        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{tiete.Id:D}/professores/vincular?professorId={existente.Professor.Id:D}"));

        using var response = await client.PostAsync(
            $"/unidade/{tiete.Id:D}/professores/vincular",
            FormVinculoProfessor(token, existente.Professor.Id,
                ModalidadeRemuneracaoProfessor.PorAula, "80,00"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(1, await db.Professores.CountAsync());
        Assert.Equal(2, await db.ProfessoresUnidades.CountAsync());
        Assert.Equal(2, await db.ProfessoresRemuneracoes.CountAsync());
        var remuneracoes = await db.ProfessoresRemuneracoes.OrderBy(item => item.Valor).ToArrayAsync();
        Assert.Equal(80m, remuneracoes[0].Valor);
        Assert.Equal(ModalidadeRemuneracaoProfessor.PorAula, remuneracoes[0].Modalidade);
        Assert.Equal(3000m, remuneracoes[1].Valor);
        Assert.Equal(ModalidadeRemuneracaoProfessor.Mensal, remuneracoes[1].Modalidade);
    }

    [Fact]
    public async Task Professor_ja_vinculado_nao_duplica_relacao()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-ja-vinculado");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var existente = await AdicionarProfessorAsync(application, organizacao.Id, unidade.Id, "12345678901");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var busca = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/vincular?termo=Professor"));
        Assert.Contains("Este professor já está vinculado a esta unidade", busca, StringComparison.Ordinal);
        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/novo"));

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/professores/vincular",
            FormVinculoProfessor(token, existente.Professor.Id,
                ModalidadeRemuneracaoProfessor.Mensal, "2000,00"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("já está vinculado", html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(1, await db.ProfessoresUnidades.CountAsync());
        Assert.Equal(1, await db.ProfessoresRemuneracoes.CountAsync());
    }

    [Fact]
    public async Task Vinculo_inativo_e_reativado_e_recebe_nova_remuneracao_no_mesmo_registro()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-reativar-prof");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var existente = await AdicionarProfessorAsync(application, organizacao.Id, unidade.Id, "12345678901");
        await InativarVinculoProfessorAsync(application, existente.Vinculo.Id);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/vincular?professorId={existente.Professor.Id:D}"));
        Assert.Contains("value=\"23/08/2026\"", pagina, StringComparison.Ordinal);
        Assert.Contains("A remuneração anterior terminou em 22/08/2026", pagina, StringComparison.Ordinal);
        Assert.Contains("data-bfa-date-min=\"2026-08-23\"", pagina, StringComparison.Ordinal);
        var token = ObterAntiforgery(pagina);

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/professores/vincular",
            FormVinculoProfessor(token, existente.Professor.Id,
                ModalidadeRemuneracaoProfessor.PorHora, "75,00"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var vinculo = await db.ProfessoresUnidades.SingleAsync();
        var remuneracoes = await db.ProfessoresRemuneracoes
            .OrderBy(item => item.VigenciaInicio)
            .ToArrayAsync();
        Assert.Equal(1, await db.Professores.CountAsync());
        Assert.Equal(existente.Vinculo.Id, vinculo.Id);
        Assert.True(vinculo.Ativo);
        Assert.Equal(2, remuneracoes.Length);
        Assert.Equal(new DateOnly(2026, 1, 1), remuneracoes[0].VigenciaInicio);
        Assert.Equal(new DateOnly(2026, 8, 22), remuneracoes[0].VigenciaFim);
        Assert.Equal(new DateOnly(2026, 8, 23), remuneracoes[1].VigenciaInicio);
        Assert.Null(remuneracoes[1].VigenciaFim);
    }

    [Fact]
    public async Task Reativacao_no_mesmo_dia_do_termino_e_rejeitada_sem_estado_parcial()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-reativar-mesmo-dia");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var existente = await AdicionarProfessorAsync(application, organizacao.Id, unidade.Id, "12345678901");
        await InativarVinculoProfessorAsync(application, existente.Vinculo.Id);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/vincular?professorId={existente.Professor.Id:D}");
        var token = ObterAntiforgery(pagina);

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/professores/vincular",
            FormVinculoProfessor(token, existente.Professor.Id,
                ModalidadeRemuneracaoProfessor.PorHora, "75,00", "22/08/2026"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("A nova remuneração deve iniciar após o término da remuneração anterior (22/08/2026).",
            html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var vinculo = await db.ProfessoresUnidades.SingleAsync();
        var remuneracao = await db.ProfessoresRemuneracoes.SingleAsync();
        Assert.False(vinculo.Ativo);
        Assert.Equal(existente.Vinculo.Id, vinculo.Id);
        Assert.Equal(new DateOnly(2026, 8, 22), remuneracao.VigenciaFim);
    }

    [Fact]
    public async Task Reativacao_depois_da_data_minima_preserva_historico_e_e_aceita()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-reativar-depois");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var existente = await AdicionarProfessorAsync(application, organizacao.Id, unidade.Id, "12345678901");
        await InativarVinculoProfessorAsync(application, existente.Vinculo.Id);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/vincular?professorId={existente.Professor.Id:D}");
        var token = ObterAntiforgery(pagina);

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/professores/vincular",
            FormVinculoProfessor(token, existente.Professor.Id,
                ModalidadeRemuneracaoProfessor.Mensal, "2000,00", "25/08/2026"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var vinculo = await db.ProfessoresUnidades.SingleAsync();
        var remuneracoes = await db.ProfessoresRemuneracoes
            .OrderBy(item => item.VigenciaInicio)
            .ToArrayAsync();
        Assert.True(vinculo.Ativo);
        Assert.Equal(existente.Vinculo.Id, vinculo.Id);
        Assert.Equal(2, remuneracoes.Length);
        Assert.Equal(new DateOnly(2026, 8, 22), remuneracoes[0].VigenciaFim);
        Assert.Equal(new DateOnly(2026, 8, 25), remuneracoes[1].VigenciaInicio);
    }

    [Fact]
    public async Task Professor_inativo_ou_de_outro_tenant_nao_pode_ser_vinculado()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-inativo-prof");
        var outra = await AdicionarOrganizacaoAsync(application, "Outra", "outra-professor");
        var origem = await AdicionarUnidadeAsync(application, organizacao.Id, "Origem");
        var destino = await AdicionarUnidadeAsync(application, organizacao.Id, "Destino");
        var externa = await AdicionarUnidadeAsync(application, outra.Id, "Externa");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, destino.Id, PerfilAcesso.AdministradorUnidade);
        var inativo = await AdicionarProfessorAsync(application, organizacao.Id, origem.Id, "12345678901");
        await InativarProfessorAsync(application, inativo.Professor.Id, inativo.Vinculo.Id);
        var externo = await AdicionarProfessorAsync(application, outra.Id, externa.Id, "98765432100");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var buscaExterna = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{destino.Id:D}/professores/vincular?termo=98765432100"));
        Assert.DoesNotContain("987.654", buscaExterna, StringComparison.Ordinal);
        var token = ObterAntiforgery(await client.GetStringAsync($"/unidade/{destino.Id:D}/professores/novo"));

        using var respostaInativo = await client.PostAsync($"/unidade/{destino.Id:D}/professores/vincular",
            FormVinculoProfessor(token, inativo.Professor.Id, ModalidadeRemuneracaoProfessor.Mensal, "100,00"));
        using var respostaExterno = await client.PostAsync($"/unidade/{destino.Id:D}/professores/vincular",
            FormVinculoProfessor(token, externo.Professor.Id, ModalidadeRemuneracaoProfessor.Mensal, "100,00"));
        var html = WebUtility.HtmlDecode(await respostaInativo.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respostaInativo.StatusCode);
        Assert.Contains("está inativo e não pode ser vinculado", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, respostaExterno.StatusCode);
    }

    [Fact]
    public async Task Cpf_duplicado_no_novo_professor_orienta_para_vinculo_existente()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-orienta-vinculo");
        var origem = await AdicionarUnidadeAsync(application, organizacao.Id, "Origem");
        var destino = await AdicionarUnidadeAsync(application, organizacao.Id, "Destino");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, destino.Id, PerfilAcesso.AdministradorUnidade);
        await AdicionarProfessorAsync(application, organizacao.Id, origem.Id, "12345678901");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync($"/unidade/{destino.Id:D}/professores/novo"));

        using var response = await client.PostAsync($"/unidade/{destino.Id:D}/professores/novo",
            FormProfessor(token, ModalidadeRemuneracaoProfessor.Mensal,
                "123.456.789-01", "1000,00", "22/08/2026"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Este professor já está cadastrado na rede", html, StringComparison.Ordinal);
        Assert.Contains("Vincular professor existente", html, StringComparison.Ordinal);
        Assert.Contains($"/unidade/{destino.Id:D}/professores/vincular", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_edita_professor_compartilhado_e_alteracao_reflete_nas_duas_unidades()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-editar-prof");
        var unidadeA = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA A");
        var unidadeB = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA B");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidadeA.Id, PerfilAcesso.AdministradorUnidade);
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidadeB.Id, PerfilAcesso.AdministradorUnidade);
        var cadastro = await AdicionarProfessorAsync(application, organizacao.Id, unidadeA.Id, "12345678901");
        await AdicionarVinculoProfessorExistenteAsync(application, cadastro.Professor, unidadeB.Id, 800m);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{unidadeA.Id:D}/professores/{cadastro.Professor.Id:D}/editar"));

        using var response = await client.PostAsync(
            $"/unidade/{unidadeA.Id:D}/professores/{cadastro.Professor.Id:D}/editar",
            FormEdicaoProfessor(token, "Professor atualizado"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var listaB = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidadeB.Id:D}/professores"));
        Assert.Contains("Professor atualizado", listaB, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(1, await db.Professores.CountAsync());
        Assert.Equal(2, await db.ProfessoresUnidades.CountAsync());
        var professor = await db.Professores.SingleAsync();
        Assert.Equal("Professor atualizado", professor.NomeCompleto);
        Assert.Equal("(11) 98888-7777", professor.Telefone);
    }

    [Fact]
    public async Task Outro_tenant_ou_administrador_sem_unidade_nao_edita_professor()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-editar-auth");
        var outra = await AdicionarOrganizacaoAsync(application, "Outra", "outra-editar-auth");
        var permitida = await AdicionarUnidadeAsync(application, organizacao.Id, "Permitida");
        var proibida = await AdicionarUnidadeAsync(application, organizacao.Id, "Proibida");
        var externa = await AdicionarUnidadeAsync(application, outra.Id, "Externa");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, permitida.Id, PerfilAcesso.AdministradorUnidade);
        var mesmoTenant = await AdicionarProfessorAsync(application, organizacao.Id, proibida.Id, "12345678901");
        var outroTenant = await AdicionarProfessorAsync(application, outra.Id, externa.Id, "98765432100");
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var semUnidade = await client.GetAsync(
            $"/unidade/{proibida.Id:D}/professores/{mesmoTenant.Professor.Id:D}/editar");
        using var crossTenant = await client.GetAsync(
            $"/unidade/{permitida.Id:D}/professores/{outroTenant.Professor.Id:D}/editar");
        AssertAcessoNegado(semUnidade);
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }

    [Fact]
    public async Task Encerramento_fecha_remuneracao_e_somente_o_vinculo_da_unidade_em_transacao()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-encerrar-prof");
        var unidadeA = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Cerquilho");
        var unidadeB = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Tietê");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidadeA.Id, PerfilAcesso.AdministradorUnidade);
        var cadastro = await AdicionarProfessorAsync(application, organizacao.Id, unidadeA.Id,
            "12345678901", 20m, ModalidadeRemuneracaoProfessor.PorAula);
        var vinculoB = await AdicionarVinculoProfessorExistenteAsync(
            application, cadastro.Professor, unidadeB.Id, 3000m);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidadeA.Id:D}/professores/{cadastro.Professor.Id:D}/encerrar"));
        Assert.Contains("Por aula", pagina, StringComparison.Ordinal);
        var token = ObterAntiforgery(pagina);

        using var response = await client.PostAsync(
            $"/unidade/{unidadeA.Id:D}/professores/{cadastro.Professor.Id:D}/encerrar",
            FormEncerramento(token, "31/08/2026"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var vinculoA = await db.ProfessoresUnidades.SingleAsync(item => item.Id == cadastro.Vinculo.Id);
        var outroVinculo = await db.ProfessoresUnidades.SingleAsync(item => item.Id == vinculoB.Vinculo.Id);
        var remuneracaoA = await db.ProfessoresRemuneracoes.SingleAsync(item => item.Id == cadastro.Remuneracao.Id);
        var remuneracaoB = await db.ProfessoresRemuneracoes.SingleAsync(item => item.Id == vinculoB.Remuneracao.Id);
        var professor = await db.Professores.SingleAsync();
        Assert.False(vinculoA.Ativo);
        Assert.True(outroVinculo.Ativo);
        Assert.True(professor.Ativo);
        Assert.Equal(new DateOnly(2026, 8, 31), remuneracaoA.VigenciaFim);
        Assert.Equal(20m, remuneracaoA.Valor);
        Assert.Null(remuneracaoB.VigenciaFim);
        Assert.Equal(3000m, remuneracaoB.Valor);

        var ativos = WebUtility.HtmlDecode(await client.GetStringAsync($"/unidade/{unidadeA.Id:D}/professores"));
        var encerrados = WebUtility.HtmlDecode(await client.GetStringAsync($"/unidade/{unidadeA.Id:D}/professores?filtro=encerrados"));
        Assert.DoesNotContain("Professor existente", ativos, StringComparison.Ordinal);
        Assert.Contains("Professor existente", encerrados, StringComparison.Ordinal);
        Assert.DoesNotContain("Encerrar vínculo profissional", encerrados, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Data_anterior_ao_inicio_e_rejeitada_sem_alterar_historico()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-data-encerramento");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var cadastro = await AdicionarProfessorAsync(application, organizacao.Id, unidade.Id, "12345678901");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/encerrar"));

        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/encerrar",
            FormEncerramento(token, "31/12/2025"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("não pode ser anterior", html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.True((await db.ProfessoresUnidades.SingleAsync()).Ativo);
        Assert.Null((await db.ProfessoresRemuneracoes.SingleAsync()).VigenciaFim);
    }

    [Fact]
    public async Task Vinculo_ja_encerrado_nao_e_encerrado_novamente()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-ja-encerrado");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var cadastro = await AdicionarProfessorAsync(application, organizacao.Id, unidade.Id, "12345678901");
        await InativarVinculoProfessorAsync(application, cadastro.Vinculo.Id);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/professores/{cadastro.Professor.Id:D}/encerrar"));

        Assert.Contains("Este vínculo já está encerrado", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("Confirmar encerramento", pagina, StringComparison.Ordinal);
    }

    private static FormUrlEncodedContent FormProfessor(
        string token,
        ModalidadeRemuneracaoProfessor modalidade,
        string? cpf,
        string valor,
        string vigencia) => new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["NomeCompleto"] = "Professora Ana Silva",
            ["Cpf"] = cpf ?? string.Empty,
            ["Telefone"] = "(15) 99999-0000",
            ["Email"] = "ana@bfa.test",
            ["Modalidade"] = modalidade.ToString(),
            ["ValorTexto"] = valor,
            ["VigenciaInicioTexto"] = vigencia,
            ["Observacao"] = "Remuneração inicial"
        });

    private static FormUrlEncodedContent FormVinculoProfessor(
        string token,
        Guid professorId,
        ModalidadeRemuneracaoProfessor modalidade,
        string valor,
        string vigencia = "23/08/2026") => new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ProfessorId"] = professorId.ToString(),
            ["Modalidade"] = modalidade.ToString(),
            ["ValorTexto"] = valor,
            ["VigenciaInicioTexto"] = vigencia,
            ["Observacao"] = "Remuneração da nova unidade"
        });

    private static FormUrlEncodedContent FormEdicaoProfessor(
        string token,
        string nome) => new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["NomeCompleto"] = nome,
            ["Cpf"] = "123.456.789-01",
            ["Telefone"] = "(11) 98888-7777",
            ["Email"] = "atualizado@bfa.test"
        });

    private static FormUrlEncodedContent FormEncerramento(
        string token,
        string data) => new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DataEncerramentoTexto"] = data
        });

    private static string NomeModalidadeTeste(ModalidadeRemuneracaoProfessor modalidade) =>
        modalidade switch
        {
            ModalidadeRemuneracaoProfessor.Mensal => "Mensal",
            ModalidadeRemuneracaoProfessor.PorAula => "Por aula",
            ModalidadeRemuneracaoProfessor.PorHora => "Por hora",
            _ => modalidade.ToString()
        };

    private static async Task<(Professor Professor, ProfessorUnidade Vinculo,
        ProfessorRemuneracao Remuneracao)> AdicionarProfessorAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid organizacaoId,
        Guid unidadeId,
        string cpf,
        decimal valor = 1000m,
        ModalidadeRemuneracaoProfessor modalidade = ModalidadeRemuneracaoProfessor.Mensal)
    {
        var professor = new Professor(Guid.NewGuid(), organizacaoId, "Professor existente",
            CriadoEmUtc, cpf: cpf);
        var vinculo = new ProfessorUnidade(Guid.NewGuid(), organizacaoId,
            professor.Id, unidadeId, CriadoEmUtc);
        var remuneracao = new ProfessorRemuneracao(Guid.NewGuid(), organizacaoId,
            vinculo.Id, modalidade, valor,
            new DateOnly(2026, 1, 1), null, application.UsuarioStore.Usuario.Id, CriadoEmUtc);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.Professores.Add(professor);
        db.ProfessoresUnidades.Add(vinculo);
        db.ProfessoresRemuneracoes.Add(remuneracao);
        await db.SaveChangesAsync();
        return (professor, vinculo, remuneracao);
    }

    private static async Task InativarVinculoProfessorAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid vinculoId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var vinculo = await db.ProfessoresUnidades.SingleAsync(item => item.Id == vinculoId);
        var remuneracao = await db.ProfessoresRemuneracoes.SingleAsync(
            item => item.ProfessorUnidadeId == vinculoId && item.VigenciaFim == null);
        remuneracao.Encerrar(new DateOnly(2026, 8, 22));
        vinculo.Desativar(CriadoEmUtc.AddDays(1));
        await db.SaveChangesAsync();
    }

    private static async Task InativarProfessorAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid professorId,
        Guid vinculoId)
    {
        await InativarVinculoProfessorAsync(application, vinculoId);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var professor = await db.Professores.SingleAsync(item => item.Id == professorId);
        professor.Desativar(CriadoEmUtc.AddDays(2));
        await db.SaveChangesAsync();
    }

    private static async Task<(ProfessorUnidade Vinculo, ProfessorRemuneracao Remuneracao)>
        AdicionarVinculoProfessorExistenteAsync(
            AreaUnidadeWebApplicationFactory application,
            Professor professor,
            Guid unidadeId,
            decimal valor)
    {
        var vinculo = new ProfessorUnidade(
            Guid.NewGuid(), professor.OrganizacaoId, professor.Id, unidadeId, CriadoEmUtc);
        var remuneracao = new ProfessorRemuneracao(
            Guid.NewGuid(), professor.OrganizacaoId, vinculo.Id,
            ModalidadeRemuneracaoProfessor.Mensal, valor,
            new DateOnly(2026, 1, 1), null,
            application.UsuarioStore.Usuario.Id, CriadoEmUtc);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.ProfessoresUnidades.Add(vinculo);
        db.ProfessoresRemuneracoes.Add(remuneracao);
        await db.SaveChangesAsync();
        return (vinculo, remuneracao);
    }
}

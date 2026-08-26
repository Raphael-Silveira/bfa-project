using System.Net;
using BFA.Domain.Acessos;
using BFA.Domain.Professores;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AreaUnidadeEndpointTests
{
    [Fact]
    public async Task Administrador_unidade_lista_estado_vazio_e_menu_de_turmas()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-turmas-vazio");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Cerquilho");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas"));

        Assert.Contains("Nenhuma turma cadastrada nesta unidade", html, StringComparison.Ordinal);
        Assert.Contains("Visão Geral", html, StringComparison.Ordinal);
        Assert.Contains("Professores", html, StringComparison.Ordinal);
        Assert.Contains("Turmas", html, StringComparison.Ordinal);
        Assert.Contains("Contrato", html, StringComparison.Ordinal);
        Assert.Contains("bfa-admin-empty-state", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrador_rede_da_organizacao_cria_turma_com_um_horario()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-turmas-rede");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Tietê");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, null, PerfilAcesso.AdministradorRede);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Thalisson");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = await client.GetStringAsync($"/unidade/{unidade.Id:D}/turmas/nova");
        var token = ObterAntiforgery(pagina);

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/turmas/nova",
            FormTurma(token, professor.Vinculo.Id, 12,
                [(DiaSemana.Segunda, "19:00", "20:00", "01/09/2026")]));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        var turma = await db.Turmas.SingleAsync();
        var horario = await db.TurmasHorarios.SingleAsync();
        Assert.Equal(professor.Vinculo.Id, turma.ProfessorUnidadeId);
        Assert.Equal(turma.Id, horario.TurmaId);
        Assert.Equal(turma.ProfessorUnidadeId, horario.ProfessorUnidadeId);
        Assert.Equal(new DateOnly(2026, 9, 1), horario.VigenciaInicio);
    }

    [Fact]
    public async Task Administrador_rede_edita_turma_antes_da_franquia()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-edicao-rede-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Pré-franquia");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, null, PerfilAcesso.AdministradorRede);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor da Rede");
        await AdicionarTurmaHorarioAsync(application, organizacao.Id, unidade.Id,
            professor.Vinculo.Id, "Turma original", new TimeOnly(19, 0), new TimeOnly(20, 0));
        Guid turmaId;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            turmaId = await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
                .Turmas.Select(item => item.Id).SingleAsync();
        }
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/editar");

        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/editar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = ObterAntiforgery(pagina),
                ["Nome"] = "Turma preparada",
                ["Capacidade"] = "16"
            }));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var verificacao = application.Services.CreateAsyncScope();
        var turma = await verificacao.ServiceProvider.GetRequiredService<BfaDbContext>()
            .Turmas.SingleAsync();
        Assert.Equal("Turma preparada", turma.Nome);
        Assert.Equal(16, turma.Capacidade);
    }

    [Fact]
    public async Task Administrador_rede_em_unidade_franqueada_consulta_sem_acoes_e_nao_altera_turmas()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-governanca-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Franqueada");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, null, PerfilAcesso.AdministradorRede);
        var professorAtual = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor atual");
        var professorNovo = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor novo");
        await AdicionarTurmaHorarioAsync(application, organizacao.Id, unidade.Id,
            professorAtual.Vinculo.Id, "Turma protegida", new TimeOnly(19, 0),
            new TimeOnly(20, 0));
        _ = await AdicionarContratoAtivoAsync(application, organizacao.Id, unidade.Id);

        Guid turmaId;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            turmaId = await db.Turmas.Select(item => item.Id).SingleAsync();
        }

        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas"));
        var token = ObterAntiforgery(pagina);

        Assert.Contains("Turma protegida", pagina, StringComparison.Ordinal);
        Assert.Contains("Unidade com operação sob responsabilidade do franqueado", pagina,
            StringComparison.Ordinal);
        Assert.Contains("Somente leitura", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("Nova turma", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("Ajustar horários", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("Trocar professor", pagina, StringComparison.Ordinal);
        Assert.Contains("Voltar à rede", pagina, StringComparison.Ordinal);

        using var criar = await client.PostAsync($"/unidade/{unidade.Id:D}/turmas/nova",
            FormTurma(token, professorAtual.Vinculo.Id, 12,
                [(DiaSemana.Segunda, "20:00", "21:00", "01/09/2026")]));
        using var editar = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/editar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Nome"] = "Alteração indevida",
                ["Capacidade"] = "20"
            }));
        using var horarios = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/horarios",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["NovaVigenciaInicioTexto"] = "01/09/2026",
                ["Horarios[0].DiaSemana"] = ((short)DiaSemana.Segunda).ToString(),
                ["Horarios[0].HoraInicio"] = "20:00",
                ["Horarios[0].HoraFim"] = "21:00"
            }));
        using var trocar = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/professor",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["NovoProfessorUnidadeId"] = professorNovo.Vinculo.Id.ToString(),
                ["DataTrocaTexto"] = "01/09/2026"
            }));

        AssertAcessoNegado(criar);
        AssertAcessoNegado(editar);
        AssertAcessoNegado(horarios);
        AssertAcessoNegado(trocar);
        await using var verificacao = application.Services.CreateAsyncScope();
        var contexto = verificacao.ServiceProvider.GetRequiredService<BfaDbContext>();
        var turma = await contexto.Turmas.SingleAsync();
        Assert.Equal("Turma protegida", turma.Nome);
        Assert.Equal(professorAtual.Vinculo.Id, turma.ProfessorUnidadeId);
        Assert.Single(await contexto.TurmasHorarios.ToArrayAsync());
    }

    [Fact]
    public async Task Administrador_unidade_continua_gerenciando_turma_em_unidade_franqueada()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-admin-local-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Local");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor local");
        _ = await AdicionarContratoAtivoAsync(application, organizacao.Id, unidade.Id);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var pagina = await client.GetStringAsync($"/unidade/{unidade.Id:D}/turmas/nova");

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/turmas/nova",
            FormTurma(ObterAntiforgery(pagina), professor.Vinculo.Id, 12,
                [(DiaSemana.Segunda, "19:00", "20:00", "01/09/2026")]));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
            .Turmas.ToArrayAsync());
    }

    [Fact]
    public async Task Criacao_com_varios_horarios_e_edicao_preservam_historico()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-turmas-varios");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professora Ana");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/nova"));
        using var criada = await client.PostAsync($"/unidade/{unidade.Id:D}/turmas/nova",
            FormTurma(token, professor.Vinculo.Id, 12,
                [(DiaSemana.Segunda, "19:00", "20:00", "01/09/2026"),
                 (DiaSemana.Quarta, "19:00", "20:00", "01/09/2026")]));
        Assert.Equal(HttpStatusCode.Found, criada.StatusCode);

        Guid turmaId;
        Guid[] horarioIds;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            turmaId = (await db.Turmas.SingleAsync()).Id;
            horarioIds = await db.TurmasHorarios.OrderBy(item => item.Id)
                .Select(item => item.Id).ToArrayAsync();
        }
        var paginaEdicao = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/editar"));
        Assert.Contains("Professora Ana", paginaEdicao, StringComparison.Ordinal);
        Assert.Contains("Segunda", paginaEdicao, StringComparison.Ordinal);
        var tokenEdicao = ObterAntiforgery(paginaEdicao);
        using var editada = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/editar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = tokenEdicao,
                ["Nome"] = "Iniciante Noite",
                ["Capacidade"] = "14"
            }));
        Assert.Equal(HttpStatusCode.Found, editada.StatusCode);

        await using var verificacao = application.Services.CreateAsyncScope();
        var contexto = verificacao.ServiceProvider.GetRequiredService<BfaDbContext>();
        var turma = await contexto.Turmas.SingleAsync();
        Assert.Equal("Iniciante Noite", turma.Nome);
        Assert.Equal(14, turma.Capacidade);
        Assert.Equal(horarioIds, await contexto.TurmasHorarios.OrderBy(item => item.Id)
            .Select(item => item.Id).ToArrayAsync());
        Assert.Equal(2, await contexto.TurmasHorarios.CountAsync());
    }

    [Fact]
    public async Task Outro_tenant_e_admin_sem_vinculo_nao_acessam_turmas()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-turmas-auth");
        var outra = await AdicionarOrganizacaoAsync(application, "Outra", "outra-turmas-auth");
        var permitida = await AdicionarUnidadeAsync(application, organizacao.Id, "Permitida");
        var semAcesso = await AdicionarUnidadeAsync(application, organizacao.Id, "Sem acesso");
        var externa = await AdicionarUnidadeAsync(application, outra.Id, "Externa");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, permitida.Id, PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var mesmaOrganizacao = await client.GetAsync($"/unidade/{semAcesso.Id:D}/turmas");
        using var outroTenant = await client.GetAsync($"/unidade/{externa.Id:D}/turmas");
        AssertAcessoNegado(mesmaOrganizacao);
        AssertAcessoNegado(outroTenant);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Professor_de_outra_unidade_ou_inativo_e_rejeitado(
        bool mesmaUnidade, bool ativo)
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", $"bfa-prof-turma-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "Destino");
        var origem = mesmaUnidade ? unidade : await AdicionarUnidadeAsync(application, organizacao.Id, "Origem");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, origem.Id, "Professor inválido", ativo);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/nova"));

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/turmas/nova",
            FormTurma(token, professor.Vinculo.Id, 10,
                [(DiaSemana.Segunda, "19:00", "20:00", "01/09/2026")]));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("professor ativo vinculado a esta unidade", html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<BfaDbContext>().Turmas.ToArrayAsync());
    }

    [Theory]
    [InlineData(0, "19:00", "20:00", true)]
    [InlineData(12, "20:00", "19:00", true)]
    [InlineData(12, "hora", "20:00", true)]
    [InlineData(12, "19:00", "20:00", false)]
    public async Task Capacidade_horario_ou_ausencia_de_horario_invalidos_nao_criam_turma(
        int capacidade, string inicio, string fim, bool incluirHorario)
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", $"bfa-turma-invalida-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(application, organizacao.Id, unidade.Id, "Professor");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync($"/unidade/{unidade.Id:D}/turmas/nova"));
        var horarios = incluirHorario
            ? new[] { (DiaSemana.Segunda, inicio, fim, "01/09/2026") }
            : [];

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/turmas/nova",
            FormTurma(token, professor.Vinculo.Id, capacidade, horarios));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Empty(await db.Turmas.ToArrayAsync());
        Assert.Empty(await db.TurmasHorarios.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Conflito_do_professor_na_mesma_ou_outra_unidade_e_rejeitado_amigavelmente(
        bool outraUnidade)
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", $"bfa-conflito-{Guid.NewGuid():N}");
        var destino = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Destino");
        var origem = outraUnidade
            ? await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Origem")
            : destino;
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, destino.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(application, organizacao.Id, origem.Id, "Thalisson");
        var vinculoDestino = outraUnidade
            ? await AdicionarVinculoProfessorTurmaAsync(application, professor.Professor, destino.Id)
            : professor.Vinculo;
        await AdicionarTurmaHorarioAsync(application, organizacao.Id, origem.Id,
            professor.Vinculo.Id, "Turma existente", new TimeOnly(19, 0), new TimeOnly(20, 0));
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync($"/unidade/{destino.Id:D}/turmas/nova"));

        using var response = await client.PostAsync($"/unidade/{destino.Id:D}/turmas/nova",
            FormTurma(token, vinculoDestino.Id, 12,
                [(DiaSemana.Segunda, "19:30", "20:30", "01/09/2026")]));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("já possui outra turma nesse horário", html, StringComparison.Ordinal);
        Assert.Contains("Turma existente", html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<BfaDbContext>().Turmas.CountAsync());
    }

    [Fact]
    public async Task Horario_adjacente_e_aceito()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-adjacente");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(application, organizacao.Id, unidade.Id, "Thalisson");
        await AdicionarTurmaHorarioAsync(application, organizacao.Id, unidade.Id,
            professor.Vinculo.Id, "Turma 19h", new TimeOnly(19, 0), new TimeOnly(20, 0));
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync($"/unidade/{unidade.Id:D}/turmas/nova"));

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/turmas/nova",
            FormTurma(token, professor.Vinculo.Id, 12,
                [(DiaSemana.Segunda, "20:00", "21:00", "01/09/2026")]));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(2, await db.Turmas.CountAsync());
        Assert.Equal(2, await db.TurmasHorarios.CountAsync());
    }

    [Fact]
    public async Task Segundo_horario_invalido_impede_criacao_da_turma_inteira()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(application, "BFA", "bfa-rollback-turma");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Unidade");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor");
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = ObterAntiforgery(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/nova"));

        using var response = await client.PostAsync($"/unidade/{unidade.Id:D}/turmas/nova",
            FormTurma(token, professor.Vinculo.Id, 12,
                [(DiaSemana.Segunda, "19:00", "20:00", "01/09/2026"),
                 (DiaSemana.Quarta, "21:00", "20:00", "01/09/2026")]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Empty(await db.Turmas.ToArrayAsync());
        Assert.Empty(await db.TurmasHorarios.ToArrayAsync());
    }

    private static FormUrlEncodedContent FormTurma(
        string token, Guid professorUnidadeId, int capacidade,
        IReadOnlyList<(DiaSemana Dia, string Inicio, string Fim, string Vigencia)> horarios)
    {
        var dados = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Nome", "Iniciante Noite"),
            new("Capacidade", capacidade.ToString()),
            new("ProfessorUnidadeId", professorUnidadeId.ToString())
        };
        for (var indice = 0; indice < horarios.Count; indice++)
        {
            dados.Add(new($"Horarios[{indice}].DiaSemana", ((short)horarios[indice].Dia).ToString()));
            dados.Add(new($"Horarios[{indice}].HoraInicio", horarios[indice].Inicio));
            dados.Add(new($"Horarios[{indice}].HoraFim", horarios[indice].Fim));
            dados.Add(new($"Horarios[{indice}].VigenciaInicioTexto", horarios[indice].Vigencia));
        }
        return new(dados);
    }

    private static async Task<(Professor Professor, ProfessorUnidade Vinculo)>
        AdicionarProfessorTurmaAsync(
            AreaUnidadeWebApplicationFactory application,
            Guid organizacaoId, Guid unidadeId, string nome, bool ativo = true)
    {
        var professor = new Professor(Guid.NewGuid(), organizacaoId, nome, CriadoEmUtc);
        var vinculo = new ProfessorUnidade(
            Guid.NewGuid(), organizacaoId, professor.Id, unidadeId, CriadoEmUtc);
        if (!ativo) vinculo.Desativar(CriadoEmUtc.AddHours(1));
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.Professores.Add(professor);
        db.ProfessoresUnidades.Add(vinculo);
        await db.SaveChangesAsync();
        return (professor, vinculo);
    }

    private static async Task<ProfessorUnidade> AdicionarVinculoProfessorTurmaAsync(
        AreaUnidadeWebApplicationFactory application, Professor professor, Guid unidadeId)
    {
        var vinculo = new ProfessorUnidade(
            Guid.NewGuid(), professor.OrganizacaoId, professor.Id, unidadeId, CriadoEmUtc);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.ProfessoresUnidades.Add(vinculo);
        await db.SaveChangesAsync();
        return vinculo;
    }

    private static async Task AdicionarTurmaHorarioAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId,
        string nome, TimeOnly inicio, TimeOnly fim)
    {
        var turma = new Turma(Guid.NewGuid(), organizacaoId, unidadeId,
            professorUnidadeId, nome, 12, application.UsuarioStore.Usuario.Id, CriadoEmUtc);
        var horario = new TurmaHorario(Guid.NewGuid(), organizacaoId, unidadeId,
            turma.Id, professorUnidadeId, DiaSemana.Segunda, inicio, fim,
            new DateOnly(2026, 1, 1), null,
            application.UsuarioStore.Usuario.Id, CriadoEmUtc);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.Turmas.Add(turma);
        db.TurmasHorarios.Add(horario);
        await db.SaveChangesAsync();
    }
}

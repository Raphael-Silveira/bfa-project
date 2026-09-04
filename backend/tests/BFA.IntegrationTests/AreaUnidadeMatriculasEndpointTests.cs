using System.Net;
using BFA.Domain.Acessos;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Planos;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AreaUnidadeEndpointTests
{
    [Fact]
    public async Task Administrador_unidade_lista_somente_matriculas_da_unidade_com_menu_e_formatacao()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-matriculas-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Cerquilho");
        var outraUnidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Tietê");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        await AdicionarMatriculaAsync(application, organizacao.Id, unidade.Id,
            "João da Silva", StatusMatricula.Ativa);
        await AdicionarMatriculaAsync(application, organizacao.Id, outraUnidade.Id,
            "Aluno de Tietê", StatusMatricula.Ativa);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var response = await client.GetAsync($"/unidade/{unidade.Id:D}/matriculas");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Matrículas", html, StringComparison.Ordinal);
        Assert.Contains("João da Silva", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Aluno de Tietê", html, StringComparison.Ordinal);
        Assert.Contains("Plano BFA 2x", html, StringComparison.Ordinal);
        Assert.Contains("R$ 270,00", NormalizarEspacos(html), StringComparison.Ordinal);
        Assert.Contains("10/09/2026", html, StringComparison.Ordinal);
        Assert.Contains("2x por semana", html, StringComparison.Ordinal);
        Assert.Contains("0 horários", html, StringComparison.Ordinal);
        Assert.Contains($"/unidade/{unidade.Id:D}/matriculas", html,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/unidade/unidade/", html,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+ Nova matrícula", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Ativa", "Aluno Ativo", "Aluno Encerrado", "Aluno Cancelado")]
    [InlineData("Encerrada", "Aluno Encerrado", "Aluno Ativo", "Aluno Cancelado")]
    [InlineData("Cancelada", "Aluno Cancelado", "Aluno Ativo", "Aluno Encerrado")]
    public async Task Filtro_status_retorna_somente_matriculas_correspondentes(
        string status, string esperado, string ausenteUm, string ausenteDois)
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarCenarioDeStatusAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas?status={status}"));

        Assert.Contains(esperado, html, StringComparison.Ordinal);
        Assert.DoesNotContain(ausenteUm, html, StringComparison.Ordinal);
        Assert.DoesNotContain(ausenteDois, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Busca_por_nome_filtra_sem_expor_cpf_email_ou_telefone_na_url()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarCenarioDeStatusAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var url = $"/unidade/{cenario.UnidadeId:D}/matriculas?texto=Encerrado";

        var html = WebUtility.HtmlDecode(await client.GetStringAsync(url));

        Assert.Contains("Aluno Encerrado", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Aluno Ativo", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cpf=", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email=", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("telefone=", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"texto\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"status\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"cpf\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Estado_vazio_distingue_unidade_sem_matriculas_de_filtro_sem_resultado()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-vazio-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Vazia");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var vazio = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/matriculas"));
        var filtrado = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/matriculas?texto=ninguém"));

        Assert.Contains("Nenhuma matrícula encontrada.", vazio, StringComparison.Ordinal);
        Assert.Contains("Nenhuma matrícula corresponde aos filtros informados.",
            filtrado, StringComparison.Ordinal);
        Assert.Contains("+ Nova matrícula", vazio, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Administrador_unidade_de_outra_unidade_e_professor_nao_acessam_matriculas()
    {
        using var adminApplication = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            adminApplication, "BFA", $"bfa-auth-matriculas-{Guid.NewGuid():N}");
        var permitida = await AdicionarUnidadeAsync(adminApplication, organizacao.Id, "Permitida");
        var restrita = await AdicionarUnidadeAsync(adminApplication, organizacao.Id, "Restrita");
        await AdicionarVinculoAsync(adminApplication,
            adminApplication.UsuarioStore.Usuario.Id, organizacao.Id, permitida.Id,
            PerfilAcesso.AdministradorUnidade);
        using var adminClient = CreateClient(adminApplication);
        await LoginAsync(adminClient, adminApplication);

        using var respostaRestrita = await adminClient.GetAsync(
            $"/unidade/{restrita.Id:D}/matriculas");
        AssertAcessoNegado(respostaRestrita);

        using var professorApplication = new AreaUnidadeWebApplicationFactory();
        var orgProfessor = await AdicionarOrganizacaoAsync(
            professorApplication, "BFA", $"bfa-prof-matriculas-{Guid.NewGuid():N}");
        var unidadeProfessor = await AdicionarUnidadeAsync(
            professorApplication, orgProfessor.Id, "BFA Professor");
        await AdicionarVinculoAsync(professorApplication,
            professorApplication.UsuarioStore.Usuario.Id, orgProfessor.Id,
            unidadeProfessor.Id, PerfilAcesso.Professor);
        using var professorClient = CreateClient(professorApplication);
        await LoginAsync(professorClient, professorApplication);

        using var respostaProfessor = await professorClient.GetAsync(
            $"/unidade/{unidadeProfessor.Id:D}/matriculas");
        AssertAcessoNegado(respostaProfessor);
    }

    [Fact]
    public async Task Administrador_rede_consulta_unidade_franqueada_em_somente_leitura()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-rede-matriculas-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Franqueada");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, null, PerfilAcesso.AdministradorRede);
        var matricula = await AdicionarMatriculaAsync(application, organizacao.Id,
            unidade.Id, "Aluno da Franquia", StatusMatricula.Ativa);
        _ = await AdicionarContratoAtivoAsync(application, organizacao.Id, unidade.Id);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var lista = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/matriculas"));
        var detalhe = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/matriculas/{matricula.MatriculaId:D}"));

        Assert.Contains("Aluno da Franquia", lista, StringComparison.Ordinal);
        Assert.Contains("Somente leitura", lista, StringComparison.Ordinal);
        Assert.Contains("Somente leitura", detalhe, StringComparison.Ordinal);
        Assert.DoesNotContain("Nova matrícula", lista, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Alterar Grade", detalhe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Encerrar matrícula", detalhe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cancelar matrícula", detalhe, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Detalhe_exibe_aluno_responsaveis_condicoes_grade_e_professor_historico()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarDetalheCompletoAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/{cenario.MatriculaId:D}"));
        var htmlEspacos = NormalizarEspacos(html);

        Assert.Contains("João da Silva", html, StringComparison.Ordinal);
        Assert.Contains("15/04/2004", html, StringComparison.Ordinal);
        Assert.Contains("***.***.***-09", html, StringComparison.Ordinal);
        Assert.DoesNotContain("12345678909", html, StringComparison.Ordinal);
        Assert.Contains("(15) 99999-0000", html, StringComparison.Ordinal);
        Assert.Contains("joao@bfa.test", html, StringComparison.Ordinal);
        Assert.Contains("Maria da Silva", html, StringComparison.Ordinal);
        Assert.Contains("Mãe", html, StringComparison.Ordinal);
        Assert.Contains("Principal contato", html, StringComparison.Ordinal);
        Assert.Contains("Responsável financeiro", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Pagador", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R$ 270,00", htmlEspacos, StringComparison.Ordinal);
        Assert.Contains("R$ 300,00", htmlEspacos, StringComparison.Ordinal);
        Assert.Contains("R$ 100,00", htmlEspacos, StringComparison.Ordinal);
        Assert.Contains("2x por semana", html, StringComparison.Ordinal);
        Assert.Contains("Grade atual", html, StringComparison.Ordinal);
        Assert.Contains("Segunda-feira", html, StringComparison.Ordinal);
        Assert.Contains("19:00 às 20:00", html, StringComparison.Ordinal);
        Assert.Contains("Professor Atual", html, StringComparison.Ordinal);
        Assert.Contains("Histórico da Grade", html, StringComparison.Ordinal);
        Assert.Contains("01/01/2026 a 31/08/2026", html, StringComparison.Ordinal);
        var inicioHistorico = html.IndexOf("Histórico da Grade", StringComparison.Ordinal);
        Assert.True(inicioHistorico >= 0);
        Assert.Contains("Professor Histórico", html[inicioHistorico..], StringComparison.Ordinal);
        Assert.Contains($"/matriculas/{cenario.MatriculaId:D}", html,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Detalhe_terminal_destaca_status_data_real_e_nao_inventa_grade_atual()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-terminal-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Terminal");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var matricula = await AdicionarMatriculaAsync(application, organizacao.Id,
            unidade.Id, "Aluno Cancelado", StatusMatricula.Cancelada);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/matriculas/{matricula.MatriculaId:D}"));

        Assert.Contains("Cancelada", html, StringComparison.Ordinal);
        Assert.Contains("Data final real", html, StringComparison.Ordinal);
        Assert.Contains("31/12/2026", html, StringComparison.Ordinal);
        Assert.Contains("Nenhum horário vigente nesta matrícula.", html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detalhe_ativo_exibe_acoes_operacionais_e_exige_antiforgery()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarDetalheCompletoAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var url = $"/unidade/{cenario.UnidadeId:D}/matriculas/{cenario.MatriculaId:D}";
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(url));

        Assert.Contains("Alterar Grade", html, StringComparison.Ordinal);
        Assert.Contains("Encerrar matrícula", html, StringComparison.Ordinal);
        Assert.Contains("Cancelar matrícula", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(
            $"{url}/alterar-grade")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(
            $"{url}/encerrar")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(
            $"{url}/cancelar")).StatusCode);

        using var semToken = await client.PostAsync(
            $"{url}/encerrar", new FormUrlEncodedContent(
                new Dictionary<string, string> { ["DataFinalTexto"] = "30/09/2026" }));
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);
    }

    [Fact]
    public async Task Alterar_grade_e_cancelar_usam_prg_e_preservam_historico()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarDetalheCompletoAsync(application);
        Guid horarioId;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            horarioId = await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
                .MatriculasHorarios
                .Where(item => item.MatriculaId == cenario.MatriculaId
                    && item.VigenciaFim == null)
                .Select(item => item.TurmaHorarioId)
                .SingleAsync();
        }

        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var gradeUrl = $"/unidade/{cenario.UnidadeId:D}/matriculas/{cenario.MatriculaId:D}/alterar-grade";
        var gradePage = await client.GetStringAsync(gradeUrl);
        Assert.Contains("data-bfa-matricula-grade", gradePage, StringComparison.Ordinal);
        Assert.Contains("name=\"TurmaHorarioIds\"", gradePage, StringComparison.Ordinal);
        Assert.Contains($"value=\"{horarioId:D}\"", gradePage, StringComparison.Ordinal);
        Assert.Contains("checked=\"checked\"", gradePage, StringComparison.Ordinal);
        Assert.Contains("data-day=", gradePage, StringComparison.Ordinal);
        Assert.Contains("data-start=", gradePage, StringComparison.Ordinal);
        Assert.Contains("data-end=", gradePage, StringComparison.Ordinal);
        var gradeForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ObterAntiforgery(gradePage),
            ["DataInicioTexto"] = "01/09/2026",
            ["TurmaHorarioIds"] = horarioId.ToString()
        });
        using var gradeResponse = await client.PostAsync(gradeUrl, gradeForm);
        Assert.Equal(HttpStatusCode.Found, gradeResponse.StatusCode);

        var cancelarUrl = $"/unidade/{cenario.UnidadeId:D}/matriculas/{cenario.MatriculaId:D}/cancelar";
        var cancelarPage = await client.GetStringAsync(cancelarUrl);
        using var cancelarResponse = await client.PostAsync(cancelarUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = ObterAntiforgery(cancelarPage),
                ["DataFinalTexto"] = "30/09/2026"
            }));
        Assert.Equal(HttpStatusCode.Found, cancelarResponse.StatusCode);
        var detalhe = WebUtility.HtmlDecode(await client.GetStringAsync(
            cancelarResponse.Headers.Location!.OriginalString));
        Assert.Contains("Cancelada", detalhe, StringComparison.Ordinal);
        Assert.Contains("Histórico da Grade", detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detalhe_cross_unit_e_inexistente_retornam_not_found_sem_revelar_tenant()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-cross-matriculas-{Guid.NewGuid():N}");
        var cerquilho = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Cerquilho");
        var tiete = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Tietê");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, cerquilho.Id, PerfilAcesso.AdministradorUnidade);
        var matriculaTiete = await AdicionarMatriculaAsync(application, organizacao.Id,
            tiete.Id, "Aluno de Tietê", StatusMatricula.Ativa);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var crossUnit = await client.GetAsync(
            $"/unidade/{cerquilho.Id:D}/matriculas/{matriculaTiete.MatriculaId:D}");
        using var inexistente = await client.GetAsync(
            $"/unidade/{cerquilho.Id:D}/matriculas/{Guid.NewGuid():D}");
        var corpo = await crossUnit.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, crossUnit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.DoesNotContain("Tietê", corpo, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<CenarioMatriculaTeste> AdicionarCenarioDeStatusAsync(
        AreaUnidadeWebApplicationFactory application)
    {
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-status-matriculas-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Status");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        await AdicionarMatriculaAsync(application, organizacao.Id, unidade.Id,
            "Aluno Ativo", StatusMatricula.Ativa);
        await AdicionarMatriculaAsync(application, organizacao.Id, unidade.Id,
            "Aluno Encerrado", StatusMatricula.Encerrada);
        await AdicionarMatriculaAsync(application, organizacao.Id, unidade.Id,
            "Aluno Cancelado", StatusMatricula.Cancelada);
        return new(unidade.Id, Guid.Empty);
    }

    private static async Task<CenarioMatriculaTeste> AdicionarMatriculaAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid organizacaoId,
        Guid unidadeId,
        string nomeAluno,
        StatusMatricula status)
    {
        var usuarioId = application.UsuarioStore.Usuario.Id;
        var plano = new Plano(Guid.NewGuid(), organizacaoId, unidadeId,
            "Plano BFA 2x", usuarioId, CriadoEmUtc);
        var versao = new PlanoVersao(Guid.NewGuid(), organizacaoId, plano.Id,
            1, 12, 2, 300m, true, 100m,
            new DateOnly(2026, 1, 1), null, usuarioId, CriadoEmUtc);
        var aluno = new Aluno(Guid.NewGuid(), organizacaoId, nomeAluno,
            new DateOnly(2000, 1, 1), new DateOnly(2026, 9, 1), CriadoEmUtc);
        var matricula = new Matricula(Guid.NewGuid(), organizacaoId, unidadeId,
            aluno.Id, versao.Id, new DateOnly(2026, 9, 10), 12,
            270m, true, 100m, usuarioId, CriadoEmUtc);
        if (status == StatusMatricula.Encerrada)
            matricula.Encerrar(new DateOnly(2026, 12, 31), usuarioId, CriadoEmUtc.AddHours(1));
        if (status == StatusMatricula.Cancelada)
            matricula.Cancelar(new DateOnly(2026, 12, 31), usuarioId, CriadoEmUtc.AddHours(1));

        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.AddRange(plano, versao, aluno, matricula);
        await db.SaveChangesAsync();
        return new(unidadeId, matricula.Id);
    }

    private static async Task<CenarioMatriculaTeste> AdicionarDetalheCompletoAsync(
        AreaUnidadeWebApplicationFactory application)
    {
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-detalhe-matricula-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(application, organizacao.Id, "BFA Detalhe");
        await AdicionarVinculoAsync(application, application.UsuarioStore.Usuario.Id,
            organizacao.Id, unidade.Id, PerfilAcesso.AdministradorUnidade);
        var professorHistorico = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor Histórico");
        var professorAtual = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor Atual");
        var usuarioId = application.UsuarioStore.Usuario.Id;
        var plano = new Plano(Guid.NewGuid(), organizacao.Id, unidade.Id,
            "Plano BFA 2x", usuarioId, CriadoEmUtc);
        var versao = new PlanoVersao(Guid.NewGuid(), organizacao.Id, plano.Id,
            3, 12, 2, 300m, true, 100m,
            new DateOnly(2026, 1, 1), null, usuarioId, CriadoEmUtc);
        var aluno = new Aluno(Guid.NewGuid(), organizacao.Id, "João da Silva",
            new DateOnly(2004, 4, 15), new DateOnly(2026, 9, 1), CriadoEmUtc,
            cpf: "12345678909", telefone: "(15) 99999-0000", email: "joao@bfa.test");
        var responsavel = new Responsavel(Guid.NewGuid(), organizacao.Id,
            "Maria da Silva", CriadoEmUtc, telefone: "(15) 98888-0000",
            email: "maria@bfa.test");
        var alunoResponsavel = new AlunoResponsavel(Guid.NewGuid(), organizacao.Id,
            aluno.Id, responsavel.Id, TipoRelacaoResponsavel.Mae,
            principalContato: true, responsavelFinanceiro: true, CriadoEmUtc);
        var matricula = new Matricula(Guid.NewGuid(), organizacao.Id, unidade.Id,
            aluno.Id, versao.Id, new DateOnly(2026, 1, 1), 12,
            270m, true, 100m, usuarioId, CriadoEmUtc);
        var turma = new Turma(Guid.NewGuid(), organizacao.Id, unidade.Id,
            professorAtual.Vinculo.Id, "Intermediário A", 12, usuarioId, CriadoEmUtc);
        var horarioHistorico = new TurmaHorario(Guid.NewGuid(), organizacao.Id, unidade.Id,
            turma.Id, professorHistorico.Vinculo.Id, DiaSemana.Segunda,
            new TimeOnly(19, 0), new TimeOnly(20, 0), new DateOnly(2026, 1, 1),
            null, usuarioId, CriadoEmUtc);
        horarioHistorico.Encerrar(
            new DateOnly(2026, 8, 31), usuarioId, CriadoEmUtc.AddHours(1));
        var horarioAtual = new TurmaHorario(Guid.NewGuid(), organizacao.Id, unidade.Id,
            turma.Id, professorAtual.Vinculo.Id, DiaSemana.Segunda,
            new TimeOnly(19, 0), new TimeOnly(20, 0), new DateOnly(2026, 9, 1),
            null, usuarioId, CriadoEmUtc);
        var gradeHistorica = new MatriculaHorario(Guid.NewGuid(), organizacao.Id,
            unidade.Id, matricula.Id, horarioHistorico.Id, new DateOnly(2026, 1, 1),
            usuarioId, CriadoEmUtc);
        gradeHistorica.Encerrar(
            new DateOnly(2026, 8, 31), usuarioId, CriadoEmUtc.AddHours(1));
        var gradeAtual = new MatriculaHorario(Guid.NewGuid(), organizacao.Id,
            unidade.Id, matricula.Id, horarioAtual.Id, new DateOnly(2026, 9, 1),
            usuarioId, CriadoEmUtc);

        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.AddRange(plano, versao, aluno, responsavel, alunoResponsavel, matricula,
            turma, horarioHistorico, horarioAtual, gradeHistorica, gradeAtual);
        await db.SaveChangesAsync();
        return new(unidade.Id, matricula.Id);
    }

    private static string NormalizarEspacos(string valor) =>
        valor.Replace('\u00a0', ' ');

    private sealed record CenarioMatriculaTeste(Guid UnidadeId, Guid MatriculaId);
}

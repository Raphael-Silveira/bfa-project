using System.Net;
using System.Text.Json;
using BFA.Domain.Acessos;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Planos;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AreaUnidadeEndpointTests
{
    [Fact]
    public async Task Administrador_unidade_ve_botao_e_wizard_sem_persistencia_intermediaria()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var lista = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas"));
        using var resposta = await client.GetAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova");
        var html = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("+ Nova matrícula", lista, StringComparison.Ordinal);
        Assert.Contains($"action=\"/unidade/{cenario.UnidadeId:D}/matriculas/nova\"",
            html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("method=\"post\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5, Contar(html, "data-step-indicator="));
        Assert.Contains("Aluno", html, StringComparison.Ordinal);
        Assert.Contains("Responsáveis", html, StringComparison.Ordinal);
        Assert.Contains("Plano", html, StringComparison.Ordinal);
        Assert.Contains("Grade", html, StringComparison.Ordinal);
        Assert.Contains("Revisar", html, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", html,
            StringComparison.Ordinal);
        Assert.Contains("bfa-matricula-wizard.js", html, StringComparison.Ordinal);
        Assert.Contains("data-plan-price=\"300,00\"", html, StringComparison.Ordinal);
        Assert.Contains("data-plan-fee-enabled=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("Cobrar taxa de matrícula", html, StringComparison.Ordinal);
        Assert.Contains("Confirmar matrícula", html, StringComparison.Ordinal);
        Assert.Contains("Criando matrícula...", html, StringComparison.Ordinal);
        Assert.Contains("data-review-student", html, StringComparison.Ordinal);
        Assert.Contains("data-review-guardians", html, StringComparison.Ordinal);
        Assert.Contains("data-review-plan", html, StringComparison.Ordinal);
        Assert.Contains("data-review-schedule", html, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionStorage", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/unidade/unidade/", html, StringComparison.OrdinalIgnoreCase);

        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Empty(await db.Matriculas.ToListAsync());
    }

    [Fact]
    public async Task Governanca_da_nova_matricula_respeita_rede_franquia_professor_e_cross_unit()
    {
        using var redeLivre = new AreaUnidadeWebApplicationFactory();
        var livre = await AdicionarBaseNovaMatriculaAsync(
            redeLivre, PerfilAcesso.AdministradorRede, vinculoOrganizacional: true);
        using var clientLivre = CreateClient(redeLivre);
        await LoginAsync(clientLivre, redeLivre);
        var listaLivre = await clientLivre.GetStringAsync(
            $"/unidade/{livre.UnidadeId:D}/matriculas");
        using var novaLivre = await clientLivre.GetAsync(
            $"/unidade/{livre.UnidadeId:D}/matriculas/nova");
        Assert.Contains("Nova matrícula", listaLivre, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, novaLivre.StatusCode);

        using var redeFranqueada = new AreaUnidadeWebApplicationFactory();
        var franqueada = await AdicionarBaseNovaMatriculaAsync(
            redeFranqueada, PerfilAcesso.AdministradorRede, vinculoOrganizacional: true);
        _ = await AdicionarContratoAtivoAsync(
            redeFranqueada, franqueada.OrganizacaoId, franqueada.UnidadeId);
        using var clientFranqueada = CreateClient(redeFranqueada);
        await LoginAsync(clientFranqueada, redeFranqueada);
        var listaFranqueada = await clientFranqueada.GetStringAsync(
            $"/unidade/{franqueada.UnidadeId:D}/matriculas");
        using var novaFranqueada = await clientFranqueada.GetAsync(
            $"/unidade/{franqueada.UnidadeId:D}/matriculas/nova");
        Assert.DoesNotContain("Nova matrícula", listaFranqueada,
            StringComparison.OrdinalIgnoreCase);
        AssertAcessoNegado(novaFranqueada);

        using var professor = new AreaUnidadeWebApplicationFactory();
        var contextoProfessor = await AdicionarBaseNovaMatriculaAsync(
            professor, PerfilAcesso.Professor);
        using var clientProfessor = CreateClient(professor);
        await LoginAsync(clientProfessor, professor);
        using var novaProfessor = await clientProfessor.GetAsync(
            $"/unidade/{contextoProfessor.UnidadeId:D}/matriculas/nova");
        AssertAcessoNegado(novaProfessor);

        using var cross = new AreaUnidadeWebApplicationFactory();
        var permitida = await AdicionarBaseNovaMatriculaAsync(cross);
        var outra = await AdicionarUnidadeAsync(
            cross, permitida.OrganizacaoId, "BFA Outra");
        using var clientCross = CreateClient(cross);
        await LoginAsync(clientCross, cross);
        using var novaCross = await clientCross.GetAsync(
            $"/unidade/{outra.Id:D}/matriculas/nova");
        AssertAcessoNegado(novaCross);
    }

    [Fact]
    public async Task Busca_de_aluno_existente_e_tenant_safe_e_exibe_responsaveis_vinculados()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        var outra = await AdicionarUnidadeAsync(
            application, cenario.OrganizacaoId, "BFA Externa");
        await AdicionarMatriculaAsync(application, cenario.OrganizacaoId,
            cenario.UnidadeId, "Aluno da Unidade", StatusMatricula.Encerrada);
        await AdicionarMatriculaAsync(application, cenario.OrganizacaoId,
            outra.Id, "Aluno de Outra Unidade", StatusMatricula.Encerrada);
        await AdicionarAlunoComResponsavelAsync(
            application, cenario.OrganizacaoId, cenario.UnidadeId);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova"));

        Assert.Contains("Aluno da Unidade", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Aluno de Outra Unidade", html, StringComparison.Ordinal);
        Assert.Contains("Buscar por nome", html, StringComparison.Ordinal);
        Assert.Contains("Maria Responsável", html, StringComparison.Ordinal);
        Assert.Contains("Principal contato", html, StringComparison.Ordinal);
        Assert.Contains("Responsável financeiro", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Pagador", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"cpf\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Consulta_mostra_somente_planos_locais_e_da_rede_disponiveis_e_vigentes()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        await AdicionarCatalogoPlanosAsync(application, cenario);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var json = await client.GetStringAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova/planos?dataInicio=02%2F09%2F2026");
        using var documento = JsonDocument.Parse(json);
        var planos = documento.RootElement.GetProperty("planos")
            .EnumerateArray().ToArray();
        var nomes = planos.Select(item => item.GetProperty("nome").GetString()).ToArray();
        var escopos = planos.Select(item => item.GetProperty("escopo").GetString()).ToArray();

        Assert.Contains("Plano Local Elegível", nomes);
        Assert.Contains("Plano Rede Disponível", nomes);
        Assert.Contains("Plano local", escopos);
        Assert.Contains("Plano da Rede", escopos);
        Assert.DoesNotContain("Plano Rede Indisponível", nomes);
        Assert.DoesNotContain("Plano Inativo", nomes);
        Assert.DoesNotContain("Plano Histórico", nomes);
    }

    [Fact]
    public async Task Consulta_de_grade_exibe_ocupacao_e_lotacao_com_periodo_recalculado()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application, capacidade: 1);
        await OcuparHorarioAsync(application, cenario);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        var json = await client.GetStringAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova/horarios"
            + $"?dataInicio=10%2F09%2F2026&planoVersaoId={cenario.PlanoVersaoId:D}");
        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;
        var horario = raiz.GetProperty("horarios").EnumerateArray().Single();

        Assert.Equal("Intermediário A", horario.GetProperty("nomeTurma").GetString());
        Assert.Equal("Professor Operacional", horario.GetProperty("professor").GetString());
        Assert.Equal(1, horario.GetProperty("ocupacao").GetInt32());
        Assert.Equal(0, horario.GetProperty("vagasDisponiveis").GetInt32());
        Assert.True(horario.GetProperty("lotado").GetBoolean());
        Assert.Equal("09/09/2027", raiz.GetProperty("dataFimPrevista").GetString());
    }

    [Fact]
    public async Task Post_final_cria_novo_adulto_preco_negociado_taxa_isenta_e_usa_prg()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = await ObterTokenNovaAsync(client, cenario.UnidadeId);

        using var form = FormNovaMatricula(
            token, cenario, novoAluno: true, nascimento: "10/01/2000",
            valorMensal: "270,00", cobrarTaxa: false);
        using var resposta = await client.PostAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova", form);

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        Assert.StartsWith($"/unidade/{cenario.UnidadeId:D}/matriculas/",
            resposta.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            var matricula = await db.Matriculas.SingleAsync();
            var aluno = await db.Alunos.SingleAsync(item => item.Id == matricula.AlunoId);
            Assert.Equal(270m, matricula.ValorMensalContratado);
            Assert.False(matricula.CobraTaxaMatricula);
            Assert.Null(matricula.ValorTaxaMatricula);
            Assert.Null(aluno.UsuarioId);
            Assert.Equal("12345678909", aluno.Cpf);
            Assert.Single(await db.MatriculasHorarios.ToListAsync());
        }

        var detalhe = WebUtility.HtmlDecode(await client.GetStringAsync(
            resposta.Headers.Location!.OriginalString));
        Assert.Contains("Matrícula criada com sucesso.", detalhe, StringComparison.Ordinal);
        _ = await client.GetStringAsync(resposta.Headers.Location.OriginalString);
        await using var scopeFinal = application.Services.CreateAsyncScope();
        Assert.Equal(1, await scopeFinal.ServiceProvider
            .GetRequiredService<BfaDbContext>().Matriculas.CountAsync());
    }

    [Fact]
    public async Task Aluno_existente_relacionado_pode_ser_selecionado_sem_novo_cadastro()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        await AdicionarMatriculaAsync(application, cenario.OrganizacaoId,
            cenario.UnidadeId, "Aluno Existente", StatusMatricula.Encerrada);
        Guid alunoId;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            alunoId = await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
                .Alunos.Where(item => item.NomeCompleto == "Aluno Existente")
                .Select(item => item.Id).SingleAsync();
        }
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = await ObterTokenNovaAsync(client, cenario.UnidadeId);
        var valores = ValoresNovaMatricula(
            token, cenario, false, "10/01/2000", "300,00", true);
        valores["AlunoId"] = alunoId.ToString();

        using var resposta = await client.PostAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova",
            new FormUrlEncodedContent(valores));

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        await using var scopeFinal = application.Services.CreateAsyncScope();
        var db = scopeFinal.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(1, await db.Alunos.CountAsync(item => item.Id == alunoId));
        Assert.Equal(2, await db.Matriculas.CountAsync(item => item.AlunoId == alunoId));
        Assert.Equal(1, await db.Matriculas.CountAsync(item =>
            item.AlunoId == alunoId && item.Status == StatusMatricula.Ativa));
    }

    [Fact]
    public async Task Menor_sem_responsavel_reapresenta_formulario_sem_criacao_parcial()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = await ObterTokenNovaAsync(client, cenario.UnidadeId);

        using var form = FormNovaMatricula(
            token, cenario, novoAluno: true, nascimento: "10/01/2012",
            valorMensal: "300,00", cobrarTaxa: true);
        using var resposta = await client.PostAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova", form);
        var html = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Informe ao menos um responsável para o aluno menor de idade.",
            html, StringComparison.Ordinal);
        Assert.Contains("Aluno Novo", html, StringComparison.Ordinal);
        Assert.DoesNotContain("PostgresException", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbUpdateException", html, StringComparison.OrdinalIgnoreCase);
        await using var scope = application.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider
            .GetRequiredService<BfaDbContext>().Matriculas.ToListAsync());
    }

    [Fact]
    public async Task Multiplos_responsaveis_com_um_principal_sao_criados_no_submit_final()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = await ObterTokenNovaAsync(client, cenario.UnidadeId);
        var valores = ValoresNovaMatricula(
            token, cenario, true, "10/01/2012", "300,00", true);
        valores["Responsaveis[0].NomeCompleto"] = "Maria da Silva";
        valores["Responsaveis[0].Telefone"] = "(15) 99999-0001";
        valores["Responsaveis[0].TipoRelacao"] = "Mae";
        valores["Responsaveis[0].PrincipalContato"] = "true";
        valores["Responsaveis[1].NomeCompleto"] = "José da Silva";
        valores["Responsaveis[1].Email"] = "jose@bfa.test";
        valores["Responsaveis[1].TipoRelacao"] = "Pai";
        valores["Responsaveis[1].ResponsavelFinanceiro"] = "true";

        using var resposta = await client.PostAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova",
            new FormUrlEncodedContent(valores));

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(2, await db.Responsaveis.CountAsync());
        Assert.Equal(2, await db.AlunosResponsaveis.CountAsync());
        Assert.Equal(1, await db.AlunosResponsaveis.CountAsync(item => item.PrincipalContato));
        Assert.Equal(1, await db.AlunosResponsaveis.CountAsync(
            item => item.ResponsavelFinanceiro));
    }

    [Theory]
    [InlineData("dois-principais", "Marque somente um responsável como Principal contato.")]
    [InlineData("outro-sem-descricao", "Descreva a relação quando escolher Outro.")]
    [InlineData("horario-duplicado", "A Grade contém horários repetidos ou inválidos.")]
    public async Task Validacoes_web_impedem_dados_ambiguos_sem_chamar_criacao(
        string cenarioErro, string mensagem)
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = await ObterTokenNovaAsync(client, cenario.UnidadeId);
        var valores = ValoresNovaMatricula(
            token, cenario, true, "10/01/2000", "300,00", true);
        valores["Responsaveis[0].NomeCompleto"] = "Responsável Um";
        valores["Responsaveis[0].Telefone"] = "15999990000";
        valores["Responsaveis[0].TipoRelacao"] =
            cenarioErro == "outro-sem-descricao" ? "Outro" : "Mae";
        if (cenarioErro == "dois-principais")
        {
            valores["Responsaveis[0].PrincipalContato"] = "true";
            valores["Responsaveis[1].NomeCompleto"] = "Responsável Dois";
            valores["Responsaveis[1].Email"] = "dois@bfa.test";
            valores["Responsaveis[1].TipoRelacao"] = "Pai";
            valores["Responsaveis[1].PrincipalContato"] = "true";
        }
        if (cenarioErro == "horario-duplicado")
            valores["TurmaHorarioIds[1]"] = cenario.TurmaHorarioId.ToString();

        using var resposta = await client.PostAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova",
            new FormUrlEncodedContent(valores));
        var html = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains(mensagem, html, StringComparison.Ordinal);
        await using var scope = application.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider
            .GetRequiredService<BfaDbContext>().Matriculas.ToListAsync());
    }

    [Fact]
    public async Task Vaga_perdida_recarrega_ocupacao_e_mensagem_amigavel_sem_excecao_tecnica()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application, capacidade: 1);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = await ObterTokenNovaAsync(client, cenario.UnidadeId);
        await OcuparHorarioAsync(application, cenario);

        using var form = FormNovaMatricula(
            token, cenario, true, "10/01/2000", "300,00", true);
        using var resposta = await client.PostAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova", form);
        var html = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Um dos horários selecionados acabou de ficar sem vagas. Revise sua Grade.",
            html, StringComparison.Ordinal);
        Assert.Contains("Lotado", html, StringComparison.Ordinal);
        Assert.DoesNotContain("SQLSTATE", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("constraint", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Plano_desativado_entre_get_e_post_reapresenta_mensagem_amigavel()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);
        var token = await ObterTokenNovaAsync(client, cenario.UnidadeId);
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            var plano = await db.Planos.SingleAsync(item => item.Id == cenario.PlanoId);
            plano.Desativar(application.UsuarioStore.Usuario.Id, CriadoEmUtc.AddHours(3));
            await db.SaveChangesAsync();
        }

        using var form = FormNovaMatricula(
            token, cenario, true, "10/01/2000", "300,00", true);
        using var resposta = await client.PostAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova", form);
        var html = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("O plano selecionado não está mais disponível.",
            html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_sem_antiforgery_e_rejeitado()
    {
        using var application = new AreaUnidadeWebApplicationFactory();
        var cenario = await AdicionarBaseNovaMatriculaAsync(application);
        using var client = CreateClient(application);
        await LoginAsync(client, application);

        using var resposta = await client.PostAsync(
            $"/unidade/{cenario.UnidadeId:D}/matriculas/nova",
            new FormUrlEncodedContent(ValoresNovaMatricula(
                string.Empty, cenario, true, "10/01/2000", "300,00", true)
                .Where(item => item.Key != "__RequestVerificationToken")));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    private static async Task<CenarioNovaMatricula> AdicionarBaseNovaMatriculaAsync(
        AreaUnidadeWebApplicationFactory application,
        PerfilAcesso perfil = PerfilAcesso.AdministradorUnidade,
        bool vinculoOrganizacional = false,
        int capacidade = 8)
    {
        var organizacao = await AdicionarOrganizacaoAsync(
            application, "BFA", $"bfa-nova-matricula-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(
            application, organizacao.Id, "BFA Operacional");
        await AdicionarVinculoAsync(
            application,
            application.UsuarioStore.Usuario.Id,
            organizacao.Id,
            vinculoOrganizacional ? null : unidade.Id,
            perfil);
        var professor = await AdicionarProfessorTurmaAsync(
            application, organizacao.Id, unidade.Id, "Professor Operacional");
        var usuarioId = application.UsuarioStore.Usuario.Id;
        var plano = new Plano(
            Guid.NewGuid(), organizacao.Id, unidade.Id,
            "Plano BFA 2x", usuarioId, CriadoEmUtc);
        var versao = new PlanoVersao(
            Guid.NewGuid(), organizacao.Id, plano.Id, 1, 12, 2, 300m,
            true, 100m, new DateOnly(2026, 1, 1), null, usuarioId, CriadoEmUtc);
        var turma = new Turma(
            Guid.NewGuid(), organizacao.Id, unidade.Id, professor.Vinculo.Id,
            "Intermediário A", capacidade, usuarioId, CriadoEmUtc);
        var horario = new TurmaHorario(
            Guid.NewGuid(), organizacao.Id, unidade.Id, turma.Id,
            professor.Vinculo.Id, DiaSemana.Segunda,
            new TimeOnly(19, 0), new TimeOnly(20, 0),
            new DateOnly(2026, 1, 1), null, usuarioId, CriadoEmUtc);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.AddRange(plano, versao, turma, horario);
        await db.SaveChangesAsync();
        return new(
            organizacao.Id, unidade.Id, plano.Id, versao.Id, horario.Id, capacidade);
    }

    private static async Task AdicionarAlunoComResponsavelAsync(
        AreaUnidadeWebApplicationFactory application,
        Guid organizacaoId,
        Guid unidadeId)
    {
        var usuarioId = application.UsuarioStore.Usuario.Id;
        var plano = new Plano(Guid.NewGuid(), organizacaoId, unidadeId,
            "Plano Anterior", usuarioId, CriadoEmUtc);
        var versao = new PlanoVersao(Guid.NewGuid(), organizacaoId, plano.Id,
            1, 1, 1, 100m, false, null, new DateOnly(2025, 1, 1), null,
            usuarioId, CriadoEmUtc);
        var aluno = new Aluno(Guid.NewGuid(), organizacaoId, "Aluno com Responsável",
            new DateOnly(2010, 1, 1), new DateOnly(2026, 9, 1), CriadoEmUtc);
        var responsavel = new Responsavel(Guid.NewGuid(), organizacaoId,
            "Maria Responsável", CriadoEmUtc, telefone: "(15) 99999-0000");
        var vinculo = new AlunoResponsavel(Guid.NewGuid(), organizacaoId,
            aluno.Id, responsavel.Id, TipoRelacaoResponsavel.Mae,
            true, true, CriadoEmUtc);
        var matricula = new Matricula(Guid.NewGuid(), organizacaoId, unidadeId,
            aluno.Id, versao.Id, new DateOnly(2025, 1, 1), 1, 100m,
            false, null, usuarioId, CriadoEmUtc);
        matricula.Encerrar(new DateOnly(2025, 1, 31), usuarioId,
            CriadoEmUtc.AddHours(1));
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.AddRange(plano, versao, aluno, responsavel, vinculo, matricula);
        await db.SaveChangesAsync();
    }

    private static async Task AdicionarCatalogoPlanosAsync(
        AreaUnidadeWebApplicationFactory application,
        CenarioNovaMatricula cenario)
    {
        var usuarioId = application.UsuarioStore.Usuario.Id;
        var local = new Plano(Guid.NewGuid(), cenario.OrganizacaoId,
            cenario.UnidadeId, "Plano Local Elegível", usuarioId, CriadoEmUtc);
        var localVersao = new PlanoVersao(Guid.NewGuid(), cenario.OrganizacaoId,
            local.Id, 1, 6, 1, 200m, false, null, new DateOnly(2026, 1, 1),
            null, usuarioId, CriadoEmUtc);
        var rede = new Plano(Guid.NewGuid(), cenario.OrganizacaoId,
            null, "Plano Rede Disponível", usuarioId, CriadoEmUtc);
        var redeVersao = new PlanoVersao(Guid.NewGuid(), cenario.OrganizacaoId,
            rede.Id, 1, 12, 3, 400m, true, 80m, new DateOnly(2026, 1, 1),
            null, usuarioId, CriadoEmUtc);
        var disponibilidade = new PlanoDisponibilidadeUnidade(
            Guid.NewGuid(), cenario.OrganizacaoId, rede.Id, cenario.UnidadeId,
            usuarioId, CriadoEmUtc);
        var indisponivel = new Plano(Guid.NewGuid(), cenario.OrganizacaoId,
            null, "Plano Rede Indisponível", usuarioId, CriadoEmUtc);
        var indisponivelVersao = new PlanoVersao(Guid.NewGuid(), cenario.OrganizacaoId,
            indisponivel.Id, 1, 12, 2, 350m, false, null,
            new DateOnly(2026, 1, 1), null, usuarioId, CriadoEmUtc);
        var inativo = new Plano(Guid.NewGuid(), cenario.OrganizacaoId,
            cenario.UnidadeId, "Plano Inativo", usuarioId, CriadoEmUtc);
        inativo.Desativar(usuarioId, CriadoEmUtc.AddHours(1));
        var inativoVersao = new PlanoVersao(Guid.NewGuid(), cenario.OrganizacaoId,
            inativo.Id, 1, 12, 2, 350m, false, null,
            new DateOnly(2026, 1, 1), null, usuarioId, CriadoEmUtc);
        var historico = new Plano(Guid.NewGuid(), cenario.OrganizacaoId,
            cenario.UnidadeId, "Plano Histórico", usuarioId, CriadoEmUtc);
        var historicoVersao = new PlanoVersao(Guid.NewGuid(), cenario.OrganizacaoId,
            historico.Id, 1, 12, 2, 250m, false, null,
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
            usuarioId, CriadoEmUtc);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.AddRange(local, localVersao, rede, redeVersao, disponibilidade,
            indisponivel, indisponivelVersao, inativo, inativoVersao,
            historico, historicoVersao);
        await db.SaveChangesAsync();
    }

    private static async Task OcuparHorarioAsync(
        AreaUnidadeWebApplicationFactory application,
        CenarioNovaMatricula cenario)
    {
        var usuarioId = application.UsuarioStore.Usuario.Id;
        var aluno = new Aluno(Guid.NewGuid(), cenario.OrganizacaoId, "Aluno Ocupante",
            new DateOnly(2000, 1, 1), new DateOnly(2026, 9, 1), CriadoEmUtc);
        var matricula = new Matricula(Guid.NewGuid(), cenario.OrganizacaoId,
            cenario.UnidadeId, aluno.Id, cenario.PlanoVersaoId,
            new DateOnly(2026, 9, 10), 12, 300m, true, 100m,
            usuarioId, CriadoEmUtc);
        var grade = new MatriculaHorario(Guid.NewGuid(), cenario.OrganizacaoId,
            cenario.UnidadeId, matricula.Id, cenario.TurmaHorarioId,
            new DateOnly(2026, 9, 10), usuarioId, CriadoEmUtc);
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.AddRange(aluno, matricula, grade);
        await db.SaveChangesAsync();
    }

    private static async Task<string> ObterTokenNovaAsync(
        HttpClient client, Guid unidadeId)
    {
        var html = await client.GetStringAsync(
            $"/unidade/{unidadeId:D}/matriculas/nova");
        return ObterAntiforgery(html);
    }

    private static FormUrlEncodedContent FormNovaMatricula(
        string token,
        CenarioNovaMatricula cenario,
        bool novoAluno,
        string nascimento,
        string valorMensal,
        bool cobrarTaxa) => new(ValoresNovaMatricula(
            token, cenario, novoAluno, nascimento, valorMensal, cobrarTaxa));

    private static Dictionary<string, string> ValoresNovaMatricula(
        string token,
        CenarioNovaMatricula cenario,
        bool novoAluno,
        string nascimento,
        string valorMensal,
        bool cobrarTaxa) => new()
    {
        ["__RequestVerificationToken"] = token,
        ["AlunoModo"] = novoAluno ? "novo" : "existente",
        ["NovoAluno.NomeCompleto"] = "Aluno Novo",
        ["NovoAluno.DataNascimentoTexto"] = nascimento,
        ["NovoAluno.Cpf"] = "123.456.789-09",
        ["NovoAluno.Telefone"] = "(15) 99999-9999",
        ["NovoAluno.Email"] = "aluno@bfa.test",
        ["DataInicioTexto"] = "10/09/2026",
        ["PlanoVersaoId"] = cenario.PlanoVersaoId.ToString(),
        ["ValorMensalContratadoTexto"] = valorMensal,
        ["CobraTaxaMatricula"] = cobrarTaxa ? "true" : "false",
        ["ValorTaxaMatriculaTexto"] = cobrarTaxa ? "100,00" : string.Empty,
        ["TurmaHorarioIds[0]"] = cenario.TurmaHorarioId.ToString()
    };

    private static int Contar(string texto, string trecho) =>
        (texto.Length - texto.Replace(trecho, string.Empty, StringComparison.Ordinal).Length)
        / trecho.Length;

    private sealed record CenarioNovaMatricula(
        Guid OrganizacaoId,
        Guid UnidadeId,
        Guid PlanoId,
        Guid PlanoVersaoId,
        Guid TurmaHorarioId,
        int Capacidade);
}

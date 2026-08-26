using System.Net;
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
    public async Task Administrador_troca_professor_copiando_programacao_e_historico(
        PerfilAcesso perfil)
    {
        using var app = new AreaUnidadeWebApplicationFactory();
        var org = await AdicionarOrganizacaoAsync(app, "BFA",
            $"troca-{perfil}-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(app, org.Id, "BFA Unidade");
        await AdicionarVinculoAsync(app, app.UsuarioStore.Usuario.Id, org.Id,
            perfil == PerfilAcesso.AdministradorRede ? null : unidade.Id, perfil);
        var anterior = await AdicionarProfessorTurmaAsync(
            app, org.Id, unidade.Id, "Thalisson");
        var novo = await AdicionarProfessorTurmaAsync(
            app, org.Id, unidade.Id, "Maria");
        await AdicionarTurmaHorarioAsync(app, org.Id, unidade.Id,
            anterior.Vinculo.Id, "Iniciante Noite", new TimeOnly(19, 0),
            new TimeOnly(20, 0));
        Guid turmaId;
        Guid horarioAnteriorId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            turmaId = await db.Turmas.Select(item => item.Id).SingleAsync();
            horarioAnteriorId = await db.TurmasHorarios.Select(item => item.Id).SingleAsync();
        }
        using var client = CreateClient(app);
        await LoginAsync(client, app);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/professor");
        var htmlPagina = WebUtility.HtmlDecode(pagina);
        Assert.Contains("Thalisson", htmlPagina, StringComparison.Ordinal);
        Assert.Contains("Maria", htmlPagina, StringComparison.Ordinal);
        Assert.Contains("19:00 às 20:00", htmlPagina, StringComparison.Ordinal);
        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/professor",
            FormTroca(ObterAntiforgery(pagina), novo.Vinculo.Id, "01/09/2026"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var verificacao = app.Services.CreateAsyncScope();
        var contexto = verificacao.ServiceProvider.GetRequiredService<BfaDbContext>();
        var turma = await contexto.Turmas.SingleAsync();
        var horarios = await contexto.TurmasHorarios.OrderBy(item => item.VigenciaInicio)
            .ToArrayAsync();
        Assert.Equal(novo.Vinculo.Id, turma.ProfessorUnidadeId);
        Assert.Equal(2, horarios.Length);
        var historico = Assert.Single(horarios, item => item.Id == horarioAnteriorId);
        Assert.Equal(anterior.Vinculo.Id, historico.ProfessorUnidadeId);
        Assert.Equal(new DateOnly(2026, 8, 31), historico.VigenciaFim);
        var atual = Assert.Single(horarios, item => item.Id != horarioAnteriorId);
        Assert.Equal(novo.Vinculo.Id, atual.ProfessorUnidadeId);
        Assert.Equal(historico.DiaSemana, atual.DiaSemana);
        Assert.Equal(historico.HoraInicio, atual.HoraInicio);
        Assert.Equal(historico.HoraFim, atual.HoraFim);
        Assert.Equal(new DateOnly(2026, 9, 1), atual.VigenciaInicio);
        Assert.True(anterior.Vinculo.Ativo);
        Assert.Single(await contexto.VinculosAcesso.ToArrayAsync());
        Assert.Empty(await contexto.ProfessoresRemuneracoes.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Professor_de_outra_unidade_ou_inativo_e_rejeitado_na_troca(
        bool outraUnidade)
    {
        using var app = new AreaUnidadeWebApplicationFactory();
        var org = await AdicionarOrganizacaoAsync(app, "BFA",
            $"troca-invalida-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(app, org.Id, "Destino");
        var origem = outraUnidade
            ? await AdicionarUnidadeAsync(app, org.Id, "Outra") : unidade;
        await AdicionarVinculoAsync(app, app.UsuarioStore.Usuario.Id, org.Id,
            unidade.Id, PerfilAcesso.AdministradorUnidade);
        var atual = await AdicionarProfessorTurmaAsync(app, org.Id, unidade.Id, "Atual");
        var candidato = await AdicionarProfessorTurmaAsync(
            app, org.Id, origem.Id, "Inválido", ativo: outraUnidade);
        await AdicionarTurmaHorarioAsync(app, org.Id, unidade.Id, atual.Vinculo.Id,
            "Turma", new TimeOnly(19, 0), new TimeOnly(20, 0));
        Guid turmaId;
        await using (var scope = app.Services.CreateAsyncScope())
            turmaId = await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
                .Turmas.Select(item => item.Id).SingleAsync();
        using var client = CreateClient(app);
        await LoginAsync(client, app);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/professor");
        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/professor",
            FormTroca(ObterAntiforgery(pagina), candidato.Vinculo.Id, "01/09/2026"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verificacao = app.Services.CreateAsyncScope();
        var turma = await verificacao.ServiceProvider.GetRequiredService<BfaDbContext>()
            .Turmas.SingleAsync();
        Assert.Equal(atual.Vinculo.Id, turma.ProfessorUnidadeId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Conflito_do_novo_professor_na_mesma_ou_outra_unidade_bloqueia_troca(
        bool outraUnidade)
    {
        using var app = new AreaUnidadeWebApplicationFactory();
        var org = await AdicionarOrganizacaoAsync(app, "BFA",
            $"troca-conflito-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(app, org.Id, "BFA A");
        var unidadeConflito = outraUnidade
            ? await AdicionarUnidadeAsync(app, org.Id, "BFA B") : unidade;
        await AdicionarVinculoAsync(app, app.UsuarioStore.Usuario.Id, org.Id,
            unidade.Id, PerfilAcesso.AdministradorUnidade);
        var anterior = await AdicionarProfessorTurmaAsync(app, org.Id, unidade.Id, "Anterior");
        var novoOrigem = await AdicionarProfessorTurmaAsync(
            app, org.Id, unidadeConflito.Id, "Novo");
        var novoDestino = outraUnidade
            ? await AdicionarVinculoProfessorTurmaAsync(app, novoOrigem.Professor, unidade.Id)
            : novoOrigem.Vinculo;
        await AdicionarTurmaHorarioAsync(app, org.Id, unidade.Id, anterior.Vinculo.Id,
            "Transferida", new TimeOnly(19, 0), new TimeOnly(20, 0));
        await AdicionarTurmaHorarioAsync(app, org.Id, unidadeConflito.Id,
            novoOrigem.Vinculo.Id, "Conflitante", new TimeOnly(19, 30),
            new TimeOnly(20, 30));
        Guid turmaId;
        await using (var scope = app.Services.CreateAsyncScope())
            turmaId = await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
                .Turmas.Where(item => item.Nome == "Transferida")
                .Select(item => item.Id).SingleAsync();
        using var client = CreateClient(app);
        await LoginAsync(client, app);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/professor");
        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/professor",
            FormTroca(ObterAntiforgery(pagina), novoDestino.Id, "01/09/2026"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("já possui outra turma", WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Turma_sem_horario_aberto_troca_sem_criar_programacao_ficticia()
    {
        using var app = new AreaUnidadeWebApplicationFactory();
        var org = await AdicionarOrganizacaoAsync(app, "BFA",
            $"troca-sem-horario-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(app, org.Id, "BFA Unidade");
        await AdicionarVinculoAsync(app, app.UsuarioStore.Usuario.Id, org.Id,
            unidade.Id, PerfilAcesso.AdministradorUnidade);
        var anterior = await AdicionarProfessorTurmaAsync(app, org.Id, unidade.Id, "Anterior");
        var novo = await AdicionarProfessorTurmaAsync(app, org.Id, unidade.Id, "Novo");
        var turma = new Turma(Guid.NewGuid(), org.Id, unidade.Id, anterior.Vinculo.Id,
            "Sem horário", 12, app.UsuarioStore.Usuario.Id, CriadoEmUtc);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<BfaDbContext>().Turmas.Add(turma);
            await scope.ServiceProvider.GetRequiredService<BfaDbContext>().SaveChangesAsync();
        }
        using var client = CreateClient(app);
        await LoginAsync(client, app);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turma.Id:D}/professor");
        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turma.Id:D}/professor",
            FormTroca(ObterAntiforgery(pagina), novo.Vinculo.Id, "01/09/2026"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var verificacao = app.Services.CreateAsyncScope();
        var db = verificacao.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.Equal(novo.Vinculo.Id, (await db.Turmas.SingleAsync()).ProfessorUnidadeId);
        Assert.Empty(await db.TurmasHorarios.ToArrayAsync());
    }

    [Fact]
    public async Task Horario_adjacente_do_novo_professor_permite_troca()
    {
        using var app = new AreaUnidadeWebApplicationFactory();
        var org = await AdicionarOrganizacaoAsync(app, "BFA",
            $"troca-adjacente-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(app, org.Id, "BFA Unidade");
        await AdicionarVinculoAsync(app, app.UsuarioStore.Usuario.Id, org.Id,
            unidade.Id, PerfilAcesso.AdministradorUnidade);
        var anterior = await AdicionarProfessorTurmaAsync(app, org.Id, unidade.Id, "Anterior");
        var novo = await AdicionarProfessorTurmaAsync(app, org.Id, unidade.Id, "Novo");
        await AdicionarTurmaHorarioAsync(app, org.Id, unidade.Id, anterior.Vinculo.Id,
            "Transferida", new TimeOnly(19, 0), new TimeOnly(20, 0));
        await AdicionarTurmaHorarioAsync(app, org.Id, unidade.Id, novo.Vinculo.Id,
            "Adjacente", new TimeOnly(18, 0), new TimeOnly(19, 0));
        Guid turmaId;
        await using (var scope = app.Services.CreateAsyncScope())
            turmaId = await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
                .Turmas.Where(item => item.Nome == "Transferida")
                .Select(item => item.Id).SingleAsync();
        using var client = CreateClient(app);
        await LoginAsync(client, app);
        var pagina = await client.GetStringAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/professor");
        using var response = await client.PostAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turmaId:D}/professor",
            FormTroca(ObterAntiforgery(pagina), novo.Vinculo.Id, "01/09/2026"));
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact]
    public async Task Outro_tenant_nao_consulta_troca_de_professor()
    {
        using var app = new AreaUnidadeWebApplicationFactory();
        var permitida = await AdicionarOrganizacaoAsync(app, "BFA",
            $"troca-permitida-{Guid.NewGuid():N}");
        var externa = await AdicionarOrganizacaoAsync(app, "Outra",
            $"troca-externa-{Guid.NewGuid():N}");
        var unidadePermitida = await AdicionarUnidadeAsync(app, permitida.Id, "Permitida");
        var unidadeExterna = await AdicionarUnidadeAsync(app, externa.Id, "Externa");
        await AdicionarVinculoAsync(app, app.UsuarioStore.Usuario.Id, permitida.Id,
            unidadePermitida.Id, PerfilAcesso.AdministradorUnidade);
        var professor = await AdicionarProfessorTurmaAsync(
            app, externa.Id, unidadeExterna.Id, "Externo");
        var turma = new Turma(Guid.NewGuid(), externa.Id, unidadeExterna.Id,
            professor.Vinculo.Id, "Externa", 12, app.UsuarioStore.Usuario.Id, CriadoEmUtc);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            db.Turmas.Add(turma);
            await db.SaveChangesAsync();
        }
        using var client = CreateClient(app);
        await LoginAsync(client, app);
        using var response = await client.GetAsync(
            $"/unidade/{unidadeExterna.Id:D}/turmas/{turma.Id:D}/professor");
        AssertAcessoNegado(response);
    }

    [Fact]
    public async Task Perfil_professor_nao_acessa_fluxo_administrativo_de_troca()
    {
        using var app = new AreaUnidadeWebApplicationFactory();
        var org = await AdicionarOrganizacaoAsync(app, "BFA",
            $"troca-perfil-professor-{Guid.NewGuid():N}");
        var unidade = await AdicionarUnidadeAsync(app, org.Id, "BFA Unidade");
        await AdicionarVinculoAsync(app, app.UsuarioStore.Usuario.Id, org.Id,
            unidade.Id, PerfilAcesso.Professor);
        var professor = await AdicionarProfessorTurmaAsync(
            app, org.Id, unidade.Id, "Professor");
        var turma = new Turma(Guid.NewGuid(), org.Id, unidade.Id,
            professor.Vinculo.Id, "Turma", 12, app.UsuarioStore.Usuario.Id, CriadoEmUtc);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            db.Turmas.Add(turma);
            await db.SaveChangesAsync();
        }
        using var client = CreateClient(app);
        await LoginAsync(client, app);

        using var response = await client.GetAsync(
            $"/unidade/{unidade.Id:D}/turmas/{turma.Id:D}/professor");

        AssertAcessoNegado(response);
    }

    private static FormUrlEncodedContent FormTroca(
        string token, Guid professorUnidadeId, string data) => new(
        new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["NovoProfessorUnidadeId"] = professorUnidadeId.ToString(),
            ["DataTrocaTexto"] = data
        });
}

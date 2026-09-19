using System.Net;
using BFA.Domain.Alunos;
using BFA.Domain.Aulas;
using BFA.Domain.Matriculas;
using BFA.Domain.Professores;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed partial class AreaProfessorEndpointTests
{
    private static readonly DateTime DataCriacao = new(
        2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Minhas_turmas_exibe_somente_turmas_do_professor_na_unidade()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync("BFA Cerquilho");
        var turmaProfessor = await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            contexto.ProfessorUnidade.Id, "Iniciante Manhã");
        var outroVinculo = await AdicionarOutroProfessorAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, "Outro Professor");
        await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            outroVinculo.Id, "Turma de outro professor");

        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        using var response = await client.GetAsync(
            $"/professor/unidade/{contexto.UnidadeId:D}/turmas");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Iniciante Manhã", html, StringComparison.Ordinal);
        Assert.Contains($"/turmas/{turmaProfessor.Id:D}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Turma de outro professor", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Criar turma", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Editar<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Minhas_turmas_nao_mistura_unidades_do_mesmo_professor()
    {
        var unidadeA = await ConfigurarUnidadeProfessorAsync("BFA A");
        var unidadeB = await ConfigurarUnidadeProfessorAsync("BFA B", limpar: false);
        await AdicionarTurmaAsync(
            unidadeA.OrganizacaoId, unidadeA.UnidadeId,
            unidadeA.ProfessorUnidade.Id, "Turma Unidade A");
        await AdicionarTurmaAsync(
            unidadeB.OrganizacaoId, unidadeB.UnidadeId,
            unidadeB.ProfessorUnidade.Id, "Turma Unidade B");

        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        var html = await client.GetStringAsync(
            $"/professor/unidade/{unidadeA.UnidadeId:D}/turmas");

        Assert.Contains("Turma Unidade A", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Turma Unidade B", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detalhe_de_turma_de_outro_professor_retorna_404_controlado()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync("BFA Cerquilho");
        var outroVinculo = await AdicionarOutroProfessorAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, "Outro Professor");
        var turma = await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            outroVinculo.Id, "Turma Protegida");

        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        using var response = await client.GetAsync(
            $"/professor/unidade/{contexto.UnidadeId:D}/turmas/{turma.Id:D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Outro_tenant_nao_expoe_turma_por_alteracao_da_url()
    {
        var autorizado = await ConfigurarUnidadeProfessorAsync("BFA Autorizada");
        var organizacaoOutra = Guid.NewGuid();
        var unidadeOutra = Guid.NewGuid();
        var outroVinculo = await AdicionarOutroProfessorAsync(
            organizacaoOutra, unidadeOutra, "Professor Outro Tenant");
        var turma = await AdicionarTurmaAsync(
            organizacaoOutra, unidadeOutra, outroVinculo.Id, "Turma Outro Tenant");

        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        using var response = await client.GetAsync(
            $"/professor/unidade/{unidadeOutra:D}/turmas/{turma.Id:D}");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, autorizado.UnidadeId);
    }

    [Fact]
    public async Task Lista_sem_turmas_exibe_estado_vazio_controlado()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync("BFA Sem Turmas");
        using var client = CriarClient();
        using var login = await AutenticarAsync(client);

        var html = await client.GetStringAsync(
            $"/professor/unidade/{contexto.UnidadeId:D}/turmas");

        Assert.Contains(
            "Nenhuma turma atribuída a você nesta unidade.",
            WebUtility.HtmlDecode(html),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lista_exibe_horario_atual_em_portugues_e_formato_local()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync("BFA Cerquilho");
        var turma = await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            contexto.ProfessorUnidade.Id, "Turma Atual");
        await AdicionarHorarioAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, turma.Id,
            contexto.ProfessorUnidade.Id, DiaSemana.Terca,
            new TimeOnly(18, 30), new TimeOnly(19, 30),
            new DateOnly(2026, 8, 1), null);

        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/professor/unidade/{contexto.UnidadeId:D}/turmas"));

        Assert.Contains("Terça-feira", html, StringComparison.Ordinal);
        Assert.Contains("18:30 às 19:30", html, StringComparison.Ordinal);
        Assert.Contains("Desde 01/08/2026", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detalhe_preserva_historico_pelo_snapshot_do_professor_unidade()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync("BFA Cerquilho");
        var outroVinculo = await AdicionarOutroProfessorAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, "Professor Anterior");
        var turma = await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            contexto.ProfessorUnidade.Id, "Turma Histórica");
        await AdicionarHorarioAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, turma.Id,
            contexto.ProfessorUnidade.Id, DiaSemana.Quarta,
            new TimeOnly(17, 0), new TimeOnly(18, 0),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        await AdicionarHorarioAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, turma.Id,
            contexto.ProfessorUnidade.Id, DiaSemana.Quinta,
            new TimeOnly(19, 0), new TimeOnly(20, 0),
            new DateOnly(2026, 8, 1), null);
        await AdicionarHorarioAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, turma.Id,
            outroVinculo.Id, DiaSemana.Segunda,
            new TimeOnly(8, 0), new TimeOnly(9, 0),
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/professor/unidade/{contexto.UnidadeId:D}/turmas/{turma.Id:D}"));

        Assert.Contains("Horários atuais", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Trocar professor", html, StringComparison.Ordinal);
        Assert.Contains("19:00 às 20:00", html, StringComparison.Ordinal);
        Assert.Contains("Histórico de horários", html, StringComparison.Ordinal);
        Assert.Contains("17:00 às 18:00", html, StringComparison.Ordinal);
        Assert.DoesNotContain("08:00 às 09:00", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detalhe_exibe_alunos_matriculados_ativos_sem_duplicar_por_horario()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync("BFA Alunos da Turma");
        var turma = await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            contexto.ProfessorUnidade.Id, "Turma com alunos");
        var horarioPrincipal = await AdicionarHorarioComIdAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            turma.Id, contexto.ProfessorUnidade.Id);
        var horarioExtra = await AdicionarHorarioComIdAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            turma.Id, contexto.ProfessorUnidade.Id);
        var alice = new Aluno(Guid.NewGuid(), contexto.OrganizacaoId, "Alice Matriculada",
            new DateOnly(2000, 1, 1), new DateOnly(2026, 1, 1), DataCriacao,
            telefone: "5511992682235");
        var bruno = new Aluno(Guid.NewGuid(), contexto.OrganizacaoId, "Bruno Matriculado",
            new DateOnly(2000, 1, 1), new DateOnly(2026, 1, 1), DataCriacao);
        var matriculaAlice = new Matricula(Guid.NewGuid(), contexto.OrganizacaoId,
            contexto.UnidadeId, alice.Id, Guid.NewGuid(), new DateOnly(2026, 1, 1),
            12, 100, false, null, _application.UsuarioStore.Usuario.Id, DataCriacao);
        var matriculaBruno = new Matricula(Guid.NewGuid(), contexto.OrganizacaoId,
            contexto.UnidadeId, bruno.Id, Guid.NewGuid(), new DateOnly(2026, 1, 1),
            12, 100, false, null, _application.UsuarioStore.Usuario.Id, DataCriacao);
        await using (var scope = _application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            db.AddRange(alice, bruno, matriculaAlice, matriculaBruno);
            db.AddRange(
                new MatriculaHorario(Guid.NewGuid(), contexto.OrganizacaoId,
                    contexto.UnidadeId, matriculaAlice.Id, horarioPrincipal.Id,
                    new DateOnly(2026, 1, 1), _application.UsuarioStore.Usuario.Id, DataCriacao),
                new MatriculaHorario(Guid.NewGuid(), contexto.OrganizacaoId,
                    contexto.UnidadeId, matriculaAlice.Id, horarioExtra.Id,
                    new DateOnly(2026, 1, 1), _application.UsuarioStore.Usuario.Id, DataCriacao),
                new MatriculaHorario(Guid.NewGuid(), contexto.OrganizacaoId,
                    contexto.UnidadeId, matriculaBruno.Id, horarioPrincipal.Id,
                    new DateOnly(2026, 1, 1), _application.UsuarioStore.Usuario.Id, DataCriacao));
            await db.SaveChangesAsync();
        }

        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/professor/unidade/{contexto.UnidadeId:D}/turmas/{turma.Id:D}"));

        Assert.Contains("Alunos da turma", html, StringComparison.Ordinal);
        Assert.Contains("2 alunos", html, StringComparison.Ordinal);
        Assert.Contains("Alice Matriculada", html, StringComparison.Ordinal);
        Assert.Contains("Bruno Matriculado", html, StringComparison.Ordinal);
        Assert.Contains("+55 (11) 99268-2235", html, StringComparison.Ordinal);
        Assert.Contains("Telefone não informado", html, StringComparison.Ordinal);
        Assert.Contains("https://wa.me/5511992682235", html, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(html, "https://wa.me/5511992682235"));
        Assert.Contains("Buscar aluno", html, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(html,
            "bfa-professor-student-item__name\">Alice Matriculada</div>"));
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }
        return count;
    }

    [Fact]
    public async Task Vinculo_profissional_inativo_nao_acessa_minhas_turmas()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync(
            "BFA Inativa", vinculoProfissionalAtivo: false);
        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        using var response = await client.GetAsync(
            $"/professor/unidade/{contexto.UnidadeId:D}/turmas");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Vinculo_de_acesso_inativo_nao_acessa_minhas_turmas()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync(
            "BFA Sem Acesso", acessoAtivo: false);
        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        using var response = await client.GetAsync(
            $"/professor/unidade/{contexto.UnidadeId:D}/turmas");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_exibe_quantidade_correta_de_turmas_ativas()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync("BFA Cerquilho");
        await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            contexto.ProfessorUnidade.Id, "Turma Ativa 1");
        await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            contexto.ProfessorUnidade.Id, "Turma Ativa 2");
        await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            contexto.ProfessorUnidade.Id, "Turma Inativa", ativo: false);

        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/professor/unidade/{contexto.UnidadeId:D}"));

        Assert.Contains("Turmas", html, StringComparison.Ordinal);
        Assert.Contains(">2<", html, StringComparison.Ordinal);
        Assert.Contains("Aulas hoje", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Professor_visualiza_turma_e_horarios_sem_poder_ajustar()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync("BFA Cerquilho");
        var turma = await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId,
            contexto.ProfessorUnidade.Id, "Turma do Professor");
        await AdicionarHorarioAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, turma.Id,
            contexto.ProfessorUnidade.Id, DiaSemana.Segunda,
            new TimeOnly(19, 0), new TimeOnly(20, 0),
            new DateOnly(2026, 8, 1), null);
        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        var detalheUrl =
            $"/professor/unidade/{contexto.UnidadeId:D}/turmas/{turma.Id:D}";
        var pagina = WebUtility.HtmlDecode(await client.GetStringAsync(detalheUrl));
        Assert.Contains("19:00 às 20:00", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("Ajustar horários", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain($"{detalheUrl}/horarios", pagina, StringComparison.Ordinal);

        using var getAjuste = await client.GetAsync($"{detalheUrl}/horarios");
        using var postAjuste = await client.PostAsync($"{detalheUrl}/horarios",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["NovaVigenciaInicioTexto"] = "01/09/2026",
                ["Horarios[0].DiaSemana"] = "1",
                ["Horarios[0].HoraInicio"] = "20:00",
                ["Horarios[0].HoraFim"] = "21:00"
            }));

        Assert.Equal(HttpStatusCode.NotFound, getAjuste.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, postAjuste.StatusCode);

        await using var scope = _application.Services.CreateAsyncScope();
        var horarios = await scope.ServiceProvider.GetRequiredService<BfaDbContext>()
            .TurmasHorarios.Where(item => item.TurmaId == turma.Id).ToArrayAsync();
        var horario = Assert.Single(horarios);
        Assert.Equal(new TimeOnly(19, 0), horario.HoraInicio);
        Assert.Equal(new TimeOnly(20, 0), horario.HoraFim);
        Assert.Null(horario.VigenciaFim);
    }

    [Fact]
    public async Task Professor_nao_visualiza_turma_de_outro_professor()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync("BFA Cerquilho");
        var outro = await AdicionarOutroProfessorAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, "Outro Professor");
        var turma = await AdicionarTurmaAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, outro.Id, "Turma Alheia");
        using var client = CriarClient();
        using var login = await AutenticarAsync(client);

        using var response = await client.GetAsync(
            $"/professor/unidade/{contexto.UnidadeId:D}/turmas/{turma.Id:D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<ProfessorUnidade> AdicionarOutroProfessorAsync(
        Guid organizacaoId,
        Guid unidadeId,
        string nome)
    {
        var professor = new Professor(
            Guid.NewGuid(), organizacaoId, nome, DataCriacao);
        var vinculo = new ProfessorUnidade(
            Guid.NewGuid(), organizacaoId, professor.Id, unidadeId, DataCriacao);
        await using var scope = _application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Professores.Add(professor);
        dbContext.ProfessoresUnidades.Add(vinculo);
        await dbContext.SaveChangesAsync();
        return vinculo;
    }

    private async Task<Turma> AdicionarTurmaAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorUnidadeId,
        string nome,
        bool ativo = true)
    {
        var turma = new Turma(
            Guid.NewGuid(), organizacaoId, unidadeId, professorUnidadeId,
            nome, 12, _application.UsuarioStore.Usuario.Id, DataCriacao);
        if (!ativo)
        {
            turma.Desativar(
                _application.UsuarioStore.Usuario.Id, DataCriacao.AddMinutes(1));
        }

        await using var scope = _application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.Turmas.Add(turma);
        await dbContext.SaveChangesAsync();
        return turma;
    }

    [Fact]
    public async Task Confirmacoes_exibe_ativos_inativos_e_preserva_somente_alunos_do_horario()
    {
        var contexto = await ConfigurarUnidadeProfessorAsync("BFA Confirmações");
        var turma = await AdicionarTurmaAsync(contexto.OrganizacaoId, contexto.UnidadeId,
            contexto.ProfessorUnidade.Id, "Turma Confirmações");
        var horario = await AdicionarHorarioComIdAsync(contexto.OrganizacaoId, contexto.UnidadeId,
            turma.Id, contexto.ProfessorUnidade.Id);
        var aula = new Aula(Guid.NewGuid(), contexto.OrganizacaoId, contexto.UnidadeId,
            turma.Id, horario.Id, new DateOnly(2026, 8, 25), new TimeOnly(18),
            new TimeOnly(19), 12, _application.UsuarioStore.Usuario.Id, DataCriacao);
        var alice = new Aluno(Guid.NewGuid(), contexto.OrganizacaoId, "Alice Barbosa",
            new DateOnly(2000, 1, 1), new DateOnly(2026, 1, 1), DataCriacao);
        var bruno = new Aluno(Guid.NewGuid(), contexto.OrganizacaoId, "Bruno Silva",
            new DateOnly(2000, 1, 1), new DateOnly(2026, 1, 1), DataCriacao);
        var carla = new Aluno(Guid.NewGuid(), contexto.OrganizacaoId, "Carla Souza",
            new DateOnly(2000, 1, 1), new DateOnly(2026, 1, 1), DataCriacao);
        var matriculas = new[] { alice, bruno, carla }.Select(aluno => new Matricula(
            Guid.NewGuid(), contexto.OrganizacaoId, contexto.UnidadeId, aluno.Id,
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 12, 100, false, null,
            _application.UsuarioStore.Usuario.Id, DataCriacao)).ToArray();
        await using (var scope = _application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            db.AddRange(aula, alice, bruno, carla);
            db.AddRange(matriculas);
            db.AddRange(matriculas.Select(m => new MatriculaHorario(Guid.NewGuid(),
                contexto.OrganizacaoId, contexto.UnidadeId, m.Id, horario.Id,
                new DateOnly(2026, 1, 1), _application.UsuarioStore.Usuario.Id, DataCriacao)));
            db.Add(new ConfirmacaoAulaAluno(Guid.NewGuid(), contexto.OrganizacaoId,
                contexto.UnidadeId, aula.Id, alice.Id, DataCriacao, DataCriacao));
            var cancelada = new ConfirmacaoAulaAluno(Guid.NewGuid(), contexto.OrganizacaoId,
                contexto.UnidadeId, aula.Id, carla.Id, DataCriacao, DataCriacao);
            cancelada.Cancelar(DataCriacao.AddMinutes(1));
            db.Add(cancelada);
            await db.SaveChangesAsync();
        }

        using var client = CriarClient();
        using var login = await AutenticarAsync(client);
        var url = $"/professor/unidade/{contexto.UnidadeId:D}/turmas/{turma.Id:D}/aulas/{aula.Id:D}/confirmacoes";
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(url));

        Assert.Contains("Alice Barbosa", html, StringComparison.Ordinal);
        Assert.Contains("Confirmou", html, StringComparison.Ordinal);
        Assert.Contains("Bruno Silva", html, StringComparison.Ordinal);
        Assert.Contains("Carla Souza", html, StringComparison.Ordinal);
        Assert.Contains("Não confirmou", html, StringComparison.Ordinal);
        Assert.Contains("Confirmados", html, StringComparison.Ordinal);
        Assert.Contains("1", html, StringComparison.Ordinal);
    }

    private async Task<TurmaHorario> AdicionarHorarioComIdAsync(Guid organizacaoId,
        Guid unidadeId, Guid turmaId, Guid professorUnidadeId)
    {
        var horario = new TurmaHorario(Guid.NewGuid(), organizacaoId, unidadeId, turmaId,
            professorUnidadeId, DiaSemana.Terca, new TimeOnly(18), new TimeOnly(19),
            new DateOnly(2026, 1, 1), null, _application.UsuarioStore.Usuario.Id, DataCriacao);
        await using var scope = _application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        db.TurmasHorarios.Add(horario);
        await db.SaveChangesAsync();
        return horario;
    }

    private async Task AdicionarHorarioAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid turmaId,
        Guid professorUnidadeId,
        DiaSemana diaSemana,
        TimeOnly horaInicio,
        TimeOnly horaFim,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim)
    {
        var horario = new TurmaHorario(
            Guid.NewGuid(), organizacaoId, unidadeId, turmaId,
            professorUnidadeId, diaSemana, horaInicio, horaFim,
            vigenciaInicio, vigenciaFim, _application.UsuarioStore.Usuario.Id,
            DataCriacao);
        await using var scope = _application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
        dbContext.TurmasHorarios.Add(horario);
        await dbContext.SaveChangesAsync();
    }

}

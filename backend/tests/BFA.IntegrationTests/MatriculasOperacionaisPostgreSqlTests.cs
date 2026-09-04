using BFA.Application.Matriculas;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Infrastructure.Matriculas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.IntegrationTests;

public sealed class MatriculasOperacionaisPostgreSqlTests(
    PostgreSqlEfemeroV013Fixture fixture)
    : IClassFixture<PostgreSqlEfemeroV013Fixture>
{
    private static readonly DateOnly Inicio = new(2026, 9, 1);
    private static readonly DateTime Agora = new(
        2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Novo_adulto_sem_responsavel_cria_matricula_e_grade_com_unidade_ativa()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        var baselineAlunos = await ContarAsync(db => db.Alunos.CountAsync());
        var baselineMatriculas = await ContarAsync(db => db.Matriculas.CountAsync());
        var baselineGrade = await ContarAsync(db => db.MatriculasHorarios.CountAsync());

        await using var db = CriarContexto();
        var resultado = await new MatriculasRepositorio(db, NullLogger<MatriculasRepositorio>.Instance).CriarAsync(
            fixture.OrganizacaoId, fixture.UnidadeUmId, fixture.UsuarioId,
            true,
            new(
                null,
                new("Aluno adulto da regressao", new DateOnly(2000, 1, 10),
                    null, null, null),
                [],
                fixture.PlanoVersaoId,
                Inicio,
                800m,
                true,
                100m,
                [fixture.HorariosUnidadeUm[0]]),
            Inicio, Agora, CancellationToken.None);

        Assert.Equal(EstadoMatriculas.Sucesso, resultado.Estado);
        Assert.NotNull(resultado.Valor);
        Assert.Equal(baselineAlunos + 1,
            await ContarAsync(contexto => contexto.Alunos.CountAsync()));
        Assert.Equal(baselineMatriculas + 1,
            await ContarAsync(contexto => contexto.Matriculas.CountAsync()));
        Assert.Equal(baselineGrade + 1,
            await ContarAsync(contexto => contexto.MatriculasHorarios.CountAsync()));

        await using var verificacao = CriarContexto();
        var matricula = await verificacao.Matriculas.SingleAsync(item =>
            item.Id == resultado.Valor.MatriculaId);
        var grade = await verificacao.MatriculasHorarios.SingleAsync(item =>
            item.MatriculaId == resultado.Valor.MatriculaId);
        Assert.True(matricula.CobraTaxaMatricula);
        Assert.Equal(100m, matricula.ValorTaxaMatricula);
        Assert.Equal(Inicio, grade.VigenciaInicio);
        Assert.Null(grade.VigenciaFim);
        Assert.Equal(fixture.UnidadeUmId, grade.UnidadeId);
        Assert.Equal(fixture.HorariosUnidadeUm[0], grade.TurmaHorarioId);
    }

    [Fact]
    public async Task Duas_confirmacoes_da_ultima_vaga_tem_um_vencedor_e_rollback_integral()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 1);
        var baselineMatriculas = await ContarAsync(db => db.Matriculas.CountAsync());
        var baselineAlunos = await ContarAsync(db => db.Alunos.CountAsync());
        using var largada = new Barrier(2);
        var tarefas = new[] { "11111111111", "22222222222" }.Select(cpf => Task.Run(async () =>
        {
            largada.SignalAndWait();
            await using var db = CriarContexto();
            return await new MatriculasRepositorio(db, NullLogger<MatriculasRepositorio>.Instance).CriarAsync(
                fixture.OrganizacaoId, fixture.UnidadeUmId, fixture.UsuarioId,
                true, NovaSolicitacao(cpf, fixture.UnidadeUmId,
                    fixture.HorariosUnidadeUm[0]),
                Inicio, Agora, CancellationToken.None);
        })).ToArray();

        var resultados = await Task.WhenAll(tarefas).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Single(resultados, item => item.Estado == EstadoMatriculas.Sucesso);
        Assert.Single(resultados, item => item.Estado == EstadoMatriculas.CapacidadeEsgotada);
        Assert.Equal(baselineMatriculas + 1,
            await ContarAsync(db => db.Matriculas.CountAsync()));
        Assert.Equal(baselineAlunos + 1,
            await ContarAsync(db => db.Alunos.CountAsync()));
        Assert.Equal(1, await ContarAsync(db => db.MatriculasHorarios.CountAsync()));
    }

    [Fact]
    public async Task Mesma_matricula_ativa_concorrente_tem_um_unico_vencedor()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        var alunoId = await fixture.CreateStudentAsync();
        using var largada = new Barrier(2);
        var tarefas = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            largada.SignalAndWait();
            await using var db = CriarContexto();
            return await new MatriculasRepositorio(db, NullLogger<MatriculasRepositorio>.Instance).CriarAsync(
                fixture.OrganizacaoId, fixture.UnidadeUmId, fixture.UsuarioId,
                true, ExistenteSolicitacao(alunoId, fixture.HorariosUnidadeUm[0]),
                Inicio, Agora, CancellationToken.None);
        })).ToArray();

        var resultados = await Task.WhenAll(tarefas).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Single(resultados, item => item.Estado == EstadoMatriculas.Sucesso);
        Assert.Single(resultados,
            item => item.Estado == EstadoMatriculas.MatriculaAtivaExistente);
        Assert.Equal(1, await ContarAsync(db => db.Matriculas.CountAsync(item =>
            item.AlunoId == alunoId && item.UnidadeId == fixture.UnidadeUmId
            && item.Status == StatusMatricula.Ativa)));
    }

    [Fact]
    public async Task Conflito_concorrente_do_mesmo_aluno_entre_unidades_reverte_perdedor()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        var alunoId = await fixture.CreateStudentAsync();
        using var largada = new Barrier(2);
        var entradas = new[]
        {
            (fixture.UnidadeUmId, fixture.HorariosUnidadeUm[0]),
            (fixture.UnidadeDoisId, fixture.HorarioUnidadeDoisId)
        };
        var tarefas = entradas.Select(entrada => Task.Run(async () =>
        {
            largada.SignalAndWait();
            await using var db = CriarContexto();
            return await new MatriculasRepositorio(db, NullLogger<MatriculasRepositorio>.Instance).CriarAsync(
                fixture.OrganizacaoId, entrada.Item1, fixture.UsuarioId,
                true, ExistenteSolicitacao(alunoId, entrada.Item2),
                Inicio, Agora, CancellationToken.None);
        })).ToArray();

        var resultados = await Task.WhenAll(tarefas).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Single(resultados, item => item.Estado == EstadoMatriculas.Sucesso);
        Assert.Single(resultados,
            item => item.Estado == EstadoMatriculas.ConflitoHorarioAluno);
        Assert.Equal(1, await ContarAsync(db => db.Matriculas.CountAsync(item =>
            item.AlunoId == alunoId)));
        Assert.Equal(1, await ContarAsync(db =>
            (from grade in db.MatriculasHorarios
             join matricula in db.Matriculas
                 on grade.MatriculaId equals matricula.Id
             where matricula.AlunoId == alunoId
             select grade).CountAsync()));
    }

    [Fact]
    public async Task Alteracoes_simultaneas_da_grade_serializam_sem_deadlock_ou_parcial()
    {
        await fixture.ResetAsync(frequencia: 2, capacidade: 4);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[1]);
        using var largada = new Barrier(2);
        var selecoes = new[]
        {
            new[] { fixture.HorariosUnidadeUm[0], fixture.HorariosUnidadeUm[2] },
            new[] { fixture.HorariosUnidadeUm[1], fixture.HorariosUnidadeUm[3] }
        };
        var tarefas = selecoes.Select(selecao => Task.Run(async () =>
        {
            largada.SignalAndWait();
            await using var db = CriarContexto();
            return await new MatriculasRepositorio(db, NullLogger<MatriculasRepositorio>.Instance).AlterarGradeAsync(
                fixture.OrganizacaoId, fixture.UnidadeUmId,
                fixture.MatriculaUnidadeUmId, fixture.UsuarioId,
                new(Inicio, selecao), Agora, CancellationToken.None);
        })).ToArray();

        var resultados = await Task.WhenAll(tarefas).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Single(resultados, item => item.Estado == EstadoMatriculas.Sucesso);
        Assert.Single(resultados, item => item.Estado == EstadoMatriculas.DataInvalida);
        Assert.Equal(2, await ContarAsync(db => db.MatriculasHorarios.CountAsync(item =>
            item.MatriculaId == fixture.MatriculaUnidadeUmId
            && item.VigenciaFim == null)));
        Assert.Equal(1, await ContarAsync(db => db.MatriculasHorarios.CountAsync(item =>
            item.MatriculaId == fixture.MatriculaUnidadeUmId
            && item.VigenciaFim != null)));
    }

    [Fact]
    public async Task Falha_da_grade_reverte_novo_aluno_responsavel_vinculo_e_matricula()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        await fixture.ExecuteAsync(
            """
            CREATE FUNCTION falhar_grade_nova_matricula_teste()
            RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
                RAISE EXCEPTION 'falha controlada da Grade operacional'
                    USING ERRCODE = '23514';
            END $f$;
            CREATE TRIGGER trg_falhar_grade_nova_matricula_teste
            BEFORE INSERT ON matriculas_horarios
            FOR EACH ROW EXECUTE FUNCTION falhar_grade_nova_matricula_teste();
            """);
        try
        {
            await using var db = CriarContexto();
            var resultado = await new MatriculasRepositorio(db, NullLogger<MatriculasRepositorio>.Instance).CriarAsync(
                fixture.OrganizacaoId, fixture.UnidadeUmId, fixture.UsuarioId,
                true, new(
                    null,
                    new("Menor rollback", new DateOnly(2012, 1, 1),
                        "33333333333", null, null),
                    [new("Responsável rollback", "44444444444", "15999990000",
                        null, TipoRelacaoResponsavel.ResponsavelLegal,
                        null, true, true)],
                    fixture.PlanoVersaoId,
                    Inicio,
                    100,
                    false,
                    null,
                    [fixture.HorariosUnidadeUm[0]]),
                Inicio, Agora, CancellationToken.None);

            Assert.Equal(EstadoMatriculas.ConflitoConcorrencia, resultado.Estado);
        }
        finally
        {
            await fixture.ExecuteAsync(
                "DROP TRIGGER trg_falhar_grade_nova_matricula_teste "
                + "ON matriculas_horarios; "
                + "DROP FUNCTION falhar_grade_nova_matricula_teste()");
        }

        Assert.Equal(0, await ContarAsync(db => db.Alunos.CountAsync(item =>
            item.Cpf == "33333333333")));
        Assert.Equal(0, await ContarAsync(db => db.Responsaveis.CountAsync(item =>
            item.Cpf == "44444444444")));
        Assert.Equal(0, await ContarAsync(db => db.AlunosResponsaveis.CountAsync(item =>
            db.Responsaveis.Any(responsavel =>
                responsavel.Id == item.ResponsavelId
                && responsavel.Cpf == "44444444444"))));
    }

    [Fact]
    public async Task Consultas_operacionais_sao_tenant_safe_e_traduzem_no_postgresql()
    {
        await fixture.ResetAsync(frequencia: 2, capacidade: 4);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await using var db = CriarContexto();
        var repositorio = new MatriculasRepositorio(db, NullLogger<MatriculasRepositorio>.Instance);

        var lista = await repositorio.ListarAsync(
            fixture.OrganizacaoId, fixture.UnidadeUmId, null, null,
            CancellationToken.None);
        var detalhe = await repositorio.ObterAsync(
            fixture.OrganizacaoId, fixture.UnidadeUmId,
            fixture.MatriculaUnidadeUmId, CancellationToken.None);
        var planos = await repositorio.ListarPlanosElegiveisAsync(
            fixture.OrganizacaoId, fixture.UnidadeUmId, Inicio,
            CancellationToken.None);
        var alunos = await repositorio.ListarAlunosRelacionadosAsync(
            fixture.OrganizacaoId, fixture.UnidadeUmId, null,
            CancellationToken.None);
        var horarios = await repositorio.ListarHorariosElegiveisAsync(
            fixture.OrganizacaoId, fixture.UnidadeUmId, Inicio,
            new DateOnly(2027, 8, 31), CancellationToken.None);
        var externo = await repositorio.ObterAsync(
            fixture.OrganizacaoId, fixture.UnidadeUmId,
            fixture.MatriculaUnidadeDoisId, CancellationToken.None);

        Assert.Single(lista);
        Assert.Equal(1, lista[0].QuantidadeHorariosAtuais);
        Assert.NotNull(detalhe);
        Assert.Single(detalhe.GradeAtual);
        Assert.Single(planos);
        Assert.Single(alunos);
        Assert.NotEmpty(horarios);
        Assert.Null(externo);
    }

    [Fact]
    public async Task Falha_ao_finalizar_matricula_reabre_grade_por_rollback()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await fixture.ExecuteAsync(
            """
            CREATE FUNCTION falhar_finalizacao_matricula_teste()
            RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
                IF NEW.status <> OLD.status THEN
                    RAISE EXCEPTION 'falha controlada na finalizacao'
                        USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER trg_falhar_finalizacao_matricula_teste
            BEFORE UPDATE ON matriculas
            FOR EACH ROW EXECUTE FUNCTION falhar_finalizacao_matricula_teste();
            """);
        try
        {
            await using var db = CriarContexto();
            var estado = await new MatriculasRepositorio(db, NullLogger<MatriculasRepositorio>.Instance).FinalizarAsync(
                fixture.OrganizacaoId, fixture.UnidadeUmId,
                fixture.MatriculaUnidadeUmId, fixture.UsuarioId,
                new DateOnly(2026, 8, 31), false, Agora,
                CancellationToken.None);

            Assert.Equal(EstadoMatriculas.ConflitoConcorrencia, estado);
        }
        finally
        {
            await fixture.ExecuteAsync(
                "DROP TRIGGER trg_falhar_finalizacao_matricula_teste ON matriculas; "
                + "DROP FUNCTION falhar_finalizacao_matricula_teste()");
        }

        await using var verificacao = CriarContexto();
        Assert.Equal(StatusMatricula.Ativa,
            (await verificacao.Matriculas.SingleAsync(item =>
                item.Id == fixture.MatriculaUnidadeUmId)).Status);
        Assert.Null((await verificacao.MatriculasHorarios.SingleAsync()).VigenciaFim);
    }

    private CriarMatriculaSolicitacao NovaSolicitacao(
        string cpf, Guid unidadeId, Guid horarioId) => new(
        null,
        new($"Aluno {cpf}", new DateOnly(2000, 1, 1), cpf, null,
            $"{cpf}@teste.local"),
        [], fixture.PlanoVersaoId, Inicio, 100, false, null, [horarioId]);

    private CriarMatriculaSolicitacao ExistenteSolicitacao(
        Guid alunoId, Guid horarioId) => new(
        alunoId, null, [], fixture.PlanoVersaoId, Inicio,
        100, false, null, [horarioId]);

    private BfaDbContext CriarContexto() => new(
        new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);

    private async Task<int> ContarAsync(Func<BfaDbContext, Task<int>> consulta)
    {
        await using var db = CriarContexto();
        return await consulta(db);
    }
}

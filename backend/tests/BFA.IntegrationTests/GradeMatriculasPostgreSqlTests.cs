using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace BFA.IntegrationTests;

public sealed class GradeMatriculasPostgreSqlTests(PostgreSqlEfemeroV013Fixture fixture)
    : IClassFixture<PostgreSqlEfemeroV013Fixture>
{
    [Fact]
    public async Task Frequencia_um_rejeita_segundo_slot_simultaneo()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 8);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            fixture.InsertGradeAsync(
                fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[1]));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Contains("frequencia semanal", exception.MessageText);
    }

    [Fact]
    public async Task Frequencia_sete_aceita_sete_e_rejeita_oito_simultaneos()
    {
        await fixture.ResetAsync(frequencia: 7, capacidade: 8);
        foreach (var horario in fixture.HorariosUnidadeUm.Take(7))
        {
            await fixture.InsertGradeAsync(fixture.MatriculaUnidadeUmId, horario);
        }

        Assert.Equal(7L, await fixture.CountGradeAsync());
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            fixture.InsertGradeAsync(
                fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[7]));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task Maximo_temporal_aceita_historicos_sucessivos_sem_falso_positivo()
    {
        await fixture.ResetAsync(frequencia: 2, capacidade: 8);
        await fixture.InsertGradeAsync(fixture.MatriculaUnidadeUmId,
            fixture.HorariosUnidadeUm[0], new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        await fixture.InsertGradeAsync(fixture.MatriculaUnidadeUmId,
            fixture.HorariosUnidadeUm[1], new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31));

        await fixture.InsertGradeAsync(fixture.MatriculaUnidadeUmId,
            fixture.HorariosUnidadeUm[2], new DateOnly(2026, 1, 1));

        Assert.Equal(3L, await fixture.CountGradeAsync());
    }

    [Fact]
    public async Task Mesmo_dia_aceita_horarios_adjacentes_e_rejeita_sobreposicao()
    {
        await fixture.ResetAsync(frequencia: 3, capacidade: 8);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[7]);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            fixture.InsertGradeAsync(
                fixture.MatriculaUnidadeUmId, fixture.HorarioSobrepostoUnidadeUmId));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Contains("conflitante", exception.MessageText);
    }

    [Fact]
    public async Task Periodos_nao_sobrepostos_permitem_reutilizar_mesmo_horario()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 1);
        var primeiro = await fixture.CreateEnrollmentAsync(fixture.UnidadeUmId);
        var segundo = await fixture.CreateEnrollmentAsync(fixture.UnidadeUmId);
        await fixture.InsertGradeAsync(primeiro, fixture.HorariosUnidadeUm[0],
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        await fixture.InsertGradeAsync(segundo, fixture.HorariosUnidadeUm[0],
            new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(2L, await fixture.CountGradeAsync());
    }

    [Fact]
    public async Task Capacidade_aceita_oitavo_rejeita_nono_e_historico_libera_vaga()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 8);
        var matriculas = new List<Guid>();
        for (var index = 0; index < 9; index++)
        {
            matriculas.Add(await fixture.CreateEnrollmentAsync(fixture.UnidadeUmId));
        }
        for (var index = 0; index < 8; index++)
        {
            await fixture.InsertGradeAsync(matriculas[index], fixture.HorariosUnidadeUm[0]);
        }

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            fixture.InsertGradeAsync(matriculas[8], fixture.HorariosUnidadeUm[0]));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);

        await fixture.CloseGradeAsync(matriculas[0], new DateOnly(2026, 6, 30));
        await fixture.InsertGradeAsync(matriculas[8], fixture.HorariosUnidadeUm[0],
            new DateOnly(2026, 7, 1));
        Assert.Equal(9L, await fixture.CountGradeAsync());
    }

    [Fact]
    public async Task Grade_aberta_bloqueia_matricula_e_horario_ate_ser_fechada()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 8);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);

        Assert.Equal(PostgresErrorCodes.CheckViolation,
            (await Assert.ThrowsAsync<PostgresException>(() => fixture.ExecuteAsync(
                "UPDATE matriculas SET status = 'Encerrada', data_fim_real = DATE '2026-06-30' "
                + "WHERE id = @id", ("id", fixture.MatriculaUnidadeUmId)))).SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation,
            (await Assert.ThrowsAsync<PostgresException>(() => fixture.ExecuteAsync(
                "UPDATE turmas_horarios SET ativo = false WHERE id = @id",
                ("id", fixture.HorariosUnidadeUm[0])))).SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation,
            (await Assert.ThrowsAsync<PostgresException>(() => fixture.ExecuteAsync(
                "UPDATE turmas_horarios SET vigencia_fim = DATE '2026-06-30' WHERE id = @id",
                ("id", fixture.HorariosUnidadeUm[0])))).SqlState);

        await fixture.CloseGradeAsync(
            fixture.MatriculaUnidadeUmId, new DateOnly(2026, 6, 30));
        await fixture.ExecuteAsync(
            "UPDATE matriculas SET status = 'Encerrada', data_fim_real = DATE '2026-06-30' "
            + "WHERE id = @id", ("id", fixture.MatriculaUnidadeUmId));
        await fixture.ExecuteAsync(
            "UPDATE turmas_horarios SET vigencia_fim = DATE '2026-06-30', ativo = false "
            + "WHERE id = @id", ("id", fixture.HorariosUnidadeUm[0]));
    }

    [Fact]
    public async Task Insert_fechado_e_rejeitado_mas_insert_aberto_continua_valido()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 8);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => fixture.ExecuteAsync(
            """
            INSERT INTO matriculas_horarios
                (id, organizacao_id, unidade_id, matricula_id, turma_horario_id,
                 vigencia_inicio, vigencia_fim, criado_por_usuario_id,
                 atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
            VALUES (@id, @organizacao, @unidade, @matricula, @horario,
                    DATE '2026-01-01', DATE '2026-06-30', @usuario, @usuario, now(), now())
            """,
            ("id", Guid.NewGuid()), ("organizacao", fixture.OrganizacaoId),
            ("unidade", fixture.UnidadeUmId), ("matricula", fixture.MatriculaUnidadeUmId),
            ("horario", fixture.HorariosUnidadeUm[0]), ("usuario", fixture.UsuarioId)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);

        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        Assert.Equal(1L, await fixture.CountGradeAsync());
    }

    [Theory]
    [InlineData("2026-08-31", "2026-06-30", false)]
    [InlineData("2026-06-30", "2026-06-30", true)]
    [InlineData("2026-05-31", "2026-06-30", true)]
    public async Task Finalizacao_da_matricula_respeita_toda_grade_historica(
        string fimGradeTexto, string fimMatriculaTexto, bool permitido)
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 8);
        var fimGrade = DateOnly.Parse(fimGradeTexto);
        var fimMatricula = DateOnly.Parse(fimMatriculaTexto);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await fixture.CloseGradeAsync(fixture.MatriculaUnidadeUmId, fimGrade);

        var operacao = () => fixture.ExecuteAsync(
            "UPDATE matriculas SET status = 'Encerrada', data_fim_real = @fim WHERE id = @id",
            ("fim", fimMatricula), ("id", fixture.MatriculaUnidadeUmId));
        if (permitido)
        {
            await operacao();
        }
        else
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(operacao);
            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        }
    }

    [Theory]
    [InlineData("2026-08-31", "2026-06-30", false)]
    [InlineData("2026-06-30", "2026-06-30", true)]
    public async Task Encerramento_do_horario_respeita_toda_grade_historica(
        string fimGradeTexto, string fimHorarioTexto, bool permitido)
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 8);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await fixture.CloseGradeAsync(
            fixture.MatriculaUnidadeUmId, DateOnly.Parse(fimGradeTexto));

        var operacao = () => fixture.ExecuteAsync(
            "UPDATE turmas_horarios SET vigencia_fim = @fim WHERE id = @id",
            ("fim", DateOnly.Parse(fimHorarioTexto)), ("id", fixture.HorariosUnidadeUm[0]));
        if (permitido)
        {
            await operacao();
        }
        else
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(operacao);
            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        }
    }

    [Fact]
    public async Task Inativacao_do_horario_ignora_historico_passado_e_bloqueia_compromisso_futuro()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 8);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await fixture.CloseGradeAsync(
            fixture.MatriculaUnidadeUmId, new DateOnly(2026, 8, 31));
        await fixture.ExecuteAsync(
            "UPDATE turmas_horarios SET ativo = false WHERE id = @id",
            ("id", fixture.HorariosUnidadeUm[0]));

        await fixture.ResetAsync(frequencia: 1, capacidade: 8);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await fixture.CloseGradeAsync(
            fixture.MatriculaUnidadeUmId, new DateOnly(2026, 12, 31));
        var exception = await Assert.ThrowsAsync<PostgresException>(() => fixture.ExecuteAsync(
            "UPDATE turmas_horarios SET ativo = false WHERE id = @id",
            ("id", fixture.HorariosUnidadeUm[0])));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task Reducao_de_capacidade_respeita_compromissos_e_ignora_historico_antigo()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 3);
        var segunda = await fixture.CreateEnrollmentAsync(fixture.UnidadeUmId);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await fixture.InsertGradeAsync(segunda, fixture.HorariosUnidadeUm[0]);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => fixture.ExecuteAsync(
            "UPDATE turmas SET capacidade = 1 WHERE id = @id",
            ("id", fixture.TurmaUnidadeUmId)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);

        await fixture.CloseAllGradeAsync(new DateOnly(2026, 8, 31));
        await fixture.ExecuteAsync(
            "UPDATE turmas SET capacidade = 1 WHERE id = @id",
            ("id", fixture.TurmaUnidadeUmId));
    }

    [Fact]
    public async Task Duas_sessoes_reais_disputando_ultima_vaga_admitem_uma_so()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 1);
        var primeira = await fixture.CreateEnrollmentAsync(fixture.UnidadeUmId);
        var segunda = await fixture.CreateEnrollmentAsync(fixture.UnidadeUmId);

        var resultados = await Task.WhenAll(
            fixture.TryInsertBatchAsync(primeira, [fixture.HorariosUnidadeUm[0]]),
            fixture.TryInsertBatchAsync(segunda, [fixture.HorariosUnidadeUm[0]]));

        Assert.Equal(1, resultados.Count(resultado => resultado));
        Assert.Equal(1L, await fixture.CountGradeAsync());
    }

    [Fact]
    public async Task Conflito_concorrente_do_mesmo_aluno_entre_unidades_nao_escapa()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 8);

        var resultados = await Task.WhenAll(
            fixture.TryInsertBatchAsync(fixture.MatriculaUnidadeUmId,
                [fixture.HorariosUnidadeUm[0]]),
            fixture.TryInsertBatchAsync(fixture.MatriculaUnidadeDoisId,
                [fixture.HorarioUnidadeDoisId]));

        Assert.Equal(1, resultados.Count(resultado => resultado));
        Assert.Equal(1L, await fixture.CountGradeAsync());
    }

    [Fact]
    public async Task Lotes_multi_slot_em_ordens_opostas_usam_ordem_canonica_sem_deadlock()
    {
        await fixture.ResetAsync(frequencia: 2, capacidade: 4);
        var outraMatricula = await fixture.CreateEnrollmentAsync(fixture.UnidadeUmId);
        var primeiro = fixture.HorariosUnidadeUm[0];
        var segundo = fixture.HorariosUnidadeUm[1];

        var resultados = await Task.WhenAll(
            fixture.TryInsertBatchAsync(fixture.MatriculaUnidadeUmId, [primeiro, segundo]),
            fixture.TryInsertBatchAsync(outraMatricula, [segundo, primeiro]));

        Assert.All(resultados, Assert.True);
        Assert.Equal(4L, await fixture.CountGradeAsync());
    }

    [Fact]
    public async Task Fechamento_da_grade_serializa_com_finalizacao_da_matricula()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 8);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await using var gradeConnection = await fixture.OpenAsync();
        await using var gradeTransaction = await gradeConnection.BeginTransactionAsync();
        await fixture.ExecuteAsync(gradeConnection,
            "UPDATE matriculas_horarios SET vigencia_fim = DATE '2026-06-30', "
            + "atualizado_por_usuario_id = @usuario, atualizado_em_utc = now() "
            + "WHERE matricula_id = @matricula",
            ("usuario", fixture.UsuarioId), ("matricula", fixture.MatriculaUnidadeUmId));

        await using var matriculaConnection = await fixture.OpenAsync();
        await using var matriculaTransaction = await matriculaConnection.BeginTransactionAsync();
        var finalizar = fixture.ExecuteAsync(matriculaConnection,
            "UPDATE matriculas SET status = 'Encerrada', data_fim_real = DATE '2026-06-30' "
            + "WHERE id = @id", ("id", fixture.MatriculaUnidadeUmId));
        await Task.Delay(100);
        Assert.False(finalizar.IsCompleted);

        await gradeTransaction.CommitAsync();
        await finalizar.WaitAsync(TimeSpan.FromSeconds(5));
        await matriculaTransaction.CommitAsync();
    }

    [Fact]
    public async Task Fechamento_da_grade_serializa_com_encerramento_do_horario()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 8);
        await fixture.InsertGradeAsync(
            fixture.MatriculaUnidadeUmId, fixture.HorariosUnidadeUm[0]);
        await using var gradeConnection = await fixture.OpenAsync();
        await using var gradeTransaction = await gradeConnection.BeginTransactionAsync();
        await fixture.ExecuteAsync(gradeConnection,
            "UPDATE matriculas_horarios SET vigencia_fim = DATE '2026-06-30', "
            + "atualizado_por_usuario_id = @usuario, atualizado_em_utc = now() "
            + "WHERE matricula_id = @matricula",
            ("usuario", fixture.UsuarioId), ("matricula", fixture.MatriculaUnidadeUmId));

        await using var horarioConnection = await fixture.OpenAsync();
        await using var horarioTransaction = await horarioConnection.BeginTransactionAsync();
        var encerrar = fixture.ExecuteAsync(horarioConnection,
            "UPDATE turmas_horarios SET vigencia_fim = DATE '2026-06-30' WHERE id = @id",
            ("id", fixture.HorariosUnidadeUm[0]));
        await Task.Delay(100);
        Assert.False(encerrar.IsCompleted);

        await gradeTransaction.CommitAsync();
        await encerrar.WaitAsync(TimeSpan.FromSeconds(5));
        await horarioTransaction.CommitAsync();
    }
}

public sealed class PostgreSqlEfemeroV013Fixture : IAsyncLifetime
{
    private string? _dataDirectory;
    private string? _pgCtlPath;

    public string ConnectionString { get; private set; } = string.Empty;
    public Guid OrganizacaoId { get; private set; }
    public Guid UnidadeUmId { get; private set; }
    public Guid UnidadeDoisId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid PlanoVersaoId { get; private set; }
    public Guid MatriculaUnidadeUmId { get; private set; }
    public Guid MatriculaUnidadeDoisId { get; private set; }
    public Guid TurmaUnidadeUmId { get; private set; }
    public Guid HorarioUnidadeDoisId { get; private set; }
    public Guid HorarioSobrepostoUnidadeUmId { get; private set; }
    public List<Guid> HorariosUnidadeUm { get; } = [];

    public async Task InitializeAsync()
    {
        var initDbPath = FindPostgreSqlExecutable("initdb");
        _pgCtlPath = FindPostgreSqlExecutable("pg_ctl");
        _dataDirectory = Path.Combine(
            Path.GetTempPath(), $"bfa-v013-concurrency-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDirectory);
        var port = GetAvailablePort();

        await RunProcessAsync(initDbPath,
            ["-D", _dataDirectory, "-A", "trust", "-U", "postgres", "--encoding=UTF8", "--no-locale"]);
        await RunProcessAsync(_pgCtlPath,
            ["-D", _dataDirectory, "-l", Path.Combine(_dataDirectory, "postgres.log"),
             "-o", $"-p {port} -h 127.0.0.1", "-w", "start"], false);
        ConnectionString = $"Host=127.0.0.1;Port={port};Database=postgres;Username=postgres;"
            + "Pooling=false;Timeout=5;Command Timeout=10";
        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        if (_pgCtlPath is not null && _dataDirectory is not null
            && File.Exists(Path.Combine(_dataDirectory, "postmaster.pid")))
        {
            await RunProcessAsync(_pgCtlPath,
                ["-D", _dataDirectory, "-m", "fast", "-w", "stop"], false);
        }
        if (_dataDirectory is not null && Directory.Exists(_dataDirectory))
        {
            var fullPath = Path.GetFullPath(_dataDirectory);
            var tempPath = Path.GetFullPath(Path.GetTempPath());
            if (!fullPath.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(fullPath).StartsWith("bfa-v013-concurrency-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Diretorio PostgreSQL temporario fora do escopo seguro.");
            }
            Directory.Delete(fullPath, true);
        }
    }

    public async Task ResetAsync(int frequencia, int capacidade)
    {
        OrganizacaoId = Guid.NewGuid();
        UnidadeUmId = Guid.NewGuid();
        UnidadeDoisId = Guid.NewGuid();
        UsuarioId = Guid.NewGuid();
        PlanoVersaoId = Guid.NewGuid();
        MatriculaUnidadeUmId = Guid.NewGuid();
        MatriculaUnidadeDoisId = Guid.NewGuid();
        TurmaUnidadeUmId = Guid.NewGuid();
        HorariosUnidadeUm.Clear();

        await ExecuteAsync("TRUNCATE organizacoes, usuarios CASCADE");
        await ExecuteAsync(
            """
            INSERT INTO organizacoes (id, nome, slug, ativa, criado_em_utc, atualizado_em_utc)
            VALUES (@organizacao, 'BFA Teste', @slug, true, now(), now());
            INSERT INTO unidades
                (id, organizacao_id, nome, slug, ativa, criado_em_utc, atualizado_em_utc)
            VALUES
                (@unidade_um, @organizacao, 'Unidade Um', @slug_um, true, now(), now()),
                (@unidade_dois, @organizacao, 'Unidade Dois', @slug_dois, true, now(), now());
            INSERT INTO usuarios
                (id, email_confirmado, telefone_confirmado, dois_fatores_habilitado,
                 bloqueio_habilitado, contagem_falhas_acesso)
            VALUES (@usuario, false, false, false, false, 0);
            """,
            ("organizacao", OrganizacaoId), ("slug", $"org-{Guid.NewGuid():N}"),
            ("unidade_um", UnidadeUmId), ("unidade_dois", UnidadeDoisId),
            ("slug_um", $"u1-{Guid.NewGuid():N}"), ("slug_dois", $"u2-{Guid.NewGuid():N}"),
            ("usuario", UsuarioId));

        var professorUm = await CreateProfessorAsync(UnidadeUmId, "Professor Um");
        var professorDois = await CreateProfessorAsync(UnidadeDoisId, "Professor Dois");
        var professorSobreposto = await CreateProfessorAsync(UnidadeUmId, "Professor Tres");
        var turmaUm = await CreateTurmaAsync(UnidadeUmId, professorUm, capacidade, "Turma Um");
        TurmaUnidadeUmId = turmaUm;
        var turmaDois = await CreateTurmaAsync(UnidadeDoisId, professorDois, capacidade, "Turma Dois");
        var turmaSobreposta = await CreateTurmaAsync(
            UnidadeUmId, professorSobreposto, capacidade, "Turma Sobreposta");

        for (short dia = 1; dia <= 7; dia++)
        {
            HorariosUnidadeUm.Add(await CreateScheduleAsync(
                UnidadeUmId, turmaUm, professorUm, dia, new TimeOnly(8, 0), new TimeOnly(9, 0)));
        }
        HorariosUnidadeUm.Add(await CreateScheduleAsync(
            UnidadeUmId, turmaUm, professorUm, 1, new TimeOnly(9, 0), new TimeOnly(10, 0)));
        HorarioSobrepostoUnidadeUmId = await CreateScheduleAsync(
            UnidadeUmId, turmaSobreposta, professorSobreposto, 1,
            new TimeOnly(8, 30), new TimeOnly(9, 30));
        HorarioUnidadeDoisId = await CreateScheduleAsync(
            UnidadeDoisId, turmaDois, professorDois, 1,
            new TimeOnly(8, 30), new TimeOnly(9, 30));

        var plano = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO planos
                (id, organizacao_id, unidade_id, nome, ativo, criado_por_usuario_id,
                 atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
            VALUES (@plano, @organizacao, NULL, 'Plano Teste', true, @usuario, @usuario, now(), now());
            INSERT INTO planos_versoes
                (id, organizacao_id, plano_id, numero_versao, duracao_meses,
                 frequencia_semanal, valor_mensal, cobra_matricula, valor_matricula,
                 vigencia_inicio, vigencia_fim, criado_por_usuario_id, criado_em_utc)
            VALUES (@versao, @organizacao, @plano, 1, 12, @frequencia, 100, false, NULL,
                    DATE '2026-01-01', NULL, @usuario, now());
            INSERT INTO planos_disponibilidades_unidades
                (id, organizacao_id, plano_id, unidade_id, ativo, criado_por_usuario_id,
                 atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
            VALUES
                (@disp_um, @organizacao, @plano, @unidade_um, true, @usuario, @usuario, now(), now()),
                (@disp_dois, @organizacao, @plano, @unidade_dois, true, @usuario, @usuario, now(), now());
            """,
            ("plano", plano), ("organizacao", OrganizacaoId), ("usuario", UsuarioId),
            ("versao", PlanoVersaoId), ("frequencia", frequencia),
            ("disp_um", Guid.NewGuid()), ("disp_dois", Guid.NewGuid()),
            ("unidade_um", UnidadeUmId), ("unidade_dois", UnidadeDoisId));

        var aluno = await CreateStudentAsync();
        MatriculaUnidadeUmId = await CreateEnrollmentForStudentAsync(aluno, UnidadeUmId);
        MatriculaUnidadeDoisId = await CreateEnrollmentForStudentAsync(aluno, UnidadeDoisId);
    }

    public async Task<Guid> CreateEnrollmentAsync(Guid unidadeId)
    {
        var aluno = await CreateStudentAsync();
        return await CreateEnrollmentForStudentAsync(aluno, unidadeId);
    }

    public async Task InsertGradeAsync(
        Guid matriculaId, Guid horarioId, DateOnly? inicio = null, DateOnly? fim = null)
    {
        var gradeId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO matriculas_horarios
                (id, organizacao_id, unidade_id, matricula_id, turma_horario_id,
                 vigencia_inicio, vigencia_fim, criado_por_usuario_id,
                 atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
            SELECT @id, @organizacao, matricula.unidade_id, matricula.id, @horario,
                   @inicio, @fim, @usuario, @usuario, now(), now()
            FROM matriculas AS matricula
            WHERE matricula.id = @matricula
            """,
            ("id", gradeId), ("organizacao", OrganizacaoId),
            ("horario", horarioId), ("inicio", inicio ?? new DateOnly(2026, 1, 1)),
            ("fim", DBNull.Value), ("usuario", UsuarioId),
            ("matricula", matriculaId));
        if (fim.HasValue)
        {
            await ExecuteAsync(
                "UPDATE matriculas_horarios SET vigencia_fim = @fim, "
                + "atualizado_por_usuario_id = @usuario, atualizado_em_utc = now() "
                + "WHERE id = @id",
                ("fim", fim.Value), ("usuario", UsuarioId), ("id", gradeId));
        }
    }

    public Task<long> CountGradeAsync() => ScalarAsync<long>(
        "SELECT count(*) FROM matriculas_horarios");

    public Task CloseGradeAsync(Guid matriculaId, DateOnly fim) => ExecuteAsync(
        "UPDATE matriculas_horarios SET vigencia_fim = @fim, "
        + "atualizado_por_usuario_id = @usuario, atualizado_em_utc = now() "
        + "WHERE matricula_id = @matricula AND vigencia_fim IS NULL",
        ("fim", fim), ("usuario", UsuarioId), ("matricula", matriculaId));

    public Task CloseAllGradeAsync(DateOnly fim) => ExecuteAsync(
        "UPDATE matriculas_horarios SET vigencia_fim = @fim, "
        + "atualizado_por_usuario_id = @usuario, atualizado_em_utc = now() "
        + "WHERE vigencia_fim IS NULL",
        ("fim", fim), ("usuario", UsuarioId));

    public async Task<bool> TryInsertBatchAsync(Guid matriculaId, IReadOnlyList<Guid> horarios)
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            foreach (var horario in horarios.OrderBy(
                id => id.ToString("D"), StringComparer.Ordinal))
            {
                await using var command = connection.CreateCommand();
                command.CommandTimeout = 8;
                command.CommandText =
                    "INSERT INTO matriculas_horarios "
                    + "(id, organizacao_id, unidade_id, matricula_id, turma_horario_id, "
                    + "vigencia_inicio, criado_por_usuario_id, atualizado_por_usuario_id, "
                    + "criado_em_utc, atualizado_em_utc) "
                    + "SELECT @id, @organizacao, unidade_id, id, @horario, DATE '2026-01-01', "
                    + "@usuario, @usuario, now(), now() FROM matriculas WHERE id = @matricula";
                command.Parameters.AddWithValue("id", Guid.NewGuid());
                command.Parameters.AddWithValue("organizacao", OrganizacaoId);
                command.Parameters.AddWithValue("horario", horario);
                command.Parameters.AddWithValue("usuario", UsuarioId);
                command.Parameters.AddWithValue("matricula", matriculaId);
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            return true;
        }
        catch (PostgresException exception) when (
            exception.SqlState is PostgresErrorCodes.CheckViolation
                or PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await command.ExecuteNonQueryAsync();
    }

    public async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 8;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public async Task<Guid> CreateStudentAsync()
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO alunos (id, organizacao_id, nome_completo, data_nascimento, ativo, "
            + "criado_em_utc, atualizado_em_utc) VALUES "
            + "(@id, @organizacao, 'Aluno Teste', DATE '2000-01-01', true, now(), now())",
            ("id", id), ("organizacao", OrganizacaoId));
        return id;
    }

    private async Task<Guid> CreateEnrollmentForStudentAsync(Guid alunoId, Guid unidadeId)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO matriculas
                (id, organizacao_id, unidade_id, aluno_id, plano_versao_id,
                 data_inicio, data_fim_prevista, data_fim_real, status,
                 valor_mensal_contratado, cobra_taxa_matricula, valor_taxa_matricula,
                 criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
            VALUES (@id, @organizacao, @unidade, @aluno, @versao,
                    DATE '2026-01-01', DATE '2026-12-31', NULL, 'Ativa',
                    100, false, NULL, @usuario, @usuario, now(), now())
            """,
            ("id", id), ("organizacao", OrganizacaoId), ("unidade", unidadeId),
            ("aluno", alunoId), ("versao", PlanoVersaoId), ("usuario", UsuarioId));
        return id;
    }

    public async Task<Guid> CreateProfessorAsync(Guid unidadeId, string nome)
    {
        var professor = Guid.NewGuid();
        var vinculo = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO professores
                (id, organizacao_id, nome_completo, ativo, criado_em_utc, atualizado_em_utc)
            VALUES (@professor, @organizacao, @nome, true, now(), now());
            INSERT INTO professores_unidades
                (id, organizacao_id, professor_id, unidade_id, ativo, criado_em_utc, atualizado_em_utc)
            VALUES (@vinculo, @organizacao, @professor, @unidade, true, now(), now());
            """,
            ("professor", professor), ("organizacao", OrganizacaoId),
            ("nome", nome), ("vinculo", vinculo), ("unidade", unidadeId));
        return vinculo;
    }

    private async Task<Guid> CreateTurmaAsync(
        Guid unidadeId, Guid professorUnidadeId, int capacidade, string nome)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO turmas
                (id, organizacao_id, unidade_id, professor_unidade_id, nome, capacidade,
                 ativo, criado_por_usuario_id, atualizado_por_usuario_id,
                 criado_em_utc, atualizado_em_utc)
            VALUES (@id, @organizacao, @unidade, @professor, @nome, @capacidade,
                    true, @usuario, @usuario, now(), now())
            """,
            ("id", id), ("organizacao", OrganizacaoId), ("unidade", unidadeId),
            ("professor", professorUnidadeId), ("nome", nome),
            ("capacidade", capacidade), ("usuario", UsuarioId));
        return id;
    }

    private async Task<Guid> CreateScheduleAsync(
        Guid unidadeId, Guid turmaId, Guid professorUnidadeId, short dia,
        TimeOnly inicio, TimeOnly fim)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO turmas_horarios
                (id, organizacao_id, unidade_id, turma_id, professor_unidade_id,
                 dia_semana, hora_inicio, hora_fim, vigencia_inicio, vigencia_fim,
                 ativo, criado_por_usuario_id, atualizado_por_usuario_id,
                 criado_em_utc, atualizado_em_utc)
            VALUES (@id, @organizacao, @unidade, @turma, @professor,
                    @dia, @inicio, @fim, DATE '2026-01-01', NULL,
                    true, @usuario, @usuario, now(), now())
            """,
            ("id", id), ("organizacao", OrganizacaoId), ("unidade", unidadeId),
            ("turma", turmaId), ("professor", professorUnidadeId), ("dia", dia),
            ("inicio", inicio), ("fim", fim), ("usuario", UsuarioId));
        return id;
    }

    private async Task ApplyMigrationsAsync()
    {
        await using var connection = await OpenAsync();
        await using (var role = connection.CreateCommand())
        {
            role.CommandText = "CREATE ROLE bfa_app_role";
            await role.ExecuteNonQueryAsync();
        }
        var directory = MigrationDirectory();
        foreach (var file in Directory.GetFiles(directory, "V*.sql")
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 30;
            command.CommandText = await File.ReadAllTextAsync(file);
            await command.ExecuteNonQueryAsync();
        }

    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static string MigrationDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "database")))
        {
            directory = directory.Parent;
        }
        return Path.Combine(directory!.FullName, "database", "migrations");
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindPostgreSqlExecutable(string executableName)
    {
        var fileName = OperatingSystem.IsWindows() ? $"{executableName}.exe" : executableName;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate)) return candidate;
        }
        if (OperatingSystem.IsWindows())
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL");
            if (Directory.Exists(root))
            {
                var candidate = Directory.GetDirectories(root)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => Path.Combine(path, "bin", fileName))
                    .FirstOrDefault(File.Exists);
                if (candidate is not null) return candidate;
            }
        }
        throw new InvalidOperationException($"PostgreSQL {fileName} e obrigatorio.");
    }

    private static async Task RunProcessAsync(
        string executable, IReadOnlyList<string> arguments, bool redirectOutput = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Nao foi possivel iniciar {executable}.");
        var outputTask = redirectOutput
            ? process.StandardOutput.ReadToEndAsync() : Task.FromResult(string.Empty);
        var errorTask = redirectOutput
            ? process.StandardError.ReadToEndAsync() : Task.FromResult(string.Empty);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(executable)} terminou com codigo {process.ExitCode}. "
                + $"Saida: {output} Erro: {error}");
        }
    }
}

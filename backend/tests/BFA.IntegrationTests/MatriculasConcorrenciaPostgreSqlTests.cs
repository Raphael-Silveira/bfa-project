using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace BFA.IntegrationTests;

public sealed class MatriculasConcorrenciaPostgreSqlTests(
    PostgreSqlEfemeroV012Fixture fixture)
    : IClassFixture<PostgreSqlEfemeroV012Fixture>
{
    [Fact]
    public async Task Reativacao_de_disponibilidade_concorrente_com_matricula_nao_deadlocka()
    {
        await fixture.ResetAsync(disponibilidadeAtiva: false);
        await using var matriculaConnection = await fixture.OpenAsync();
        await using var disponibilidadeConnection = await fixture.OpenAsync();
        await using var matriculaTransaction = await matriculaConnection.BeginTransactionAsync();
        await SetPauseAsync(matriculaConnection);

        var inserirMatricula = InserirMatriculaRedeAsync(
            matriculaConnection,
            new DateOnly(2026, 10, 15));
        await Task.Delay(75);

        await using var disponibilidadeTransaction =
            await disponibilidadeConnection.BeginTransactionAsync();
        var reativar = ExecuteAsync(
            disponibilidadeConnection,
            "UPDATE disponibilidades SET ativo = true WHERE id = @id",
            ("id", fixture.DisponibilidadeId));

        Assert.Equal(1, await reativar.WaitAsync(TimeSpan.FromSeconds(5)));
        await disponibilidadeTransaction.CommitAsync();
        Assert.Equal(
            1,
            await inserirMatricula.WaitAsync(TimeSpan.FromSeconds(5)));
        await matriculaTransaction.CommitAsync();

        Assert.True(await ScalarAsync<bool>(
            matriculaConnection,
            "SELECT ativo FROM disponibilidades WHERE id = @id",
            ("id", fixture.DisponibilidadeId)));
        Assert.Equal(
            1L,
            await ScalarAsync<long>(matriculaConnection, "SELECT count(*) FROM matriculas"));
    }

    [Fact]
    public async Task Desativacao_de_disponibilidade_concorrente_rejeita_matricula_posterior()
    {
        await fixture.ResetAsync(disponibilidadeAtiva: true);
        await using var disponibilidadeConnection = await fixture.OpenAsync();
        await using var matriculaConnection = await fixture.OpenAsync();
        await using var disponibilidadeTransaction =
            await disponibilidadeConnection.BeginTransactionAsync();
        Assert.Equal(
            1,
            await ExecuteAsync(
                disponibilidadeConnection,
                "UPDATE disponibilidades SET ativo = false WHERE id = @id",
                ("id", fixture.DisponibilidadeId)));

        await using var matriculaTransaction = await matriculaConnection.BeginTransactionAsync();
        var inserirMatricula = InserirMatriculaRedeAsync(
            matriculaConnection,
            new DateOnly(2026, 10, 15));
        await Task.Delay(100);
        Assert.False(inserirMatricula.IsCompleted);

        await disponibilidadeTransaction.CommitAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
            await inserirMatricula.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        await matriculaTransaction.RollbackAsync();

        Assert.False(await ScalarAsync<bool>(
            disponibilidadeConnection,
            "SELECT ativo FROM disponibilidades WHERE id = @id",
            ("id", fixture.DisponibilidadeId)));
        Assert.Equal(
            0L,
            await ScalarAsync<long>(
                disponibilidadeConnection,
                "SELECT count(*) FROM matriculas"));
    }

    [Fact]
    public async Task Matricula_que_bloqueia_primeiro_impede_encerramento_temporalmente_invalido()
    {
        await fixture.ResetAsync(disponibilidadeAtiva: true);
        await using var matriculaConnection = await fixture.OpenAsync();
        await using var versaoConnection = await fixture.OpenAsync();
        await using var matriculaTransaction = await matriculaConnection.BeginTransactionAsync();
        await SetPauseAsync(matriculaConnection);
        var inserirMatricula = InserirMatriculaLocalAsync(
            matriculaConnection,
            new DateOnly(2026, 10, 15));
        await Task.Delay(75);

        await using var versaoTransaction = await versaoConnection.BeginTransactionAsync();
        var encerrarVersao = ExecuteAsync(
            versaoConnection,
            "UPDATE versoes SET vigencia_fim = DATE '2026-09-30' WHERE id = @id",
            ("id", fixture.VersaoLocalId));

        Assert.Equal(
            1,
            await inserirMatricula.WaitAsync(TimeSpan.FromSeconds(5)));
        await matriculaTransaction.CommitAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
            await encerrarVersao.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        await versaoTransaction.RollbackAsync();

        Assert.Null(await ScalarAsync<DateOnly?>(
            matriculaConnection,
            "SELECT vigencia_fim FROM versoes WHERE id = @id",
            ("id", fixture.VersaoLocalId)));
        Assert.Equal(
            1L,
            await ScalarAsync<long>(matriculaConnection, "SELECT count(*) FROM matriculas"));
    }

    [Fact]
    public async Task Encerramento_que_bloqueia_primeiro_faz_matricula_revalidar_vigencia()
    {
        await fixture.ResetAsync(disponibilidadeAtiva: true);
        await using var versaoConnection = await fixture.OpenAsync();
        await using var matriculaConnection = await fixture.OpenAsync();
        await using var versaoTransaction = await versaoConnection.BeginTransactionAsync();
        Assert.Equal(
            1,
            await ExecuteAsync(
                versaoConnection,
                "UPDATE versoes SET vigencia_fim = DATE '2026-09-30' WHERE id = @id",
                ("id", fixture.VersaoLocalId)));

        await using var matriculaTransaction = await matriculaConnection.BeginTransactionAsync();
        var inserirMatricula = InserirMatriculaLocalAsync(
            matriculaConnection,
            new DateOnly(2026, 10, 15));
        await Task.Delay(100);
        Assert.False(inserirMatricula.IsCompleted);

        await versaoTransaction.CommitAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
            await inserirMatricula.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        await matriculaTransaction.RollbackAsync();

        Assert.Equal(
            new DateOnly(2026, 9, 30),
            await ScalarAsync<DateOnly>(
                versaoConnection,
                "SELECT vigencia_fim FROM versoes WHERE id = @id",
                ("id", fixture.VersaoLocalId)));
        Assert.Equal(
            0L,
            await ScalarAsync<long>(versaoConnection, "SELECT count(*) FROM matriculas"));
    }

    private Task<int> InserirMatriculaRedeAsync(
        NpgsqlConnection connection,
        DateOnly dataInicio) =>
        InserirMatriculaAsync(connection, fixture.VersaoRedeId, dataInicio);

    private Task<int> InserirMatriculaLocalAsync(
        NpgsqlConnection connection,
        DateOnly dataInicio) =>
        InserirMatriculaAsync(connection, fixture.VersaoLocalId, dataInicio);

    private async Task<int> InserirMatriculaAsync(
        NpgsqlConnection connection,
        Guid versaoId,
        DateOnly dataInicio) =>
        await ExecuteAsync(
            connection,
            """
            INSERT INTO matriculas
                (id, organizacao_id, unidade_id, aluno_id, plano_versao_id,
                 data_inicio, status)
            VALUES
                (@id, @organizacao_id, @unidade_id, @aluno_id, @plano_versao_id,
                 @data_inicio, 'Ativa')
            """,
            ("id", Guid.NewGuid()),
            ("organizacao_id", fixture.OrganizacaoId),
            ("unidade_id", fixture.UnidadeId),
            ("aluno_id", fixture.AlunoId),
            ("plano_versao_id", versaoId),
            ("data_inicio", dataInicio));

    private static Task<int> SetPauseAsync(NpgsqlConnection connection) =>
        ExecuteAsync(connection, "SET LOCAL bfa.test_pause = 'on'");

    private static async Task<int> ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 6;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default! : (T)result;
    }
}

public sealed class PostgreSqlEfemeroV012Fixture : IAsyncLifetime
{
    private string? _dataDirectory;
    private string? _pgCtlPath;

    public string ConnectionString { get; private set; } = string.Empty;
    public Guid OrganizacaoId { get; private set; }
    public Guid UnidadeId { get; private set; }
    public Guid AlunoId { get; private set; }
    public Guid PlanoRedeId { get; private set; }
    public Guid PlanoLocalId { get; private set; }
    public Guid VersaoRedeId { get; private set; }
    public Guid VersaoLocalId { get; private set; }
    public Guid DisponibilidadeId { get; private set; }

    public async Task InitializeAsync()
    {
        var initDbPath = FindPostgreSqlExecutable("initdb");
        _pgCtlPath = FindPostgreSqlExecutable("pg_ctl");
        _dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"bfa-v012-concurrency-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDirectory);
        var port = GetAvailablePort();

        await RunProcessAsync(
            initDbPath,
            ["-D", _dataDirectory, "-A", "trust", "-U", "postgres", "--encoding=UTF8", "--no-locale"]);
        await RunProcessAsync(
            _pgCtlPath,
            [
                "-D", _dataDirectory,
                "-l", Path.Combine(_dataDirectory, "postgres.log"),
                "-o", $"-p {port} -h 127.0.0.1",
                "-w", "start"
            ],
            redirectOutput: false);

        ConnectionString =
            $"Host=127.0.0.1;Port={port};Database=postgres;Username=postgres;"
            + "Pooling=false;Timeout=5;Command Timeout=6";
        await CreateStructureAsync();
    }

    public async Task DisposeAsync()
    {
        if (_pgCtlPath is not null
            && _dataDirectory is not null
            && File.Exists(Path.Combine(_dataDirectory, "postmaster.pid")))
        {
            await RunProcessAsync(
                _pgCtlPath,
                ["-D", _dataDirectory, "-m", "fast", "-w", "stop"],
                redirectOutput: false);
        }

        if (_dataDirectory is not null && Directory.Exists(_dataDirectory))
        {
            var fullPath = Path.GetFullPath(_dataDirectory);
            var tempPath = Path.GetFullPath(Path.GetTempPath());
            if (!fullPath.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(fullPath).StartsWith(
                    "bfa-v012-concurrency-",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "O diretorio temporario do PostgreSQL nao passou pela validacao de seguranca.");
            }
            Directory.Delete(fullPath, recursive: true);
        }
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task ResetAsync(bool disponibilidadeAtiva)
    {
        OrganizacaoId = Guid.NewGuid();
        UnidadeId = Guid.NewGuid();
        AlunoId = Guid.NewGuid();
        PlanoRedeId = Guid.NewGuid();
        PlanoLocalId = Guid.NewGuid();
        VersaoRedeId = Guid.NewGuid();
        VersaoLocalId = Guid.NewGuid();
        DisponibilidadeId = Guid.NewGuid();

        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            TRUNCATE matriculas, disponibilidades, versoes, planos, alunos, unidades;
            INSERT INTO unidades (id, organizacao_id, ativo)
            VALUES (@unidade_id, @organizacao_id, true);
            INSERT INTO alunos (id, organizacao_id, ativo)
            VALUES (@aluno_id, @organizacao_id, true);
            INSERT INTO planos (id, organizacao_id, unidade_id, ativo)
            VALUES
                (@plano_rede_id, @organizacao_id, NULL, true),
                (@plano_local_id, @organizacao_id, @unidade_id, true);
            INSERT INTO versoes
                (id, organizacao_id, plano_id, vigencia_inicio, vigencia_fim)
            VALUES
                (@versao_rede_id, @organizacao_id, @plano_rede_id,
                 DATE '2026-01-01', NULL),
                (@versao_local_id, @organizacao_id, @plano_local_id,
                 DATE '2026-01-01', NULL);
            INSERT INTO disponibilidades
                (id, organizacao_id, plano_id, unidade_id, ativo)
            VALUES
                (@disponibilidade_id, @organizacao_id, @plano_rede_id,
                 @unidade_id, @disponibilidade_ativa);
            """;
        command.Parameters.AddWithValue("unidade_id", UnidadeId);
        command.Parameters.AddWithValue("organizacao_id", OrganizacaoId);
        command.Parameters.AddWithValue("aluno_id", AlunoId);
        command.Parameters.AddWithValue("plano_rede_id", PlanoRedeId);
        command.Parameters.AddWithValue("plano_local_id", PlanoLocalId);
        command.Parameters.AddWithValue("versao_rede_id", VersaoRedeId);
        command.Parameters.AddWithValue("versao_local_id", VersaoLocalId);
        command.Parameters.AddWithValue("disponibilidade_id", DisponibilidadeId);
        command.Parameters.AddWithValue("disponibilidade_ativa", disponibilidadeAtiva);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateStructureAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE unidades (
                id uuid PRIMARY KEY,
                organizacao_id uuid NOT NULL,
                ativo boolean NOT NULL,
                UNIQUE (organizacao_id, id));
            CREATE TABLE alunos (
                id uuid PRIMARY KEY,
                organizacao_id uuid NOT NULL,
                ativo boolean NOT NULL,
                UNIQUE (organizacao_id, id));
            CREATE TABLE planos (
                id uuid PRIMARY KEY,
                organizacao_id uuid NOT NULL,
                unidade_id uuid NULL,
                ativo boolean NOT NULL,
                UNIQUE (organizacao_id, id));
            CREATE TABLE versoes (
                id uuid PRIMARY KEY,
                organizacao_id uuid NOT NULL,
                plano_id uuid NOT NULL,
                vigencia_inicio date NOT NULL,
                vigencia_fim date NULL,
                UNIQUE (organizacao_id, id),
                FOREIGN KEY (organizacao_id, plano_id)
                    REFERENCES planos (organizacao_id, id));
            CREATE TABLE disponibilidades (
                id uuid PRIMARY KEY,
                organizacao_id uuid NOT NULL,
                plano_id uuid NOT NULL,
                unidade_id uuid NOT NULL,
                ativo boolean NOT NULL,
                UNIQUE (organizacao_id, plano_id, unidade_id),
                FOREIGN KEY (organizacao_id, plano_id)
                    REFERENCES planos (organizacao_id, id),
                FOREIGN KEY (organizacao_id, unidade_id)
                    REFERENCES unidades (organizacao_id, id));
            CREATE TABLE matriculas (
                id uuid PRIMARY KEY,
                organizacao_id uuid NOT NULL,
                unidade_id uuid NOT NULL,
                aluno_id uuid NOT NULL,
                plano_versao_id uuid NOT NULL,
                data_inicio date NOT NULL,
                status varchar(20) NOT NULL,
                FOREIGN KEY (organizacao_id, unidade_id)
                    REFERENCES unidades (organizacao_id, id),
                FOREIGN KEY (organizacao_id, aluno_id)
                    REFERENCES alunos (organizacao_id, id),
                FOREIGN KEY (organizacao_id, plano_versao_id)
                    REFERENCES versoes (organizacao_id, id));

            CREATE FUNCTION proteger_disponibilidade()
            RETURNS trigger LANGUAGE plpgsql AS $f$
            DECLARE plano_ativo boolean; plano_unidade_id uuid; unidade_ativa boolean;
            BEGIN
                SELECT ativo, unidade_id INTO plano_ativo, plano_unidade_id
                FROM planos
                WHERE organizacao_id = NEW.organizacao_id AND id = NEW.plano_id;
                IF plano_unidade_id IS NOT NULL THEN
                    RAISE EXCEPTION 'plano local' USING ERRCODE = '23514';
                END IF;
                IF NEW.ativo THEN
                    SELECT ativo INTO unidade_ativa FROM unidades
                    WHERE organizacao_id = NEW.organizacao_id AND id = NEW.unidade_id;
                    IF plano_ativo IS DISTINCT FROM true OR unidade_ativa IS DISTINCT FROM true THEN
                        RAISE EXCEPTION 'referencia inativa' USING ERRCODE = '23514';
                    END IF;
                END IF;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER trg_proteger_disponibilidade
            BEFORE INSERT OR UPDATE ON disponibilidades
            FOR EACH ROW EXECUTE FUNCTION proteger_disponibilidade();

            CREATE FUNCTION proteger_matricula()
            RETURNS trigger LANGUAGE plpgsql AS $f$
            DECLARE plano_id_atual uuid; inicio date; fim date; plano_ativo boolean;
                plano_unidade_id uuid; disponibilidade_ativa boolean;
                unidade_ativa boolean; aluno_ativo boolean;
            BEGIN
                SELECT plano_id, vigencia_inicio, vigencia_fim
                INTO plano_id_atual, inicio, fim
                FROM versoes
                WHERE organizacao_id = NEW.organizacao_id
                  AND id = NEW.plano_versao_id
                FOR UPDATE;
                IF NEW.data_inicio < inicio
                    OR (fim IS NOT NULL AND NEW.data_inicio > fim) THEN
                    RAISE EXCEPTION 'fora da vigencia' USING ERRCODE = '23514';
                END IF;
                SELECT ativo, unidade_id
                INTO plano_ativo, plano_unidade_id
                FROM planos
                WHERE organizacao_id = NEW.organizacao_id AND id = plano_id_atual
                FOR UPDATE;
                IF current_setting('bfa.test_pause', true) = 'on' THEN
                    PERFORM pg_sleep(0.30);
                END IF;
                IF plano_ativo IS DISTINCT FROM true THEN
                    RAISE EXCEPTION 'plano inativo' USING ERRCODE = '23514';
                END IF;
                IF plano_unidade_id IS NULL THEN
                    SELECT ativo INTO disponibilidade_ativa
                    FROM disponibilidades
                    WHERE organizacao_id = NEW.organizacao_id
                      AND plano_id = plano_id_atual
                      AND unidade_id = NEW.unidade_id
                    FOR UPDATE;
                    IF disponibilidade_ativa IS DISTINCT FROM true THEN
                        RAISE EXCEPTION 'disponibilidade inativa' USING ERRCODE = '23514';
                    END IF;
                ELSIF plano_unidade_id <> NEW.unidade_id THEN
                    RAISE EXCEPTION 'unidade divergente' USING ERRCODE = '23514';
                END IF;
                SELECT ativo INTO unidade_ativa FROM unidades
                WHERE organizacao_id = NEW.organizacao_id AND id = NEW.unidade_id
                FOR UPDATE;
                SELECT ativo INTO aluno_ativo FROM alunos
                WHERE organizacao_id = NEW.organizacao_id AND id = NEW.aluno_id
                FOR UPDATE;
                IF unidade_ativa IS DISTINCT FROM true OR aluno_ativo IS DISTINCT FROM true THEN
                    RAISE EXCEPTION 'participante inativo' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER trg_proteger_matricula
            BEFORE INSERT ON matriculas
            FOR EACH ROW EXECUTE FUNCTION proteger_matricula();

            CREATE FUNCTION proteger_versao_base()
            RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
                PERFORM 1 FROM planos
                WHERE organizacao_id = NEW.organizacao_id AND id = NEW.plano_id
                FOR UPDATE;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER trg_proteger_plano_versao
            BEFORE UPDATE ON versoes
            FOR EACH ROW EXECUTE FUNCTION proteger_versao_base();

            CREATE FUNCTION proteger_versao_matriculas()
            RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
                IF OLD.vigencia_fim IS NULL AND NEW.vigencia_fim IS NOT NULL AND EXISTS (
                    SELECT 1 FROM matriculas
                    WHERE organizacao_id = NEW.organizacao_id
                      AND plano_versao_id = NEW.id
                      AND data_inicio > NEW.vigencia_fim) THEN
                    RAISE EXCEPTION 'vigencia invalida' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER trg_proteger_plano_versao_matriculas
            BEFORE UPDATE ON versoes
            FOR EACH ROW EXECUTE FUNCTION proteger_versao_matriculas();
            """;
        await command.ExecuteNonQueryAsync();
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
        var fileName = OperatingSystem.IsWindows()
            ? $"{executableName}.exe"
            : executableName;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PostgreSQL");
            if (Directory.Exists(root))
            {
                var candidate = Directory.GetDirectories(root)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => Path.Combine(path, "bin", fileName))
                    .FirstOrDefault(File.Exists);
                if (candidate is not null)
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException(
            $"O executavel PostgreSQL {fileName} e obrigatorio para os testes concorrentes reais.");
    }

    private static async Task RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        bool redirectOutput = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Nao foi possivel iniciar {executable}.");
        var outputTask = redirectOutput
            ? process.StandardOutput.ReadToEndAsync()
            : Task.FromResult(string.Empty);
        var errorTask = redirectOutput
            ? process.StandardError.ReadToEndAsync()
            : Task.FromResult(string.Empty);
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

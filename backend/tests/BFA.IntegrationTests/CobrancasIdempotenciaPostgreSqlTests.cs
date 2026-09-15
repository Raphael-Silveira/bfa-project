using BFA.Domain.Cobrancas;
using BFA.Infrastructure.Cobrancas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace BFA.IntegrationTests;

public sealed class CobrancasIdempotenciaPostgreSqlTests(
    PostgreSqlCobrancasFixture fixture) : IClassFixture<PostgreSqlCobrancasFixture>
{
    [Fact]
    public async Task V021_aplica_em_base_sem_duplicidade()
    {
        await fixture.ResetAsync();

        await fixture.AplicarV021Async();

        Assert.True(await fixture.IndiceExisteAsync("uq_cobrancas_mensalidade_competencia"));
        Assert.True(await fixture.IndiceExisteAsync("uq_cobrancas_taxa_matricula"));
    }

    [Fact]
    public async Task V021_falha_antes_dos_indices_com_mensalidades_duplicadas()
    {
        await fixture.ResetAsync();
        await fixture.InserirAsync(TipoCobranca.Mensalidade, fixture.OrganizacaoId, fixture.UnidadeId, fixture.MatriculaId, new(2026, 9, 5), StatusCobranca.Pendente);
        await fixture.InserirAsync(TipoCobranca.Mensalidade, fixture.OrganizacaoId, fixture.UnidadeId, fixture.MatriculaId, new(2026, 9, 6), StatusCobranca.Cancelada);

        var exception = await Assert.ThrowsAsync<PostgresException>(fixture.AplicarV021Async);

        Assert.Contains("V021 bloqueada: duplicidade de mensalidade", exception.Message, StringComparison.Ordinal);
        Assert.False(await fixture.IndiceExisteAsync("uq_cobrancas_mensalidade_competencia"));
        Assert.False(await fixture.IndiceExisteAsync("uq_cobrancas_taxa_matricula"));
    }

    [Fact]
    public async Task V021_falha_antes_dos_indices_com_taxas_duplicadas()
    {
        await fixture.ResetAsync();
        await fixture.InserirAsync(TipoCobranca.Matricula, fixture.OrganizacaoId, fixture.UnidadeId, fixture.MatriculaId, new(2026, 9, 5), StatusCobranca.Pendente);
        await fixture.InserirAsync(TipoCobranca.Matricula, fixture.OrganizacaoId, fixture.UnidadeId, fixture.MatriculaId, new(2026, 9, 6), StatusCobranca.Cancelada);

        var exception = await Assert.ThrowsAsync<PostgresException>(fixture.AplicarV021Async);

        Assert.Contains("V021 bloqueada: duplicidade de taxa de matricula", exception.Message, StringComparison.Ordinal);
        Assert.False(await fixture.IndiceExisteAsync("uq_cobrancas_mensalidade_competencia"));
        Assert.False(await fixture.IndiceExisteAsync("uq_cobrancas_taxa_matricula"));
    }

    [Fact]
    public async Task V021_permite_duas_cobrancas_avulsas()
    {
        await fixture.ResetAsync();
        await fixture.InserirAsync(TipoCobranca.Avulso, fixture.OrganizacaoId, fixture.UnidadeId, fixture.MatriculaId, new(2026, 9, 5), StatusCobranca.Pendente);
        await fixture.InserirAsync(TipoCobranca.Avulso, fixture.OrganizacaoId, fixture.UnidadeId, fixture.MatriculaId, new(2026, 9, 5), StatusCobranca.Pendente);

        await fixture.AplicarV021Async();

        Assert.Equal(2, await fixture.ContarAsync());
    }

    [Fact]
    public async Task V021_permite_competencias_mensais_diferentes_e_tenants_diferentes()
    {
        await fixture.ResetAsync();
        await fixture.InserirAsync(TipoCobranca.Mensalidade, fixture.OrganizacaoId, fixture.UnidadeId, fixture.MatriculaId, new(2026, 9, 5), StatusCobranca.Pendente);
        await fixture.InserirAsync(TipoCobranca.Mensalidade, fixture.OrganizacaoId, fixture.UnidadeId, fixture.MatriculaId, new(2026, 10, 5), StatusCobranca.Pendente);
        await fixture.InserirAsync(TipoCobranca.Mensalidade, Guid.NewGuid(), Guid.NewGuid(), fixture.MatriculaId, new(2026, 9, 5), StatusCobranca.Pendente);

        await fixture.AplicarV021Async();

        Assert.Equal(3, await fixture.ContarAsync());
    }

    [Fact]
    public async Task Criacao_automatica_concorrente_de_mensalidade_retorna_um_vencedor()
    {
        await fixture.ResetAsync();
        await fixture.AplicarV021Async();

        var cobrancaUm = fixture.NovaCobranca(TipoCobranca.Mensalidade, new(2026, 9, 5));
        var cobrancaDois = fixture.NovaCobranca(TipoCobranca.Mensalidade, new(2026, 9, 6));

        var resultados = await CriarConcorrenteAsync(cobrancaUm, cobrancaDois);

        Assert.Equal(resultados[0].Id, resultados[1].Id);
        Assert.Equal(1, await fixture.ContarAsync());
    }

    [Fact]
    public async Task Criacao_automatica_concorrente_de_taxa_retorna_um_vencedor()
    {
        await fixture.ResetAsync();
        await fixture.AplicarV021Async();

        var cobrancaUm = fixture.NovaCobranca(TipoCobranca.Matricula, new(2026, 9, 5));
        var cobrancaDois = fixture.NovaCobranca(TipoCobranca.Matricula, new(2026, 9, 5));

        var resultados = await CriarConcorrenteAsync(cobrancaUm, cobrancaDois);

        Assert.Equal(resultados[0].Id, resultados[1].Id);
        Assert.Equal(1, await fixture.ContarAsync());
    }

    [Fact]
    public async Task Violacao_de_unicidade_nao_financeira_e_propagada()
    {
        await fixture.ResetAsync();
        await fixture.AplicarV021Async();

        var cobranca = fixture.NovaCobranca(TipoCobranca.Mensalidade, new(2026, 9, 5));
        await fixture.InserirAsync(
            cobranca.Tipo,
            cobranca.OrganizacaoId,
            cobranca.UnidadeId,
            cobranca.MatriculaId,
            cobranca.DataVencimento,
            cobranca.Status,
            cobranca.Id);

        await using var conexao = new NpgsqlConnection(fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var contexto = fixture.CriarContexto(conexao);
        var repositorio = new CobrancasRepositorio(
            contexto, NullLogger<CobrancasRepositorio>.Instance);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repositorio.CriarAutomaticaIdempotenteAsync(cobranca, CancellationToken.None));
    }

    [Fact]
    public async Task Pagamento_consolidado_quita_cada_cobranca_integralmente()
    {
        await fixture.ResetAsync();
        await fixture.AplicarV021Async();
        var primeiraId = Guid.NewGuid();
        var segundaId = Guid.NewGuid();
        await fixture.InserirAsync(
            TipoCobranca.Avulso, fixture.OrganizacaoId, fixture.UnidadeId,
            fixture.MatriculaId, new(2026, 9, 5), StatusCobranca.Pendente, primeiraId);
        await fixture.InserirAsync(
            TipoCobranca.Avulso, fixture.OrganizacaoId, fixture.UnidadeId,
            fixture.MatriculaId, new(2026, 9, 6), StatusCobranca.Pendente, segundaId);

        await using var conexao = new NpgsqlConnection(fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var contexto = fixture.CriarContexto(conexao);
        var repositorio = new CobrancasRepositorio(
            contexto, NullLogger<CobrancasRepositorio>.Instance);

        var pagamentos = await repositorio.RegistrarPagamentoConsolidadoAsync(
            fixture.OrganizacaoId, fixture.UnidadeId, [primeiraId, segundaId],
            new(2026, 9, 15), FormaPagamento.Pix, null, Guid.NewGuid(),
            DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(2, pagamentos.Count);
        Assert.Equal(2, await fixture.ContarPagamentosAsync());
        Assert.Equal(2, await fixture.ContarCobrancasComStatusAsync(StatusCobranca.Paga));
        Assert.Equal(0, await fixture.ContarCobrancasComValorPagoMenorQueValorAsync());
    }

    private async Task<Cobranca[]> CriarConcorrenteAsync(Cobranca primeira, Cobranca segunda)
    {
        await using var conexaoUm = new NpgsqlConnection(fixture.ConnectionString);
        await using var conexaoDois = new NpgsqlConnection(fixture.ConnectionString);
        await conexaoUm.OpenAsync();
        await conexaoDois.OpenAsync();

        await using var contextoUm = fixture.CriarContexto(conexaoUm);
        await using var contextoDois = fixture.CriarContexto(conexaoDois);
        var repositorioUm = new CobrancasRepositorio(contextoUm, NullLogger<CobrancasRepositorio>.Instance);
        var repositorioDois = new CobrancasRepositorio(contextoDois, NullLogger<CobrancasRepositorio>.Instance);

        return await Task.WhenAll(
            repositorioUm.CriarAutomaticaIdempotenteAsync(primeira, CancellationToken.None),
            repositorioDois.CriarAutomaticaIdempotenteAsync(segunda, CancellationToken.None));
    }
}

public sealed class PostgreSqlCobrancasFixture : IAsyncLifetime
{
    public readonly Guid OrganizacaoId = Guid.NewGuid();
    public readonly Guid UnidadeId = Guid.NewGuid();
    public readonly Guid MatriculaId = Guid.NewGuid();
    public readonly Guid AlunoId = Guid.NewGuid();
    public string ConnectionString { get; private set; } = string.Empty;

    private string? _dataDirectory;
    private string? _pgCtlPath;

    public async Task InitializeAsync()
    {
        var initDbPath = FindPostgreSqlExecutable("initdb");
        _pgCtlPath = FindPostgreSqlExecutable("pg_ctl");
        _dataDirectory = Path.Combine(Path.GetTempPath(), $"bfa-financeiro-idempotencia-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDirectory);
        var port = GetAvailablePort();

        await RunProcessAsync(initDbPath, ["-D", _dataDirectory, "-A", "trust", "-U", "postgres", "--encoding=UTF8", "--no-locale"]);
        await RunProcessAsync(_pgCtlPath, ["-D", _dataDirectory, "-l", Path.Combine(_dataDirectory, "postgres.log"), "-o", $"-p {port} -h 127.0.0.1", "-w", "start"], redirectOutput: false);

        ConnectionString = $"Host=127.0.0.1;Port={port};Database=postgres;Username=postgres;Pooling=false;Timeout=5;Command Timeout=10";
        await ResetAsync();
    }

    public async Task DisposeAsync()
    {
        if (_pgCtlPath is not null && _dataDirectory is not null)
            await RunProcessAsync(_pgCtlPath, ["-D", _dataDirectory, "-m", "immediate", "stop"], redirectOutput: false);

        if (_dataDirectory is not null && Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DROP TABLE IF EXISTS cobrancas;
            DROP TABLE IF EXISTS bfa_schema_history;
            DROP TABLE IF EXISTS pagamentos;
            CREATE TABLE cobrancas (
                id uuid NOT NULL PRIMARY KEY,
                organizacao_id uuid NOT NULL,
                unidade_id uuid NOT NULL,
                aluno_id uuid NOT NULL,
                matricula_id uuid NOT NULL,
                tipo varchar(20) NOT NULL,
                descricao varchar(200) NOT NULL,
                valor numeric(12,2) NOT NULL,
                valor_pago numeric(12,2) NOT NULL,
                data_emissao date NOT NULL,
                data_vencimento date NOT NULL,
                data_pagamento date NULL,
                status varchar(20) NOT NULL,
                observacoes text NULL,
                criado_por_usuario_id uuid NULL,
                atualizado_por_usuario_id uuid NULL,
                criado_em_utc timestamptz NOT NULL,
                atualizado_em_utc timestamptz NOT NULL
            );
            CREATE TABLE bfa_schema_history (
                version varchar(10) NOT NULL,
                descricao text NOT NULL
            );
            CREATE TABLE pagamentos (
                id uuid NOT NULL PRIMARY KEY,
                organizacao_id uuid NOT NULL,
                unidade_id uuid NOT NULL,
                cobranca_id uuid NOT NULL,
                valor numeric(12,2) NOT NULL,
                data_pagamento date NOT NULL,
                data_registro timestamptz NOT NULL,
                forma_pagamento varchar(20) NOT NULL,
                observacoes text NULL,
                registrado_por_usuario_id uuid NOT NULL,
                criado_em_utc timestamptz NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task AplicarV021Async()
    {
        var migrationPath = EncontrarRaizRepositorio();
        var sql = await File.ReadAllTextAsync(Path.Combine(
            migrationPath, "database", "migrations", "V021__garantir_idempotencia_cobrancas_automaticas.sql"));
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task InserirAsync(
        TipoCobranca tipo,
        Guid organizacaoId,
        Guid unidadeId,
        Guid matriculaId,
        DateOnly vencimento,
        StatusCobranca status,
        Guid? id = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO cobrancas (
                id, organizacao_id, unidade_id, aluno_id, matricula_id, tipo,
                descricao, valor, valor_pago, data_emissao, data_vencimento,
                data_pagamento, status, criado_em_utc, atualizado_em_utc)
            VALUES (@id, @organizacao, @unidade, @aluno, @matricula, @tipo,
                    'Teste', 100, 0, @emissao, @vencimento, NULL, @status, NOW(), NOW());
            """;
        command.Parameters.AddWithValue("id", id ?? Guid.NewGuid());
        command.Parameters.AddWithValue("organizacao", organizacaoId);
        command.Parameters.AddWithValue("unidade", unidadeId);
        command.Parameters.AddWithValue("aluno", AlunoId);
        command.Parameters.AddWithValue("matricula", matriculaId);
        command.Parameters.AddWithValue("tipo", tipo.ToString());
        command.Parameters.AddWithValue("emissao", vencimento.AddDays(-1));
        command.Parameters.AddWithValue("vencimento", vencimento);
        command.Parameters.AddWithValue("status", status.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public Cobranca NovaCobranca(TipoCobranca tipo, DateOnly vencimento) => new(
        Guid.NewGuid(),
        OrganizacaoId,
        UnidadeId,
        AlunoId,
        MatriculaId,
        tipo,
        "Teste",
        100m,
        vencimento.AddDays(-1),
        vencimento,
        null,
        DateTime.UtcNow);

    public BfaDbContext CriarContexto(NpgsqlConnection connection) =>
        new(new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql(connection)
            .Options);

    public async Task<int> ContarAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM cobrancas";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<int> ContarPagamentosAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pagamentos";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<int> ContarCobrancasComStatusAsync(StatusCobranca status)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM cobrancas WHERE status = @status";
        command.Parameters.AddWithValue("status", status.ToString());
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<int> ContarCobrancasComValorPagoMenorQueValorAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM cobrancas WHERE valor_pago > 0 AND valor_pago < valor";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<bool> IndiceExisteAsync(string nome)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass(@nome) IS NOT NULL";
        command.Parameters.AddWithValue("nome", nome);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static string EncontrarRaizRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !Directory.Exists(Path.Combine(atual.FullName, "database", "migrations")))
            atual = atual.Parent;

        return atual?.FullName
            ?? throw new InvalidOperationException("Raiz do repositorio nao encontrada.");
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
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL");
            if (Directory.Exists(root))
            {
                var candidate = Directory.GetDirectories(root)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => Path.Combine(path, "bin", fileName))
                    .FirstOrDefault(File.Exists);
                if (candidate is not null) return candidate;
            }
        }

        throw new InvalidOperationException($"Executavel PostgreSQL ausente: {fileName}.");
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task RunProcessAsync(string executable, IReadOnlyList<string> arguments, bool redirectOutput = true)
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
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            var erro = redirectOutput ? await process.StandardError.ReadToEndAsync() : string.Empty;
            throw new InvalidOperationException($"{executable} falhou: {erro}");
        }
    }
}

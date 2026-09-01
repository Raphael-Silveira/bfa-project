using BFA.Application.Planos;
using BFA.Domain.Planos;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Planos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace BFA.IntegrationTests;

public sealed class PlanosRepositorioTests
{
    [Fact]
    public async Task Nova_versao_fecha_anterior_e_persiste_termos_no_PostgreSQL()
    {
        await using var connection = new NpgsqlConnection(ObterConnectionString());
        await connection.OpenAsync();
        await CriarTabelasTemporariasAsync(connection);
        await using var dbContext = CriarDbContext(connection);
        var repositorio = new PlanosRepositorio(dbContext);
        var cenario = await CriarPlanoInicialAsync(repositorio);

        var resultado = await repositorio.CriarNovaVersaoAsync(
            cenario.OrganizacaoId,
            null,
            cenario.PlanoId,
            new(1, 7, 290.00m, true, 200.00m, new DateOnly(2026, 9, 9)),
            cenario.UsuarioId,
            new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var versoes = await dbContext.PlanosVersoes.AsNoTracking()
            .OrderBy(versao => versao.NumeroVersao)
            .ToArrayAsync();

        Assert.Equal(EstadoPersistenciaPlano.Sucesso, resultado);
        Assert.Equal(2, versoes.Length);
        Assert.Equal(new DateOnly(2026, 9, 8), versoes[0].VigenciaFim);
        Assert.Equal(2, versoes[1].NumeroVersao);
        Assert.Equal(1, versoes[1].DuracaoMeses);
        Assert.Equal(7, versoes[1].FrequenciaSemanal);
        Assert.Equal(290.00m, versoes[1].ValorMensal);
        Assert.True(versoes[1].CobraMatricula);
        Assert.Equal(200.00m, versoes[1].ValorMatricula);
        Assert.Equal(new DateOnly(2026, 9, 9), versoes[1].VigenciaInicio);
        Assert.Null(versoes[1].VigenciaFim);
    }

    [Fact]
    public async Task Falha_ao_inserir_nova_versao_reverte_encerramento_da_anterior()
    {
        await using var connection = new NpgsqlConnection(ObterConnectionString());
        await connection.OpenAsync();
        await CriarTabelasTemporariasAsync(connection);
        await using var dbContext = CriarDbContext(connection);
        var repositorio = new PlanosRepositorio(dbContext);
        var cenario = await CriarPlanoInicialAsync(repositorio);

        var resultado = await repositorio.CriarNovaVersaoAsync(
            cenario.OrganizacaoId,
            null,
            cenario.PlanoId,
            new(1, 7, 999.00m, true, 200.00m, new DateOnly(2026, 9, 9)),
            cenario.UsuarioId,
            new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var versoes = await dbContext.PlanosVersoes.AsNoTracking().ToArrayAsync();

        Assert.Equal(EstadoPersistenciaPlano.ConflitoConcorrencia, resultado);
        var anterior = Assert.Single(versoes);
        Assert.Equal(1, anterior.NumeroVersao);
        Assert.Null(anterior.VigenciaFim);
    }

    private static async Task<(Guid OrganizacaoId, Guid PlanoId, Guid UsuarioId)>
        CriarPlanoInicialAsync(PlanosRepositorio repositorio)
    {
        var organizacaoId = Guid.NewGuid();
        var planoId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var criadoEmUtc = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var plano = new Plano(
            planoId,
            organizacaoId,
            null,
            "Plano BFA 3x",
            usuarioId,
            criadoEmUtc);
        var versao = new PlanoVersao(
            Guid.NewGuid(),
            organizacaoId,
            planoId,
            1,
            12,
            3,
            250.00m,
            true,
            100.00m,
            new DateOnly(2026, 8, 1),
            null,
            usuarioId,
            criadoEmUtc);

        Assert.Equal(
            EstadoPersistenciaPlano.Sucesso,
            await repositorio.CriarAsync(plano, versao, CancellationToken.None));
        return (organizacaoId, planoId, usuarioId);
    }

    private static async Task CriarTabelasTemporariasAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TEMP TABLE planos (
                id uuid PRIMARY KEY,
                organizacao_id uuid NOT NULL,
                unidade_id uuid NULL,
                nome varchar(150) NOT NULL,
                ativo boolean NOT NULL,
                criado_por_usuario_id uuid NOT NULL,
                atualizado_por_usuario_id uuid NOT NULL,
                criado_em_utc timestamptz NOT NULL,
                atualizado_em_utc timestamptz NOT NULL,
                UNIQUE (organizacao_id, id)
            );

            CREATE TEMP TABLE planos_versoes (
                id uuid PRIMARY KEY,
                organizacao_id uuid NOT NULL,
                plano_id uuid NOT NULL,
                numero_versao integer NOT NULL,
                duracao_meses smallint NOT NULL,
                frequencia_semanal smallint NOT NULL,
                valor_mensal numeric(12,2) NOT NULL,
                cobra_matricula boolean NOT NULL,
                valor_matricula numeric(12,2) NULL,
                vigencia_inicio date NOT NULL,
                vigencia_fim date NULL,
                criado_por_usuario_id uuid NOT NULL,
                criado_em_utc timestamptz NOT NULL,
                UNIQUE (plano_id, numero_versao),
                CHECK (duracao_meses > 0),
                CHECK (frequencia_semanal BETWEEN 1 AND 7),
                CHECK (valor_mensal > 0 AND valor_mensal <> 999.00),
                CHECK (
                    (cobra_matricula AND valor_matricula > 0)
                    OR (NOT cobra_matricula AND valor_matricula IS NULL)),
                CHECK (vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio)
            );

            CREATE UNIQUE INDEX uq_planos_versoes_aberta_teste
                ON planos_versoes (plano_id)
                WHERE vigencia_fim IS NULL;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static BfaDbContext CriarDbContext(NpgsqlConnection connection)
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseNpgsql(connection)
            .Options;
        return new BfaDbContext(options);
    }

    private static string ObterConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("BfaDatabase");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Configure ConnectionStrings:BfaDatabase para executar os testes PostgreSQL.");
        return connectionString;
    }
}

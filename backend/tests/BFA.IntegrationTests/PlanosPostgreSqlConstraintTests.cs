using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace BFA.IntegrationTests;

public sealed class PlanosPostgreSqlConstraintTests
{
    public static TheoryData<bool, decimal?, bool> CenariosTaxaMatricula => new()
    {
        { true, 100m, true },
        { false, null, true },
        { true, null, false },
        { true, 0m, false },
        { true, -10m, false },
        { false, 100m, false },
        { false, 0m, false }
    };

    [Theory]
    [MemberData(nameof(CenariosTaxaMatricula))]
    public async Task PostgreSQL_aplica_check_exato_da_taxa_de_matricula(
        bool cobraMatricula,
        decimal? valorMatricula,
        bool deveAceitar)
    {
        await using var connection = new NpgsqlConnection(ObterConnectionString());
        await connection.OpenAsync();
        await CriarTabelaTemporariaAsync(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO teste_planos_versoes_taxa
                (cobra_matricula, valor_matricula)
            VALUES
                (@cobra_matricula, @valor_matricula);
            """;
        command.Parameters.AddWithValue("cobra_matricula", cobraMatricula);
        command.Parameters.AddWithValue(
            "valor_matricula",
            NpgsqlDbType.Numeric,
            valorMatricula.HasValue ? valorMatricula.Value : DBNull.Value);

        if (deveAceitar)
        {
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
            return;
        }

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_teste_planos_versoes_matricula_valida", exception.ConstraintName);
    }

    private static async Task CriarTabelaTemporariaAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TEMP TABLE teste_planos_versoes_taxa (
                cobra_matricula boolean NOT NULL,
                valor_matricula numeric(12,2) NULL,
                CONSTRAINT ck_teste_planos_versoes_matricula_valida
                    CHECK (
                        (
                            cobra_matricula = true
                            AND valor_matricula IS NOT NULL
                            AND valor_matricula > 0
                        )
                        OR
                        (cobra_matricula = false AND valor_matricula IS NULL)
                    )
            );
            """;

        await command.ExecuteNonQueryAsync();
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

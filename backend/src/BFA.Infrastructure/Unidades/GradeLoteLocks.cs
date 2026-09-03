using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace BFA.Infrastructure.Unidades;

internal static class GradeLoteLocks
{
    public static Task BloquearMatriculasAsync(
        BfaDbContext dbContext,
        Guid organizacaoId,
        Guid unidadeId,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken) =>
        BloquearAsync(
            dbContext,
            """
            SELECT id
            FROM matriculas
            WHERE organizacao_id = @organizacao_id
              AND unidade_id = @unidade_id
              AND id = ANY (@ids)
            ORDER BY id
            FOR UPDATE
            """,
            organizacaoId,
            unidadeId,
            ids,
            cancellationToken);

    public static Task BloquearAlunosAsync(
        BfaDbContext dbContext,
        Guid organizacaoId,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken) =>
        BloquearAsync(
            dbContext,
            """
            SELECT id
            FROM alunos
            WHERE organizacao_id = @organizacao_id
              AND id = ANY (@ids)
            ORDER BY id
            FOR UPDATE
            """,
            organizacaoId,
            null,
            ids,
            cancellationToken);

    public static Task BloquearTurmasHorariosAsync(
        BfaDbContext dbContext,
        Guid organizacaoId,
        Guid unidadeId,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken) =>
        BloquearAsync(
            dbContext,
            """
            SELECT id
            FROM turmas_horarios
            WHERE organizacao_id = @organizacao_id
              AND unidade_id = @unidade_id
              AND id = ANY (@ids)
            ORDER BY id
            FOR UPDATE
            """,
            organizacaoId,
            unidadeId,
            ids,
            cancellationToken);

    private static async Task BloquearAsync(
        BfaDbContext dbContext,
        string sql,
        Guid organizacaoId,
        Guid? unidadeId,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var idsOrdenados = ids.Distinct().OrderBy(id => id).ToArray();
        if (idsOrdenados.Length == 0
            || dbContext.Database.ProviderName?.Contains(
                "Npgsql", StringComparison.Ordinal) != true)
        {
            return;
        }

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction?)dbContext.Database
            .CurrentTransaction?.GetDbTransaction();
        command.CommandText = sql;
        command.Parameters.AddWithValue("organizacao_id", organizacaoId);
        if (unidadeId.HasValue)
            command.Parameters.AddWithValue("unidade_id", unidadeId.Value);
        command.Parameters.AddWithValue("ids", idsOrdenados);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
        }
    }
}

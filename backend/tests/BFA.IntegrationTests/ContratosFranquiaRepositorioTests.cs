using BFA.Application.Contratos;
using BFA.Application.Franqueadora.Contratos;
using BFA.Domain.Contratos;
using BFA.Infrastructure.Franqueadora;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BFA.IntegrationTests;

public sealed class ContratosFranquiaRepositorioTests
{
    [Fact]
    public async Task Falha_do_banco_depois_da_confirmacao_compensa_arquivo_final()
    {
        var storage = new StorageFake();
        await using var dbContext = CriarContexto(new FalharSaveChangesInterceptor());
        var repositorio = new ContratosFranquiaRepositorio(dbContext, storage);
        var documento = CriarDocumento();

        var resultado = await repositorio.SalvarDocumentoAsync(
            documento,
            ".temporarios/teste.tmp",
            CancellationToken.None);

        Assert.Equal(EstadoPersistenciaContratoFranquia.Falha, resultado);
        Assert.True(storage.Confirmado);
        Assert.True(storage.FinalDescartado);
        Assert.Equal(0, await dbContext.DocumentosContratoFranquia.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Falha_do_filesystem_nao_adiciona_metadata_ao_contexto()
    {
        var storage = new StorageFake { FalharConfirmacao = true };
        await using var dbContext = CriarContexto();
        var repositorio = new ContratosFranquiaRepositorio(dbContext, storage);

        var resultado = await repositorio.SalvarDocumentoAsync(
            CriarDocumento(),
            ".temporarios/teste.tmp",
            CancellationToken.None);

        Assert.Equal(EstadoPersistenciaContratoFranquia.Falha, resultado);
        Assert.Empty(dbContext.ChangeTracker.Entries<DocumentoContratoFranquia>());
        Assert.False(storage.FinalDescartado);
    }

    private static BfaDbContext CriarContexto(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseInMemoryDatabase($"contratos-repositorio-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(
                InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(interceptors)
            .Options;
        return new(options);
    }

    private static DocumentoContratoFranquia CriarDocumento() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        TipoDocumentoContratoFranquia.Contrato,
        "contrato.pdf",
        $"contratos/{Guid.NewGuid():N}/versoes/{Guid.NewGuid():N}/{Guid.NewGuid():N}.pdf",
        "application/pdf",
        100,
        new string('a', 64),
        DateTime.UtcNow,
        Guid.NewGuid());

    private sealed class FalharSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(
                new DbUpdateException("Falha simulada de persistência."));
    }

    private sealed class StorageFake : IArmazenamentoDocumentosContrato
    {
        public bool FalharConfirmacao { get; init; }
        public bool Confirmado { get; private set; }
        public bool FinalDescartado { get; private set; }

        public Task ConfirmarTemporarioAsync(string identificadorTemporario, string chaveArmazenamento, CancellationToken cancellationToken = default)
        {
            if (FalharConfirmacao) throw new IOException("Falha simulada no filesystem.");
            Confirmado = true;
            return Task.CompletedTask;
        }
        public Task DescartarArquivoNaoConfirmadoAsync(string chaveArmazenamento, CancellationToken cancellationToken = default)
        {
            FinalDescartado = true;
            return Task.CompletedTask;
        }
        public Task DescartarTemporarioAsync(string identificadorTemporario, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExisteAsync(string chaveArmazenamento, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Stream> AbrirLeituraAsync(string chaveArmazenamento, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task SalvarAsync(string chaveArmazenamento, Stream conteudo, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ArquivoTemporarioDocumentoContrato> SalvarTemporarioAsync(Stream conteudo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

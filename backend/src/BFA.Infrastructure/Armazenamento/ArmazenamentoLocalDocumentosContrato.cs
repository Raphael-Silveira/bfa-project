using BFA.Application.Contratos;
using Microsoft.Extensions.Options;

namespace BFA.Infrastructure.Armazenamento;

public sealed class ArmazenamentoLocalDocumentosContrato
    : IArmazenamentoDocumentosContrato
{
    private readonly string _diretorioBase;

    public ArmazenamentoLocalDocumentosContrato(
        IOptions<ArmazenamentoDocumentosContratoOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var diretorioConfigurado = options.Value.DiretorioBase;

        if (string.IsNullOrWhiteSpace(diretorioConfigurado))
        {
            throw new InvalidOperationException(
                $"A configuracao {ArmazenamentoDocumentosContratoOptions.SecaoConfiguracao}:DiretorioBase e obrigatoria.");
        }

        if (!Path.IsPathFullyQualified(diretorioConfigurado))
        {
            throw new InvalidOperationException(
                "O diretorio base do armazenamento de documentos deve ser um caminho absoluto.");
        }

        _diretorioBase = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(diretorioConfigurado));
    }

    public async Task SalvarAsync(
        string chaveArmazenamento,
        Stream conteudo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (!conteudo.CanRead)
        {
            throw new ArgumentException("O conteudo deve permitir leitura.", nameof(conteudo));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var caminhoArquivo = ResolverCaminhoSeguro(chaveArmazenamento);
        var diretorioArquivo = Path.GetDirectoryName(caminhoArquivo)
            ?? throw new InvalidOperationException("Nao foi possivel resolver o diretorio do documento.");

        Directory.CreateDirectory(diretorioArquivo);
        var arquivoCriado = false;

        try
        {
            await using var arquivo = new FileStream(
                caminhoArquivo,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            arquivoCriado = true;

            await conteudo.CopyToAsync(arquivo, cancellationToken);
            await arquivo.FlushAsync(cancellationToken);
        }
        catch
        {
            if (arquivoCriado && File.Exists(caminhoArquivo))
            {
                File.Delete(caminhoArquivo);
            }

            throw;
        }
    }

    public Task<Stream> AbrirLeituraAsync(
        string chaveArmazenamento,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminhoArquivo = ResolverCaminhoSeguro(chaveArmazenamento);

        Stream arquivo = new FileStream(
            caminhoArquivo,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(arquivo);
    }

    public Task<bool> ExisteAsync(
        string chaveArmazenamento,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolverCaminhoSeguro(chaveArmazenamento)));
    }

    private string ResolverCaminhoSeguro(string chaveArmazenamento)
    {
        if (string.IsNullOrWhiteSpace(chaveArmazenamento))
        {
            throw new ArgumentException(
                "A chave de armazenamento deve ser informada.",
                nameof(chaveArmazenamento));
        }

        if (Path.IsPathRooted(chaveArmazenamento)
            || chaveArmazenamento.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A chave de armazenamento deve ser relativa e usar separadores logicos.",
                nameof(chaveArmazenamento));
        }

        var segmentos = chaveArmazenamento.Split('/');

        if (segmentos.Any(segmento =>
                string.IsNullOrWhiteSpace(segmento)
                || segmento is "." or ".."
                || segmento.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new ArgumentException(
                "A chave de armazenamento possui um segmento invalido.",
                nameof(chaveArmazenamento));
        }

        var caminhoCombinado = segmentos.Aggregate(_diretorioBase, Path.Combine);
        var caminhoNormalizado = Path.GetFullPath(caminhoCombinado);
        var caminhoRelativo = Path.GetRelativePath(_diretorioBase, caminhoNormalizado);

        if (Path.IsPathRooted(caminhoRelativo)
            || caminhoRelativo.Equals("..", StringComparison.Ordinal)
            || caminhoRelativo.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A chave de armazenamento aponta para fora do diretorio base.",
                nameof(chaveArmazenamento));
        }

        return caminhoNormalizado;
    }
}

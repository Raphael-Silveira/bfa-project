using BFA.Application.Contratos;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace BFA.Infrastructure.Armazenamento;

public sealed class ArmazenamentoLocalDocumentosContrato
    : IArmazenamentoDocumentosContrato
{
    private readonly string _diretorioBase;
    private readonly long _tamanhoMaximoBytes;

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

        if (options.Value.TamanhoMaximoBytes <= 0)
        {
            throw new InvalidOperationException(
                "O tamanho maximo de documentos deve ser maior que zero.");
        }

        _diretorioBase = Path.TrimEndingDirectorySeparator(
            Path.IsPathFullyQualified(diretorioConfigurado)
                ? Path.GetFullPath(diretorioConfigurado)
                : Path.GetFullPath(diretorioConfigurado, Directory.GetCurrentDirectory()));
        _tamanhoMaximoBytes = options.Value.TamanhoMaximoBytes;
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

    public async Task<ArquivoTemporarioDocumentoContrato> SalvarTemporarioAsync(
        Stream conteudo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (!conteudo.CanRead)
        {
            throw new ArgumentException("O conteudo deve permitir leitura.", nameof(conteudo));
        }

        var identificador = $".temporarios/{Guid.NewGuid():N}.tmp";
        var caminho = ResolverCaminhoSeguro(identificador);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)
            ?? throw new InvalidOperationException("Nao foi possivel criar o diretorio temporario."));
        var buffer = new byte[81920];
        var assinatura = new byte[5];
        var assinaturaLida = 0;
        long tamanho = 0;

        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var destino = new FileStream(
                caminho,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                buffer.Length,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            while (true)
            {
                var lidos = await conteudo.ReadAsync(buffer, cancellationToken);

                if (lidos == 0)
                {
                    break;
                }

                tamanho += lidos;

                if (tamanho > _tamanhoMaximoBytes)
                {
                    throw new TamanhoDocumentoContratoExcedidoException(_tamanhoMaximoBytes);
                }

                if (assinaturaLida < assinatura.Length)
                {
                    var quantidade = Math.Min(assinatura.Length - assinaturaLida, lidos);
                    buffer.AsSpan(0, quantidade).CopyTo(assinatura.AsSpan(assinaturaLida));
                    assinaturaLida += quantidade;
                }

                hash.AppendData(buffer, 0, lidos);
                await destino.WriteAsync(buffer.AsMemory(0, lidos), cancellationToken);
            }

            await destino.FlushAsync(cancellationToken);
            var hashSha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            var pdf = assinaturaLida == assinatura.Length
                && assinatura.AsSpan().SequenceEqual("%PDF-"u8);
            return new(identificador, tamanho, hashSha256, pdf);
        }
        catch
        {
            if (File.Exists(caminho))
            {
                File.Delete(caminho);
            }

            throw;
        }
    }

    public Task ConfirmarTemporarioAsync(
        string identificadorTemporario,
        string chaveArmazenamento,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var origem = ResolverIdentificadorTemporario(identificadorTemporario);
        var destino = ResolverCaminhoSeguro(chaveArmazenamento);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)
            ?? throw new InvalidOperationException("Nao foi possivel criar o diretorio final."));
        File.Move(origem, destino, overwrite: false);
        return Task.CompletedTask;
    }

    public Task DescartarTemporarioAsync(
        string identificadorTemporario,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminho = ResolverIdentificadorTemporario(identificadorTemporario);

        if (File.Exists(caminho))
        {
            File.Delete(caminho);
        }

        return Task.CompletedTask;
    }

    public Task DescartarArquivoNaoConfirmadoAsync(
        string chaveArmazenamento,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminho = ResolverCaminhoSeguro(chaveArmazenamento);

        if (File.Exists(caminho))
        {
            File.Delete(caminho);
        }

        return Task.CompletedTask;
    }

    private string ResolverIdentificadorTemporario(string identificadorTemporario)
    {
        if (!identificadorTemporario.StartsWith(".temporarios/", StringComparison.Ordinal)
            || !identificadorTemporario.EndsWith(".tmp", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "O identificador temporario e invalido.",
                nameof(identificadorTemporario));
        }

        return ResolverCaminhoSeguro(identificadorTemporario);
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

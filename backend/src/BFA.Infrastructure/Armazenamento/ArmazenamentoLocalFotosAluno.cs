using BFA.Application.AlunoArea;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
using System.Security.Cryptography;

namespace BFA.Infrastructure.Armazenamento;

public sealed class ArmazenamentoLocalFotosAluno(
    IOptions<ArmazenamentoFotosAlunoOptions> options,
    ILogger<ArmazenamentoLocalFotosAluno> logger) : IFotoPerfilAluno
{
    private readonly string _diretorioBase = ResolverBase(options.Value);
    private readonly long _tamanhoMaximo = options.Value.TamanhoMaximoBytes;
    private readonly ILogger<ArmazenamentoLocalFotosAluno> _logger = logger;

    public async Task<FotoPerfilArmazenada> ValidarProcessarSalvarAsync(
        Guid organizacaoId,
        Guid alunoId,
        FotoPerfilUpload upload,
        CancellationToken cancellationToken)
    {
        if (upload.TamanhoBytes <= 0 || upload.TamanhoBytes > _tamanhoMaximo)
            throw new ArgumentException("A foto deve possuir no máximo 2 MB.", nameof(upload));

        await using var origem = new MemoryStream();
        await upload.Conteudo.CopyToAsync(origem, cancellationToken);
        if (origem.Length == 0 || origem.Length > _tamanhoMaximo)
            throw new ArgumentException("A foto deve possuir no máximo 2 MB.", nameof(upload));

        origem.Position = 0;
        using var codec = SKCodec.Create(origem);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            throw new ArgumentException("Envie uma imagem JPEG, PNG ou WebP válida.", nameof(upload));

        origem.Position = 0;
        using var bitmap = SKBitmap.Decode(origem);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            throw new ArgumentException("A imagem enviada é inválida.", nameof(upload));

        var lado = Math.Min(bitmap.Width, bitmap.Height);
        var origemRect = new SKRectI(
            (bitmap.Width - lado) / 2,
            (bitmap.Height - lado) / 2,
            (bitmap.Width + lado) / 2,
            (bitmap.Height + lado) / 2);
        using var avatar = new SKBitmap(256, 256, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(avatar))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(
                bitmap,
                origemRect,
                new SKRect(0, 0, 256, 256),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        var chave = $"alunos/{organizacaoId:D}/{alunoId:D}/perfil/{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}.webp";
        var caminho = ResolverCaminho(chave);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        try
        {
            using var imagem = avatar.Encode(SKEncodedImageFormat.Webp, 85)
                ?? throw new InvalidOperationException("Não foi possível processar a foto do aluno.");
            await using var arquivo = new FileStream(caminho, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            imagem.AsStream().CopyTo(arquivo);
            await arquivo.FlushAsync(cancellationToken);
            return new FotoPerfilArmazenada(chave, "image/webp", DateTime.UtcNow);
        }
        catch (Exception exception)
        {
            if (File.Exists(caminho)) File.Delete(caminho);
            _logger.LogError(exception, "Falha ao armazenar foto do Aluno {AlunoId}", alunoId);
            throw;
        }
    }

    public Task<Stream?> AbrirAsync(string chave, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminho = ResolverCaminho(chave);
        if (!File.Exists(caminho)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous));
    }

    public Task ExcluirAsync(string chave, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminho = ResolverCaminho(chave);
        if (File.Exists(caminho)) File.Delete(caminho);
        return Task.CompletedTask;
    }

    private string ResolverCaminho(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave) || Path.IsPathFullyQualified(chave) || chave.Contains("..", StringComparison.Ordinal) || chave.Contains('\\'))
            throw new ArgumentException("A chave de armazenamento é inválida.", nameof(chave));
        var caminho = Path.GetFullPath(chave.Replace('/', Path.DirectorySeparatorChar), _diretorioBase);
        if (!caminho.StartsWith(_diretorioBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A chave de armazenamento é inválida.", nameof(chave));
        return caminho;
    }

    private static string ResolverBase(ArmazenamentoFotosAlunoOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DiretorioBase) || options.TamanhoMaximoBytes <= 0)
            throw new InvalidOperationException("A configuração de armazenamento de fotos é obrigatória.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.DiretorioBase));
    }
}

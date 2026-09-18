using BFA.Application.AlunoArea;
using BFA.Infrastructure.Armazenamento;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace BFA.IntegrationTests;

public sealed class ArmazenamentoFotosAlunoTests : IDisposable
{
    private readonly string _diretorio = Path.Combine(Path.GetTempPath(), "bfa-fotos-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Foto_jpeg_e_processada_para_avatar_webp_privado()
    {
        Directory.CreateDirectory(_diretorio);
        var storage = CriarStorage();
        await using var origem = CriarJpeg();

        var resultado = await storage.ValidarProcessarSalvarAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            new FotoPerfilUpload(origem, "image/jpeg", origem.Length), CancellationToken.None);

        Assert.EndsWith(".webp", resultado.Chave, StringComparison.Ordinal);
        Assert.Equal("image/webp", resultado.ContentType);
        await using var leitura = await storage.AbrirAsync(resultado.Chave, CancellationToken.None);
        Assert.NotNull(leitura);
    }

    [Fact]
    public async Task Arquivo_invalido_e_rejeitado()
    {
        Directory.CreateDirectory(_diretorio);
        var storage = CriarStorage();
        await using var origem = new MemoryStream("not-an-image"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(() => storage.ValidarProcessarSalvarAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            new FotoPerfilUpload(origem, "image/svg+xml", origem.Length), CancellationToken.None));
    }

    private ArmazenamentoLocalFotosAluno CriarStorage() => new(
        Options.Create(new ArmazenamentoFotosAlunoOptions
        {
            DiretorioBase = _diretorio,
            TamanhoMaximoBytes = 2 * 1024 * 1024
        }), NullLogger<ArmazenamentoLocalFotosAluno>.Instance);

    private static MemoryStream CriarJpeg()
    {
        using var bitmap = new SKBitmap(320, 180);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Gold);
        using var imagem = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
        return new MemoryStream(imagem!.ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_diretorio)) Directory.Delete(_diretorio, recursive: true);
    }
}

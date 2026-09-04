using System.Text;
using System.Security.Cryptography;
using BFA.Application.Contratos;
using BFA.Infrastructure.Armazenamento;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.IntegrationTests;

public sealed class ArmazenamentoLocalDocumentosContratoTests : IDisposable
{
    private readonly string _diretorioBase = Path.Combine(
        Path.GetTempPath(),
        "bfa-storage-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Salva_abre_e_localiza_conteudo_em_subdiretorios_privados()
    {
        const string chave = "contratos/aaaaaaaa/versoes/bbbbbbbb/cccccccc.pdf";
        const string conteudoEsperado = "documento contratual de teste";
        var storage = CriarStorage();
        await using var origem = new MemoryStream(Encoding.UTF8.GetBytes(conteudoEsperado));

        await storage.SalvarAsync(chave, origem, CancellationToken.None);

        Assert.True(await storage.ExisteAsync(chave, CancellationToken.None));
        Assert.True(File.Exists(Path.Combine(
            _diretorioBase,
            "contratos",
            "aaaaaaaa",
            "versoes",
            "bbbbbbbb",
            "cccccccc.pdf")));

        await using var leitura = await storage.AbrirLeituraAsync(
            chave,
            CancellationToken.None);
        using var leitor = new StreamReader(leitura, Encoding.UTF8);

        Assert.Equal(
            conteudoEsperado,
            await leitor.ReadToEndAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("../fora.pdf")]
    [InlineData("contratos/../../fora.pdf")]
    [InlineData("contratos/./fora.pdf")]
    [InlineData("contratos//fora.pdf")]
    [InlineData("contratos\\fora.pdf")]
    public async Task Rejeita_chave_relativa_invalida_ou_com_path_traversal(string chave)
    {
        var storage = CriarStorage();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.ExisteAsync(chave, CancellationToken.None));
    }

    [Fact]
    public async Task Rejeita_chave_absoluta()
    {
        var storage = CriarStorage();
        var chaveAbsoluta = Path.Combine(_diretorioBase, "fora.pdf");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.ExisteAsync(chaveAbsoluta, CancellationToken.None));
    }

    [Fact]
    public async Task Tentativa_de_sobrescrita_preserva_arquivo_existente()
    {
        const string chave = "contratos/a/versoes/b/c.pdf";
        var storage = CriarStorage();
        await using var primeiro = new MemoryStream(Encoding.UTF8.GetBytes("original"));
        await storage.SalvarAsync(chave, primeiro, CancellationToken.None);
        await using var segundo = new MemoryStream(Encoding.UTF8.GetBytes("novo"));

        await Assert.ThrowsAsync<IOException>(() =>
            storage.SalvarAsync(chave, segundo, CancellationToken.None));

        await using var leitura = await storage.AbrirLeituraAsync(
            chave,
            CancellationToken.None);
        using var leitor = new StreamReader(leitura, Encoding.UTF8);
        Assert.Equal("original", await leitor.ReadToEndAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Temporario_pdf_calcula_tamanho_hash_assinatura_e_confirma_na_chave_logica()
    {
        const string chave = "contratos/a/versoes/b/c.pdf";
        var bytes = "%PDF-1.7\nconteudo"u8.ToArray();
        var storage = CriarStorage();
        await using var origem = new MemoryStream(bytes);

        var temporario = await storage.SalvarTemporarioAsync(origem, CancellationToken.None);

        Assert.True(temporario.PossuiAssinaturaPdf);
        Assert.Equal(bytes.LongLength, temporario.TamanhoBytes);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            temporario.HashSha256);
        Assert.StartsWith(".temporarios/", temporario.Identificador, StringComparison.Ordinal);

        await storage.ConfirmarTemporarioAsync(
            temporario.Identificador,
            chave,
            CancellationToken.None);

        Assert.True(await storage.ExisteAsync(chave, CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(
            _diretorioBase,
            ".temporarios",
            Path.GetFileName(temporario.Identificador))));
        Assert.DoesNotContain("wwwroot", _diretorioBase, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Temporario_identifica_conteudo_sem_magic_bytes_pdf()
    {
        var storage = CriarStorage();
        await using var origem = new MemoryStream("nao-pdf"u8.ToArray());

        var temporario = await storage.SalvarTemporarioAsync(origem, CancellationToken.None);

        Assert.False(temporario.PossuiAssinaturaPdf);
        await storage.DescartarTemporarioAsync(
            temporario.Identificador,
            CancellationToken.None);
    }

    [Fact]
    public async Task Temporario_acima_do_limite_e_rejeitado_e_removido()
    {
        var storage = CriarStorage(tamanhoMaximoBytes: 8);
        await using var origem = new MemoryStream("%PDF-1234"u8.ToArray());

        var exception = await Assert.ThrowsAsync<TamanhoDocumentoContratoExcedidoException>(() =>
            storage.SalvarTemporarioAsync(origem, CancellationToken.None));

        Assert.Equal(8, exception.TamanhoMaximoBytes);
        var temporarios = Path.Combine(_diretorioBase, ".temporarios");
        Assert.False(Directory.Exists(temporarios)
            && Directory.EnumerateFiles(temporarios).Any());
    }

    [Theory]
    [InlineData("temporarios/arquivo.tmp")]
    [InlineData(".temporarios/arquivo.pdf")]
    [InlineData("../.temporarios/arquivo.tmp")]
    public async Task Cleanup_temporario_rejeita_identificador_adulterado(string identificador)
    {
        var storage = CriarStorage();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.DescartarTemporarioAsync(identificador, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_diretorioBase))
        {
            Directory.Delete(_diretorioBase, recursive: true);
        }
    }

    private ArmazenamentoLocalDocumentosContrato CriarStorage(
        long tamanhoMaximoBytes = ArmazenamentoDocumentosContratoOptions.TamanhoMaximoPadraoBytes) => new(
        Options.Create(new ArmazenamentoDocumentosContratoOptions
        {
            DiretorioBase = _diretorioBase,
            TamanhoMaximoBytes = tamanhoMaximoBytes
        }),
        NullLogger<ArmazenamentoLocalDocumentosContrato>.Instance);
}

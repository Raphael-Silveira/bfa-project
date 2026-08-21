using System.Text;
using BFA.Infrastructure.Armazenamento;
using Microsoft.Extensions.Options;

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

    public void Dispose()
    {
        if (Directory.Exists(_diretorioBase))
        {
            Directory.Delete(_diretorioBase, recursive: true);
        }
    }

    private ArmazenamentoLocalDocumentosContrato CriarStorage() => new(
        Options.Create(new ArmazenamentoDocumentosContratoOptions
        {
            DiretorioBase = _diretorioBase
        }));
}

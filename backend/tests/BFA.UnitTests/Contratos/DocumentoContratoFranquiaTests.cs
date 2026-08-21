using BFA.Domain.Contratos;

namespace BFA.UnitTests.Contratos;

public sealed class DocumentoContratoFranquiaTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        21,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public void Criacao_define_metadata_e_normaliza_hash_sha256()
    {
        var hashMaiusculo = new string('A', DocumentoContratoFranquia.HashSha256Tamanho);

        var documento = Criar(hashSha256: hashMaiusculo);

        Assert.Equal(TipoDocumentoContratoFranquia.Contrato, documento.TipoDocumento);
        Assert.Equal("Contrato assinado.pdf", documento.NomeOriginal);
        Assert.Equal("application/pdf", documento.ContentType);
        Assert.Equal(1024, documento.TamanhoBytes);
        Assert.Equal(hashMaiusculo.ToLowerInvariant(), documento.HashSha256);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("contratoFranquiaVersaoId")]
    [InlineData("enviadoPorUsuarioId")]
    public void Criacao_rejeita_identificador_obrigatorio_vazio(string parametro)
    {
        var exception = Assert.Throws<ArgumentException>(() => new DocumentoContratoFranquia(
            parametro == "id" ? Guid.Empty : Guid.NewGuid(),
            parametro == "contratoFranquiaVersaoId" ? Guid.Empty : Guid.NewGuid(),
            TipoDocumentoContratoFranquia.Contrato,
            "Contrato.pdf",
            "contratos/a/versoes/b/c.pdf",
            "application/pdf",
            1,
            null,
            CriadoEmUtc,
            parametro == "enviadoPorUsuarioId" ? Guid.Empty : Guid.NewGuid()));

        Assert.Equal(parametro, exception.ParamName);
    }

    [Theory]
    [InlineData("nomeOriginal")]
    [InlineData("chaveArmazenamento")]
    [InlineData("contentType")]
    public void Criacao_rejeita_metadata_obrigatoria_vazia(string parametro)
    {
        var exception = Assert.Throws<ArgumentException>(() => new DocumentoContratoFranquia(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TipoDocumentoContratoFranquia.Contrato,
            parametro == "nomeOriginal" ? " " : "Contrato.pdf",
            parametro == "chaveArmazenamento" ? " " : "contratos/a/versoes/b/c.pdf",
            parametro == "contentType" ? " " : "application/pdf",
            1,
            null,
            CriadoEmUtc,
            Guid.NewGuid()));

        Assert.Equal(parametro, exception.ParamName);
    }

    [Fact]
    public void Tamanho_deve_ser_maior_que_zero()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Criar(tamanhoBytes: 0));

        Assert.Equal("tamanhoBytes", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Hash_sha256_invalido_e_rejeitado(string hash)
    {
        var exception = Assert.Throws<ArgumentException>(() => Criar(hashSha256: hash));

        Assert.Equal("hashSha256", exception.ParamName);
    }

    [Fact]
    public void Novo_documento_e_criado_com_nova_identidade_e_chave()
    {
        var versaoId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var primeiro = new DocumentoContratoFranquia(
            Guid.NewGuid(),
            versaoId,
            TipoDocumentoContratoFranquia.Contrato,
            "Contrato.pdf",
            "contratos/a/versoes/b/primeiro.pdf",
            "application/pdf",
            100,
            null,
            CriadoEmUtc,
            usuarioId);
        var segundo = new DocumentoContratoFranquia(
            Guid.NewGuid(),
            versaoId,
            TipoDocumentoContratoFranquia.Aditivo,
            "Aditivo.pdf",
            "contratos/a/versoes/b/segundo.pdf",
            "application/pdf",
            200,
            null,
            CriadoEmUtc.AddMinutes(1),
            usuarioId);

        Assert.NotEqual(primeiro.Id, segundo.Id);
        Assert.NotEqual(primeiro.ChaveArmazenamento, segundo.ChaveArmazenamento);
        Assert.Equal(versaoId, primeiro.ContratoFranquiaVersaoId);
        Assert.Equal(versaoId, segundo.ContratoFranquiaVersaoId);
    }

    private static DocumentoContratoFranquia Criar(
        long tamanhoBytes = 1024,
        string? hashSha256 = null) => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TipoDocumentoContratoFranquia.Contrato,
            "Contrato assinado.pdf",
            "contratos/aaaaaaaa/versoes/bbbbbbbb/cccccccc.pdf",
            "application/pdf",
            tamanhoBytes,
            hashSha256,
            CriadoEmUtc,
            Guid.NewGuid());
}

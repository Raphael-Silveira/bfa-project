namespace BFA.Infrastructure.Armazenamento;

public sealed class ArmazenamentoDocumentosContratoOptions
{
    public const string SecaoConfiguracao = "Armazenamento:Documentos";

    public const long TamanhoMaximoPadraoBytes = 20 * 1024 * 1024;

    public string DiretorioBase { get; set; } = string.Empty;

    public long TamanhoMaximoBytes { get; set; } = TamanhoMaximoPadraoBytes;
}

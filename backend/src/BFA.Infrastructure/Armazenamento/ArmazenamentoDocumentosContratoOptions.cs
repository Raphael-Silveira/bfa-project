namespace BFA.Infrastructure.Armazenamento;

public sealed class ArmazenamentoDocumentosContratoOptions
{
    public const string SecaoConfiguracao = "Armazenamento:Documentos";

    public string DiretorioBase { get; set; } = string.Empty;
}

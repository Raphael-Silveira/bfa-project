namespace BFA.Infrastructure.Armazenamento;

public sealed class ArmazenamentoFotosAlunoOptions
{
    public const string SecaoConfiguracao = "Armazenamento:FotosAluno";
    public const long TamanhoMaximoPadraoBytes = 2 * 1024 * 1024;

    public string DiretorioBase { get; set; } = string.Empty;
    public long TamanhoMaximoBytes { get; set; } = TamanhoMaximoPadraoBytes;
}

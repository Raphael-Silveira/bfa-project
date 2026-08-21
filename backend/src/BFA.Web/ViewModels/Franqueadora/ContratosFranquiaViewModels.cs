using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BFA.Domain.Contratos;
using Microsoft.AspNetCore.Http;

namespace BFA.Web.ViewModels.Franqueadora;

public sealed class ContratoFranquiaPainelViewModel
{
    public Guid FranqueadoId { get; init; }
    public string FranqueadoNome { get; init; } = string.Empty;
    public Guid UnidadeId { get; init; }
    public string UnidadeNome { get; init; } = string.Empty;
    public bool VinculoAtivo { get; init; }
    public Guid? ContratoId { get; init; }
    public string? Numero { get; init; }
    public StatusContratoFranquia? Status { get; init; }
    public long TamanhoMaximoDocumentoBytes { get; init; }
    public VersaoContratoFranquiaViewModel? VersaoAtual { get; init; }
    public IReadOnlyList<VersaoContratoFranquiaViewModel> Versoes { get; init; } = [];
}

public sealed record VersaoContratoFranquiaViewModel(
    Guid Id,
    int NumeroVersao,
    DateOnly DataInicio,
    DateOnly? DataFim,
    decimal PercentualRoyalties,
    decimal MensalidadeFixa,
    decimal? TaxaAdesao,
    int? DiaVencimento,
    StatusVersaoContratoFranquia Status,
    string? MotivoAlteracao,
    string? Observacoes,
    DateTime CriadoEmUtc,
    string CriadoPor,
    IReadOnlyList<DocumentoContratoFranquiaViewModel> Documentos);

public sealed record DocumentoContratoFranquiaViewModel(
    Guid Id,
    TipoDocumentoContratoFranquia TipoDocumento,
    string NomeOriginal,
    long TamanhoBytes,
    DateTime CriadoEmUtc,
    string EnviadoPor);

public sealed class ContratoFranquiaFormViewModel : IValidatableObject
{
    public Guid FranqueadoId { get; set; }
    public string FranqueadoNome { get; set; } = string.Empty;
    public Guid UnidadeId { get; set; }
    public string UnidadeNome { get; set; } = string.Empty;
    public Guid? ContratoId { get; set; }
    public Guid? VersaoId { get; set; }
    public int NumeroVersao { get; set; } = 1;

    [StringLength(ContratoFranquia.NumeroTamanhoMaximo)]
    [Display(Name = "Número do contrato")]
    public string? NumeroContrato { get; set; }

    [Required(ErrorMessage = "Informe a data de início.")]
    [DataType(DataType.Date)]
    [Display(Name = "Data de início")]
    public DateOnly? DataInicio { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data de término")]
    public DateOnly? DataFim { get; set; }

    [Required(ErrorMessage = "Informe o percentual de royalties.")]
    [Display(Name = "Royalties (%)")]
    public string PercentualRoyalties { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a mensalidade fixa.")]
    [Display(Name = "Mensalidade fixa")]
    public string MensalidadeFixa { get; set; } = string.Empty;

    [Display(Name = "Taxa de adesão")]
    public string? TaxaAdesao { get; set; }

    [Range(1, 31, ErrorMessage = "Informe um dia entre 1 e 31.")]
    [Display(Name = "Dia de vencimento")]
    public int? DiaVencimento { get; set; }

    [StringLength(ContratoFranquiaVersao.MotivoAlteracaoTamanhoMaximo)]
    [Display(Name = "Motivo da alteração")]
    public string? MotivoAlteracao { get; set; }

    [StringLength(ContratoFranquiaVersao.ObservacoesTamanhoMaximo)]
    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DataInicio is { } inicio && DataFim is { } fim && fim < inicio)
        {
            yield return new(
                "A data de término não pode ser anterior à data de início.",
                [nameof(DataFim)]);
        }

        if (!TentarDecimal(PercentualRoyalties, out var royalties)
            || royalties is < 0 or > 100)
        {
            yield return new(
                "Informe um percentual entre 0 e 100.",
                [nameof(PercentualRoyalties)]);
        }

        if (!TentarDecimal(MensalidadeFixa, out var mensalidade) || mensalidade < 0)
        {
            yield return new(
                "Informe uma mensalidade válida.",
                [nameof(MensalidadeFixa)]);
        }

        if (!string.IsNullOrWhiteSpace(TaxaAdesao)
            && (!TentarDecimal(TaxaAdesao, out var taxa) || taxa < 0))
        {
            yield return new(
                "Informe uma taxa de adesão válida.",
                [nameof(TaxaAdesao)]);
        }

        if (NumeroVersao > 1 && string.IsNullOrWhiteSpace(MotivoAlteracao))
        {
            yield return new(
                "Informe o motivo da alteração.",
                [nameof(MotivoAlteracao)]);
        }
    }

    public static bool TentarDecimal(string? valor, out decimal resultado)
    {
        const NumberStyles estilos = NumberStyles.Number;
        return decimal.TryParse(valor, estilos, CultureInfo.GetCultureInfo("pt-BR"), out resultado)
            || decimal.TryParse(valor, estilos, CultureInfo.InvariantCulture, out resultado);
    }
}

public sealed class NovaVersaoContratoFranquiaViewModel
{
    [Required(ErrorMessage = "Informe o motivo da alteração.")]
    [StringLength(ContratoFranquiaVersao.MotivoAlteracaoTamanhoMaximo)]
    public string MotivoAlteracao { get; set; } = string.Empty;
}

public sealed class UploadDocumentoContratoFranquiaViewModel
{
    [Required(ErrorMessage = "Selecione o tipo do documento.")]
    public TipoDocumentoContratoFranquia? TipoDocumento { get; set; }

    [Required(ErrorMessage = "Selecione um arquivo PDF.")]
    public IFormFile? Arquivo { get; set; }
}

public sealed class ContratoVersaoDetalheViewModel
{
    public Guid FranqueadoId { get; init; }
    public string FranqueadoNome { get; init; } = string.Empty;
    public Guid UnidadeId { get; init; }
    public string UnidadeNome { get; init; } = string.Empty;
    public Guid ContratoId { get; init; }
    public string? NumeroContrato { get; init; }
    public required VersaoContratoFranquiaViewModel Versao { get; init; }
}

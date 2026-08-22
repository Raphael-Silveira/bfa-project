using BFA.Application.Unidades.Contratos;
using BFA.Domain.Contratos;

namespace BFA.Web.ViewModels.Unidade;

public interface IUnidadeContextoViewModel
{
    Guid OrganizacaoId { get; }

    Guid UnidadeId { get; }

    string NomeUnidade { get; }

    bool PodeTrocarUnidade { get; }
}

public sealed class PainelUnidadeViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }

    public required Guid UnidadeId { get; init; }

    public required string NomeUnidade { get; init; }

    public required bool PodeTrocarUnidade { get; init; }

    public ContratoUnidadeViewModel? Contrato { get; init; }
}

public sealed class ContratoUnidadeDetalheViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }

    public required Guid UnidadeId { get; init; }

    public required string NomeUnidade { get; init; }

    public required bool PodeTrocarUnidade { get; init; }

    public ContratoUnidadeViewModel? Contrato { get; init; }
}

public sealed record ContratoUnidadeViewModel(
    Guid ContratoId,
    string? Numero,
    StatusContratoFranquia Status,
    Guid VersaoId,
    int NumeroVersao,
    DateOnly DataInicio,
    DateOnly? DataFim,
    decimal PercentualRoyalties,
    decimal MensalidadeFixa,
    decimal? TaxaAdesao,
    int? DiaVencimento,
    string? Observacoes,
    IReadOnlyList<DocumentoContratoUnidadeViewModel> Documentos);

public sealed record DocumentoContratoUnidadeViewModel(
    Guid Id,
    TipoDocumentoContratoFranquia TipoDocumento,
    string NomeOriginal,
    long TamanhoBytes);

public sealed class SelecaoUnidadeViewModel
{
    public string? PrimeiroNomeUsuario { get; init; }

    public required IReadOnlyList<UnidadeSelecaoItemViewModel> Unidades { get; init; }
}

public sealed record UnidadeSelecaoItemViewModel(Guid UnidadeId, string Nome);

internal static class ContratoUnidadeViewModelMapper
{
    public static ContratoUnidadeViewModel? Mapear(ContratoAtivoUnidadeResumo? contrato) =>
        contrato is null
            ? null
            : new(
                contrato.ContratoId,
                contrato.Numero,
                contrato.Status,
                contrato.VersaoId,
                contrato.NumeroVersao,
                contrato.DataInicio,
                contrato.DataFim,
                contrato.PercentualRoyalties,
                contrato.MensalidadeFixa,
                contrato.TaxaAdesao,
                contrato.DiaVencimento,
                contrato.Observacoes,
                contrato.Documentos.Select(documento => new DocumentoContratoUnidadeViewModel(
                    documento.Id,
                    documento.TipoDocumento,
                    documento.NomeOriginal,
                    documento.TamanhoBytes)).ToArray());
}

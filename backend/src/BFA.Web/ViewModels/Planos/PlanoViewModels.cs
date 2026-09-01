using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BFA.Application.Planos;
using BFA.Domain.Planos;
using BFA.Web.ViewModels.Unidade;

namespace BFA.Web.ViewModels.Planos;

public sealed class PlanosListaViewModel : IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; init; }
    public Guid UnidadeId { get; init; }
    public string NomeUnidade { get; init; } = string.Empty;
    public bool PodeTrocarUnidade { get; init; }
    public bool EhLocal { get; init; }
    public bool PodeGerenciar { get; init; }
    public bool PossuiFranqueadoAtivo { get; init; }
    public FiltroPlanos Filtro { get; init; }
    public required string RotaBase { get; init; }
    public IReadOnlyList<PlanoResumo> Planos { get; init; } = [];
}

public sealed class PlanoDetalheViewModel : IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; init; }
    public Guid UnidadeId { get; init; }
    public string NomeUnidade { get; init; } = string.Empty;
    public bool PodeTrocarUnidade { get; init; }
    public bool EhLocal { get; init; }
    public bool PodeGerenciar { get; init; }
    public bool PossuiFranqueadoAtivo { get; init; }
    public required string RotaBase { get; init; }
    public required PlanoDetalheResumo Plano { get; init; }
}

public sealed record PlanoAcoesViewModel(
    Guid PlanoId,
    bool Ativo,
    bool PodeGerenciar,
    string RotaBase);

public sealed class PlanoFormViewModel : IValidatableObject, IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }
    public bool EhLocal { get; set; }
    public bool NovaVersao { get; set; }
    public Guid? PlanoId { get; set; }
    public string NomePlanoAtual { get; set; } = string.Empty;
    public string RotaBase { get; set; } = string.Empty;

    [Display(Name = "Nome do plano")]
    [StringLength(Plano.NomeTamanhoMaximo)]
    public string? Nome { get; set; }

    [Display(Name = "Duração em meses")]
    [Range(1, short.MaxValue, ErrorMessage = "Informe uma duração maior que zero.")]
    public int? DuracaoMeses { get; set; }

    [Display(Name = "Frequência semanal")]
    [Range(1, 7, ErrorMessage = "Selecione uma frequência entre 1 e 7 vezes por semana.")]
    public int? FrequenciaSemanal { get; set; }

    [Display(Name = "Valor mensal")]
    public string ValorMensal { get; set; } = string.Empty;

    [Display(Name = "Cobrar taxa de matrícula?")]
    public bool CobraMatricula { get; set; }

    [Display(Name = "Valor da taxa de matrícula")]
    public string? ValorMatricula { get; set; }

    [Display(Name = "Início da vigência")]
    public string VigenciaInicioTexto { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!NovaVersao && string.IsNullOrWhiteSpace(Nome))
            yield return new("Informe o nome do plano.", [nameof(Nome)]);
        if (!TentarDecimal(ValorMensal, out var valorMensal) || valorMensal <= 0)
            yield return new("Informe um valor mensal maior que zero.", [nameof(ValorMensal)]);
        if (CobraMatricula)
        {
            if (!TentarDecimal(ValorMatricula, out var valorMatricula)
                || valorMatricula <= 0)
                yield return new(
                    "Informe o valor da taxa de matrícula maior que zero.",
                    [nameof(ValorMatricula)]);
        }
        else
        {
            ValorMatricula = null;
        }
        if (!DateOnly.TryParseExact(
                VigenciaInicioTexto?.Trim(), "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            yield return new(
                "Informe o início da vigência no formato dd/mm/aaaa.",
                [nameof(VigenciaInicioTexto)]);
    }

    public bool TentarCriarTermos(out PlanoTermosSolicitacao? termos)
    {
        termos = null;
        if (!DuracaoMeses.HasValue || !FrequenciaSemanal.HasValue
            || !TentarDecimal(ValorMensal, out var valorMensal)
            || !DateOnly.TryParseExact(
                VigenciaInicioTexto?.Trim(), "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var vigenciaInicio))
            return false;
        decimal? valorMatricula = null;
        if (CobraMatricula)
        {
            if (!TentarDecimal(ValorMatricula, out var valor)) return false;
            valorMatricula = valor;
        }
        termos = new(
            DuracaoMeses.Value, FrequenciaSemanal.Value, valorMensal,
            CobraMatricula, valorMatricula, vigenciaInicio);
        return true;
    }

    public static bool TentarDecimal(string? valor, out decimal resultado) =>
        decimal.TryParse(
            valor, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out resultado)
        || decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out resultado);
}

internal static class PlanoViewModelMapper
{
    public static PlanoFormViewModel Novo(
        bool local, string rotaBase, ContextoPlanosResumo? contexto = null,
        bool podeTrocar = false) => new()
    {
        EhLocal = local,
        RotaBase = rotaBase,
        OrganizacaoId = contexto?.OrganizacaoId ?? Guid.Empty,
        UnidadeId = contexto?.UnidadeId ?? Guid.Empty,
        NomeUnidade = contexto?.NomeUnidade ?? string.Empty,
        PodeTrocarUnidade = podeTrocar,
        FrequenciaSemanal = 1
    };

    public static PlanoFormViewModel NovaVersao(
        DetalhePlanoResultado resultado, string rotaBase, bool local, bool podeTrocar)
    {
        var atual = resultado.Plano.VersaoAtual;
        return new()
        {
            OrganizacaoId = resultado.Contexto.OrganizacaoId,
            UnidadeId = resultado.Contexto.UnidadeId ?? Guid.Empty,
            NomeUnidade = resultado.Contexto.NomeUnidade ?? string.Empty,
            PodeTrocarUnidade = podeTrocar,
            EhLocal = local,
            NovaVersao = true,
            PlanoId = resultado.Plano.Id,
            NomePlanoAtual = resultado.Plano.Nome,
            RotaBase = rotaBase,
            DuracaoMeses = atual?.DuracaoMeses,
            FrequenciaSemanal = atual?.FrequenciaSemanal,
            ValorMensal = atual?.ValorMensal.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"))
                ?? string.Empty,
            CobraMatricula = atual?.CobraMatricula ?? false,
            ValorMatricula = atual?.ValorMatricula?.ToString(
                "N2", CultureInfo.GetCultureInfo("pt-BR")),
            VigenciaInicioTexto = string.Empty
        };
    }
}

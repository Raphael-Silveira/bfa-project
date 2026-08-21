using System.ComponentModel.DataAnnotations;
using BFA.Domain.Franqueados;
using BFA.Domain.Contratos;

namespace BFA.Web.ViewModels.Franqueadora;

public sealed class FranqueadosIndexViewModel
{
    public IReadOnlyList<FranqueadoItemViewModel> Franqueados { get; init; } = [];
}

public sealed record FranqueadoItemViewModel(
    Guid Id,
    string NomeRazaoSocial,
    string? NomeFantasia,
    string DocumentoFormatado,
    string TipoPessoa,
    int QuantidadeUnidadesAtivas,
    bool Ativo);

public sealed class FranqueadoDetalheViewModel
{
    public Guid Id { get; init; }

    public string NomeRazaoSocial { get; init; } = string.Empty;

    public string? NomeFantasia { get; init; }

    public string DocumentoFormatado { get; init; } = string.Empty;

    public string TipoPessoa { get; init; } = string.Empty;

    public string? Telefone { get; init; }

    public string Email { get; init; } = string.Empty;

    public string? EmailFinanceiro { get; init; }

    public string? ResponsavelLegal { get; init; }

    public string Endereco { get; init; } = string.Empty;

    public string? Observacoes { get; init; }

    public bool Ativo { get; init; }

    public IReadOnlyList<FranqueadoUsuarioItemViewModel> Usuarios { get; init; } = [];

    public IReadOnlyList<FranqueadoUnidadeItemViewModel> Unidades { get; init; } = [];

    public IReadOnlyList<UnidadeDisponivelFranqueadoViewModel> UnidadesDisponiveis { get; init; } = [];

    [Required(ErrorMessage = "Selecione uma unidade.")]
    public Guid? UnidadeId { get; set; }
}

public sealed record FranqueadoUsuarioItemViewModel(
    Guid UsuarioId,
    string Nome,
    string Email,
    bool Principal,
    bool Ativo);

public sealed record FranqueadoUnidadeItemViewModel(
    Guid UnidadeId,
    string Nome,
    bool VinculoAtivo,
    bool UnidadeAtiva,
    DateTime CriadoEmUtc,
    StatusContratoFranquia? StatusContrato);

public sealed record UnidadeDisponivelFranqueadoViewModel(Guid Id, string Nome);

public sealed class EditarFranqueadoViewModel : IValidatableObject
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Selecione o tipo de pessoa.")]
    [Display(Name = "Tipo de pessoa")]
    public TipoPessoaFranqueado? TipoPessoa { get; set; }

    [Required(ErrorMessage = "Informe o nome ou razão social.")]
    [StringLength(Franqueado.NomeRazaoSocialTamanhoMaximo)]
    [Display(Name = "Nome / Razão social")]
    public string NomeRazaoSocial { get; set; } = string.Empty;

    [StringLength(Franqueado.NomeFantasiaTamanhoMaximo)]
    [Display(Name = "Nome fantasia")]
    public string? NomeFantasia { get; set; }

    [Required(ErrorMessage = "Informe o CPF ou CNPJ.")]
    [StringLength(18)]
    [Display(Name = "CPF / CNPJ")]
    public string Documento { get; set; } = string.Empty;

    [StringLength(Franqueado.TelefoneTamanhoMaximo)]
    [Display(Name = "Telefone comercial")]
    public string? Telefone { get; set; }

    [Required(ErrorMessage = "Informe o email comercial.")]
    [EmailAddress(ErrorMessage = "Informe um email comercial válido.")]
    [StringLength(Franqueado.EmailTamanhoMaximo)]
    [Display(Name = "Email comercial")]
    public string Email { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Informe um email financeiro válido.")]
    [StringLength(Franqueado.EmailFinanceiroTamanhoMaximo)]
    [Display(Name = "Email financeiro")]
    public string? EmailFinanceiro { get; set; }

    [StringLength(Franqueado.ResponsavelLegalTamanhoMaximo)]
    [Display(Name = "Representante legal")]
    public string? ResponsavelLegal { get; set; }

    [StringLength(Franqueado.LogradouroTamanhoMaximo)]
    [Display(Name = "Logradouro")]
    public string? Logradouro { get; set; }

    [StringLength(Franqueado.NumeroTamanhoMaximo)]
    [Display(Name = "Número")]
    public string? Numero { get; set; }

    [StringLength(Franqueado.ComplementoTamanhoMaximo)]
    [Display(Name = "Complemento")]
    public string? Complemento { get; set; }

    [StringLength(Franqueado.BairroTamanhoMaximo)]
    [Display(Name = "Bairro")]
    public string? Bairro { get; set; }

    [Display(Name = "Estado")]
    public int? EstadoCodigoIbge { get; set; }

    [Display(Name = "Município")]
    public int? MunicipioCodigoIbge { get; set; }

    [StringLength(10)]
    [Display(Name = "CEP")]
    public string? Cep { get; set; }

    [StringLength(Franqueado.ObservacoesTamanhoMaximo)]
    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }

    public bool Ativo { get; set; }

    public IReadOnlyList<EstadoSelecaoLocalidadeViewModel> Estados { get; set; } = [];

    public IReadOnlyList<MunicipioSelecaoLocalidadeViewModel> Municipios { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TipoPessoa is null || !Enum.IsDefined(TipoPessoa.Value))
        {
            yield return new("Selecione um tipo de pessoa válido.", [nameof(TipoPessoa)]);
        }

        if (EstadoCodigoIbge is not > 0)
        {
            yield return new("Selecione um Estado.", [nameof(EstadoCodigoIbge)]);
        }

        if (MunicipioCodigoIbge is not > 0)
        {
            yield return new("Selecione um Município.", [nameof(MunicipioCodigoIbge)]);
        }
    }
}

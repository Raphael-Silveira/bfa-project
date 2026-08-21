using System.ComponentModel.DataAnnotations;
using BFA.Application.Franqueadora.Usuarios;
using BFA.Domain.Franqueados;
using BFA.Domain.Usuarios;

namespace BFA.Web.ViewModels.Franqueadora;

public sealed class UsuariosFranqueadoraIndexViewModel
{
    public IReadOnlyList<UsuarioFranqueadoraItemViewModel> Usuarios { get; init; } = [];
}

public sealed record UsuarioFranqueadoraItemViewModel(
    Guid Id,
    string Nome,
    string Email,
    IReadOnlyList<string> Funcoes,
    IReadOnlyList<string> Unidades,
    bool Ativo);

public sealed class EditarUsuarioFranqueadoraViewModel
{
    public Guid UsuarioId { get; set; }

    [Required(ErrorMessage = "Informe o nome completo.")]
    [StringLength(
        PerfilUsuario.NomeCompletoTamanhoMaximo,
        ErrorMessage = "O nome completo deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o email.")]
    [EmailAddress(ErrorMessage = "Informe um email válido.")]
    [StringLength(256, ErrorMessage = "O email deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Email de acesso")]
    public string Email { get; set; } = string.Empty;

    [StringLength(
        PerfilUsuario.TelefoneTamanhoMaximo,
        ErrorMessage = "O telefone deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Telefone")]
    public string? Telefone { get; set; }

    public string? MensagemBloqueio { get; set; }

    public bool EdicaoBloqueada => !string.IsNullOrWhiteSpace(MensagemBloqueio);
}

public sealed class NovoUsuarioFranqueadoraViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Selecione o tipo de cadastro.")]
    [Display(Name = "Tipo de cadastro")]
    public TipoCadastroUsuario? TipoCadastro { get; set; }

    [Required(ErrorMessage = "Informe o nome completo.")]
    [StringLength(
        PerfilUsuario.NomeCompletoTamanhoMaximo,
        ErrorMessage = "O nome completo deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o email.")]
    [EmailAddress(ErrorMessage = "Informe um email válido.")]
    [StringLength(256, ErrorMessage = "O email deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Email de acesso")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o telefone.")]
    [StringLength(
        PerfilUsuario.TelefoneTamanhoMaximo,
        ErrorMessage = "O telefone deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Telefone")]
    public string Telefone { get; set; } = string.Empty;

    [Display(Name = "Tipo de pessoa")]
    public TipoPessoaFranqueado? TipoPessoa { get; set; }

    [Display(Name = "Razão social")]
    public string? NomeRazaoSocial { get; set; }

    [Display(Name = "Nome fantasia")]
    public string? NomeFantasia { get; set; }

    [StringLength(18)]
    [Display(Name = "CPF / CNPJ")]
    public string? Documento { get; set; }

    [Display(Name = "Telefone comercial")]
    public string? TelefoneFranqueado { get; set; }

    [Display(Name = "Email comercial")]
    public string? EmailFranqueado { get; set; }

    [EmailAddress(ErrorMessage = "Informe um email financeiro válido.")]
    [StringLength(Franqueado.EmailFinanceiroTamanhoMaximo)]
    [Display(Name = "Email financeiro")]
    public string? EmailFinanceiro { get; set; }

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

    public List<Guid> UnidadesIds { get; set; } = [];

    public IReadOnlyList<UnidadeSelecaoUsuarioViewModel> Unidades { get; set; } = [];

    public IReadOnlyList<EstadoSelecaoLocalidadeViewModel> Estados { get; set; } = [];

    public IReadOnlyList<MunicipioSelecaoLocalidadeViewModel> Municipios { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TipoCadastro != TipoCadastroUsuario.Franqueado)
        {
            yield break;
        }

        if (TipoPessoa is null || !Enum.IsDefined(TipoPessoa.Value))
        {
            yield return new("Selecione o tipo de pessoa.", [nameof(TipoPessoa)]);
        }

        if (string.IsNullOrWhiteSpace(Documento))
        {
            yield return new("Informe o CPF ou CNPJ.", [nameof(Documento)]);
        }

        if (TipoPessoa == TipoPessoaFranqueado.PessoaJuridica)
        {
            if (string.IsNullOrWhiteSpace(NomeRazaoSocial))
            {
                yield return new("Informe a razão social.", [nameof(NomeRazaoSocial)]);
            }
            else if (NomeRazaoSocial.Trim().Length > Franqueado.NomeRazaoSocialTamanhoMaximo)
            {
                yield return new(
                    $"A razão social deve possuir no máximo {Franqueado.NomeRazaoSocialTamanhoMaximo} caracteres.",
                    [nameof(NomeRazaoSocial)]);
            }

            if (!string.IsNullOrWhiteSpace(NomeFantasia)
                && NomeFantasia.Trim().Length > Franqueado.NomeFantasiaTamanhoMaximo)
            {
                yield return new(
                    $"O nome fantasia deve possuir no máximo {Franqueado.NomeFantasiaTamanhoMaximo} caracteres.",
                    [nameof(NomeFantasia)]);
            }

            if (!string.IsNullOrWhiteSpace(ResponsavelLegal)
                && ResponsavelLegal.Trim().Length > Franqueado.ResponsavelLegalTamanhoMaximo)
            {
                yield return new(
                    $"O representante legal deve possuir no máximo {Franqueado.ResponsavelLegalTamanhoMaximo} caracteres.",
                    [nameof(ResponsavelLegal)]);
            }

            if (!string.IsNullOrWhiteSpace(TelefoneFranqueado)
                && TelefoneFranqueado.Trim().Length > Franqueado.TelefoneTamanhoMaximo)
            {
                yield return new(
                    $"O telefone comercial deve possuir no máximo {Franqueado.TelefoneTamanhoMaximo} caracteres.",
                    [nameof(TelefoneFranqueado)]);
            }

            var emailComercial = EmailFranqueado?.Trim();

            if (string.IsNullOrWhiteSpace(emailComercial))
            {
                yield return new("Informe o email comercial.", [nameof(EmailFranqueado)]);
            }
            else if (emailComercial.Length > Franqueado.EmailTamanhoMaximo
                || !new EmailAddressAttribute().IsValid(emailComercial))
            {
                yield return new("Informe um email comercial válido.", [nameof(EmailFranqueado)]);
            }
        }

        if (UnidadesIds.Count == 0)
        {
            yield return new("Selecione ao menos uma unidade.", [nameof(UnidadesIds)]);
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

public sealed record UnidadeSelecaoUsuarioViewModel(
    Guid Id,
    string Nome,
    bool Selecionada);

public sealed record EstadoSelecaoLocalidadeViewModel(
    int CodigoIbge,
    string Sigla,
    string Nome);

public sealed record MunicipioSelecaoLocalidadeViewModel(
    int CodigoIbge,
    string Nome);

public sealed record UsuarioFranqueadoraCriadoViewModel(
    string NomeCompleto,
    string Email,
    string TipoCadastro,
    string LinkDefinicaoSenha);

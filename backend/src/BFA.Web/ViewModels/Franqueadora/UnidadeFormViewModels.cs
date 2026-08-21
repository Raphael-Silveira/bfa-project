using System.ComponentModel.DataAnnotations;
using UnidadeDomain = BFA.Domain.Unidades.Unidade;

namespace BFA.Web.ViewModels.Franqueadora;

public sealed class NovaUnidadeViewModel
{
    [Required(ErrorMessage = "Informe o nome da unidade.")]
    [StringLength(
        UnidadeDomain.NomeTamanhoMaximo,
        ErrorMessage = "O nome deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o slug da unidade.")]
    [StringLength(
        UnidadeDomain.SlugTamanhoMaximo,
        ErrorMessage = "O slug deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Slug")]
    public string Slug { get; set; } = string.Empty;
}

public sealed class EditarUnidadeViewModel
{
    public Guid Id { get; init; }

    [Required(ErrorMessage = "Informe o nome da unidade.")]
    [StringLength(
        UnidadeDomain.NomeTamanhoMaximo,
        ErrorMessage = "O nome deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o slug da unidade.")]
    [StringLength(
        UnidadeDomain.SlugTamanhoMaximo,
        ErrorMessage = "O slug deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Slug")]
    public string Slug { get; set; } = string.Empty;
}

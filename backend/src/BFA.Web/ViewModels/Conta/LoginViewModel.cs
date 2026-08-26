using System.ComponentModel.DataAnnotations;

namespace BFA.Web.ViewModels.Conta;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o e-mail ou usuário.")]
    [Display(Name = "E-mail ou usuário")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Senha { get; set; } = string.Empty;

    [Display(Name = "Lembrar-me")]
    public bool LembrarMe { get; set; }

    public string? ReturnUrl { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace BFA.Web.ViewModels.Conta;

public sealed class TrocarSenhaViewModel
{
    [Required(ErrorMessage = "Informe a senha temporária atual.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha atual")]
    public string SenhaAtual { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nova senha")]
    public string NovaSenha { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a nova senha.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NovaSenha), ErrorMessage = "A confirmação deve ser igual à nova senha.")]
    [Display(Name = "Confirmar nova senha")]
    public string ConfirmacaoSenha { get; set; } = string.Empty;
}

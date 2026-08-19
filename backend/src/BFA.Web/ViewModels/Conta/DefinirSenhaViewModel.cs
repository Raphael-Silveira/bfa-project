using System.ComponentModel.DataAnnotations;

namespace BFA.Web.ViewModels.Conta;

public sealed class DefinirSenhaViewModel
{
    public Guid UsuarioId { get; set; }

    public string Token { get; set; } = string.Empty;

    public bool LinkValido { get; set; }

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

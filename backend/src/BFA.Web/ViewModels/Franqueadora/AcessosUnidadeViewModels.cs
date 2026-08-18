using System.ComponentModel.DataAnnotations;

namespace BFA.Web.ViewModels.Franqueadora;

public sealed class AcessosUnidadeViewModel
{
    public Guid UnidadeId { get; init; }

    public string UnidadeNome { get; init; } = string.Empty;

    public bool UnidadeAtiva { get; init; }

    public IReadOnlyList<AdministradorUnidadeItemViewModel> Administradores { get; init; } = [];

    [Required(ErrorMessage = "Informe o email do usuário.")]
    [EmailAddress(ErrorMessage = "Informe um email válido.")]
    [StringLength(256, ErrorMessage = "O email deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}

public sealed record AdministradorUnidadeItemViewModel(
    Guid UnidadeId,
    Guid VinculoId,
    Guid UsuarioId,
    string Email,
    bool Ativo,
    DateTime CriadoEmUtc);

namespace BFA.Web.ViewModels.Shared;

public sealed class AdminShellViewModel
{
    public required string AreaNome { get; init; }

    public string? ContextoNome { get; init; }

    public required string HomeUrl { get; init; }

    public required string HomeAriaLabel { get; init; }

    public required string NavegacaoAriaLabel { get; init; }

    public required string NavegacaoPartial { get; init; }

    public string? TrocarContextoUrl { get; init; }

    public string? TrocarContextoTexto { get; init; }
}

public static class AdminShellViewData
{
    public const string Chave = "BfaAdminShell";
}

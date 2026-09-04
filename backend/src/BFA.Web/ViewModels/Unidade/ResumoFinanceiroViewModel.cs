namespace BFA.Web.ViewModels.Unidade;

public sealed class ResumoFinanceiroViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required string TotalReceita { get; init; }
    public required string TotalPendente { get; init; }
    public required string TotalAtrasado { get; init; }
    public required int CobrancasPendentes { get; init; }
    public required int CobrancasAtrasadas { get; init; }
    public required int TotalAlunosComDebito { get; init; }
}

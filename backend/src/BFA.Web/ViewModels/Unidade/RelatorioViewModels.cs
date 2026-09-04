using BFA.Application.Relatorios;
using BFA.Application.Unidades;
using BFA.Domain.Cobrancas;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.ViewModels.Unidade;

public sealed class RelatorioIndexViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required ResumoGeralRelatorios Resumo { get; init; }
}

public sealed class RelatorioFinanceiroViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required string TotalReceita { get; init; }
    public required string TotalPendente { get; init; }
    public required string TotalAtrasado { get; init; }
    public IReadOnlyList<FinanceiroPorTipoViewModel> PorTipo { get; init; } = [];
    public IReadOnlyList<FinanceiroPorStatusViewModel> PorStatus { get; init; } = [];
    public IReadOnlyList<FinanceiroPorPeriodoViewModel> PorPeriodo { get; init; } = [];

    [BindProperty(SupportsGet = true)]
    public DateOnly? DataInicio { get; init; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DataFim { get; init; }
}

public sealed record FinanceiroPorTipoViewModel(
    string Tipo,
    string Valor,
    int Quantidade);

public sealed record FinanceiroPorStatusViewModel(
    string Status,
    string Valor,
    int Quantidade);

public sealed record FinanceiroPorPeriodoViewModel(
    string Periodo,
    string Receita,
    string Pendente);

public sealed class RelatorioInadimplenciaViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required string TotalAtrasado { get; init; }
    public required int TotalAlunos { get; init; }
    public IReadOnlyList<InadimplenciaAlunoViewModel> Alunos { get; init; } = [];
}

public sealed record InadimplenciaAlunoViewModel(
    Guid AlunoId,
    string NomeCompleto,
    string? Cpf,
    int CobrancasAtrasadas,
    string ValorTotalAtrasado,
    string? PrimeiraDataVencimento,
    string? UltimaDataVencimento);

public static class RelatorioViewModelMapper
{
    public static RelatorioIndexViewModel MapearIndex(
        UnidadeAcessoResumo contexto,
        ResumoGeralRelatorios resumo) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.Nome,
        PodeTrocarUnidade = false,
        PodeGerenciar = true,
        Resumo = resumo
    };

    public static RelatorioFinanceiroViewModel MapearFinanceiro(
        UnidadeAcessoResumo contexto,
        FinanceiroDetalheRelatorio relatorio,
        DateOnly? dataInicio,
        DateOnly? dataFim) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.Nome,
        PodeTrocarUnidade = false,
        PodeGerenciar = true,
        TotalReceita = relatorio.TotalReceita.ToString("C"),
        TotalPendente = relatorio.TotalPendente.ToString("C"),
        TotalAtrasado = relatorio.TotalAtrasado.ToString("C"),
        PorTipo = relatorio.PorTipo.Select(t => new FinanceiroPorTipoViewModel(
            MapearTipo(t.Tipo),
            t.Valor.ToString("C"),
            t.Quantidade)).ToList(),
        PorStatus = relatorio.PorStatus.Select(s => new FinanceiroPorStatusViewModel(
            MapearStatus(s.Status),
            s.Valor.ToString("C"),
            s.Quantidade)).ToList(),
        PorPeriodo = relatorio.PorPeriodo.Select(p => new FinanceiroPorPeriodoViewModel(
            $"{p.Mes:D2}/{p.Ano}",
            p.Receita.ToString("C"),
            p.Pendente.ToString("C"))).ToList(),
        DataInicio = dataInicio,
        DataFim = dataFim
    };

    public static RelatorioInadimplenciaViewModel MapearInadimplencia(
        UnidadeAcessoResumo contexto,
        InadimplenciaRelatorio relatorio) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.Nome,
        PodeTrocarUnidade = false,
        PodeGerenciar = true,
        TotalAtrasado = relatorio.TotalAtrasado.ToString("C"),
        TotalAlunos = relatorio.TotalAlunos,
        Alunos = relatorio.Alunos.Select(a => new InadimplenciaAlunoViewModel(
            a.AlunoId,
            a.NomeCompleto,
            a.Cpf,
            a.CobrancasAtrasadas,
            a.ValorTotalAtrasado.ToString("C"),
            a.PrimeiraDataVencimento?.ToString("dd/MM/yyyy"),
            a.UltimaDataVencimento?.ToString("dd/MM/yyyy"))).ToList()
    };

    private static string MapearTipo(TipoCobranca tipo) => tipo switch
    {
        TipoCobranca.Matricula => "Matrícula",
        TipoCobranca.Mensalidade => "Mensalidade",
        TipoCobranca.Avulso => "Avulso",
        _ => tipo.ToString()
    };

    private static string MapearStatus(StatusCobranca status) => status switch
    {
        StatusCobranca.Pendente => "Pendente",
        StatusCobranca.Paga => "Paga",
        StatusCobranca.Atrasada => "Atrasada",
        StatusCobranca.Cancelada => "Cancelada",
        _ => status.ToString()
    };
}

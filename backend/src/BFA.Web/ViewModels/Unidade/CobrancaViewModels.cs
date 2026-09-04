using BFA.Application.Cobrancas;
using BFA.Application.Unidades;
using BFA.Domain.Cobrancas;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace BFA.Web.ViewModels.Unidade;

public sealed class CobrancasListaViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public IReadOnlyList<CobrancaResumoViewModel> Cobrancas { get; init; } = [];

    [BindProperty(SupportsGet = true)]
    public Guid? AlunoId { get; init; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; init; }

    [BindProperty(SupportsGet = true)]
    public string? Tipo { get; init; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DataVencimentoInicio { get; init; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DataVencimentoFim { get; init; }
}

public sealed record CobrancaResumoViewModel(
    Guid CobrancaId,
    string AlunoNome,
    string Descricao,
    string Tipo,
    string Valor,
    string ValorPago,
    string DataVencimento,
    string Status);

public sealed class CobrancaDetalheViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required CobrancaDetalheItemViewModel Cobranca { get; init; }
}

public sealed class CobrancaDetalheItemViewModel
{
    public required Guid CobrancaId { get; init; }
    public required string AlunoNome { get; init; }
    public required string? AlunoCpf { get; init; }
    public required string Descricao { get; init; }
    public required string Tipo { get; init; }
    public required string Valor { get; init; }
    public required string ValorPago { get; init; }
    public required string SaldoDevedor { get; init; }
    public required string DataEmissao { get; init; }
    public required string DataVencimento { get; init; }
    public required string? DataPagamento { get; init; }
    public required string Status { get; init; }
    public string? Observacoes { get; init; }
    public IReadOnlyList<PagamentoResumoViewModel> Pagamentos { get; init; } = [];
    public bool PodeRegistrarPagamento => Status is "Pendente" or "Atrasada";
    public bool PodeCancelar => Status == "Pendente";
}

public sealed record PagamentoResumoViewModel(
    Guid PagamentoId,
    string Valor,
    string DataPagamento,
    string FormaPagamento,
    string? Observacoes);

public sealed class CobrancaFormViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }

    [Required(ErrorMessage = "Selecione o aluno.")]
    public Guid? AlunoId { get; set; }

    [Required(ErrorMessage = "Selecione o tipo da cobranca.")]
    public string? Tipo { get; set; }

    [Required(ErrorMessage = "Informe a descricao.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "A descricao deve ter entre 3 e 200 caracteres.")]
    public string? Descricao { get; set; }

    [Required(ErrorMessage = "Informe o valor.")]
    [Range(0.01, 99999999.99, ErrorMessage = "O valor deve ser maior que zero.")]
    public decimal? Valor { get; set; }

    [Required(ErrorMessage = "Informe a data de vencimento.")]
    public DateOnly? DataVencimento { get; set; }

    public string? Observacoes { get; set; }

    public IReadOnlyList<AlunoParaSelecaoViewModel> Alunos { get; init; } = [];
}

public sealed record AlunoParaSelecaoViewModel(
    Guid AlunoId,
    string NomeCompleto,
    string? Cpf,
    Guid MatriculaId);

public sealed class PagamentoFormViewModel
{
    [Required(ErrorMessage = "Informe o valor do pagamento.")]
    [Range(0.01, 99999999.99, ErrorMessage = "O valor deve ser maior que zero.")]
    public decimal? Valor { get; set; }

    [Required(ErrorMessage = "Informe a data do pagamento.")]
    public DateOnly? DataPagamento { get; set; }

    [Required(ErrorMessage = "Selecione a forma de pagamento.")]
    public string? FormaPagamento { get; set; }

    public string? Observacoes { get; set; }
}

public static class CobrancaViewModelMapper
{
    public static CobrancasListaViewModel MapearLista(
        UnidadeAcessoResumo contexto,
        IReadOnlyList<CobrancaListaItem> itens,
        Guid? alunoId,
        string? status,
        string? tipo,
        DateOnly? dataVencimentoInicio,
        DateOnly? dataVencimentoFim) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.Nome,
        PodeTrocarUnidade = false,
        PodeGerenciar = true,
        Cobrancas = itens.Select(MapearResumo).ToArray(),
        AlunoId = alunoId,
        Status = status,
        Tipo = tipo,
        DataVencimentoInicio = dataVencimentoInicio,
        DataVencimentoFim = dataVencimentoFim
    };

    public static CobrancaDetalheViewModel MapearDetalhe(
        UnidadeAcessoResumo contexto,
        CobrancaDetalhe detalhe) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.Nome,
        PodeTrocarUnidade = false,
        PodeGerenciar = true,
        Cobranca = new CobrancaDetalheItemViewModel
        {
            CobrancaId = detalhe.CobrancaId,
            AlunoNome = detalhe.AlunoNome,
            AlunoCpf = detalhe.AlunoCpf,
            Descricao = detalhe.Descricao,
            Tipo = MapearTipo(detalhe.Tipo),
            Valor = detalhe.Valor.ToString("C"),
            ValorPago = detalhe.ValorPago.ToString("C"),
            SaldoDevedor = detalhe.SaldoDevedor.ToString("C"),
            DataEmissao = detalhe.DataEmissao.ToString("dd/MM/yyyy"),
            DataVencimento = detalhe.DataVencimento.ToString("dd/MM/yyyy"),
            DataPagamento = detalhe.DataPagamento?.ToString("dd/MM/yyyy"),
            Status = MapearStatus(detalhe.Status),
            Observacoes = detalhe.Observacoes,
            Pagamentos = detalhe.Pagamentos.Select(MapearPagamento).ToArray()
        }
    };

    public static CobrancaFormViewModel MapearFormularioCriacao(
        UnidadeAcessoResumo contexto,
        IReadOnlyList<AlunoParaSelecao> alunos) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.Nome,
        PodeTrocarUnidade = false,
        PodeGerenciar = true,
        Alunos = alunos.Select(a => new AlunoParaSelecaoViewModel(
            a.AlunoId,
            a.NomeCompleto,
            a.Cpf,
            a.MatriculaId)).ToArray()
    };

    public static CobrancaFormViewModel ReconstituirFormularioCriacao(
        UnidadeAcessoResumo contexto,
        CobrancaFormViewModel model,
        IReadOnlyList<AlunoParaSelecao> alunos) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.Nome,
        PodeTrocarUnidade = false,
        PodeGerenciar = true,
        AlunoId = model.AlunoId,
        Tipo = model.Tipo,
        Descricao = model.Descricao,
        Valor = model.Valor,
        DataVencimento = model.DataVencimento,
        Observacoes = model.Observacoes,
        Alunos = alunos.Select(a => new AlunoParaSelecaoViewModel(
            a.AlunoId,
            a.NomeCompleto,
            a.Cpf,
            a.MatriculaId)).ToArray()
    };

    public static ResumoFinanceiroViewModel MapearResumoFinanceiro(
        UnidadeAcessoResumo contexto,
        ResumoFinanceiro resumo) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.Nome,
        PodeTrocarUnidade = false,
        PodeGerenciar = true,
        TotalReceita = resumo.TotalReceita.ToString("C"),
        TotalPendente = resumo.TotalPendente.ToString("C"),
        TotalAtrasado = resumo.TotalAtrasado.ToString("C"),
        CobrancasPendentes = resumo.CobrancasPendentes,
        CobrancasAtrasadas = resumo.CobrancasAtrasadas,
        TotalAlunosComDebito = resumo.TotalAlunosComDebito
    };

    private static CobrancaResumoViewModel MapearResumo(CobrancaListaItem item) => new(
        item.CobrancaId,
        item.AlunoNome,
        item.Descricao,
        MapearTipo(item.Tipo),
        item.Valor.ToString("C"),
        item.ValorPago.ToString("C"),
        item.DataVencimento.ToString("dd/MM/yyyy"),
        MapearStatus(item.Status));

    private static PagamentoResumoViewModel MapearPagamento(PagamentoResumo pgto) => new(
        pgto.PagamentoId,
        pgto.Valor.ToString("C"),
        pgto.DataPagamento.ToString("dd/MM/yyyy"),
        MapearFormaPagamento(pgto.FormaPagamento),
        pgto.Observacoes);

    private static string MapearStatus(StatusCobranca status) => status switch
    {
        StatusCobranca.Pendente => "Pendente",
        StatusCobranca.Paga => "Paga",
        StatusCobranca.Atrasada => "Atrasada",
        StatusCobranca.Cancelada => "Cancelada",
        _ => status.ToString()
    };

    private static string MapearTipo(TipoCobranca tipo) => tipo switch
    {
        TipoCobranca.Matricula => "Matricula",
        TipoCobranca.Mensalidade => "Mensalidade",
        TipoCobranca.Avulso => "Avulso",
        _ => tipo.ToString()
    };

    private static string MapearFormaPagamento(FormaPagamento forma) => forma switch
    {
        FormaPagamento.Dinheiro => "Dinheiro",
        FormaPagamento.Pix => "Pix",
        FormaPagamento.CartaoCredito => "Cartao de Credito",
        FormaPagamento.CartaoDebito => "Cartao de Debito",
        FormaPagamento.Boleto => "Boleto",
        FormaPagamento.Transferencia => "Transferencia",
        FormaPagamento.Outros => "Outros",
        _ => forma.ToString()
    };
}

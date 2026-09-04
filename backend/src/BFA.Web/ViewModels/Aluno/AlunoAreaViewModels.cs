using BFA.Application.AlunoArea;

namespace BFA.Web.ViewModels.AlunoArea;

public interface IAlunoContextoViewModel
{
    Guid OrganizacaoId { get; }
    Guid UnidadeId { get; }
    string NomeAluno { get; }
}

public sealed class DashboardAlunoViewModel : IAlunoContextoViewModel
{
    public Guid OrganizacaoId { get; init; }
    public Guid UnidadeId { get; init; }
    public required string NomeAluno { get; init; }
    public required string NomeUnidade { get; init; }
    public string? ProximaAula { get; init; }
    public required string PercentualFrequencia { get; init; }
    public required string TotalPendente { get; init; }
    public int TotalAulas { get; init; }

    public static DashboardAlunoViewModel Mapear(DashboardAlunoDto dto, Guid unidadeId)
    {
        return new DashboardAlunoViewModel
        {
            OrganizacaoId = dto.Perfil.AlunoId,
            UnidadeId = unidadeId,
            NomeAluno = dto.Perfil.NomeCompleto,
            NomeUnidade = dto.NomeUnidade,
            ProximaAula = dto.ProximaAula,
            PercentualFrequencia = dto.PercentualFrequencia,
            TotalPendente = dto.TotalPendente,
            TotalAulas = dto.TotalAulas
        };
    }
}

public sealed class PerfilAlunoViewModel
{
    public required string NomeCompleto { get; init; }
    public string? CpfFormatado { get; init; }
    public string? Telefone { get; init; }
    public string? Email { get; init; }
    public required string DataNascimento { get; init; }
    public required string Idade { get; init; }

    public static PerfilAlunoViewModel Mapear(PerfilAlunoDto dto)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var idade = hoje.Year - dto.DataNascimento.Year;
        if (hoje < dto.DataNascimento.AddYears(idade))
            idade--;

        return new PerfilAlunoViewModel
        {
            NomeCompleto = dto.NomeCompleto,
            CpfFormatado = FormatCpf(dto.Cpf),
            Telefone = dto.Telefone,
            Email = dto.Email,
            DataNascimento = dto.DataNascimento.ToString("dd/MM/yyyy"),
            Idade = $"{idade} anos"
        };
    }

    private static string? FormatCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11)
            return cpf;

        return $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";
    }
}

public sealed class MatriculaAlunoViewModel
{
    public required string PlanoNome { get; init; }
    public required string Status { get; init; }
    public required string DataInicio { get; init; }
    public required string DataFimPrevista { get; init; }
    public string? DataFimReal { get; init; }
    public required string ValorMensal { get; init; }

    public static MatriculaAlunoViewModel Mapear(MatriculaAlunoDto dto)
    {
        return new MatriculaAlunoViewModel
        {
            PlanoNome = dto.PlanoNome,
            Status = dto.Status,
            DataInicio = dto.DataInicio.ToString("dd/MM/yyyy"),
            DataFimPrevista = dto.DataFimPrevista.ToString("dd/MM/yyyy"),
            DataFimReal = dto.DataFimReal?.ToString("dd/MM/yyyy"),
            ValorMensal = $"R$ {dto.ValorMensal:N2}"
        };
    }
}

public sealed class AulaAlunoViewModel
{
    public required string Data { get; init; }
    public required string HoraInicio { get; init; }
    public required string HoraFim { get; init; }
    public required string TurmaNome { get; init; }
    public required string Status { get; init; }
    public bool IsProgramada => Status == "Programada";

    public static AulaAlunoViewModel Mapear(AulaAlunoDto dto)
    {
        return new AulaAlunoViewModel
        {
            Data = dto.Data.ToString("dd/MM/yyyy"),
            HoraInicio = dto.HoraInicio,
            HoraFim = dto.HoraFim,
            TurmaNome = dto.TurmaNome,
            Status = dto.Status
        };
    }
}

public sealed class PresencaAlunoViewModel
{
    public required string Data { get; init; }
    public required string TurmaNome { get; init; }
    public required string HoraInicio { get; init; }
    public required string HoraFim { get; init; }
    public required string Status { get; init; }
    public string? Observacoes { get; init; }

    public static PresencaAlunoViewModel Mapear(PresencaAlunoDto dto)
    {
        return new PresencaAlunoViewModel
        {
            Data = dto.Data.ToString("dd/MM/yyyy"),
            TurmaNome = dto.TurmaNome,
            HoraInicio = dto.HoraInicio,
            HoraFim = dto.HoraFim,
            Status = dto.Status,
            Observacoes = dto.Observacoes
        };
    }
}

public sealed class FrequenciaResumoAlunoViewModel
{
    public required string Percentual { get; init; }
    public int TotalAulas { get; init; }
    public int Presentes { get; init; }
    public int Ausentes { get; init; }
    public int Justificados { get; init; }
    public required string PeriodoInicio { get; init; }
    public required string PeriodoFim { get; init; }
    public IReadOnlyList<PresencaAlunoViewModel> Presencas { get; init; } = [];

    public static FrequenciaResumoAlunoViewModel Mapear(
        FrequenciaResumoDto dto,
        IReadOnlyList<PresencaAlunoDto> presencas,
        DateOnly dataInicio,
        DateOnly dataFim)
    {
        return new FrequenciaResumoAlunoViewModel
        {
            Percentual = dto.PercentualFrequencia.ToString("N1"),
            TotalAulas = dto.TotalAulas,
            Presentes = dto.Presentes,
            Ausentes = dto.Ausentes,
            Justificados = dto.Justificados,
            PeriodoInicio = dataInicio.ToString("dd/MM/yyyy"),
            PeriodoFim = dataFim.ToString("dd/MM/yyyy"),
            Presencas = presencas.Select(PresencaAlunoViewModel.Mapear).ToList()
        };
    }
}

public sealed class FinanceiroAlunoViewModel
{
    public required string TotalPendente { get; init; }
    public required string TotalPago { get; init; }
    public IReadOnlyList<CobrancaAlunoViewModel> Cobrancas { get; init; } = [];
    public IReadOnlyList<PagamentoAlunoViewModel> Pagamentos { get; init; } = [];

    public static FinanceiroAlunoViewModel Mapear(FinanceiroResumoDto dto)
    {
        return new FinanceiroAlunoViewModel
        {
            TotalPendente = dto.TotalPendente,
            TotalPago = dto.TotalPago,
            Cobrancas = dto.Cobrancas.Select(CobrancaAlunoViewModel.Mapear).ToList(),
            Pagamentos = dto.Pagamentos.Select(PagamentoAlunoViewModel.Mapear).ToList()
        };
    }
}

public sealed class CobrancaAlunoViewModel
{
    public required string Descricao { get; init; }
    public required string Tipo { get; init; }
    public required string Valor { get; init; }
    public required string ValorPago { get; init; }
    public required string SaldoDevedor { get; init; }
    public required string DataVencimento { get; init; }
    public required string Status { get; init; }
    public int DiasAtraso { get; init; }
    public bool IsAtrasada => Status == "Atrasada";

    public static CobrancaAlunoViewModel Mapear(CobrancaAlunoDto dto)
    {
        return new CobrancaAlunoViewModel
        {
            Descricao = dto.Descricao,
            Tipo = dto.Tipo,
            Valor = dto.Valor,
            ValorPago = dto.ValorPago,
            SaldoDevedor = dto.SaldoDevedor,
            DataVencimento = dto.DataVencimento.ToString("dd/MM/yyyy"),
            Status = dto.Status,
            DiasAtraso = dto.DiasAtraso
        };
    }
}

public sealed class PagamentoAlunoViewModel
{
    public required string DataPagamento { get; init; }
    public required string Valor { get; init; }
    public required string FormaPagamento { get; init; }

    public static PagamentoAlunoViewModel Mapear(PagamentoAlunoDto dto)
    {
        return new PagamentoAlunoViewModel
        {
            DataPagamento = dto.DataPagamento.ToString("dd/MM/yyyy"),
            Valor = dto.Valor,
            FormaPagamento = dto.FormaPagamento
        };
    }
}

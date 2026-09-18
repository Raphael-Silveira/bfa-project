using BFA.Application.AlunoArea;
using BFA.Domain.Alunos;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
    public MatriculaAlunoViewModel? MatriculaAtiva { get; init; }
    public Guid? ProximaAulaId { get; init; }
    public bool ProximaAulaConfirmada { get; init; }
    public bool PodeAlterarConfirmacao { get; init; }

    public static DashboardAlunoViewModel Mapear(DashboardAlunoDto dto, Guid unidadeId)
    {
        return new DashboardAlunoViewModel
        {
            OrganizacaoId = dto.OrganizacaoId,
            UnidadeId = unidadeId,
            NomeAluno = dto.Perfil.NomeCompleto,
            NomeUnidade = dto.NomeUnidade,
            ProximaAula = dto.ProximaAula,
            PercentualFrequencia = dto.PercentualFrequencia,
            TotalPendente = dto.TotalPendente,
            TotalAulas = dto.TotalAulas,
            ProximaAulaId = dto.ProximaAulaId,
            ProximaAulaConfirmada = dto.ProximaAulaConfirmada,
            PodeAlterarConfirmacao = dto.PodeAlterarConfirmacao,
            MatriculaAtiva = dto.MatriculaAtiva is null
                ? null
                : MatriculaAlunoViewModel.Mapear(dto.MatriculaAtiva)
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
    public string? Apelido { get; init; }
    public string? Cep { get; init; }
    public string? Estado { get; init; }
    public string? Municipio { get; init; }
    public string? Bairro { get; init; }
    public string? Logradouro { get; init; }
    public string? Numero { get; init; }
    public string? Complemento { get; init; }
    public bool PossuiFoto { get; init; }

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
            Telefone = TelefoneBrasileiro.Formatar(dto.Telefone),
            Email = dto.Email,
            DataNascimento = dto.DataNascimento.ToString("dd/MM/yyyy"),
            Idade = $"{idade} anos"
            ,Apelido = dto.Apelido
            ,Cep = FormatCep(dto.Cep)
            ,Estado = dto.EstadoSigla is null ? null : $"{dto.EstadoNome} ({dto.EstadoSigla})"
            ,Municipio = dto.MunicipioNome
            ,Bairro = dto.Bairro
            ,Logradouro = dto.Logradouro
            ,Numero = dto.Numero
            ,Complemento = dto.Complemento
            ,PossuiFoto = !string.IsNullOrWhiteSpace(dto.FotoPerfilChave)
        };
    }

    private static string? FormatCep(string? cep) =>
        cep is { Length: 8 } ? $"{cep[..5]}-{cep[5..]}" : cep;

    private static string? FormatCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11)
            return cpf;

        return $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";
    }
}

public sealed class EditarPerfilAlunoViewModel
{
    [BindNever]
    public string NomeCompleto { get; private set; } = string.Empty;

    [BindNever]
    public string CpfFormatado { get; private set; } = string.Empty;

    [BindNever]
    public string DataNascimento { get; private set; } = string.Empty;

    [BindNever]
    public string Idade { get; private set; } = string.Empty;

    [StringLength(Aluno.ApelidoTamanhoMaximo, ErrorMessage = "O apelido deve possuir no máximo {1} caracteres.")]
    public string? Apelido { get; set; }

    [StringLength(Aluno.CepTamanho + 1, ErrorMessage = "Informe um CEP válido.")]
    public string? Cep { get; set; }

    public int? EstadoCodigoIbge { get; set; }
    public int? MunicipioCodigoIbge { get; set; }

    [StringLength(Aluno.BairroTamanhoMaximo)]
    public string? Bairro { get; set; }

    [StringLength(Aluno.LogradouroTamanhoMaximo)]
    public string? Logradouro { get; set; }

    [StringLength(Aluno.NumeroTamanhoMaximo)]
    public string? Numero { get; set; }

    [StringLength(Aluno.ComplementoTamanhoMaximo)]
    public string? Complemento { get; set; }

    public IFormFile? FotoPerfil { get; set; }
    public string? FotoPerfilUrl { get; set; }
    public IReadOnlyList<LocalidadeOpcaoViewModel> Estados { get; set; } = [];
    public IReadOnlyList<LocalidadeOpcaoViewModel> Municipios { get; set; } = [];

    [Required(ErrorMessage = "Informe um e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "O telefone deve possuir no máximo 30 caracteres.")]
    public string? Telefone { get; set; }

    public static EditarPerfilAlunoViewModel Mapear(PerfilAlunoDto dto) => new()
    {
        NomeCompleto = dto.NomeCompleto,
        CpfFormatado = FormatCpf(dto.Cpf) ?? "Não informado",
        DataNascimento = dto.DataNascimento.ToString("dd/MM/yyyy"),
        Idade = CalcularIdade(dto.DataNascimento),
        Apelido = dto.Apelido,
        Cep = FormatCep(dto.Cep),
        EstadoCodigoIbge = dto.EstadoCodigoIbge,
        MunicipioCodigoIbge = dto.MunicipioCodigoIbge,
        Bairro = dto.Bairro,
        Logradouro = dto.Logradouro,
        Numero = dto.Numero,
        Complemento = dto.Complemento,
        FotoPerfilUrl = null,
        Email = dto.Email ?? string.Empty,
        Telefone = TelefoneBrasileiro.FormatarLocal(dto.Telefone)
    };

    public void AplicarDadosSomenteLeitura(PerfilAlunoDto dto)
    {
        NomeCompleto = dto.NomeCompleto;
        CpfFormatado = FormatCpf(dto.Cpf) ?? "Não informado";
        DataNascimento = dto.DataNascimento.ToString("dd/MM/yyyy");
        Idade = CalcularIdade(dto.DataNascimento);
    }

    private static string CalcularIdade(DateOnly nascimento)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var idade = hoje.Year - nascimento.Year;
        if (hoje < nascimento.AddYears(idade)) idade--;
        return $"{idade} anos";
    }

    private static string? FormatCpf(string? cpf) =>
        string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11
            ? cpf
            : $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";

    private static string? FormatCep(string? cep) =>
        cep is { Length: 8 } ? $"{cep[..5]}-{cep[5..]}" : cep;
}

public sealed record LocalidadeOpcaoViewModel(int CodigoIbge, string Nome, string? Sigla = null);

public sealed class MatriculaAlunoViewModel
{
    public required string PlanoNome { get; init; }
    public required string Status { get; init; }
    public required string DataInicio { get; init; }
    public required string DataFimPrevista { get; init; }
    public string? DataFimReal { get; init; }
    public required string ValorMensal { get; init; }
    public int FrequenciaSemanal { get; init; }
    public IReadOnlyList<HorarioMatriculaViewModel> Horarios { get; init; } = [];

    public static MatriculaAlunoViewModel Mapear(MatriculaAlunoDto dto)
    {
        return new MatriculaAlunoViewModel
        {
            PlanoNome = dto.PlanoNome,
            Status = dto.Status,
            DataInicio = dto.DataInicio.ToString("dd/MM/yyyy"),
            DataFimPrevista = dto.DataFimPrevista.ToString("dd/MM/yyyy"),
            DataFimReal = dto.DataFimReal?.ToString("dd/MM/yyyy"),
            ValorMensal = $"R$ {dto.ValorMensal:N2}",
            FrequenciaSemanal = dto.FrequenciaSemanal,
            Horarios = dto.Horarios.Select(HorarioMatriculaViewModel.Mapear).ToList()
        };
    }
}

public sealed class HorarioMatriculaViewModel
{
    public required string DiaSemana { get; init; }
    public required string HoraInicio { get; init; }
    public required string HoraFim { get; init; }
    public required string TurmaNome { get; init; }

    public static HorarioMatriculaViewModel Mapear(HorarioMatriculaDto dto) => new()
    {
        DiaSemana = dto.DiaSemana,
        HoraInicio = dto.HoraInicio,
        HoraFim = dto.HoraFim,
        TurmaNome = dto.TurmaNome
    };
}

public sealed class AulaAlunoViewModel
{
    public Guid AulaId { get; init; }
    public required string Data { get; init; }
    public required string HoraInicio { get; init; }
    public required string HoraFim { get; init; }
    public required string TurmaNome { get; init; }
    public required string Status { get; init; }
    public bool ConfirmacaoAtiva { get; init; }
    public bool PodeAlterarConfirmacao { get; init; }
    public bool IsProgramada => Status == "Programada";

    public static AulaAlunoViewModel Mapear(AulaAlunoDto dto)
    {
        return new AulaAlunoViewModel
        {
            AulaId = dto.AulaId,
            Data = dto.Data.ToString("dd/MM/yyyy"),
            HoraInicio = dto.HoraInicio,
            HoraFim = dto.HoraFim,
            TurmaNome = dto.TurmaNome,
            Status = dto.Status,
            ConfirmacaoAtiva = dto.ConfirmacaoAtiva,
            PodeAlterarConfirmacao = dto.PodeAlterarConfirmacao
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
            Presencas = dto.Presencas.Select(PresencaAlunoViewModel.Mapear).ToList()
        };
    }
}

public sealed class FinanceiroAlunoViewModel
{
    public required string TotalPendente { get; init; }
    public required string TotalPago { get; init; }
    public required string Periodo { get; init; }
    public string DataInicio { get; init; } = string.Empty;
    public string DataFim { get; init; } = string.Empty;
    public IReadOnlyList<CobrancaAlunoViewModel> Cobrancas { get; init; } = [];
    public IReadOnlyList<PagamentoAlunoViewModel> Pagamentos { get; init; } = [];

    public static FinanceiroAlunoViewModel Mapear(
        FinanceiroResumoDto dto,
        string periodo,
        DateOnly? dataInicio,
        DateOnly? dataFim)
    {
        return new FinanceiroAlunoViewModel
        {
            TotalPendente = dto.TotalPendente,
            TotalPago = dto.TotalPago,
            Periodo = periodo,
            DataInicio = dataInicio?.ToString("dd/MM/yyyy") ?? string.Empty,
            DataFim = dataFim?.ToString("dd/MM/yyyy") ?? string.Empty,
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
    public required string Tipo { get; init; }
    public required string Valor { get; init; }
    public required string FormaPagamento { get; init; }

    public static PagamentoAlunoViewModel Mapear(PagamentoAlunoDto dto)
    {
        return new PagamentoAlunoViewModel
        {
            DataPagamento = dto.DataPagamento.ToString("dd/MM/yyyy"),
            Tipo = dto.Tipo,
            Valor = dto.Valor,
            FormaPagamento = dto.FormaPagamento
        };
    }
}

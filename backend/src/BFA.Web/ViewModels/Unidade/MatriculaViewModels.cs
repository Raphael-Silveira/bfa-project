using System.Globalization;
using BFA.Application.Matriculas;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Turmas;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BFA.Web.ViewModels.Unidade;

public sealed class MatriculasListaViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public string? Texto { get; init; }
    public StatusMatricula? Status { get; init; }
    public IReadOnlyList<MatriculaListaItemViewModel> Matriculas { get; init; } = [];
    public bool PossuiFiltros => Texto is not null || Status.HasValue;
    public required int PaginaAtual { get; init; }
    public required int TamanhoPagina { get; init; }
    public required int TotalItens { get; init; }
    public required int TotalAtivas { get; init; }
    public required int TotalEncerradas { get; init; }
    public required int TotalCanceladas { get; init; }
}

public sealed record MatriculaListaItemViewModel(
    Guid MatriculaId,
    string NomeAluno,
    string Plano,
    string Status,
    bool Ativa,
    string DataInicio,
    string DataFimPrevista,
    string ValorMensalContratado,
    string FrequenciaSemanal,
    string GradeAtual);

public sealed class MatriculaDetalheViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public string? TextoRetorno { get; init; }
    public StatusMatricula? StatusRetorno { get; init; }
    public required MatriculaDetalheItemViewModel Matricula { get; init; }
}

public sealed class AlterarGradeMatriculaViewModel : IUnidadeContextoViewModel
{
    [BindNever, ValidateNever]
    public required Guid OrganizacaoId { get; init; }
    [BindNever, ValidateNever]
    public required Guid UnidadeId { get; init; }
    [BindNever, ValidateNever]
    public required string NomeUnidade { get; init; }
    [BindNever, ValidateNever]
    public required bool PodeTrocarUnidade { get; init; }
    [BindNever, ValidateNever]
    public required Guid MatriculaId { get; init; }
    [BindNever, ValidateNever]
    public required string NomeAluno { get; init; }
    [BindNever, ValidateNever]
    public required string DataInicioMatricula { get; init; }
    [BindNever, ValidateNever]
    public required string DataFimPrevista { get; init; }
    [BindNever, ValidateNever]
    public required string FrequenciaSemanal { get; init; }
    [BindNever, ValidateNever]
    public required int FrequenciaSemanalNumero { get; init; }
    public required string DataInicioTexto { get; set; }
    public List<Guid> TurmaHorarioIds { get; set; } = [];
    [BindNever, ValidateNever]
    public IReadOnlyList<HorarioMatriculaOpcaoViewModel> Horarios { get; set; } = [];
    [BindNever, ValidateNever]
    public required IReadOnlySet<Guid> HorariosAtuais { get; set; } = new HashSet<Guid>();
    [BindNever, ValidateNever]
    public required string DataMinimaGrade { get; init; }
}

public sealed class FinalizarMatriculaViewModel : IUnidadeContextoViewModel
{
    [BindNever, ValidateNever]
    public required Guid OrganizacaoId { get; init; }
    [BindNever, ValidateNever]
    public required Guid UnidadeId { get; init; }
    [BindNever, ValidateNever]
    public required string NomeUnidade { get; init; }
    [BindNever, ValidateNever]
    public required bool PodeTrocarUnidade { get; init; }
    [BindNever, ValidateNever]
    public required Guid MatriculaId { get; init; }
    [BindNever, ValidateNever]
    public required string NomeAluno { get; init; }
    [BindNever, ValidateNever]
    public required string DataInicioMatricula { get; init; }
    [BindNever, ValidateNever]
    public required string DataFimPrevista { get; init; }
    [BindNever, ValidateNever]
    public required bool Cancelar { get; init; }
    public required string DataFinalTexto { get; set; }
    [BindNever, ValidateNever]
    public required string DataMinimaEncerramento { get; init; }
}

public sealed record MatriculaDetalheItemViewModel(
    Guid MatriculaId,
    string NomeAluno,
    string DataNascimento,
    string CpfMascarado,
    string Telefone,
    string Email,
    string Status,
    bool Ativa,
    string DataInicio,
    string DataFimPrevista,
    string? DataFimReal,
    string Plano,
    string VersaoComercial,
    string Duracao,
    string FrequenciaSemanal,
    string ValorMensalCatalogo,
    string ValorMensalContratado,
    bool ValorContratadoDiferenteCatalogo,
    string TaxaMatricula,
    IReadOnlyList<ResponsavelMatriculaViewModel> Responsaveis,
    IReadOnlyList<GradeMatriculaViewModel> GradeAtual,
    IReadOnlyList<GradeMatriculaViewModel> HistoricoGrade);

public sealed record ResponsavelMatriculaViewModel(
    string Nome,
    string Relacao,
    string Telefone,
    string Email,
    bool PrincipalContato,
    bool ResponsavelFinanceiro,
    bool Ativo);

public sealed record GradeMatriculaViewModel(
    string DiaSemana,
    string Horario,
    string Turma,
    string Professor,
    string Periodo);

internal static class MatriculasViewModelMapper
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static MatriculasListaViewModel Lista(
        ContextoMatriculasResumo contexto,
        IReadOnlyList<MatriculaListaItem> matriculas,
        bool podeTrocarUnidade,
        string? texto,
        StatusMatricula? status,
        int paginaAtual,
        int tamanhoPagina,
        int totalItens,
        int totalAtivas,
        int totalEncerradas,
        int totalCanceladas) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = podeTrocarUnidade,
        PodeGerenciar = contexto.PodeGerenciar,
        Texto = texto,
        Status = status,
        Matriculas = matriculas.Select(item => new MatriculaListaItemViewModel(
            item.MatriculaId,
            item.NomeCompleto,
            $"{item.Plano} · Versão {item.NumeroVersao}",
            NomeStatus(item.Status),
            item.Status == StatusMatricula.Ativa,
            FormatarData(item.DataInicio),
            FormatarData(item.DataFimPrevista),
            FormatarMoeda(item.ValorMensalContratado),
            FormatarFrequencia(item.FrequenciaSemanal),
            item.QuantidadeHorariosAtuais == 1
                ? "1 horário"
                : $"{item.QuantidadeHorariosAtuais} horários"))
            .ToArray(),
        PaginaAtual = paginaAtual,
        TamanhoPagina = tamanhoPagina,
        TotalItens = totalItens,
        TotalAtivas = totalAtivas,
        TotalEncerradas = totalEncerradas,
        TotalCanceladas = totalCanceladas
    };

    public static MatriculaDetalheViewModel Detalhe(
        ContextoMatriculasResumo contexto,
        MatriculaDetalhe matricula,
        bool podeTrocarUnidade,
        string? textoRetorno,
        StatusMatricula? statusRetorno) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = podeTrocarUnidade,
        PodeGerenciar = contexto.PodeGerenciar,
        TextoRetorno = textoRetorno,
        StatusRetorno = statusRetorno,
        Matricula = new MatriculaDetalheItemViewModel(
            matricula.MatriculaId,
            matricula.NomeAluno,
            FormatarData(matricula.DataNascimentoAluno),
            FormatarCpfMascarado(matricula.CpfAluno),
            InformadoOuPadrao(matricula.TelefoneAluno),
            InformadoOuPadrao(matricula.EmailAluno),
            NomeStatus(matricula.Status),
            matricula.Status == StatusMatricula.Ativa,
            FormatarData(matricula.DataInicio),
            FormatarData(matricula.DataFimPrevista),
            matricula.DataFimReal is { } dataFimReal
                ? FormatarData(dataFimReal)
                : null,
            matricula.Plano,
            $"Versão {matricula.NumeroVersao}",
            matricula.DuracaoMeses == 1
                ? "1 mês"
                : $"{matricula.DuracaoMeses} meses",
            FormatarFrequencia(matricula.FrequenciaSemanal),
            FormatarMoeda(matricula.ValorMensalCatalogo),
            FormatarMoeda(matricula.ValorMensalContratado),
            matricula.ValorMensalCatalogo != matricula.ValorMensalContratado,
            matricula.CobraTaxaMatricula
                ? FormatarMoeda(matricula.ValorTaxaMatricula!.Value)
                : "Isenta",
            matricula.Responsaveis.Select(MapearResponsavel).ToArray(),
            matricula.GradeAtual
                .OrderBy(item => item.DiaSemana)
                .ThenBy(item => item.HoraInicio)
                .Select(MapearGrade)
                .ToArray(),
            matricula.HistoricoGrade
                .OrderByDescending(item => item.VigenciaFim)
                .ThenBy(item => item.DiaSemana)
                .ThenBy(item => item.HoraInicio)
                .Select(MapearGrade)
                .ToArray())
    };

    public static AlterarGradeMatriculaViewModel AlterarGrade(
        ContextoMatriculasResumo contexto,
        MatriculaDetalhe matricula,
        IReadOnlyList<HorarioElegivelMatriculaResumo> horarios,
        DateOnly dataInicio) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = false,
        MatriculaId = matricula.MatriculaId,
        NomeAluno = matricula.NomeAluno,
        DataInicioMatricula = FormatarData(matricula.DataInicio),
        DataFimPrevista = FormatarData(matricula.DataFimPrevista),
        FrequenciaSemanal = FormatarFrequencia(matricula.FrequenciaSemanal),
        FrequenciaSemanalNumero = matricula.FrequenciaSemanal,
        DataInicioTexto = FormatarData(dataInicio),
        Horarios = horarios.Select(NovaMatriculaViewModelMapper.MapearHorario).ToArray(),
        HorariosAtuais = matricula.GradeAtual
            .Where(item => item.VigenciaFim is null)
            .Select(item => item.TurmaHorarioId)
            .ToHashSet(),
        DataMinimaGrade = FormatarData(
            matricula.GradeAtual
                .Where(item => item.VigenciaFim is null)
                .Select(item => item.VigenciaInicio)
                .DefaultIfEmpty(matricula.DataInicio)
                .Max().AddDays(1))
    };

    public static FinalizarMatriculaViewModel Finalizar(
        ContextoMatriculasResumo contexto,
        MatriculaDetalhe matricula,
        bool cancelar) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = false,
        MatriculaId = matricula.MatriculaId,
        NomeAluno = matricula.NomeAluno,
        DataInicioMatricula = FormatarData(matricula.DataInicio),
        DataFimPrevista = FormatarData(matricula.DataFimPrevista),
        Cancelar = cancelar,
        DataFinalTexto = FormatarData(matricula.DataFimPrevista),
        DataMinimaEncerramento = FormatarData(
            matricula.GradeAtual
                .Where(item => item.VigenciaFim is null)
                .Select(item => item.VigenciaInicio)
                .DefaultIfEmpty(matricula.DataInicio)
                .Max())
    };

    private static ResponsavelMatriculaViewModel MapearResponsavel(
        ResponsavelMatriculaResumo responsavel) => new(
            responsavel.NomeCompleto,
            NomeRelacao(responsavel.TipoRelacao, responsavel.DescricaoRelacao),
            InformadoOuPadrao(responsavel.Telefone),
            InformadoOuPadrao(responsavel.Email),
            responsavel.PrincipalContato,
            responsavel.ResponsavelFinanceiro,
            responsavel.VinculoAtivo && responsavel.ResponsavelAtivo);

    private static GradeMatriculaViewModel MapearGrade(GradeMatriculaResumo grade) => new(
        NomeDia(grade.DiaSemana),
        $"{grade.HoraInicio:HH\\:mm} às {grade.HoraFim:HH\\:mm}",
        grade.Turma,
        grade.ProfessorSnapshot,
        grade.VigenciaFim is { } fim
            ? $"{FormatarData(grade.VigenciaInicio)} a {FormatarData(fim)}"
            : $"Desde {FormatarData(grade.VigenciaInicio)}");

    private static string NomeStatus(StatusMatricula status) => status switch
    {
        StatusMatricula.Ativa => "Ativa",
        StatusMatricula.Encerrada => "Encerrada",
        StatusMatricula.Cancelada => "Cancelada",
        _ => status.ToString()
    };

    private static string NomeRelacao(
        TipoRelacaoResponsavel tipo, string? descricao) => tipo switch
    {
        TipoRelacaoResponsavel.Pai => "Pai",
        TipoRelacaoResponsavel.Mae => "Mãe",
        TipoRelacaoResponsavel.ResponsavelLegal => "Responsável legal",
        TipoRelacaoResponsavel.Tutor => "Tutor",
        TipoRelacaoResponsavel.Avo => "Avó/Avô",
        TipoRelacaoResponsavel.Outro => descricao ?? "Outro",
        _ => tipo.ToString()
    };

    private static string NomeDia(DiaSemana dia) => dia switch
    {
        DiaSemana.Segunda => "Segunda-feira",
        DiaSemana.Terca => "Terça-feira",
        DiaSemana.Quarta => "Quarta-feira",
        DiaSemana.Quinta => "Quinta-feira",
        DiaSemana.Sexta => "Sexta-feira",
        DiaSemana.Sabado => "Sábado",
        _ => "Domingo"
    };

    private static string FormatarFrequencia(int frequencia) =>
        $"{frequencia}x por semana";

    private static string FormatarData(DateOnly data) => data.ToString("dd/MM/yyyy");

    private static string FormatarMoeda(decimal valor) =>
        valor.ToString("C", CulturaPtBr);

    private static string FormatarCpfMascarado(string? cpf) => cpf is { Length: 11 }
        ? $"***.***.***-{cpf[^2..]}"
        : "Não informado";

    private static string InformadoOuPadrao(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? "Não informado" : valor;
}

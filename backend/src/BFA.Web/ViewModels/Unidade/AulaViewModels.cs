using BFA.Application.Aulas;
using BFA.Domain.Aulas;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace BFA.Web.ViewModels.Unidade;

public sealed class AulasListaViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required DateOnly DataInicio { get; init; }
    public required DateOnly DataFim { get; init; }
    public IReadOnlyList<AulaResumoViewModel> Aulas { get; init; } = [];
}

public sealed record AulaResumoViewModel(
    Guid AulaId,
    string TurmaNome,
    string ProfessorNome,
    string Data,
    string HoraInicio,
    string HoraFim,
    string Status,
    int Capacidade,
    int Inscritos);

public sealed class AulaDetalheViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required AulaDetalheItemViewModel Aula { get; init; }
}

public sealed class AulaDetalheItemViewModel
{
    public required Guid AulaId { get; init; }
    public required Guid TurmaId { get; init; }
    public required string TurmaNome { get; init; }
    public required string ProfessorNome { get; init; }
    public required string Data { get; init; }
    public required string HoraInicio { get; init; }
    public required string HoraFim { get; init; }
    public required string Status { get; init; }
    public required int Capacidade { get; init; }
    public string? Observacoes { get; init; }
    public IReadOnlyList<AlunoPresencaViewModel> Alunos { get; init; } = [];
    public bool PodeConcluir => Status == "Programada";
    public bool PodeCancelar => Status == "Programada";
    public bool PodeChamada => Status == "Programada" || Status == "Concluida";
}

public sealed record AlunoPresencaViewModel(
    Guid AlunoId,
    string NomeCompleto,
    string? Status,
    string? ChegouAs,
    string? SaiuAs);

public sealed class AulaFormViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }

    [Required(ErrorMessage = "Selecione o horario da turma.")]
    [Display(Name = "Horario da Turma")]
    public Guid? TurmaHorarioId { get; set; }

    [Required(ErrorMessage = "Informe a data da aula.")]
    [Display(Name = "Data")]
    public DateOnly? Data { get; set; }

    [Required(ErrorMessage = "Informe a hora de inicio.")]
    [Display(Name = "Hora Inicio")]
    public TimeOnly? HoraInicio { get; set; }

    [Required(ErrorMessage = "Informe a hora de fim.")]
    [Display(Name = "Hora Fim")]
    public TimeOnly? HoraFim { get; set; }

    [Display(Name = "Observacoes")]
    [StringLength(500)]
    public string? Observacoes { get; set; }
}

public sealed class AulaEdicaoFormViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required Guid AulaId { get; init; }
    public required string StatusAtual { get; init; }

    [Display(Name = "Status")]
    public StatusAula? Status { get; set; }

    [Display(Name = "Observacoes")]
    [StringLength(500)]
    public string? Observacoes { get; set; }
}

public sealed class AulaChamadaViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required AulaResumoChamadaViewModel Aula { get; init; }
    public IReadOnlyList<AlunoChamadaViewModel> Alunos { get; init; } = [];
}

public sealed record AulaResumoChamadaViewModel(
    Guid AulaId,
    string TurmaNome,
    string ProfessorNome,
    string Data,
    string HoraInicio,
    string HoraFim,
    string Status);

public sealed class AlunoChamadaViewModel
{
    public Guid AlunoId { get; init; }
    public string NomeCompleto { get; init; } = string.Empty;
    public StatusPresenca Status { get; set; } = StatusPresenca.Ausente;
}

public sealed class ChamadaFormViewModel
{
    public List<RegistroPresencaFormViewModel> Registros { get; set; } = [];
}

public sealed class RegistroPresencaFormViewModel
{
    public Guid? AlunoId { get; set; }
    public StatusPresenca Status { get; set; } = StatusPresenca.Ausente;
}

public sealed class AulaFrequenciaViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required DateOnly DataInicio { get; init; }
    public required DateOnly DataFim { get; init; }
    public Guid? TurmaId { get; init; }
    public IReadOnlyList<FrequenciaAlunoViewModel> Alunos { get; init; } = [];
}

public sealed record FrequenciaAlunoViewModel(
    Guid AlunoId,
    string NomeCompleto,
    int TotalAulas,
    int Presentes,
    int Ausentes,
    int Justificados,
    int Isentos,
    string PercentualFrequencia);

internal static class AulasViewModelMapper
{
    private static readonly System.Globalization.CultureInfo CulturaPtBr =
        new("pt-BR");

    public static AulasListaViewModel MapearLista(
        ContextoAulasResumo contexto,
        IReadOnlyList<AulaResumo> itens,
        DateOnly dataInicio,
        DateOnly dataFim) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = false,
        PodeGerenciar = contexto.PodeGerenciar,
        DataInicio = dataInicio,
        DataFim = dataFim,
        Aulas = itens.Select(MapearResumo).ToArray()
    };

    public static AulaDetalheViewModel MapearDetalhe(
        ContextoAulasResumo contexto,
        AulaDetalhe detalhe) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = false,
        PodeGerenciar = contexto.PodeGerenciar,
        Aula = new AulaDetalheItemViewModel
        {
            AulaId = detalhe.AulaId,
            TurmaId = detalhe.TurmaId,
            TurmaNome = detalhe.TurmaNome,
            ProfessorNome = detalhe.ProfessorNome,
            Data = detalhe.Data.ToString("dd/MM/yyyy"),
            HoraInicio = detalhe.HoraInicio.ToString("HH:mm"),
            HoraFim = detalhe.HoraFim.ToString("HH:mm"),
            Status = MapearStatus(detalhe.Status),
            Capacidade = detalhe.Capacidade,
            Observacoes = detalhe.Observacoes,
            Alunos = detalhe.Alunos.Select(MapearPresenca).ToArray()
        }
    };

    public static AulaFormViewModel MapearFormularioCriacao(
        ContextoAulasResumo contexto) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = false,
        PodeGerenciar = contexto.PodeGerenciar
    };

    public static AulaFormViewModel ReconstituirFormularioCriacao(
        ContextoAulasResumo contexto,
        AulaFormViewModel model) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = false,
        PodeGerenciar = contexto.PodeGerenciar,
        TurmaHorarioId = model.TurmaHorarioId,
        Data = model.Data,
        HoraInicio = model.HoraInicio,
        HoraFim = model.HoraFim,
        Observacoes = model.Observacoes
    };

    public static AulaEdicaoFormViewModel MapearFormularioEdicao(
        ContextoAulasResumo contexto,
        AulaDetalhe detalhe) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = false,
        PodeGerenciar = contexto.PodeGerenciar,
        AulaId = detalhe.AulaId,
        StatusAtual = MapearStatus(detalhe.Status),
        Observacoes = detalhe.Observacoes
    };

    public static AulaChamadaViewModel MapearChamada(
        ContextoAulasResumo contexto,
        AulaDetalhe detalhe,
        IReadOnlyList<AlunoPresencaResumo> alunos) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = false,
        PodeGerenciar = contexto.PodeGerenciar,
        Aula = new AulaResumoChamadaViewModel(
            detalhe.AulaId,
            detalhe.TurmaNome,
            detalhe.ProfessorNome,
            detalhe.Data.ToString("dd/MM/yyyy"),
            detalhe.HoraInicio.ToString("HH:mm"),
            detalhe.HoraFim.ToString("HH:mm"),
            MapearStatus(detalhe.Status)),
        Alunos = alunos.Select(a => new AlunoChamadaViewModel
        {
            AlunoId = a.AlunoId,
            NomeCompleto = a.NomeCompleto,
            Status = a.Status ?? StatusPresenca.Ausente
        }).ToArray()
    };

    public static AulaFrequenciaViewModel MapearFrequencia(
        ContextoAulasResumo contexto,
        IReadOnlyList<FrequenciaAlunoResumo> itens,
        Guid? turmaId,
        DateOnly dataInicio,
        DateOnly dataFim) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = false,
        PodeGerenciar = contexto.PodeGerenciar,
        TurmaId = turmaId,
        DataInicio = dataInicio,
        DataFim = dataFim,
        Alunos = itens.Select(f => new FrequenciaAlunoViewModel(
            f.AlunoId,
            f.NomeCompleto,
            f.TotalAulas,
            f.Presentes,
            f.Ausentes,
            f.Justificados,
            f.Isentos,
            f.PercentualFrequencia.ToString("N1", CulturaPtBr) + "%")).ToArray()
    };

    private static AulaResumoViewModel MapearResumo(AulaResumo item) => new(
        item.AulaId,
        item.TurmaNome,
        item.ProfessorNome,
        item.Data.ToString("dd/MM/yyyy"),
        item.HoraInicio.ToString("HH:mm"),
        item.HoraFim.ToString("HH:mm"),
        MapearStatus(item.Status),
        item.Capacidade,
        item.Inscritos);

    private static AlunoPresencaViewModel MapearPresenca(AlunoPresencaResumo item) => new(
        item.AlunoId,
        item.NomeCompleto,
        item.Status?.ToString(),
        item.ChegouAs?.ToString("HH:mm"),
        item.SaiuAs?.ToString("HH:mm"));

    private static string MapearStatus(StatusAula status) => status switch
    {
        StatusAula.Programada => "Programada",
        StatusAula.Concluida => "Concluida",
        StatusAula.Cancelada => "Cancelada",
        _ => status.ToString()
    };
}

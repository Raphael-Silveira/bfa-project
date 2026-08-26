using BFA.Web.ViewModels.Unidade;
using BFA.Application.Professores.Turmas;
using BFA.Application.Unidades.Turmas;
using BFA.Domain.Turmas;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BFA.Web.ViewModels.Professor;

public interface IProfessorContextoViewModel
{
    Guid UnidadeId { get; }
    string NomeUnidade { get; }
    bool PodeTrocarUnidade { get; }
}

public sealed record ProfessorInicioViewModel(
    Guid UnidadeId,
    string NomeUnidade,
    bool PodeTrocarUnidade,
    string? PrimeiroNome,
    int QuantidadeTurmasAtivas) : IProfessorContextoViewModel;

public sealed record MinhasTurmasProfessorViewModel(
    Guid UnidadeId,
    string NomeUnidade,
    bool PodeTrocarUnidade,
    IReadOnlyList<TurmaProfessorResumo> Turmas) : IProfessorContextoViewModel;

public sealed record TurmaProfessorDetalheViewModel(
    Guid UnidadeId,
    string NomeUnidade,
    bool PodeTrocarUnidade,
    TurmaProfessorDetalhe Turma) : IProfessorContextoViewModel;

public sealed record ProfessorSelecaoUnidadeViewModel(
    string? PrimeiroNomeUsuario,
    IReadOnlyList<UnidadeSelecaoItemViewModel> Unidades);

public sealed class AjustarHorariosTurmaViewModel : IProfessorContextoViewModel,
    IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public Guid TurmaId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }
    public string NomeTurma { get; set; } = string.Empty;
    public string NomeProfessor { get; set; } = string.Empty;
    public bool AreaProfessor { get; set; }
    public IReadOnlyList<TurmaHorarioResumo> HorariosAtuais { get; set; } = [];

    [Display(Name = "Data de início da nova programação")]
    [Required(ErrorMessage = "Informe a data de início da nova programação.")]
    public string NovaVigenciaInicioTexto { get; set; } = string.Empty;

    public List<NovoHorarioTurmaFormViewModel> Horarios { get; set; } = [new()];

    public bool TryCriarSolicitacao(out AjustarHorariosTurmaSolicitacao? solicitacao)
    {
        solicitacao = null;
        if (!DateOnly.TryParseExact(NovaVigenciaInicioTexto, "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var vigencia)
            || Horarios.Count == 0)
            return false;
        var horarios = new List<NovoHorarioTurmaSolicitacao>(Horarios.Count);
        foreach (var item in Horarios)
        {
            if (!item.TryCriar(out var horario) || horario is null) return false;
            horarios.Add(horario);
        }
        solicitacao = new(vigencia, horarios);
        return true;
    }
}

public sealed class NovoHorarioTurmaFormViewModel
{
    [Display(Name = "Dia da semana")]
    [Required(ErrorMessage = "Selecione o dia da semana.")]
    public DiaSemana? DiaSemana { get; set; }

    [Display(Name = "Hora inicial")]
    [Required(ErrorMessage = "Informe a hora inicial.")]
    public string HoraInicio { get; set; } = string.Empty;

    [Display(Name = "Hora final")]
    [Required(ErrorMessage = "Informe a hora final.")]
    public string HoraFim { get; set; } = string.Empty;

    public bool TryCriar(out NovoHorarioTurmaSolicitacao? horario)
    {
        horario = null;
        if (DiaSemana is not { } dia || !Enum.IsDefined(dia)
            || !TimeOnly.TryParseExact(HoraInicio, "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio)
            || !TimeOnly.TryParseExact(HoraFim, "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var fim)
            || inicio >= fim)
            return false;
        horario = new(dia, inicio, fim);
        return true;
    }
}

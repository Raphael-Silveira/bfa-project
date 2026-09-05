using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BFA.Application.Unidades.Turmas;
using BFA.Domain.Turmas;

namespace BFA.Web.ViewModels.Unidade;

public sealed class TurmasUnidadeIndexViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required IReadOnlyList<TurmaResumo> Turmas { get; init; }
    public required int PaginaAtual { get; init; }
    public required int TamanhoPagina { get; init; }
    public required int TotalItens { get; init; }
}

public sealed class TurmaHorarioFormViewModel
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

    [Display(Name = "Início da vigência")]
    [Required(ErrorMessage = "Informe o início da vigência.")]
    public string VigenciaInicioTexto { get; set; } = string.Empty;

    public bool TryCriar(out TurmaHorarioSolicitacao? horario)
    {
        horario = null;
        if (DiaSemana is not { } dia || !Enum.IsDefined(dia)
            || !TimeOnly.TryParseExact(HoraInicio, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var inicio)
            || !TimeOnly.TryParseExact(HoraFim, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var fim)
            || !DateOnly.TryParseExact(VigenciaInicioTexto, "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var vigencia)
            || inicio >= fim)
        {
            return false;
        }

        horario = new(dia, inicio, fim, vigencia);
        return true;
    }
}

public sealed class TurmaNovaViewModel : IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }

    [Display(Name = "Nome da turma")]
    [Required(ErrorMessage = "Informe o nome da turma.")]
    [StringLength(150, ErrorMessage = "O nome deve possuir no máximo 150 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    [Display(Name = "Capacidade")]
    [Range(1, int.MaxValue, ErrorMessage = "A capacidade deve ser maior que zero.")]
    public int? Capacidade { get; set; }

    [Display(Name = "Professor responsável")]
    [Required(ErrorMessage = "Selecione o professor responsável.")]
    public Guid? ProfessorUnidadeId { get; set; }

    public List<TurmaHorarioFormViewModel> Horarios { get; set; } = [];
    public IReadOnlyList<ProfessorTurmaOpcao> Professores { get; set; } = [];

    public bool TryCriarSolicitacao(out CriarTurmaSolicitacao? solicitacao)
    {
        solicitacao = null;
        if (Capacidade is not > 0 || ProfessorUnidadeId is not { } professorId
            || Horarios.Count == 0)
        {
            return false;
        }

        var horarios = new List<TurmaHorarioSolicitacao>(Horarios.Count);
        foreach (var item in Horarios)
        {
            if (!item.TryCriar(out var horario) || horario is null) return false;
            horarios.Add(horario);
        }

        solicitacao = new(Nome, Capacidade.Value, professorId, horarios);
        return true;
    }
}

public sealed class TurmaEditarViewModel : IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public Guid TurmaId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }

    [Display(Name = "Nome da turma")]
    [Required(ErrorMessage = "Informe o nome da turma.")]
    [StringLength(150, ErrorMessage = "O nome deve possuir no máximo 150 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    [Display(Name = "Capacidade")]
    [Range(1, int.MaxValue, ErrorMessage = "A capacidade deve ser maior que zero.")]
    public int? Capacidade { get; set; }

    public string NomeProfessor { get; set; } = string.Empty;
    public IReadOnlyList<TurmaHorarioResumo> Horarios { get; set; } = [];
}

public sealed class TrocarProfessorTurmaViewModel : IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public Guid TurmaId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }
    public string NomeTurma { get; set; } = string.Empty;
    public string NomeProfessorAtual { get; set; } = string.Empty;
    public IReadOnlyList<TurmaHorarioResumo> HorariosAtuais { get; set; } = [];
    public IReadOnlyList<ProfessorTrocaOpcao> ProfessoresDisponiveis { get; set; } = [];

    [Display(Name = "Novo professor")]
    [Required(ErrorMessage = "Selecione o novo professor.")]
    public Guid? NovoProfessorUnidadeId { get; set; }

    [Display(Name = "Data da troca")]
    [Required(ErrorMessage = "Informe a data da troca.")]
    public string DataTrocaTexto { get; set; } = string.Empty;

    public bool TryCriarSolicitacao(out TrocarProfessorTurmaSolicitacao? solicitacao)
    {
        solicitacao = null;
        if (NovoProfessorUnidadeId is not { } professorId
            || !DateOnly.TryParseExact(DataTrocaTexto, "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            return false;
        solicitacao = new(professorId, data);
        return true;
    }
}

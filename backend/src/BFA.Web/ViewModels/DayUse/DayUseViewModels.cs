using System.ComponentModel.DataAnnotations;
using BFA.Application.DayUses;
using BFA.Web.ViewModels.Professor;
using BFA.Web.ViewModels.Unidade;

namespace BFA.Web.ViewModels.DayUse;

public sealed class DayUseFormViewModel
{
    public Guid UnidadeId { get; set; }
    public string TipoParticipante { get; set; } = "Aluno";
    public Guid? AlunoId { get; set; }
    public string? NomeAvulso { get; set; }
    public string? TelefoneAvulso { get; set; }
    public string? EmailAvulso { get; set; }
    [Required] public string DataUso { get; set; } = string.Empty;
    public decimal ValorSugerido { get; set; }
    [Range(0, double.MaxValue)] public decimal ValorCobrado { get; set; }
    public bool Pago { get; set; }
    public IReadOnlyList<DayUseAlunoResumo> Alunos { get; set; } = [];
}

public sealed class DayUseConfiguracaoViewModel : IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }
    public decimal? ValorSugerido { get; set; }
    public DayUseFormViewModel Formulario { get; set; } = new();
    public DayUsePagina Pagina { get; set; } = new([], 1, 1, 0);
    public string? DataInicial { get; set; }
    public string? DataFinal { get; set; }
    public string? Participante { get; set; }
}

public sealed class DayUseProfessorViewModel : IProfessorContextoViewModel
{
    public Guid UnidadeId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }
    public DayUseFormViewModel Formulario { get; set; } = new();
    public DayUsePagina Pagina { get; set; } = new([], 1, 1, 0);
    public string? DataInicial { get; set; }
    public string? DataFinal { get; set; }
    public string? Participante { get; set; }
}

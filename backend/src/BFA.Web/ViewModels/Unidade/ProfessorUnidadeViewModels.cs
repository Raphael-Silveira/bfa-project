using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BFA.Application.Unidades.Professores;
using BFA.Domain.Professores;

namespace BFA.Web.ViewModels.Unidade;

public sealed class ProfessoresUnidadeIndexViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required IReadOnlyList<ProfessorUnidadeResumo> Professores { get; init; }
    public required FiltroProfessoresUnidade Filtro { get; init; }
}

public sealed record ProfessorUnidadeAcoesViewModel(
    Guid UnidadeId,
    Guid ProfessorId,
    bool VinculoAtivo);

public abstract class ProfessorRemuneracaoInicialViewModel
{
    [Required(ErrorMessage = "Selecione a modalidade.")]
    [Display(Name = "Modalidade")]
    public ModalidadeRemuneracaoProfessor? Modalidade { get; set; }

    [Required(ErrorMessage = "Informe o valor.")]
    [Display(Name = "Valor")]
    public string ValorTexto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o início da vigência.")]
    [Display(Name = "Início da vigência")]
    public string VigenciaInicioTexto { get; set; } = string.Empty;

    public DateOnly? VigenciaInicioMinima { get; set; }

    [StringLength(ProfessorRemuneracao.ObservacaoTamanhoMaximo)]
    [Display(Name = "Observação")]
    public string? Observacao { get; set; }

    public bool TryObterRemuneracao(
        out ModalidadeRemuneracaoProfessor modalidade,
        out decimal valor,
        out DateOnly vigenciaInicio)
    {
        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        var valorValido = decimal.TryParse(
            ValorTexto, NumberStyles.Number, cultura, out valor);
        var dataValida = DateOnly.TryParseExact(
            VigenciaInicioTexto, "dd/MM/yyyy", cultura, DateTimeStyles.None, out vigenciaInicio);
        modalidade = Modalidade ?? default;
        return valorValido && valor >= 0 && dataValida && Modalidade is not null;
    }
}

public sealed class ProfessorUnidadeNovoViewModel
    : ProfessorRemuneracaoInicialViewModel, IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }

    [Required(ErrorMessage = "Informe o nome completo.")]
    [StringLength(Professor.NomeCompletoTamanhoMaximo)]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [StringLength(14)]
    [Display(Name = "CPF")]
    public string? Cpf { get; set; }

    [StringLength(Professor.TelefoneTamanhoMaximo)]
    [Display(Name = "Telefone")]
    public string? Telefone { get; set; }

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(Professor.EmailTamanhoMaximo)]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    public bool CpfJaCadastradoNaRede { get; set; }

    public bool TryCriarSolicitacao(out CriarProfessorUnidadeSolicitacao? solicitacao)
    {
        if (!TryObterRemuneracao(out var modalidade, out var valor, out var data))
        {
            solicitacao = null;
            return false;
        }

        solicitacao = new(
            NomeCompleto, Cpf, Telefone, Email, modalidade,
            valor, data, Observacao);
        return true;
    }
}

public sealed class ProfessorUnidadeVincularViewModel
    : ProfessorRemuneracaoInicialViewModel, IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }
    public string? Termo { get; set; }
    public Guid? ProfessorId { get; set; }
    public IReadOnlyList<ProfessorExistenteResumo> Resultados { get; set; } = [];
    public ProfessorExistenteResumo? ProfessorSelecionado { get; set; }

    public bool TryCriarSolicitacao(out VincularProfessorExistenteSolicitacao? solicitacao)
    {
        if (ProfessorId is not { } professorId
            || !TryObterRemuneracao(out var modalidade, out var valor, out var data))
        {
            solicitacao = null;
            return false;
        }

        solicitacao = new(professorId, modalidade, valor, data, Observacao);
        return true;
    }
}

public sealed class ProfessorUnidadeEditarViewModel : IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public Guid ProfessorId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }

    [Required(ErrorMessage = "Informe o nome completo.")]
    [StringLength(Professor.NomeCompletoTamanhoMaximo)]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [StringLength(14)]
    public string? Cpf { get; set; }

    [StringLength(Professor.TelefoneTamanhoMaximo)]
    public string? Telefone { get; set; }

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(Professor.EmailTamanhoMaximo)]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }
}

public sealed class ProfessorUnidadeEncerrarViewModel : IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public Guid ProfessorId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }
    public string NomeProfessor { get; set; } = string.Empty;
    public bool VinculoAtivo { get; set; }
    public ModalidadeRemuneracaoProfessor? ModalidadeAtual { get; set; }
    public decimal? ValorAtual { get; set; }
    public DateOnly? VigenciaInicioAtual { get; set; }

    [Required(ErrorMessage = "Informe a data de encerramento.")]
    [Display(Name = "Data de encerramento")]
    public string DataEncerramentoTexto { get; set; } = string.Empty;

    public bool TryObterData(out DateOnly data) => DateOnly.TryParseExact(
        DataEncerramentoTexto,
        "dd/MM/yyyy",
        CultureInfo.GetCultureInfo("pt-BR"),
        DateTimeStyles.None,
        out data);
}

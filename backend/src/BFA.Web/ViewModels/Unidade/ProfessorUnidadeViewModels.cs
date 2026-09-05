using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BFA.Application.Unidades.Professores;
using BFA.Domain.Professores;
using ProfessorEntidade = BFA.Domain.Professores.Professor;

namespace BFA.Web.ViewModels.Unidade;

public sealed class ProfessoresUnidadeIndexViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required IReadOnlyList<ProfessorUnidadeResumo> Professores { get; init; }
    public required FiltroProfessoresUnidade Filtro { get; init; }
    public string? TermoBusca { get; init; }
    public int PaginaAtual { get; init; } = 1;
    public int TamanhoPagina { get; init; } = 10;
    public int TotalItens { get; init; }
    public int TotalAtivos { get; init; }
    public int TotalEncerrados { get; init; }
}

public sealed record ProfessorUnidadeAcoesViewModel(
    Guid UnidadeId,
    Guid ProfessorId,
    bool VinculoAtivo,
    Guid? UsuarioId,
    string? NomeUsuario,
    bool AcessoProfessorAtivo);

public sealed class ProfessorAcessoViewModel : IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public Guid ProfessorId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }
    public string NomeProfessor { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool UsuarioExistente { get; set; }

    [Required(ErrorMessage = "Informe o nome de usuário.")]
    [StringLength(256)]
    [Display(Name = "Nome de usuário")]
    public string NomeUsuario { get; set; } = string.Empty;
}

public sealed record ProfessorAcessoConcedidoViewModel(
    Guid UnidadeId,
    string NomeUnidade,
    bool PodeTrocarUnidade,
    string NomeProfessor,
    string NomeUsuario,
    string? LinkPrimeiroAcesso) : IUnidadeContextoViewModel
{
    public Guid OrganizacaoId => Guid.Empty;
}

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
    [StringLength(ProfessorEntidade.NomeCompletoTamanhoMaximo)]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [StringLength(14)]
    [Display(Name = "CPF")]
    public string? Cpf { get; set; }

    [StringLength(ProfessorEntidade.TelefoneTamanhoMaximo)]
    [Display(Name = "Telefone")]
    public string? Telefone { get; set; }

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(ProfessorEntidade.EmailTamanhoMaximo)]
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
    [StringLength(ProfessorEntidade.NomeCompletoTamanhoMaximo)]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [StringLength(14)]
    public string? Cpf { get; set; }

    [StringLength(ProfessorEntidade.TelefoneTamanhoMaximo)]
    public string? Telefone { get; set; }

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(ProfessorEntidade.EmailTamanhoMaximo)]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }
}

public sealed class ProfessorRemuneracaoAlterarViewModel
    : ProfessorRemuneracaoInicialViewModel, IUnidadeContextoViewModel
{
    public Guid OrganizacaoId { get; set; }
    public Guid UnidadeId { get; set; }
    public Guid ProfessorId { get; set; }
    public string NomeUnidade { get; set; } = string.Empty;
    public bool PodeTrocarUnidade { get; set; }
    public string NomeProfessor { get; set; } = string.Empty;
    public bool VinculoAtivo { get; set; }
    public ProfessorRemuneracaoResumo? RemuneracaoAtual { get; set; }
    public IReadOnlyList<ProfessorRemuneracaoResumo> Historico { get; set; } = [];

    public bool TryCriarSolicitacao(out AlterarProfessorRemuneracaoSolicitacao? solicitacao)
    {
        if (!TryObterRemuneracao(out var modalidade, out var valor, out var data))
        {
            solicitacao = null;
            return false;
        }

        solicitacao = new(modalidade, valor, data, Observacao);
        return true;
    }
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

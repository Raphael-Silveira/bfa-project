using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BFA.Application.Matriculas;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Turmas;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BFA.Web.ViewModels.Unidade;

public sealed class NovaMatriculaViewModel : IUnidadeContextoViewModel
{
    [BindNever, ValidateNever]
    public Guid OrganizacaoId { get; set; }

    [BindNever, ValidateNever]
    public Guid UnidadeId { get; set; }

    [BindNever, ValidateNever]
    public string NomeUnidade { get; set; } = string.Empty;

    [BindNever, ValidateNever]
    public bool PodeTrocarUnidade { get; set; }
    public int PassoInicial { get; set; } = 1;

    public string AlunoModo { get; set; } = "existente";
    public Guid? AlunoId { get; set; }
    public NovoAlunoMatriculaInputModel NovoAluno { get; set; } = new();
    public List<NovoResponsavelMatriculaInputModel> Responsaveis { get; set; } = [];

    [Display(Name = "Data de início da matrícula")]
    public string DataInicioTexto { get; set; } = string.Empty;

    public Guid PlanoVersaoId { get; set; }

    [Display(Name = "Mensalidade contratada")]
    public string ValorMensalContratadoTexto { get; set; } = string.Empty;

    [Display(Name = "Cobrar taxa de matrícula")]
    public bool CobraTaxaMatricula { get; set; }

    [Display(Name = "Valor da taxa de matrícula")]
    public string? ValorTaxaMatriculaTexto { get; set; }

    public List<Guid> TurmaHorarioIds { get; set; } = [];

    [BindNever, ValidateNever]
    public IReadOnlyList<AlunoMatriculaOpcaoViewModel> Alunos { get; set; } = [];

    [BindNever, ValidateNever]
    public IReadOnlyList<PlanoMatriculaOpcaoViewModel> Planos { get; set; } = [];

    [BindNever, ValidateNever]
    public IReadOnlyList<HorarioMatriculaOpcaoViewModel> Horarios { get; set; } = [];
}

public sealed class NovoAlunoMatriculaInputModel
{
    [Display(Name = "Nome completo")]
    public string? NomeCompleto { get; set; }

    [Display(Name = "Data de nascimento")]
    public string? DataNascimentoTexto { get; set; }

    [Display(Name = "CPF")]
    public string? Cpf { get; set; }

    [Display(Name = "Telefone")]
    public string? Telefone { get; set; }

    [Display(Name = "E-mail")]
    public string? Email { get; set; }
}

public sealed class NovoResponsavelMatriculaInputModel
{
    [Display(Name = "Nome completo")]
    public string? NomeCompleto { get; set; }

    [Display(Name = "CPF")]
    public string? Cpf { get; set; }

    [Display(Name = "Telefone")]
    public string? Telefone { get; set; }

    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [Display(Name = "Tipo de relação")]
    public TipoRelacaoResponsavel? TipoRelacao { get; set; }

    [Display(Name = "Descrição da relação")]
    public string? DescricaoRelacao { get; set; }

    [Display(Name = "Principal contato")]
    public bool PrincipalContato { get; set; }

    [Display(Name = "Responsável financeiro")]
    public bool ResponsavelFinanceiro { get; set; }
}

public sealed record AlunoMatriculaOpcaoViewModel(
    Guid AlunoId,
    string NomeCompleto,
    string DataNascimento,
    string DataNascimentoIso,
    bool PossuiMatriculaAtiva,
    IReadOnlyList<ResponsavelExistenteMatriculaViewModel> Responsaveis);

public sealed record ResponsavelExistenteMatriculaViewModel(
    string NomeCompleto,
    string Relacao,
    string Contato,
    bool PrincipalContato,
    bool ResponsavelFinanceiro);

public sealed record PlanoMatriculaOpcaoViewModel(
    Guid PlanoVersaoId,
    string Nome,
    string Frequencia,
    int FrequenciaSemanal,
    string Duracao,
    int DuracaoMeses,
    string ValorMensal,
    string ValorMensalInput,
    bool CobraMatricula,
    string ValorMatricula,
    string ValorMatriculaInput,
    string Escopo);

public sealed record HorarioMatriculaOpcaoViewModel(
    Guid TurmaHorarioId,
    string DiaSemana,
    int DiaSemanaOrdem,
    string Horario,
    string HoraInicio,
    string HoraFim,
    string NomeTurma,
    string Professor,
    int Capacidade,
    int Ocupacao,
    int VagasDisponiveis,
    bool Lotado);

internal sealed record ResultadoMapeamentoNovaMatricula(
    CriarMatriculaSolicitacao? Solicitacao,
    IReadOnlyList<(string Campo, string Mensagem)> Erros);

internal static class NovaMatriculaViewModelMapper
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static void PreencherOpcoes(
        NovaMatriculaViewModel model,
        ContextoMatriculasResumo contexto,
        bool podeTrocarUnidade,
        IReadOnlyList<AlunoRelacionadoUnidadeResumo> alunos,
        IReadOnlyList<PlanoElegivelMatriculaResumo> planos,
        IReadOnlyList<HorarioElegivelMatriculaResumo> horarios)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.NomeUnidade = contexto.NomeUnidade;
        model.PodeTrocarUnidade = podeTrocarUnidade;
        model.Alunos = alunos.Select(MapearAluno).ToArray();
        model.Planos = planos.Select(MapearPlano).ToArray();
        model.Horarios = horarios.Select(MapearHorario).ToArray();
    }

    public static ResultadoMapeamentoNovaMatricula MapearSolicitacao(
        NovaMatriculaViewModel model,
        DateOnly dataCivilAtual)
    {
        var erros = new List<(string Campo, string Mensagem)>();
        var alunoExistente = string.Equals(
            model.AlunoModo, "existente", StringComparison.OrdinalIgnoreCase);
        var alunoNovo = string.Equals(
            model.AlunoModo, "novo", StringComparison.OrdinalIgnoreCase);
        if (!alunoExistente && !alunoNovo)
            erros.Add((nameof(model.AlunoModo), "Escolha entre aluno existente e novo aluno."));

        Guid? alunoId = null;
        NovoAlunoMatriculaSolicitacao? novoAluno = null;
        if (alunoExistente)
        {
            if (model.AlunoId is not { } id || id == Guid.Empty)
                erros.Add((nameof(model.AlunoId), "Selecione um aluno da unidade."));
            else
                alunoId = id;
        }
        else if (alunoNovo)
        {
            var nome = Normalizar(model.NovoAluno.NomeCompleto);
            if (nome is null)
                erros.Add(("NovoAluno.NomeCompleto", "Informe o nome completo do aluno."));
            var nascimento = ParseData(model.NovoAluno.DataNascimentoTexto);
            if (nascimento is null)
                erros.Add(("NovoAluno.DataNascimentoTexto", "Informe uma data de nascimento válida."));
            else if (nascimento > dataCivilAtual)
                erros.Add(("NovoAluno.DataNascimentoTexto", "A data de nascimento não pode estar no futuro."));

            if (nome is not null && nascimento is not null && nascimento <= dataCivilAtual)
                novoAluno = new(
                    nome,
                    nascimento.Value,
                    NormalizarCpf(model.NovoAluno.Cpf),
                    Normalizar(model.NovoAluno.Telefone),
                    Normalizar(model.NovoAluno.Email));
        }

        var responsaveis = new List<NovoResponsavelMatriculaSolicitacao>();
        for (var indice = 0; indice < model.Responsaveis.Count; indice++)
        {
            var item = model.Responsaveis[indice];
            var prefixo = $"Responsaveis[{indice}]";
            var nome = Normalizar(item.NomeCompleto);
            var telefone = Normalizar(item.Telefone);
            var email = Normalizar(item.Email);
            if (nome is null)
                erros.Add(($"{prefixo}.NomeCompleto", "Informe o nome completo do responsável."));
            if (telefone is null && email is null)
                erros.Add(($"{prefixo}.Telefone", "Informe telefone ou e-mail para contato."));
            if (item.TipoRelacao is null || !Enum.IsDefined(item.TipoRelacao.Value))
                erros.Add(($"{prefixo}.TipoRelacao", "Informe o tipo de relação."));
            if (item.TipoRelacao == TipoRelacaoResponsavel.Outro
                && Normalizar(item.DescricaoRelacao) is null)
                erros.Add(($"{prefixo}.DescricaoRelacao", "Descreva a relação quando escolher Outro."));

            if (nome is not null && (telefone is not null || email is not null)
                && item.TipoRelacao is { } tipo && Enum.IsDefined(tipo)
                && (tipo != TipoRelacaoResponsavel.Outro
                    || Normalizar(item.DescricaoRelacao) is not null))
                responsaveis.Add(new(
                    nome,
                    NormalizarCpf(item.Cpf),
                    telefone,
                    email,
                    tipo,
                    tipo == TipoRelacaoResponsavel.Outro
                        ? Normalizar(item.DescricaoRelacao)
                        : null,
                    item.PrincipalContato,
                    item.ResponsavelFinanceiro));
        }

        if (model.Responsaveis.Count(item => item.PrincipalContato) > 1)
            erros.Add((nameof(model.Responsaveis), "Marque somente um responsável como Principal contato."));

        var dataInicio = ParseData(model.DataInicioTexto);
        if (dataInicio is null)
            erros.Add((nameof(model.DataInicioTexto), "Informe uma data de início válida."));
        if (model.PlanoVersaoId == Guid.Empty)
            erros.Add((nameof(model.PlanoVersaoId), "Selecione um plano elegível."));
        var valorMensal = ParseMoeda(model.ValorMensalContratadoTexto);
        if (valorMensal is not > 0)
            erros.Add((nameof(model.ValorMensalContratadoTexto),
                "Informe uma mensalidade contratada maior que zero."));
        var valorTaxa = model.CobraTaxaMatricula
            ? ParseMoeda(model.ValorTaxaMatriculaTexto)
            : null;
        if (model.CobraTaxaMatricula && valorTaxa is not > 0)
            erros.Add((nameof(model.ValorTaxaMatriculaTexto),
                "Informe uma taxa de matrícula maior que zero ou marque a isenção."));
        if (model.TurmaHorarioIds.Count == 0)
            erros.Add((nameof(model.TurmaHorarioIds), "Selecione ao menos um horário para a Grade."));
        if (model.TurmaHorarioIds.Any(id => id == Guid.Empty)
            || model.TurmaHorarioIds.Distinct().Count() != model.TurmaHorarioIds.Count)
            erros.Add((nameof(model.TurmaHorarioIds), "A Grade contém horários repetidos ou inválidos."));

        if (erros.Count > 0 || dataInicio is null || valorMensal is null)
            return new(null, erros);

        return new(new(
            alunoId,
            novoAluno,
            responsaveis,
            model.PlanoVersaoId,
            dataInicio.Value,
            valorMensal.Value,
            model.CobraTaxaMatricula,
            model.CobraTaxaMatricula ? valorTaxa : null,
            model.TurmaHorarioIds.Distinct().ToArray()), erros);
    }

    public static DateOnly? ParseData(string? valor) =>
        DateOnly.TryParseExact(valor?.Trim(), "dd/MM/yyyy", CulturaPtBr,
            DateTimeStyles.None, out var data) ? data : null;

    public static decimal? ParseMoeda(string? valor) =>
        decimal.TryParse(valor?.Trim(), NumberStyles.Number, CulturaPtBr, out var numero)
            ? numero
            : null;

    public static string FormatarData(DateOnly data) => data.ToString("dd/MM/yyyy", CulturaPtBr);

    public static string FormatarMoedaInput(decimal valor) => valor.ToString("N2", CulturaPtBr);

    private static AlunoMatriculaOpcaoViewModel MapearAluno(
        AlunoRelacionadoUnidadeResumo aluno) => new(
        aluno.AlunoId,
        aluno.NomeCompleto,
        FormatarData(aluno.DataNascimento),
        aluno.DataNascimento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        aluno.PossuiMatriculaAtiva,
        aluno.Responsaveis.Select(item => new ResponsavelExistenteMatriculaViewModel(
            item.NomeCompleto,
            NomeRelacao(item.TipoRelacao, item.DescricaoRelacao),
            Contato(item.Telefone, item.Email),
            item.PrincipalContato,
            item.ResponsavelFinanceiro)).ToArray());

    public static PlanoMatriculaOpcaoViewModel MapearPlano(
        PlanoElegivelMatriculaResumo plano) => new(
        plano.PlanoVersaoId,
        plano.Nome,
        $"{plano.FrequenciaSemanal}x por semana",
        plano.FrequenciaSemanal,
        plano.DuracaoMeses == 1 ? "1 mês" : $"{plano.DuracaoMeses} meses",
        plano.DuracaoMeses,
        plano.ValorMensal.ToString("C", CulturaPtBr),
        FormatarMoedaInput(plano.ValorMensal),
        plano.CobraMatricula,
        plano.CobraMatricula && plano.ValorMatricula is { } taxa
            ? taxa.ToString("C", CulturaPtBr)
            : "Sem taxa de matrícula",
        plano.ValorMatricula is { } valor ? FormatarMoedaInput(valor) : string.Empty,
        plano.Escopo == EscopoPlanoMatricula.Rede ? "Plano da Rede" : "Plano local");

    public static HorarioMatriculaOpcaoViewModel MapearHorario(
        HorarioElegivelMatriculaResumo horario) => new(
        horario.TurmaHorarioId,
        NomeDia(horario.DiaSemana),
        (int)horario.DiaSemana,
        $"{horario.HoraInicio:HH\\:mm} – {horario.HoraFim:HH\\:mm}",
        horario.HoraInicio.ToString("HH\\:mm", CultureInfo.InvariantCulture),
        horario.HoraFim.ToString("HH\\:mm", CultureInfo.InvariantCulture),
        horario.NomeTurma,
        horario.Professor,
        horario.Capacidade,
        horario.Ocupacao,
        horario.VagasDisponiveis,
        horario.VagasDisponiveis <= 0);

    private static string NomeRelacao(
        TipoRelacaoResponsavel tipo, string? descricao) => tipo switch
    {
        TipoRelacaoResponsavel.Pai => "Pai",
        TipoRelacaoResponsavel.Mae => "Mãe",
        TipoRelacaoResponsavel.ResponsavelLegal => "Responsável legal",
        TipoRelacaoResponsavel.Tutor => "Tutor",
        TipoRelacaoResponsavel.Avo => "Avô/Avó",
        TipoRelacaoResponsavel.Outro => Normalizar(descricao) ?? "Outro",
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

    private static string Contato(string? telefone, string? email) =>
        string.Join(" · ", new[] { Normalizar(telefone), Normalizar(email) }
            .Where(item => item is not null));

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? NormalizarCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return null;
        var digitos = new string(cpf.Where(char.IsDigit).ToArray());
        return digitos.Length == Aluno.CpfTamanho ? digitos : cpf.Trim();
    }
}

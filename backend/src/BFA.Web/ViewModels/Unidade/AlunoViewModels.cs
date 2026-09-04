using BFA.Application.Alunos;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace BFA.Web.ViewModels.Unidade;

public sealed class AlunosListaViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public string? Texto { get; init; }
    public IReadOnlyList<AlunoListaItemViewModel> Alunos { get; init; } = [];
    public bool PossuiFiltros => Texto is not null;
}

public sealed record AlunoListaItemViewModel(
    Guid AlunoId,
    string NomeCompleto,
    string DataNascimento,
    int Idade,
    string? Contato,
    string SituacaoCadastro,
    string? MatriculaAtual,
    string? PlanoAtual,
    string? StatusMatricula,
    bool PossuiMatriculaAtiva);

public sealed class AlunoDetalheViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }
    public required bool PodeGerenciar { get; init; }
    public required AlunoDetalheItemViewModel Aluno { get; init; }
}

public sealed class AlunoDetalheItemViewModel
{
    public required Guid AlunoId { get; init; }
    public required string NomeCompleto { get; init; }
    public required string DataNascimento { get; init; }
    public required int Idade { get; init; }
    public string? CpfMascarado { get; init; }
    public string? Telefone { get; init; }
    public string? Email { get; init; }
    public required bool Ativo { get; init; }
    public IReadOnlyList<ResponsavelAlunoViewModel> Responsaveis { get; init; } = [];
    public MatriculaAtualViewModel? MatriculaAtiva { get; init; }
    public IReadOnlyList<MatriculaHistoricoViewModel> HistoricoMatriculas { get; init; } = [];
}

public sealed record ResponsavelAlunoViewModel(
    Guid ResponsavelId,
    string NomeCompleto,
    string? Telefone,
    string? Email,
    string TipoRelacao,
    string? DescricaoRelacao,
    bool PrincipalContato,
    bool ResponsavelFinanceiro,
    bool VinculoAtivo,
    bool ResponsavelAtivo);

public sealed record MatriculaAtualViewModel(
    Guid MatriculaId,
    string Plano,
    string Status,
    string DataInicio,
    string DataFimPrevista,
    int FrequenciaSemanal,
    decimal ValorMensalContratado);

public sealed record MatriculaHistoricoViewModel(
    Guid MatriculaId,
    string Plano,
    string Status,
    string DataInicio,
    string DataFimPrevista,
    string? DataFimReal);

internal static class AlunosViewModelMapper
{
    private static readonly System.Globalization.CultureInfo CulturaPtBr =
        new("pt-BR");

    public static AlunosListaViewModel MapearLista(
        ContextoAlunosResumo contexto,
        IReadOnlyList<AlunoListaItem> itens,
        string? texto,
        bool podeTrocar) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = podeTrocar,
        PodeGerenciar = contexto.PodeGerenciar,
        Texto = texto,
        Alunos = itens.Select(MapearListaItem).ToArray()
    };

    public static AlunoDetalheViewModel MapearDetalhe(
        ContextoAlunosResumo contexto,
        AlunoDetalhe detalhe,
        bool podeTrocar) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = podeTrocar,
        PodeGerenciar = contexto.PodeGerenciar,
        Aluno = MapearDetalheItem(detalhe)
    };

    private static AlunoListaItemViewModel MapearListaItem(AlunoListaItem item)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var idade = hoje.Year - item.DataNascimento.Year;
        if (hoje < item.DataNascimento.AddYears(idade))
            idade--;

        return new AlunoListaItemViewModel(
            item.AlunoId,
            item.NomeCompleto,
            item.DataNascimento.ToString("dd/MM/yyyy"),
            idade,
            FormatContact(item.Telefone, item.Email),
            item.Ativo ? "Ativo" : "Inativo",
            item.StatusMatricula switch
            {
                StatusMatricula.Ativa => "Ativa",
                StatusMatricula.Encerrada => "Encerrada",
                StatusMatricula.Cancelada => "Cancelada",
                _ => null
            },
            item.PlanoAtual,
            item.StatusMatricula switch
            {
                StatusMatricula.Ativa => "Ativa",
                _ => "Sem matrícula ativa"
            },
            item.StatusMatricula == StatusMatricula.Ativa);
    }

    private static AlunoDetalheItemViewModel MapearDetalheItem(AlunoDetalhe detalhe)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var idade = hoje.Year - detalhe.DataNascimento.Year;
        if (hoje < detalhe.DataNascimento.AddYears(idade))
            idade--;

        return new AlunoDetalheItemViewModel
        {
            AlunoId = detalhe.AlunoId,
            NomeCompleto = detalhe.NomeCompleto,
            DataNascimento = detalhe.DataNascimento.ToString("dd/MM/yyyy"),
            Idade = idade,
            CpfMascarado = FormatCpf(detalhe.Cpf),
            Telefone = detalhe.Telefone,
            Email = detalhe.Email,
            Ativo = detalhe.Ativo,
            Responsaveis = detalhe.Responsaveis.Select(r => new ResponsavelAlunoViewModel(
                r.ResponsavelId,
                r.NomeCompleto,
                r.Telefone,
                r.Email,
                FormatTipoRelacao(r.TipoRelacao, r.DescricaoRelacao),
                r.DescricaoRelacao,
                r.PrincipalContato,
                r.ResponsavelFinanceiro,
                r.VinculoAtivo,
                r.ResponsavelAtivo)).ToArray(),
            MatriculaAtiva = detalhe.MatriculaAtiva is { } ma
                ? new MatriculaAtualViewModel(
                    ma.MatriculaId,
                    $"{ma.Plano} {ma.NumeroVersao}ª versão",
                    "Ativa",
                    ma.DataInicio.ToString("dd/MM/yyyy"),
                    ma.DataFimPrevista.ToString("dd/MM/yyyy"),
                    ma.FrequenciaSemanal,
                    ma.ValorMensalContratado)
                : null,
            HistoricoMatriculas = detalhe.HistoricoMatriculas.Select(m => new MatriculaHistoricoViewModel(
                m.MatriculaId,
                $"{m.Plano} {m.NumeroVersao}ª versão",
                m.Status switch
                {
                    StatusMatricula.Encerrada => "Encerrada",
                    StatusMatricula.Cancelada => "Cancelada",
                    _ => m.Status.ToString()
                },
                m.DataInicio.ToString("dd/MM/yyyy"),
                m.DataFimPrevista.ToString("dd/MM/yyyy"),
                m.DataFimReal?.ToString("dd/MM/yyyy"))).ToArray()
        };
    }

    private static string FormatContact(string? telefone, string? email)
    {
        if (!string.IsNullOrWhiteSpace(telefone) && !string.IsNullOrWhiteSpace(email))
            return $"{telefone} · {email}";
        if (!string.IsNullOrWhiteSpace(telefone))
            return telefone;
        if (!string.IsNullOrWhiteSpace(email))
            return email;
        return "Não informado";
    }

    private static string FormatCpf(string? cpf) =>
        cpf is { Length: 11 }
            ? $"***.***.***-{cpf[^2..]}"
            : "Não informado";

    private static string FormatTipoRelacao(
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
}

public sealed class EditarAlunoViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string NomeUnidade { get; init; }
    public required bool PodeTrocarUnidade { get; init; }

    [HiddenInput]
    public Guid AlunoId { get; init; }

    [Required(ErrorMessage = "O nome completo deve ser informado.")]
    [StringLength(Aluno.NomeCompletoTamanhoMaximo, ErrorMessage = "O nome completo deve possuir no máximo {1} caracteres.")]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "A data de nascimento deve ser informada.")]
    [DataType(DataType.Date)]
    [Display(Name = "Data de nascimento")]
    public DateOnly? DataNascimento { get; set; }

    [Display(Name = "Telefone")]
    [StringLength(Aluno.TelefoneTamanhoMaximo, ErrorMessage = "O telefone deve possuir no máximo {1} caracteres.")]
    public string? Telefone { get; set; }

    [EmailAddress(ErrorMessage = "O e-mail informado não é válido.")]
    [StringLength(Aluno.EmailTamanhoMaximo, ErrorMessage = "O e-mail deve possuir no máximo {1} caracteres.")]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [Display(Name = "CPF")]
    public string? CpfMascarado { get; init; }
}

internal static class EditarAlunoMapper
{
    public static EditarAlunoViewModel Mapear(
        ContextoAlunosResumo contexto,
        AlunoDadosEdicao dados,
        bool podeTrocar) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.NomeUnidade,
        PodeTrocarUnidade = podeTrocar,
        AlunoId = dados.AlunoId,
        NomeCompleto = dados.NomeCompleto,
        DataNascimento = dados.DataNascimento,
        Telefone = dados.Telefone,
        Email = dados.Email,
        CpfMascarado = dados.Cpf is { Length: 11 } cpf
            ? $"***.***.***-{cpf[^2..]}"
            : "Não informado"
    };
}

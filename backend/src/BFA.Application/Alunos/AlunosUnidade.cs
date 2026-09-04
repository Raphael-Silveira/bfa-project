using BFA.Application.Unidades;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;

namespace BFA.Application.Alunos;

public enum EstadoAlunosUnidade
{
    Sucesso,
    SemAcesso,
    UnidadeNaoEncontrada,
    AlunoNaoEncontrado,
    AlunoNaoRelacionadoUnidade,
    DataNascimentoInvalida,
    MenorSemResponsavel,
    DadosInvalidos,
    Falha
}

public sealed record ContextoAlunosResumo(
    Guid OrganizacaoId,
    Guid UnidadeId,
    string NomeUnidade,
    bool PodeGerenciar);

public sealed record AlunoListaItem(
    Guid AlunoId,
    string NomeCompleto,
    DateOnly DataNascimento,
    string? Telefone,
    string? Email,
    bool Ativo,
    Guid? MatriculaAtivaId,
    string? PlanoAtual,
    int? FrequenciaSemanal,
    StatusMatricula? StatusMatricula,
    DateOnly? DataInicioMatricula,
    DateOnly? DataFimPrevistaMatricula);

public sealed record ResponsavelAlunoResumo(
    Guid ResponsavelId,
    string NomeCompleto,
    string? Telefone,
    string? Email,
    TipoRelacaoResponsavel TipoRelacao,
    string? DescricaoRelacao,
    bool PrincipalContato,
    bool ResponsavelFinanceiro,
    bool VinculoAtivo,
    bool ResponsavelAtivo);

public sealed record MatriculaAlunoResumo(
    Guid MatriculaId,
    string Plano,
    int NumeroVersao,
    StatusMatricula Status,
    DateOnly DataInicio,
    DateOnly DataFimPrevista,
    DateOnly? DataFimReal,
    int FrequenciaSemanal,
    decimal ValorMensalContratado);

public sealed record AlunoDetalhe(
    Guid AlunoId,
    string NomeCompleto,
    DateOnly DataNascimento,
    string? Cpf,
    string? Telefone,
    string? Email,
    bool Ativo,
    IReadOnlyList<ResponsavelAlunoResumo> Responsaveis,
    MatriculaAlunoResumo? MatriculaAtiva,
    IReadOnlyList<MatriculaAlunoResumo> HistoricoMatriculas);

public sealed record ResultadoAlunosUnidade<T>(
    EstadoAlunosUnidade Estado,
    T? Valor = default,
    ContextoAlunosResumo? Contexto = null);

public sealed record AlunoDadosEdicao(
    Guid AlunoId,
    string NomeCompleto,
    DateOnly DataNascimento,
    string? Cpf,
    string? Telefone,
    string? Email);

public sealed record MatriculaAtivaResumo(
    Guid MatriculaId,
    DateOnly DataInicio);

public sealed record ResponsavelAtivoResumo(
    Guid ResponsavelId,
    bool VinculoAtivo,
    bool ResponsavelAtivo);

public sealed record DadosEdicaoAluno(
    AlunoDadosEdicao Aluno,
    IReadOnlyList<MatriculaAtivaResumo> MatriculasAtivas,
    IReadOnlyList<ResponsavelAtivoResumo> Responsaveis);

public interface IAlunosRepositorio
{
    Task<IReadOnlyList<AlunoListaItem>> ListarAsync(
        Guid organizacaoId, Guid unidadeId, string? texto,
        CancellationToken cancellationToken);

    Task<AlunoDetalhe?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken);

    Task<DadosEdicaoAluno?> ObterParaEdicaoAsync(
        Guid organizacaoId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MatriculaAtivaResumo>> ObterMatriculasAtivasAlunoAsync(
        Guid organizacaoId, Guid alunoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ResponsavelAtivoResumo>> ObterResponsaveisAlunoAsync(
        Guid organizacaoId, Guid alunoId,
        CancellationToken cancellationToken);

    Task<bool> ExisteRelacaoAlunoUnidadeAsync(
        Guid organizacaoId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken);

    Task<bool> PersistirAtualizacaoAsync(Aluno aluno, CancellationToken cancellationToken);
}

public interface IAlunosServico
{
    Task<ResultadoAlunosUnidade<IReadOnlyList<AlunoListaItem>>> ListarAsync(
        Guid usuarioId, Guid unidadeId, string? texto,
        CancellationToken cancellationToken);

    Task<ResultadoAlunosUnidade<AlunoDetalhe>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken);

    Task<ResultadoAlunosUnidade<DadosEdicaoAluno>> ObterDadosEdicaoAsync(
        Guid usuarioId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken);

    Task<ResultadoAlunosUnidade<Guid>> AtualizarDadosAsync(
        Guid usuarioId, Guid unidadeId, Guid alunoId,
        string nomeCompleto, DateOnly dataNascimento, string? telefone, string? email,
        CancellationToken cancellationToken);
}

public sealed class AlunosServico(
    IAlunosRepositorio repositorio,
    IGovernancaOperacionalUnidade governancaOperacional,
    IUnidadeContextoConsulta unidadeContextoConsulta) : IAlunosServico
{
    public async Task<ResultadoAlunosUnidade<IReadOnlyList<AlunoListaItem>>> ListarAsync(
        Guid usuarioId, Guid unidadeId, string? texto,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        if (contexto.Estado != EstadoAlunosUnidade.Sucesso)
            return new(contexto.Estado);
        var itens = await repositorio.ListarAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, texto, cancellationToken);
        return new(EstadoAlunosUnidade.Sucesso, itens, contexto.Valor);
    }

    public async Task<ResultadoAlunosUnidade<AlunoDetalhe>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        if (contexto.Estado != EstadoAlunosUnidade.Sucesso)
            return new(contexto.Estado);
        if (alunoId == Guid.Empty)
            return new(EstadoAlunosUnidade.AlunoNaoEncontrado);
        var detalhe = await repositorio.ObterAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, alunoId, cancellationToken);
        if (detalhe is null)
            return new(EstadoAlunosUnidade.AlunoNaoEncontrado);
        return new(EstadoAlunosUnidade.Sucesso, detalhe, contexto.Valor);
    }

    public async Task<ResultadoAlunosUnidade<DadosEdicaoAluno>> ObterDadosEdicaoAsync(
        Guid usuarioId, Guid unidadeId, Guid alunoId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoAlunosUnidade.Sucesso)
            return new(contexto.Estado);
        if (alunoId == Guid.Empty)
            return new(EstadoAlunosUnidade.AlunoNaoEncontrado);
        var dados = await repositorio.ObterParaEdicaoAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, alunoId, cancellationToken);
        if (dados is null)
            return new(EstadoAlunosUnidade.AlunoNaoEncontrado);
        return new(EstadoAlunosUnidade.Sucesso, dados, contexto.Valor);
    }

    public async Task<ResultadoAlunosUnidade<Guid>> AtualizarDadosAsync(
        Guid usuarioId, Guid unidadeId, Guid alunoId,
        string nomeCompleto, DateOnly dataNascimento, string? telefone, string? email,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoAlunosUnidade.Sucesso)
            return new(contexto.Estado);
        if (alunoId == Guid.Empty
            || string.IsNullOrWhiteSpace(nomeCompleto)
            || dataNascimento == default)
        {
            return new(EstadoAlunosUnidade.DadosInvalidos);
        }

        var dadosExistentes = await repositorio.ObterParaEdicaoAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, alunoId, cancellationToken);
        if (dadosExistentes is null)
            return new(EstadoAlunosUnidade.AlunoNaoEncontrado);

        var agoraUtc = DateTime.UtcNow;
        var dataCivilAtual = DateOnly.FromDateTime(DateTime.Today);

        var matriculasAtivas = await repositorio.ObterMatriculasAtivasAlunoAsync(
            contexto.Valor.OrganizacaoId, alunoId, cancellationToken);

        if (dataNascimento != dadosExistentes.Aluno.DataNascimento
            && matriculasAtivas.Count > 0)
        {
            var responsaveis = await repositorio.ObterResponsaveisAlunoAsync(
                contexto.Valor.OrganizacaoId, alunoId, cancellationToken);

            foreach (var matricula in matriculasAtivas)
            {
                var ehMenorNaDataInicio = matricula.DataInicio
                    < dataNascimento.AddYears(Aluno.IdadeMaioridade);

                if (!ehMenorNaDataInicio)
                    continue;

                var possuiResponsavelAtivo = responsaveis.Any(r =>
                    r.VinculoAtivo && r.ResponsavelAtivo);

                if (!possuiResponsavelAtivo)
                {
                    return new(EstadoAlunosUnidade.MenorSemResponsavel);
                }
            }
        }

        var alunoParaAtualizar = new Aluno(
            alunoId,
            contexto.Valor.OrganizacaoId,
            nomeCompleto,
            dataNascimento,
            dataCivilAtual,
            agoraUtc,
            cpf: dadosExistentes.Aluno.Cpf,
            telefone: telefone,
            email: email);

        var sucesso = await repositorio.PersistirAtualizacaoAsync(
            alunoParaAtualizar, cancellationToken);

        return sucesso
            ? new(EstadoAlunosUnidade.Sucesso, alunoId, contexto.Valor)
            : new(EstadoAlunosUnidade.Falha);
    }

    private async Task<ResultadoAlunosUnidade<ContextoAlunosResumo>> ObterContextoAsync(
        Guid usuarioId, Guid unidadeId, bool exigirGerenciamento,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty || unidadeId == Guid.Empty)
            return new(EstadoAlunosUnidade.SemAcesso);
        var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId, cancellationToken);
        if (unidade is null)
            return new(EstadoAlunosUnidade.UnidadeNaoEncontrada);
        var governanca = await governancaOperacional.ObterAsync(
            usuarioId, unidade.OrganizacaoId, unidadeId, cancellationToken);
        var autorizado = exigirGerenciamento
            ? governanca.PodeGerenciarAlunos
            : governanca.PodeAcessar;
        if (!autorizado)
            return new(EstadoAlunosUnidade.SemAcesso);
        return new(EstadoAlunosUnidade.Sucesso, new(
            unidade.OrganizacaoId, unidadeId, unidade.Nome,
            governanca.PodeGerenciarAlunos));
    }
}

using BFA.Application.Unidades;
using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Turmas;

namespace BFA.Application.Matriculas;

public enum EstadoMatriculas
{
    Sucesso,
    SemAcesso,
    UnidadeNaoEncontrada,
    MatriculaNaoEncontrada,
    AlunoNaoEncontrado,
    AlunoNaoRelacionadoUnidade,
    AlunoDuplicado,
    ResponsavelInvalido,
    ResponsavelDuplicado,
    MenorSemResponsavel,
    PlanoNaoElegivel,
    HorarioDuplicado,
    HorarioNaoElegivel,
    FrequenciaExcedida,
    ConflitoHorarioAluno,
    CapacidadeEsgotada,
    MatriculaAtivaExistente,
    DataInvalida,
    EstadoTerminal,
    ConflitoConcorrencia,
    DadosInvalidos,
    Falha
}

public enum EscopoPlanoMatricula
{
    Rede,
    Local
}

public sealed record ContextoMatriculasResumo(
    Guid OrganizacaoId,
    Guid UnidadeId,
    string NomeUnidade,
    bool PodeGerenciar,
    bool PossuiFranqueadoAtivo);

public sealed record MatriculaListaItem(
    Guid MatriculaId,
    Guid AlunoId,
    string NomeCompleto,
    StatusMatricula Status,
    DateOnly DataInicio,
    DateOnly DataFimPrevista,
    DateOnly? DataFimReal,
    string Plano,
    int NumeroVersao,
    int FrequenciaSemanal,
    decimal ValorMensalContratado,
    int QuantidadeHorariosAtuais);

public sealed record ResponsavelMatriculaResumo(
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

public sealed record GradeMatriculaResumo(
    Guid MatriculaHorarioId,
    Guid TurmaHorarioId,
    Guid TurmaId,
    string Turma,
    string ProfessorSnapshot,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim);

public sealed record MatriculaDetalhe(
    Guid MatriculaId,
    Guid AlunoId,
    string NomeAluno,
    DateOnly DataNascimentoAluno,
    string? CpfAluno,
    string? TelefoneAluno,
    string? EmailAluno,
    StatusMatricula Status,
    DateOnly DataInicio,
    DateOnly DataFimPrevista,
    DateOnly? DataFimReal,
    Guid PlanoId,
    Guid PlanoVersaoId,
    string Plano,
    int NumeroVersao,
    int DuracaoMeses,
    int FrequenciaSemanal,
    decimal ValorMensalCatalogo,
    decimal ValorMensalContratado,
    bool CobraTaxaMatricula,
    decimal? ValorTaxaMatricula,
    IReadOnlyList<ResponsavelMatriculaResumo> Responsaveis,
    IReadOnlyList<GradeMatriculaResumo> GradeAtual,
    IReadOnlyList<GradeMatriculaResumo> HistoricoGrade);

public sealed record AlunoRelacionadoUnidadeResumo(
    Guid AlunoId,
    string NomeCompleto,
    DateOnly DataNascimento,
    bool PossuiMatriculaAtiva,
    IReadOnlyList<ResponsavelMatriculaResumo> Responsaveis);

public sealed record PlanoElegivelMatriculaResumo(
    Guid PlanoId,
    Guid PlanoVersaoId,
    string Nome,
    int NumeroVersao,
    int DuracaoMeses,
    int FrequenciaSemanal,
    decimal ValorMensal,
    bool CobraMatricula,
    decimal? ValorMatricula,
    EscopoPlanoMatricula Escopo);

public sealed record HorarioElegivelMatriculaResumo(
    Guid TurmaHorarioId,
    Guid TurmaId,
    string NomeTurma,
    string Professor,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    int Capacidade,
    int Ocupacao,
    int VagasDisponiveis);

public sealed record NovoAlunoMatriculaSolicitacao(
    string NomeCompleto,
    DateOnly DataNascimento,
    string? Cpf,
    string? Telefone,
    string? Email);

public sealed record NovoResponsavelMatriculaSolicitacao(
    string NomeCompleto,
    string? Cpf,
    string? Telefone,
    string? Email,
    TipoRelacaoResponsavel TipoRelacao,
    string? DescricaoRelacao,
    bool PrincipalContato,
    bool ResponsavelFinanceiro);

public sealed record CriarMatriculaSolicitacao(
    Guid? AlunoId,
    NovoAlunoMatriculaSolicitacao? NovoAluno,
    IReadOnlyList<NovoResponsavelMatriculaSolicitacao> Responsaveis,
    Guid PlanoVersaoId,
    DateOnly DataInicio,
    decimal ValorMensalContratado,
    bool CobraTaxaMatricula,
    decimal? ValorTaxaMatricula,
    IReadOnlyList<Guid> TurmaHorarioIds);

public sealed record AlterarGradeMatriculaSolicitacao(
    DateOnly DataInicioNovaConfiguracao,
    IReadOnlyList<Guid> TurmaHorarioIds);

public sealed record ResultadoCriacaoMatricula(
    Guid MatriculaId,
    Guid AlunoId,
    int HorariosCriados);

public sealed record ResultadoAlteracaoGrade(
    int HorariosPreservados,
    int HorariosEncerrados,
    int HorariosCriados);

public sealed record IntervaloVigenciaGrade(DateOnly Inicio, DateOnly? Fim);

public sealed record IntervaloHorarioGrade(
    DiaSemana DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFim);

public static class RegraGradeMatricula
{
    public static bool PossuiConflito(
        IReadOnlyList<IntervaloHorarioGrade> horarios) =>
        horarios.SelectMany((primeiro, indice) =>
                horarios.Skip(indice + 1).Select(segundo => (primeiro, segundo)))
            .Any(par => par.primeiro.DiaSemana == par.segundo.DiaSemana
                && par.primeiro.HoraInicio < par.segundo.HoraFim
                && par.segundo.HoraInicio < par.primeiro.HoraFim);

    public static int MaximoSimultaneo(
        IEnumerable<IntervaloVigenciaGrade> intervalos,
        DateOnly inicioConsiderado,
        DateOnly? fimConsiderado)
    {
        ArgumentNullException.ThrowIfNull(intervalos);
        if (inicioConsiderado == default
            || fimConsiderado.HasValue && fimConsiderado < inicioConsiderado)
            throw new ArgumentException("O período considerado é inválido.");
        var itens = intervalos.Where(item =>
                (item.Fim is null || item.Fim >= inicioConsiderado)
                && (fimConsiderado is null || item.Inicio <= fimConsiderado))
            .ToArray();
        if (itens.Length == 0) return 0;
        var pontos = itens.Select(item => item.Inicio < inicioConsiderado
                ? inicioConsiderado : item.Inicio)
            .Distinct();
        return pontos.Max(ponto => itens.Count(item =>
            item.Inicio <= ponto && (item.Fim is null || item.Fim >= ponto)));
    }
}

public sealed record ResultadoMatriculas<T>(
    EstadoMatriculas Estado,
    T? Valor = default,
    ContextoMatriculasResumo? Contexto = null);

public interface IMatriculasRepositorio
{
    Task<IReadOnlyList<MatriculaListaItem>> ListarAsync(
        Guid organizacaoId, Guid unidadeId, string? texto, StatusMatricula? status,
        CancellationToken cancellationToken);

    Task<MatriculaDetalhe?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid matriculaId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AlunoRelacionadoUnidadeResumo>> ListarAlunosRelacionadosAsync(
        Guid organizacaoId, Guid unidadeId, string? texto,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanoElegivelMatriculaResumo>> ListarPlanosElegiveisAsync(
        Guid organizacaoId, Guid unidadeId, DateOnly dataInicio,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HorarioElegivelMatriculaResumo>> ListarHorariosElegiveisAsync(
        Guid organizacaoId, Guid unidadeId, DateOnly dataInicio, DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<ResultadoMatriculas<ResultadoCriacaoMatricula>> CriarAsync(
        Guid organizacaoId, Guid unidadeId, Guid usuarioId,
        bool permitirReusoOrganizacional, CriarMatriculaSolicitacao solicitacao,
        DateOnly dataCivilAtual, DateTime agoraUtc,
        CancellationToken cancellationToken);

    Task<ResultadoMatriculas<ResultadoAlteracaoGrade>> AlterarGradeAsync(
        Guid organizacaoId, Guid unidadeId, Guid matriculaId, Guid usuarioId,
        AlterarGradeMatriculaSolicitacao solicitacao, DateTime agoraUtc,
        CancellationToken cancellationToken);

    Task<EstadoMatriculas> FinalizarAsync(
        Guid organizacaoId, Guid unidadeId, Guid matriculaId, Guid usuarioId,
        DateOnly dataFinalEfetiva, bool cancelar, DateTime agoraUtc,
        CancellationToken cancellationToken);
}

public interface IMatriculasServico
{
    Task<ResultadoMatriculas<IReadOnlyList<MatriculaListaItem>>> ListarAsync(
        Guid usuarioId, Guid unidadeId, string? texto, StatusMatricula? status,
        CancellationToken cancellationToken);

    Task<ResultadoMatriculas<MatriculaDetalhe>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid matriculaId,
        CancellationToken cancellationToken);

    Task<ResultadoMatriculas<IReadOnlyList<AlunoRelacionadoUnidadeResumo>>>
        ListarAlunosRelacionadosAsync(
            Guid usuarioId, Guid unidadeId, string? texto,
            CancellationToken cancellationToken);

    Task<ResultadoMatriculas<IReadOnlyList<PlanoElegivelMatriculaResumo>>>
        ListarPlanosElegiveisAsync(
            Guid usuarioId, Guid unidadeId, DateOnly dataInicio,
            CancellationToken cancellationToken);

    Task<ResultadoMatriculas<IReadOnlyList<HorarioElegivelMatriculaResumo>>>
        ListarHorariosElegiveisAsync(
            Guid usuarioId, Guid unidadeId, DateOnly dataInicio, DateOnly dataFim,
            CancellationToken cancellationToken);

    Task<ResultadoMatriculas<ResultadoCriacaoMatricula>> CriarAsync(
        Guid usuarioId, Guid unidadeId, CriarMatriculaSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoMatriculas<ResultadoAlteracaoGrade>> AlterarGradeAsync(
        Guid usuarioId, Guid unidadeId, Guid matriculaId,
        AlterarGradeMatriculaSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoMatriculas<Guid>> EncerrarAsync(
        Guid usuarioId, Guid unidadeId, Guid matriculaId, DateOnly dataFinalEfetiva,
        CancellationToken cancellationToken);

    Task<ResultadoMatriculas<Guid>> CancelarAsync(
        Guid usuarioId, Guid unidadeId, Guid matriculaId, DateOnly dataFinalEfetiva,
        CancellationToken cancellationToken);
}

public sealed class MatriculasServico(
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IGovernancaOperacionalUnidade governancaOperacional,
    IMatriculasRepositorio repositorio,
    TimeProvider timeProvider) : IMatriculasServico
{
    public async Task<ResultadoMatriculas<IReadOnlyList<MatriculaListaItem>>> ListarAsync(
        Guid usuarioId, Guid unidadeId, string? texto, StatusMatricula? status,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        if (contexto.Estado != EstadoMatriculas.Sucesso) return new(contexto.Estado);
        var itens = await repositorio.ListarAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, texto, status, cancellationToken);
        return new(EstadoMatriculas.Sucesso, itens, contexto.Valor);
    }

    public async Task<ResultadoMatriculas<MatriculaDetalhe>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid matriculaId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        if (contexto.Estado != EstadoMatriculas.Sucesso) return new(contexto.Estado);
        var detalhe = await repositorio.ObterAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, matriculaId, cancellationToken);
        return detalhe is null
            ? new(EstadoMatriculas.MatriculaNaoEncontrada)
            : new(EstadoMatriculas.Sucesso, detalhe, contexto.Valor);
    }

    public async Task<ResultadoMatriculas<IReadOnlyList<AlunoRelacionadoUnidadeResumo>>>
        ListarAlunosRelacionadosAsync(
            Guid usuarioId, Guid unidadeId, string? texto,
            CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoMatriculas.Sucesso) return new(contexto.Estado);
        var itens = await repositorio.ListarAlunosRelacionadosAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, texto, cancellationToken);
        return new(EstadoMatriculas.Sucesso, itens, contexto.Valor);
    }

    public async Task<ResultadoMatriculas<IReadOnlyList<PlanoElegivelMatriculaResumo>>>
        ListarPlanosElegiveisAsync(
            Guid usuarioId, Guid unidadeId, DateOnly dataInicio,
            CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoMatriculas.Sucesso) return new(contexto.Estado);
        if (dataInicio == default) return new(EstadoMatriculas.DataInvalida);
        var itens = await repositorio.ListarPlanosElegiveisAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, dataInicio, cancellationToken);
        return new(EstadoMatriculas.Sucesso, itens, contexto.Valor);
    }

    public async Task<ResultadoMatriculas<IReadOnlyList<HorarioElegivelMatriculaResumo>>>
        ListarHorariosElegiveisAsync(
            Guid usuarioId, Guid unidadeId, DateOnly dataInicio, DateOnly dataFim,
            CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoMatriculas.Sucesso) return new(contexto.Estado);
        if (dataInicio == default || dataFim < dataInicio)
            return new(EstadoMatriculas.DataInvalida);
        var itens = await repositorio.ListarHorariosElegiveisAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, dataInicio, dataFim,
            cancellationToken);
        return new(EstadoMatriculas.Sucesso, itens, contexto.Valor);
    }

    public async Task<ResultadoMatriculas<ResultadoCriacaoMatricula>> CriarAsync(
        Guid usuarioId, Guid unidadeId, CriarMatriculaSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoMatriculas.Sucesso) return new(contexto.Estado);
        if (!SolicitacaoCriacaoValida(solicitacao, out var erro)) return new(erro);
        return await repositorio.CriarAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, usuarioId,
            contexto.Valor.PossuiFranqueadoAtivo is false
                && await EhAdministradorRedeAsync(usuarioId, contexto.Valor, cancellationToken),
            solicitacao,
            DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }

    public async Task<ResultadoMatriculas<ResultadoAlteracaoGrade>> AlterarGradeAsync(
        Guid usuarioId, Guid unidadeId, Guid matriculaId,
        AlterarGradeMatriculaSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoMatriculas.Sucesso) return new(contexto.Estado);
        if (matriculaId == Guid.Empty
            || solicitacao.DataInicioNovaConfiguracao == default)
            return new(EstadoMatriculas.DataInvalida);
        if (!IdsHorariosValidos(solicitacao.TurmaHorarioIds, out var erro))
            return new(erro);
        return await repositorio.AlterarGradeAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, matriculaId, usuarioId,
            solicitacao, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }

    public Task<ResultadoMatriculas<Guid>> EncerrarAsync(
        Guid usuarioId, Guid unidadeId, Guid matriculaId, DateOnly dataFinalEfetiva,
        CancellationToken cancellationToken) => FinalizarAsync(
            usuarioId, unidadeId, matriculaId, dataFinalEfetiva,
            cancelar: false, cancellationToken);

    public Task<ResultadoMatriculas<Guid>> CancelarAsync(
        Guid usuarioId, Guid unidadeId, Guid matriculaId, DateOnly dataFinalEfetiva,
        CancellationToken cancellationToken) => FinalizarAsync(
            usuarioId, unidadeId, matriculaId, dataFinalEfetiva,
            cancelar: true, cancellationToken);

    private async Task<ResultadoMatriculas<Guid>> FinalizarAsync(
        Guid usuarioId, Guid unidadeId, Guid matriculaId, DateOnly dataFinalEfetiva,
        bool cancelar, CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoMatriculas.Sucesso) return new(contexto.Estado);
        if (matriculaId == Guid.Empty || dataFinalEfetiva == default)
            return new(EstadoMatriculas.DataInvalida);
        var estado = await repositorio.FinalizarAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, matriculaId, usuarioId,
            dataFinalEfetiva, cancelar, timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        return estado == EstadoMatriculas.Sucesso
            ? new(estado, matriculaId, contexto.Valor)
            : new(estado);
    }

    private async Task<ResultadoMatriculas<ContextoMatriculasResumo>> ObterContextoAsync(
        Guid usuarioId, Guid unidadeId, bool exigirGerenciamento,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty || unidadeId == Guid.Empty)
            return new(EstadoMatriculas.SemAcesso);
        var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId, cancellationToken);
        if (unidade is null) return new(EstadoMatriculas.UnidadeNaoEncontrada);
        var governanca = await governancaOperacional.ObterAsync(
            usuarioId, unidade.OrganizacaoId, unidadeId, cancellationToken);
        var autorizado = exigirGerenciamento
            ? governanca.PodeGerenciarMatriculas
            : governanca.PodeAcessar;
        if (!autorizado) return new(EstadoMatriculas.SemAcesso);
        return new(EstadoMatriculas.Sucesso, new(
            unidade.OrganizacaoId, unidadeId, unidade.Nome,
            governanca.PodeGerenciarMatriculas,
            governanca.PossuiFranqueadoAtivo));
    }

    private async Task<bool> EhAdministradorRedeAsync(
        Guid usuarioId, ContextoMatriculasResumo contexto,
        CancellationToken cancellationToken)
    {
        var governanca = await governancaOperacional.ObterAsync(
            usuarioId, contexto.OrganizacaoId, contexto.UnidadeId, cancellationToken);
        return governanca.EhAdministradorRede;
    }

    private static bool SolicitacaoCriacaoValida(
        CriarMatriculaSolicitacao solicitacao, out EstadoMatriculas erro)
    {
        erro = EstadoMatriculas.DadosInvalidos;
        if (solicitacao is null
            || solicitacao.PlanoVersaoId == Guid.Empty
            || solicitacao.DataInicio == default
            || solicitacao.ValorMensalContratado <= 0
            || solicitacao.Responsaveis is null
            || (solicitacao.AlunoId.HasValue == (solicitacao.NovoAluno is not null))
            || solicitacao.AlunoId == Guid.Empty
            || (solicitacao.CobraTaxaMatricula
                ? solicitacao.ValorTaxaMatricula is not > 0
                : solicitacao.ValorTaxaMatricula is not null))
            return false;
        if (!IdsHorariosValidos(solicitacao.TurmaHorarioIds, out erro)) return false;
        erro = EstadoMatriculas.Sucesso;
        return true;
    }

    private static bool IdsHorariosValidos(
        IReadOnlyList<Guid>? ids, out EstadoMatriculas erro)
    {
        erro = EstadoMatriculas.DadosInvalidos;
        if (ids is null || ids.Count == 0 || ids.Any(id => id == Guid.Empty)) return false;
        if (ids.Distinct().Count() != ids.Count)
        {
            erro = EstadoMatriculas.HorarioDuplicado;
            return false;
        }
        erro = EstadoMatriculas.Sucesso;
        return true;
    }
}

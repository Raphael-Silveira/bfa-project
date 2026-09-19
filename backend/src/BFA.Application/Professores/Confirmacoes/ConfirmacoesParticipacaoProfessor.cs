using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Application.Professores.Turmas;
using BFA.Domain.Acessos;
using BFA.Domain.Aulas;
using BFA.Domain.Matriculas;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Professores.Confirmacoes;

public sealed record AulaConfirmacoesProfessorResumo(
    Guid AulaId, Guid TurmaId, string TurmaNome, DateOnly Data, TimeOnly HoraInicio, TimeOnly HoraFim,
    StatusAula Status, string? MotivoCancelamento, DateTime? CanceladaEmUtc,
    Guid? CanceladaPorUsuarioId);

public sealed record AlunoConfirmacaoProfessorResumo(
    Guid AlunoId, string NomeCompleto, bool Confirmou, StatusPresenca? Presenca);

public sealed record AulaConfirmacoesProfessorDetalhe(
    AulaConfirmacoesProfessorResumo Aula,
    int TotalAlunos,
    int TotalConfirmados,
    int TotalPresentes,
    int TotalAusentes,
    int TotalNaoMarcados,
    IReadOnlyList<AlunoConfirmacaoProfessorResumo> Alunos);

public sealed record AulasProfessorPagina(
    IReadOnlyList<AulaConfirmacoesProfessorResumo> Aulas,
    int PaginaAtual,
    int TotalPaginas,
    int TotalItens);

public sealed record RegistroChamadaProfessor(Guid AlunoId, StatusPresenca Status);

public enum EstadoConfirmacoesProfessor
{
    Sucesso,
    SemAcesso,
    AulaNaoEncontrada
    ,DadosInvalidos
    ,AulaCancelada
    ,ChamadaExistente
    ,Falha
}

public sealed record ResultadoConfirmacoesProfessor<T>(
    EstadoConfirmacoesProfessor Estado, T? Valor = default);

public interface IConfirmacoesParticipacaoProfessorRepositorio
{
    Task<AulasProfessorPagina> ListarAgendaAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId,
        DateOnly? dataInicial, DateOnly? dataFinal, StatusAula? status,
        bool incluirTodas, int pagina, int tamanhoPagina, CancellationToken cancellationToken);

    Task<AulaConfirmacoesProfessorDetalhe?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId, Guid turmaId,
        Guid aulaId, CancellationToken cancellationToken);

    Task<EstadoConfirmacoesProfessor> RegistrarChamadaAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId, Guid turmaId,
        Guid aulaId, IReadOnlyList<RegistroChamadaProfessor> registros,
        Guid usuarioId, DateTime agoraUtc, CancellationToken cancellationToken);

    Task<EstadoConfirmacoesProfessor> CancelarAsync(
        Guid organizacaoId, Guid unidadeId, Guid professorUnidadeId, Guid turmaId,
        Guid aulaId, string motivo, Guid usuarioId, DateTime agoraUtc,
        CancellationToken cancellationToken);
}

public interface IConfirmacoesParticipacaoProfessorConsulta
{
    Task<ResultadoConfirmacoesProfessor<AulasProfessorPagina>> ListarAgendaAsync(
        Guid usuarioId, Guid unidadeId, DateOnly? dataInicial, DateOnly? dataFinal,
        StatusAula? status, bool incluirTodas, int pagina, CancellationToken cancellationToken);

    Task<ResultadoConfirmacoesProfessor<AulaConfirmacoesProfessorDetalhe>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId, Guid aulaId,
        CancellationToken cancellationToken);

    Task<ResultadoConfirmacoesProfessorSimples> RegistrarChamadaAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId, Guid aulaId,
        IReadOnlyList<RegistroChamadaProfessor> registros,
        CancellationToken cancellationToken);

    Task<ResultadoConfirmacoesProfessorSimples> CancelarAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId, Guid aulaId, string motivo,
        CancellationToken cancellationToken);
}

public sealed record ResultadoConfirmacoesProfessorSimples(EstadoConfirmacoesProfessor Estado);

public sealed class ConfirmacoesParticipacaoProfessorConsulta(
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IMinhasTurmasProfessorRepositorio turmasRepositorio,
    IConfirmacoesParticipacaoProfessorRepositorio repositorio,
    TimeProvider timeProvider,
    ILogger<ConfirmacoesParticipacaoProfessorConsulta> logger)
    : IConfirmacoesParticipacaoProfessorConsulta
{
    public async Task<ResultadoConfirmacoesProfessor<AulasProfessorPagina>> ListarAgendaAsync(
        Guid usuarioId, Guid unidadeId, DateOnly? dataInicial, DateOnly? dataFinal,
        StatusAula? status, bool incluirTodas, int pagina, CancellationToken cancellationToken)
    {
        var contexto = await ResolverAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return new(EstadoConfirmacoesProfessor.SemAcesso);
        var hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        StatusAula? filtroStatus = incluirTodas ? null : status ?? StatusAula.Programada;
        var filtroInicial = dataInicial ?? (!incluirTodas && (status is null or StatusAula.Programada) ? hoje : null);
        var paginaSegura = Math.Max(1, pagina);
        return new(EstadoConfirmacoesProfessor.Sucesso,
            await repositorio.ListarAgendaAsync(contexto.OrganizacaoId, unidadeId,
                contexto.ProfessorUnidadeId, filtroInicial, dataFinal, filtroStatus, incluirTodas,
                paginaSegura, 20, cancellationToken));
    }

    public async Task<ResultadoConfirmacoesProfessor<AulaConfirmacoesProfessorDetalhe>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId, Guid aulaId,
        CancellationToken cancellationToken)
    {
        var contexto = await ResolverAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return new(EstadoConfirmacoesProfessor.SemAcesso);
        var detalhe = await repositorio.ObterAsync(contexto.OrganizacaoId, unidadeId,
            contexto.ProfessorUnidadeId, turmaId, aulaId, cancellationToken);
        if (detalhe is null) return new(EstadoConfirmacoesProfessor.AulaNaoEncontrada);
        logger.LogDebug("Confirmações consultadas para aula {AulaId}", aulaId);
        return new(EstadoConfirmacoesProfessor.Sucesso, detalhe);
    }

    public async Task<ResultadoConfirmacoesProfessorSimples> RegistrarChamadaAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId, Guid aulaId,
        IReadOnlyList<RegistroChamadaProfessor> registros,
        CancellationToken cancellationToken)
    {
        var contexto = await ResolverAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return new(EstadoConfirmacoesProfessor.SemAcesso);
        if (registros.Any(r =>
                r.AlunoId == Guid.Empty
                || r.Status is not (StatusPresenca.Presente or StatusPresenca.Ausente)))
            return new(EstadoConfirmacoesProfessor.DadosInvalidos);

        var estado = await repositorio.RegistrarChamadaAsync(
            contexto.OrganizacaoId, unidadeId, contexto.ProfessorUnidadeId, turmaId,
            aulaId, registros, usuarioId, timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        return new(estado);
    }

    public async Task<ResultadoConfirmacoesProfessorSimples> CancelarAsync(
        Guid usuarioId, Guid unidadeId, Guid turmaId, Guid aulaId, string motivo,
        CancellationToken cancellationToken)
    {
        var contexto = await ResolverAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return new(EstadoConfirmacoesProfessor.SemAcesso);
        if (string.IsNullOrWhiteSpace(motivo)
            || motivo.Trim().Length > BFA.Domain.Aulas.Aula.MotivoCancelamentoTamanhoMaximo)
            return new(EstadoConfirmacoesProfessor.DadosInvalidos);

        var estado = await repositorio.CancelarAsync(
            contexto.OrganizacaoId, unidadeId, contexto.ProfessorUnidadeId, turmaId,
            aulaId, motivo, usuarioId, timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        return new(estado);
    }

    private async Task<Contexto?> ResolverAsync(Guid usuarioId, Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var unidade = await unidadesUsuarioConsulta.ObterProfessorAsync(usuarioId, unidadeId, cancellationToken);
        if (unidade is null || !await acessoUsuarioConsulta.PossuiPerfilNaUnidadeAsync(
                usuarioId, unidade.OrganizacaoId, unidadeId, PerfilAcesso.Professor, cancellationToken))
            return null;
        var professorUnidadeId = await turmasRepositorio.ObterProfessorUnidadeAtivoAsync(
            usuarioId, unidade.OrganizacaoId, unidadeId, cancellationToken);
        return professorUnidadeId is null ? null : new(unidade.OrganizacaoId, professorUnidadeId.Value);
    }

    private sealed record Contexto(Guid OrganizacaoId, Guid ProfessorUnidadeId);
}

using BFA.Application.Unidades;
using BFA.Domain.Aulas;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Aulas;

public enum EstadoAulasUnidade
{
    Sucesso,
    SemAcesso,
    UnidadeNaoEncontrada,
    AulaNaoEncontrada,
    TurmaNaoEncontrada,
    TurmaHorarioNaoEncontrado,
    DadosInvalidos,
    AulaNaoProgramada,
    AlunoNaoMatriculado,
    CapacidadeExcedida,
    ConflitoHorario,
    Falha
}

public sealed record ContextoAulasResumo(
    Guid OrganizacaoId,
    Guid UnidadeId,
    string NomeUnidade,
    bool PodeGerenciar);

public sealed record AulaResumo(
    Guid AulaId,
    string TurmaNome,
    string ProfessorNome,
    DateOnly Data,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    StatusAula Status,
    int Capacidade,
    int Inscritos);

public sealed record AulaDetalhe(
    Guid AulaId,
    Guid TurmaId,
    string TurmaNome,
    string ProfessorNome,
    DateOnly Data,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    StatusAula Status,
    int Capacidade,
    string? Observacoes,
    IReadOnlyList<AlunoPresencaResumo> Alunos);

public sealed record AlunoPresencaResumo(
    Guid AlunoId,
    string NomeCompleto,
    StatusPresenca? Status,
    TimeOnly? ChegouAs,
    TimeOnly? SaiuAs);

public sealed record CriarAulaSolicitacao(
    Guid TurmaHorarioId,
    DateOnly Data,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    string? Observacoes);

public sealed record AtualizarAulaSolicitacao(
    StatusAula? Status,
    string? Observacoes);

public sealed record RegistrarPresencaSolicitacao(
    StatusPresenca Status,
    TimeOnly? ChegouAs,
    TimeOnly? SaiuAs,
    string? Observacoes);

public sealed record RegistroPresencaLoteItem(
    Guid AlunoId,
    StatusPresenca Status,
    TimeOnly? ChegouAs,
    TimeOnly? SaiuAs,
    string? Observacoes);

public sealed record AulaResumoPaginado(
    IReadOnlyList<AulaResumo> Itens,
    int TotalItens);

public sealed record FrequenciaAlunoResumo(
    Guid AlunoId,
    string NomeCompleto,
    int TotalAulas,
    int Presentes,
    int Ausentes,
    int Justificados,
    int Isentos,
    decimal PercentualFrequencia);

public sealed record ResultadoAulasUnidadeSimples(
    EstadoAulasUnidade Estado,
    ContextoAulasResumo? Contexto = null);

public sealed record ResultadoAulasUnidade<T>(
    EstadoAulasUnidade Estado,
    T? Valor = default,
    ContextoAulasResumo? Contexto = null);

public interface IAulasRepositorio
{
    Task<IReadOnlyList<AulaResumo>> ListarAsync(
        Guid organizacaoId, Guid unidadeId,
        DateOnly dataInicio, DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<AulaResumoPaginado> ListarPaginadoAsync(
        Guid organizacaoId, Guid unidadeId,
        DateOnly dataInicio, DateOnly dataFim,
        int skip, int take,
        CancellationToken cancellationToken);

    Task<AulaDetalhe?> ObterAsync(
        Guid organizacaoId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken);

    Task<bool> ExisteAulaNoHorarioAsync(
        Guid organizacaoId, Guid turmaId,
        DateOnly data, TimeOnly horaInicio,
        CancellationToken cancellationToken);

    Task<bool> CriarAsync(Aula aula, CancellationToken cancellationToken);

    Task<bool> AtualizarAsync(Aula aula, CancellationToken cancellationToken);

    Task<IReadOnlyList<AlunoPresencaResumo>> ListarAlunosParaChamadaAsync(
        Guid organizacaoId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken);

    Task<Presenca?> ObterPresencaAsync(
        Guid organizacaoId, Guid aulaId, Guid alunoId,
        CancellationToken cancellationToken);

    Task<bool> RegistrarPresencaAsync(
        Presenca presenca, CancellationToken cancellationToken);

    Task<bool> RegistrarPresencasEmLoteAsync(
        IReadOnlyList<Presenca> presencas,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FrequenciaAlunoResumo>> ObterFrequenciaAsync(
        Guid organizacaoId, Guid unidadeId, Guid? turmaId,
        DateOnly dataInicio, DateOnly dataFim,
        CancellationToken cancellationToken);
}

public interface IAulasServico
{
    Task<ResultadoAulasUnidade<IReadOnlyList<AulaResumo>>> ListarAsync(
        Guid usuarioId, Guid unidadeId,
        DateOnly dataInicio, DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<ResultadoAulasUnidade<AulaResumoPaginado>> ListarPaginadoAsync(
        Guid usuarioId, Guid unidadeId,
        DateOnly dataInicio, DateOnly dataFim,
        int pagina, int tamanhoPagina,
        CancellationToken cancellationToken);

    Task<ResultadoAulasUnidade<AulaDetalhe>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken);

    Task<ResultadoAulasUnidade<Guid>> CriarAsync(
        Guid usuarioId, Guid unidadeId,
        CriarAulaSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoAulasUnidadeSimples> AtualizarAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        AtualizarAulaSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoAulasUnidadeSimples> ConcluirAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken);

    Task<ResultadoAulasUnidadeSimples> CancelarAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken);

    Task<ResultadoAulasUnidade<IReadOnlyList<AlunoPresencaResumo>>> ListarAlunosParaChamadaAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken);

    Task<ResultadoAulasUnidadeSimples> RegistrarPresencaAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId, Guid alunoId,
        RegistrarPresencaSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoAulasUnidadeSimples> RegistrarPresencasEmLoteAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        IReadOnlyList<RegistroPresencaLoteItem> registros,
        CancellationToken cancellationToken);

    Task<ResultadoAulasUnidade<IReadOnlyList<FrequenciaAlunoResumo>>> ObterFrequenciaAsync(
        Guid usuarioId, Guid unidadeId, Guid? turmaId,
        DateOnly dataInicio, DateOnly dataFim,
        CancellationToken cancellationToken);
}

public sealed class AulasServico(
    IAulasRepositorio repositorio,
    IGovernancaOperacionalUnidade governancaOperacional,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    TimeProvider timeProvider,
    ILogger<AulasServico> logger) : IAulasServico
{
    public async Task<ResultadoAulasUnidade<IReadOnlyList<AulaResumo>>> ListarAsync(
        Guid usuarioId, Guid unidadeId,
        DateOnly dataInicio, DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (dataInicio > dataFim)
            return new(EstadoAulasUnidade.DadosInvalidos);

        var itens = await repositorio.ListarAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, dataInicio, dataFim,
            cancellationToken);

        return new(EstadoAulasUnidade.Sucesso, itens, contexto.Valor);
    }

    public async Task<ResultadoAulasUnidade<AulaResumoPaginado>> ListarPaginadoAsync(
        Guid usuarioId, Guid unidadeId,
        DateOnly dataInicio, DateOnly dataFim,
        int pagina, int tamanhoPagina,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (dataInicio > dataFim)
            return new(EstadoAulasUnidade.DadosInvalidos);

        var paginaSegura = Math.Max(1, pagina);
        var tamanhoSeguro = Math.Clamp(tamanhoPagina, 1, 50);
        var skip = (paginaSegura - 1) * tamanhoSeguro;

        var resultado = await repositorio.ListarPaginadoAsync(
            contexto.Valor!.OrganizacaoId, unidadeId,
            dataInicio, dataFim, skip, tamanhoSeguro,
            cancellationToken);

        return new(EstadoAulasUnidade.Sucesso, resultado, contexto.Valor);
    }

    public async Task<ResultadoAulasUnidade<AulaDetalhe>> ObterAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (aulaId == Guid.Empty)
            return new(EstadoAulasUnidade.AulaNaoEncontrada);

        var detalhe = await repositorio.ObterAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, aulaId, cancellationToken);

        if (detalhe is null)
            return new(EstadoAulasUnidade.AulaNaoEncontrada);

        return new(EstadoAulasUnidade.Sucesso, detalhe, contexto.Valor);
    }

    public async Task<ResultadoAulasUnidade<Guid>> CriarAsync(
        Guid usuarioId, Guid unidadeId,
        CriarAulaSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);

        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (solicitacao.TurmaHorarioId == Guid.Empty
            || solicitacao.Data == default
            || solicitacao.HoraInicio >= solicitacao.HoraFim)
        {
            return new(EstadoAulasUnidade.DadosInvalidos);
        }

        var existeConflito = await repositorio.ExisteAulaNoHorarioAsync(
            contexto.Valor!.OrganizacaoId,
            Guid.Empty, // turmaId sera validado pelo trigger
            solicitacao.Data,
            solicitacao.HoraInicio,
            cancellationToken);

        if (existeConflito)
            return new(EstadoAulasUnidade.ConflitoHorario);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var aulaId = Guid.NewGuid();

        // A capacidade sera resolvida pelo trigger/infra ao inserir
        var aula = new Aula(
            aulaId,
            contexto.Valor.OrganizacaoId,
            unidadeId,
            Guid.Empty, // sera resolvido pelo repositorio
            solicitacao.TurmaHorarioId,
            solicitacao.Data,
            solicitacao.HoraInicio,
            solicitacao.HoraFim,
            1, // placeholder — repositorio resolve a capacidade da turma
            usuarioId,
            agora,
            solicitacao.Observacoes);

        var sucesso = await repositorio.CriarAsync(aula, cancellationToken);

        if (sucesso)
        {
            logger.LogInformation(
                "CriarAula concluído: aula {AulaId} para turma horário {TurmaHorarioId}",
                aulaId, solicitacao.TurmaHorarioId);
        }

        return sucesso
            ? new(EstadoAulasUnidade.Sucesso, aulaId, contexto.Valor)
            : new(EstadoAulasUnidade.Falha);
    }

    public async Task<ResultadoAulasUnidadeSimples> AtualizarAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        AtualizarAulaSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);

        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (aulaId == Guid.Empty)
            return new(EstadoAulasUnidade.AulaNaoEncontrada);

        var existente = await repositorio.ObterAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, aulaId, cancellationToken);
        if (existente is null)
            return new(EstadoAulasUnidade.AulaNaoEncontrada);

        if (solicitacao.Status is not null && solicitacao.Status != existente.Status)
        {
            if (solicitacao.Status == StatusAula.Concluida)
                return await ConcluirAsync(usuarioId, unidadeId, aulaId, cancellationToken);

            if (solicitacao.Status == StatusAula.Cancelada)
                return await CancelarAsync(usuarioId, unidadeId, aulaId, cancellationToken);
        }

        if (solicitacao.Observacoes is not null)
        {
            var agora = timeProvider.GetUtcNow().UtcDateTime;
            var aulaAtualizada = new Aula(
                aulaId,
                contexto.Valor.OrganizacaoId,
                unidadeId,
                existente.TurmaId,
                Guid.Empty, // turma_horario_id nao necessario para update
                existente.Data,
                existente.HoraInicio,
                existente.HoraFim,
                existente.Capacidade,
                existente.AulaId, // criado_por placeholder
                existente.Data.ToDateTime(TimeOnly.MinValue).ToUniversalTime(), // criado_em placeholder
                existente.Observacoes);

            aulaAtualizada.AtualizarObservacoes(
                solicitacao.Observacoes, usuarioId, agora);

            var sucesso = await repositorio.AtualizarAsync(aulaAtualizada, cancellationToken);

            if (sucesso)
            {
                logger.LogInformation(
                    "AtualizarAula concluído: aula {AulaId}", aulaId);
            }

            return sucesso
                ? new(EstadoAulasUnidade.Sucesso, Contexto: contexto.Valor)
                : new(EstadoAulasUnidade.Falha);
        }

        return new(EstadoAulasUnidade.Sucesso, Contexto: contexto.Valor);
    }

    public async Task<ResultadoAulasUnidadeSimples> ConcluirAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (aulaId == Guid.Empty)
            return new(EstadoAulasUnidade.AulaNaoEncontrada);

        var existente = await repositorio.ObterAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, aulaId, cancellationToken);
        if (existente is null)
            return new(EstadoAulasUnidade.AulaNaoEncontrada);

        if (existente.Status != StatusAula.Programada)
            return new(EstadoAulasUnidade.AulaNaoProgramada);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var aula = new Aula(
            aulaId,
            contexto.Valor.OrganizacaoId,
            unidadeId,
            existente.TurmaId,
            Guid.Empty,
            existente.Data,
            existente.HoraInicio,
            existente.HoraFim,
            existente.Capacidade,
            usuarioId,
            agora,
            existente.Observacoes);

        aula.Concluir(usuarioId, agora);

        var sucesso = await repositorio.AtualizarAsync(aula, cancellationToken);

        if (sucesso)
        {
            logger.LogInformation(
                "ConcluirAula concluído: aula {AulaId}", aulaId);
        }

        return sucesso
            ? new(EstadoAulasUnidade.Sucesso, Contexto: contexto.Valor)
            : new(EstadoAulasUnidade.Falha);
    }

    public async Task<ResultadoAulasUnidadeSimples> CancelarAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (aulaId == Guid.Empty)
            return new(EstadoAulasUnidade.AulaNaoEncontrada);

        var existente = await repositorio.ObterAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, aulaId, cancellationToken);
        if (existente is null)
            return new(EstadoAulasUnidade.AulaNaoEncontrada);

        if (existente.Status != StatusAula.Programada)
            return new(EstadoAulasUnidade.AulaNaoProgramada);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var aula = new Aula(
            aulaId,
            contexto.Valor.OrganizacaoId,
            unidadeId,
            existente.TurmaId,
            Guid.Empty,
            existente.Data,
            existente.HoraInicio,
            existente.HoraFim,
            existente.Capacidade,
            usuarioId,
            agora,
            existente.Observacoes);

        aula.Cancelar(usuarioId, agora);

        var sucesso = await repositorio.AtualizarAsync(aula, cancellationToken);

        if (sucesso)
        {
            logger.LogInformation(
                "CancelarAula concluído: aula {AulaId}", aulaId);
        }

        return sucesso
            ? new(EstadoAulasUnidade.Sucesso, Contexto: contexto.Valor)
            : new(EstadoAulasUnidade.Falha);
    }

    public async Task<ResultadoAulasUnidade<IReadOnlyList<AlunoPresencaResumo>>> ListarAlunosParaChamadaAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (aulaId == Guid.Empty)
            return new(EstadoAulasUnidade.AulaNaoEncontrada);

        var detalhe = await repositorio.ObterAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, aulaId, cancellationToken);
        if (detalhe is null)
            return new(EstadoAulasUnidade.AulaNaoEncontrada);

        var alunos = await repositorio.ListarAlunosParaChamadaAsync(
            contexto.Valor.OrganizacaoId, unidadeId, aulaId, cancellationToken);

        return new(EstadoAulasUnidade.Sucesso, alunos, contexto.Valor);
    }

    public async Task<ResultadoAulasUnidadeSimples> RegistrarPresencaAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId, Guid alunoId,
        RegistrarPresencaSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);

        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (aulaId == Guid.Empty || alunoId == Guid.Empty)
            return new(EstadoAulasUnidade.DadosInvalidos);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var presencaId = Guid.NewGuid();

        var presenca = new Presenca(
            presencaId,
            contexto.Valor!.OrganizacaoId,
            unidadeId,
            aulaId,
            alunoId,
            Guid.Empty, // matricula_id — resolvido pelo repositorio/trigger
            solicitacao.Status,
            usuarioId,
            agora,
            solicitacao.Observacoes);

        if (solicitacao.ChegouAs is not null || solicitacao.SaiuAs is not null)
        {
            presenca.RegistrarHorarios(solicitacao.ChegouAs, solicitacao.SaiuAs, agora);
        }

        var sucesso = await repositorio.RegistrarPresencaAsync(presenca, cancellationToken);

        if (sucesso)
        {
            logger.LogInformation(
                "RegistrarPresenca concluído: aula {AulaId} aluno {AlunoId}",
                aulaId, alunoId);
        }

        return sucesso
            ? new(EstadoAulasUnidade.Sucesso, Contexto: contexto.Valor)
            : new(EstadoAulasUnidade.Falha);
    }

    public async Task<ResultadoAulasUnidadeSimples> RegistrarPresencasEmLoteAsync(
        Guid usuarioId, Guid unidadeId, Guid aulaId,
        IReadOnlyList<RegistroPresencaLoteItem> registros,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (aulaId == Guid.Empty || registros is null || registros.Count == 0)
            return new(EstadoAulasUnidade.DadosInvalidos);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var presencas = new List<Presenca>(registros.Count);

        foreach (var registro in registros)
        {
            if (registro.AlunoId == Guid.Empty)
                continue;

            var presenca = new Presenca(
                Guid.NewGuid(),
                contexto.Valor!.OrganizacaoId,
                unidadeId,
                aulaId,
                registro.AlunoId,
                Guid.Empty, // matricula_id — resolvido pelo repositorio/trigger
                registro.Status,
                usuarioId,
                agora,
                registro.Observacoes);

            if (registro.ChegouAs is not null || registro.SaiuAs is not null)
            {
                presenca.RegistrarHorarios(registro.ChegouAs, registro.SaiuAs, agora);
            }

            presencas.Add(presenca);
        }

        if (presencas.Count == 0)
            return new(EstadoAulasUnidade.DadosInvalidos);

        var sucesso = await repositorio.RegistrarPresencasEmLoteAsync(
            presencas, cancellationToken);

        if (sucesso)
        {
            logger.LogInformation(
                "RegistrarPresencasLote concluído: aula {AulaId} — {Count} registros",
                aulaId, presencas.Count);
        }

        return sucesso
            ? new(EstadoAulasUnidade.Sucesso, Contexto: contexto.Valor)
            : new(EstadoAulasUnidade.Falha);
    }

    public async Task<ResultadoAulasUnidade<IReadOnlyList<FrequenciaAlunoResumo>>> ObterFrequenciaAsync(
        Guid usuarioId, Guid unidadeId, Guid? turmaId,
        DateOnly dataInicio, DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        if (contexto.Estado != EstadoAulasUnidade.Sucesso)
            return new(contexto.Estado);

        if (dataInicio > dataFim)
            return new(EstadoAulasUnidade.DadosInvalidos);

        var frequencia = await repositorio.ObterFrequenciaAsync(
            contexto.Valor!.OrganizacaoId, unidadeId, turmaId,
            dataInicio, dataFim, cancellationToken);

        return new(EstadoAulasUnidade.Sucesso, frequencia, contexto.Valor);
    }

    private async Task<ResultadoAulasUnidade<ContextoAulasResumo>> ObterContextoAsync(
        Guid usuarioId, Guid unidadeId, bool exigirGerenciamento,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty || unidadeId == Guid.Empty)
            return new(EstadoAulasUnidade.SemAcesso);

        var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId, cancellationToken);
        if (unidade is null)
            return new(EstadoAulasUnidade.UnidadeNaoEncontrada);

        var governanca = await governancaOperacional.ObterAsync(
            usuarioId, unidade.OrganizacaoId, unidadeId, cancellationToken);

        var autorizado = exigirGerenciamento
            ? governanca.PodeGerenciarTurmas
            : governanca.PodeAcessar;

        if (!autorizado)
            return new(EstadoAulasUnidade.SemAcesso);

        return new(EstadoAulasUnidade.Sucesso, new(
            unidade.OrganizacaoId, unidadeId, unidade.Nome,
            governanca.PodeGerenciarTurmas));
    }
}

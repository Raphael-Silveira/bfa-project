using BFA.Application.AlunoArea;
using BFA.Domain.Cobrancas;
using Microsoft.Extensions.Logging;

namespace BFA.Application.AlunoArea;

public sealed class AlunoAreaServico(
    IAlunoAreaRepositorio repositorio,
    ILogger<AlunoAreaServico> logger)
    : IAlunoAreaServico
{
    public async Task<DashboardAlunoDto?> ObterDashboardAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterDashboard iniciado para {UsuarioId} na unidade {UnidadeId}",
            usuarioId, unidadeId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            logger.LogWarning("Aluno não encontrado para {UsuarioId} na unidade {UnidadeId}",
                usuarioId, unidadeId);
            return null;
        }

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var fimMes = inicioMes.AddMonths(1).AddDays(-1);

        var aulas = await repositorio.ListarAulasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id,
            hoje.AddDays(-30), hoje, cancellationToken);

        var proximaAula = aulas
            .Where(a => a.Data >= hoje)
            .OrderBy(a => a.Data)
            .ThenBy(a => a.HoraInicio)
            .FirstOrDefault();

        var totalAulas = await repositorio.ContarAulasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id,
            inicioMes, fimMes, cancellationToken);

        var presentes = await repositorio.ContarPresencasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id,
            inicioMes, fimMes, cancellationToken);

        var percentual = totalAulas > 0
            ? Math.Round((decimal)presentes / totalAulas * 100, 1)
            : 0m;

        var cobrancas = await repositorio.ListarCobrancasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id, cancellationToken);

        var totalPendente = cobrancas
            .Where(c => c.Status is StatusCobranca.Pendente or StatusCobranca.Atrasada)
            .Sum(c => c.Valor - c.ValorPago);

        var nomeUnidade = await repositorio.ObterNomeUnidadeAsync(
            unidadeId, cancellationToken);

        var resultado = new DashboardAlunoDto(
            new PerfilAlunoDto(
                aluno.Aluno.Id,
                aluno.Aluno.NomeCompleto,
                aluno.Aluno.Cpf,
                aluno.Aluno.Telefone,
                aluno.Aluno.Email,
                aluno.Aluno.DataNascimento,
                aluno.Aluno.Ativo),
            nomeUnidade ?? "Unidade",
            proximaAula.Data != default
                ? $"{proximaAula.Data:dd/MM} - {proximaAula.TurmaNome} ({proximaAula.HoraInicio}–{proximaAula.HoraFim})"
                : null,
            $"{percentual}%",
            FormatBrl(totalPendente),
            totalAulas);

        logger.LogDebug("ObterDashboard concluído para {AlunoId}", aluno.Aluno.Id);
        return resultado;
    }

    public async Task<PerfilAlunoDto?> ObterPerfilAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterPerfil iniciado para {UsuarioId}", usuarioId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            return null;
        }

        return new PerfilAlunoDto(
            aluno.Aluno.Id,
            aluno.Aluno.NomeCompleto,
            aluno.Aluno.Cpf,
            aluno.Aluno.Telefone,
            aluno.Aluno.Email,
            aluno.Aluno.DataNascimento,
            aluno.Aluno.Ativo);
    }

    public async Task<IReadOnlyList<MatriculaAlunoDto>> ObterMatriculasAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterMatriculas iniciado para {UsuarioId}", usuarioId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            return [];
        }

        var matriculas = await repositorio.ListarMatriculasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id, cancellationToken);

        return matriculas.Select(m => new MatriculaAlunoDto(
            m.Id,
            m.PlanoVersaoId.ToString()[..8],
            m.Status.ToString(),
            m.DataInicio,
            m.DataFimPrevista,
            m.DataFimReal,
            m.ValorMensalContratado,
            []))
            .ToList();
    }

    public async Task<IReadOnlyList<AulaAlunoDto>> ObterAgendaAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterAgenda iniciado para {UsuarioId}", usuarioId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            return [];
        }

        var aulas = await repositorio.ListarAulasAsync(
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id,
            dataInicio, dataFim, cancellationToken);

        return aulas.Select(a => new AulaAlunoDto(
            Guid.Empty,
            a.Data,
            a.HoraInicio,
            a.HoraFim,
            a.TurmaNome,
            a.Status)).ToList();
    }

    public async Task<FrequenciaResumoDto?> ObterFrequenciaAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterFrequencia iniciado para {UsuarioId}", usuarioId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            return null;
        }

        var orgId = aluno.Aluno.OrganizacaoId;
        var id = aluno.Aluno.Id;

        var total = await repositorio.ContarAulasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var presentes = await repositorio.ContarPresencasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var ausentes = await repositorio.ContarAusenciasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var justificados = await repositorio.ContarJustificativasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var percentual = total > 0
            ? Math.Round((decimal)presentes / total * 100, 1)
            : 0m;

        var presencas = await repositorio.ListarPresencasAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        return new FrequenciaResumoDto(
            total,
            presentes,
            ausentes,
            justificados,
            percentual);
    }

    public async Task<FinanceiroResumoDto?> ObterFinanceiroAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("ObterFinanceiro iniciado para {UsuarioId}", usuarioId);

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            return null;
        }

        var orgId = aluno.Aluno.OrganizacaoId;
        var id = aluno.Aluno.Id;

        var cobrancas = await repositorio.ListarCobrancasAsync(
            orgId, unidadeId, id, cancellationToken);

        var pagamentos = await repositorio.ListarPagamentosAsync(
            orgId, unidadeId, id, cancellationToken);

        var totalPendente = cobrancas
            .Where(c => c.Status is StatusCobranca.Pendente or StatusCobranca.Atrasada)
            .Sum(c => c.Valor - c.ValorPago);

        var totalPago = cobrancas
            .Where(c => c.Status == StatusCobranca.Paga)
            .Sum(c => c.ValorPago);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        return new FinanceiroResumoDto(
            FormatBrl(totalPendente),
            FormatBrl(totalPago),
            cobrancas.Select(c => new CobrancaAlunoDto(
                c.Id,
                c.Descricao,
                c.Tipo.ToString(),
                FormatBrl(c.Valor),
                FormatBrl(c.ValorPago),
                FormatBrl(c.Valor - c.ValorPago),
                c.DataVencimento,
                c.Status.ToString(),
                c.Status == StatusCobranca.Atrasada
                    ? (int)(hoje.ToDateTime(TimeOnly.MinValue) - c.DataVencimento.ToDateTime(TimeOnly.MinValue)).TotalDays
                    : 0)).ToList(),
            pagamentos.Select(p => new PagamentoAlunoDto(
                p.DataPagamento,
                FormatBrl(p.Valor),
                p.FormaPagamento.ToString())).ToList());
    }

    private static string FormatBrl(decimal valor)
        => $"R$ {valor:N2}";
}

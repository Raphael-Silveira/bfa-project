using BFA.Application.AlunoArea;
using BFA.Domain.Aulas;
using BFA.Domain.Cobrancas;
using BFA.Domain.Matriculas;
using BFA.Domain.Alunos;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BFA.Application.AlunoArea;

public sealed class AlunoAreaServico(
    IAlunoAreaRepositorio repositorio,
    ILogger<AlunoAreaServico> logger,
    TimeProvider timeProvider,
    TimeZoneInfo timeZoneInfo)
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
            hoje, hoje.AddDays(30), cancellationToken);

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
            aluno.Aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id, null, null, cancellationToken);

        var matriculaAtiva = (await repositorio.ListarMatriculasAsync(
                aluno.Aluno.OrganizacaoId,
                unidadeId,
                aluno.Aluno.Id,
                cancellationToken))
            .FirstOrDefault(m => m.Status == StatusMatricula.Ativa);

        var totalPendente = cobrancas
            .Where(c => c.Status is StatusCobranca.Pendente or StatusCobranca.Atrasada)
            .Sum(c => c.Valor - c.ValorPago);

        var nomeUnidade = await repositorio.ObterNomeUnidadeAsync(
            aluno.Aluno.OrganizacaoId,
            unidadeId,
            cancellationToken);

        Guid? proximaAulaId = proximaAula.AulaId == Guid.Empty ? null : proximaAula.AulaId;
        var podeAlterarConfirmacao = proximaAulaId.HasValue
            && AulaAindaElegivel(proximaAula.Data, proximaAula.HoraInicio, proximaAula.Status);

        var resultado = new DashboardAlunoDto(
            aluno.Aluno.OrganizacaoId,
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
            totalAulas,
            matriculaAtiva is null ? null : MapearMatricula(matriculaAtiva),
            proximaAulaId,
            proximaAula.ConfirmacaoAtiva,
            podeAlterarConfirmacao);

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

    public async Task<ResultadoAtualizacaoPerfilAluno> AtualizarPerfilAsync(
        Guid usuarioId,
        Guid unidadeId,
        string? telefone,
        string? email,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("AtualizarPerfil iniciado para {UsuarioId} na unidade {UnidadeId}",
            usuarioId, unidadeId);

        var emailNormalizado = email?.Trim();
        if (string.IsNullOrWhiteSpace(emailNormalizado)
            || emailNormalizado.Length > 256
            || !new EmailAddressAttribute().IsValid(emailNormalizado))
        {
            logger.LogWarning("AtualizarPerfil rejeitado por e-mail inválido para {UsuarioId}", usuarioId);
            return ResultadoAtualizacaoPerfilAluno.EmailInvalido;
        }

        string? telefoneNormalizado;
        try
        {
            telefoneNormalizado = TelefoneBrasileiro.Normalizar(telefone);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("AtualizarPerfil rejeitado por telefone inválido para {UsuarioId}", usuarioId);
            return ResultadoAtualizacaoPerfilAluno.TelefoneInvalido;
        }

        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
        {
            logger.LogWarning("AtualizarPerfil não encontrado para {UsuarioId} na unidade {UnidadeId}",
                usuarioId, unidadeId);
            return ResultadoAtualizacaoPerfilAluno.NaoEncontrado;
        }

        var atualizado = await repositorio.AtualizarPerfilAsync(
            aluno.OrganizacaoId,
            unidadeId,
            aluno.Aluno.Id,
            telefoneNormalizado,
            emailNormalizado,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        if (!atualizado)
        {
            logger.LogWarning("AtualizarPerfil perdeu o vínculo autorizado para {UsuarioId}", usuarioId);
            return ResultadoAtualizacaoPerfilAluno.NaoEncontrado;
        }

        logger.LogInformation("AtualizarPerfil concluído para {AlunoId}", aluno.Aluno.Id);
        return ResultadoAtualizacaoPerfilAluno.Sucesso;
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

        return matriculas.Select(MapearMatricula)
            .ToList();
    }

    private bool AulaAindaElegivel(DateOnly data, string horaInicio, string status)
    {
        if (!string.Equals(status, StatusAula.Programada.ToString(), StringComparison.Ordinal))
            return false;

        if (!TimeOnly.TryParseExact(
                horaInicio, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var hora))
            return false;

        var agoraLocal = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZoneInfo);
        var inicioLocal = DateTime.SpecifyKind(data.ToDateTime(hora), DateTimeKind.Unspecified);
        return agoraLocal.DateTime < inicioLocal;
    }

    private static MatriculaAlunoDto MapearMatricula(MatriculaAlunoConsulta matricula) =>
        new(
            matricula.MatriculaId,
            matricula.PlanoNome,
            matricula.FrequenciaSemanal,
            matricula.Status.ToString(),
            matricula.DataInicio,
            matricula.DataFimPrevista,
            matricula.DataFimReal,
            matricula.ValorMensal,
            matricula.Horarios);

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
            a.AulaId,
            a.Data,
            a.HoraInicio,
            a.HoraFim,
            a.TurmaNome,
            a.Status,
            a.ConfirmacaoAtiva,
            AulaAindaElegivel(a.Data, a.HoraInicio, a.Status))).ToList();
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
            percentual,
            presencas.Select(p => new PresencaAlunoDto(
                p.Data,
                p.TurmaNome,
                p.HoraInicio,
                p.HoraFim,
                p.Status,
                p.Observacoes)).ToList());
    }

    public async Task<FinanceiroResumoDto?> ObterFinanceiroAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly? dataInicio,
        DateOnly? dataFim,
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
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

        var pagamentos = await repositorio.ListarPagamentosAsync(
            orgId, unidadeId, id, dataInicio, dataFim, cancellationToken);

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
                p.Pagamento.DataPagamento,
                p.Tipo.ToString(),
                FormatBrl(p.Pagamento.Valor),
                p.Pagamento.FormaPagamento.ToString())).ToList());
    }

    private static string FormatBrl(decimal valor)
        => $"R$ {valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"))}";
}

using BFA.Domain.Aulas;
using Microsoft.Extensions.Logging;

namespace BFA.Application.AlunoArea;

public sealed class ConfirmacaoAulaAlunoServico(
    IAlunoAreaRepositorio repositorio,
    TimeProvider timeProvider,
    TimeZoneInfo timeZoneInfo,
    ILogger<ConfirmacaoAulaAlunoServico> logger)
    : IConfirmacaoAulaAlunoServico
{
    public Task<ResultadoConfirmacaoAula> ConfirmarAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken) =>
        AlterarAsync(usuarioId, unidadeId, aulaId, confirmar: true, cancellationToken);

    public Task<ResultadoConfirmacaoAula> CancelarAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken) =>
        AlterarAsync(usuarioId, unidadeId, aulaId, confirmar: false, cancellationToken);

    private async Task<ResultadoConfirmacaoAula> AlterarAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid aulaId,
        bool confirmar,
        CancellationToken cancellationToken)
    {
        var aluno = await repositorio.ObterAlunoPorUsuarioAsync(
            usuarioId, unidadeId, cancellationToken);

        if (aluno is null)
            return ResultadoConfirmacaoAula.NaoElegivel;

        var aula = await repositorio.ObterAulaParaConfirmacaoAsync(
            aluno.OrganizacaoId, unidadeId, aluno.Aluno.Id, aulaId, cancellationToken);

        if (aula is null)
            return ResultadoConfirmacaoAula.NaoEncontrada;

        if (aula.Status != StatusAula.Programada)
            return ResultadoConfirmacaoAula.JanelaEncerrada;

        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;
        var agoraLocal = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZoneInfo);
        var inicioLocal = DateTime.SpecifyKind(
            aula.Data.ToDateTime(aula.HoraInicio), DateTimeKind.Unspecified);

        if (agoraLocal.DateTime >= inicioLocal)
            return ResultadoConfirmacaoAula.JanelaEncerrada;

        if (!confirmar && !aula.ConfirmacaoExiste)
            return ResultadoConfirmacaoAula.Sucesso;

        logger.LogInformation(
            "{Acao} confirmação da aula {AulaId} pelo aluno {AlunoId}",
            confirmar ? "Confirmando" : "Cancelando", aulaId, aluno.Aluno.Id);

        if (confirmar)
        {
            await repositorio.ConfirmarAulaAsync(
                aluno.OrganizacaoId, unidadeId, aulaId, aluno.Aluno.Id,
                agoraUtc, cancellationToken);
        }
        else
        {
            await repositorio.CancelarConfirmacaoAulaAsync(
                aluno.OrganizacaoId, unidadeId, aulaId, aluno.Aluno.Id,
                agoraUtc, cancellationToken);
        }

        return ResultadoConfirmacaoAula.Sucesso;
    }
}

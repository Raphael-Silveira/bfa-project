using BFA.Application.AlunoArea;
using BFA.Domain.Acessos;
using BFA.Domain.Alunos;
using BFA.Domain.Aulas;
using BFA.Domain.Cobrancas;
using BFA.Domain.Matriculas;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.AlunoArea;

public sealed class AlunoAreaRepositorio(BfaDbContext dbContext)
    : IAlunoAreaRepositorio
{
    public async Task<AlunoComUnidade?> ObterAlunoPorUsuarioAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return await (from vinculo in dbContext.VinculosAcesso.AsNoTracking()
                      join aluno in dbContext.Alunos.AsNoTracking()
                          on new { vinculo.OrganizacaoId, UsuarioId = (Guid?)vinculo.UsuarioId }
                          equals new { aluno.OrganizacaoId, aluno.UsuarioId }
                      where vinculo.UsuarioId == usuarioId
                          && vinculo.UnidadeId == unidadeId
                          && vinculo.Perfil == PerfilAcesso.Aluno
                          && vinculo.Ativo
                          && dbContext.Unidades.Any(unidade =>
                              unidade.OrganizacaoId == vinculo.OrganizacaoId
                              && unidade.Id == unidadeId
                              && unidade.Ativa)
                          && dbContext.Organizacoes.Any(organizacao =>
                              organizacao.Id == vinculo.OrganizacaoId
                              && organizacao.Ativa)
                          && aluno.Ativo
                      select new AlunoComUnidade(
                          aluno,
                          vinculo.OrganizacaoId,
                          unidadeId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AtualizarPerfilAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        string? telefone,
        string? email,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken)
    {
        var aluno = await dbContext.Alunos.FirstOrDefaultAsync(aluno =>
            aluno.Id == alunoId
            && aluno.OrganizacaoId == organizacaoId
            && aluno.Ativo
            && dbContext.Organizacoes.Any(organizacao =>
                organizacao.Id == organizacaoId && organizacao.Ativa)
            && dbContext.Unidades.Any(unidade =>
                unidade.Id == unidadeId
                && unidade.OrganizacaoId == organizacaoId
                && unidade.Ativa)
            && aluno.UsuarioId != null
            && dbContext.VinculosAcesso.Any(vinculo =>
                vinculo.UsuarioId == aluno.UsuarioId
                && vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.Perfil == PerfilAcesso.Aluno
                && vinculo.Ativo), cancellationToken);

        if (aluno is null)
        {
            return false;
        }

        aluno.AtualizarContato(telefone, email, atualizadoEmUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<MatriculaAlunoConsulta>> ListarMatriculasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        var matriculas = await dbContext.Matriculas.AsNoTracking()
            .Where(m => m.OrganizacaoId == organizacaoId
                && m.UnidadeId == unidadeId
                && m.AlunoId == alunoId)
            .OrderByDescending(m => m.DataInicio)
            .ToListAsync(cancellationToken);
                var versaoIds = matriculas.Select(m => m.PlanoVersaoId).Distinct().ToArray();
                var versoes = await dbContext.PlanosVersoes.AsNoTracking()
                    .Where(v => versaoIds.Contains(v.Id) && v.OrganizacaoId == organizacaoId)
                    .ToDictionaryAsync(v => v.Id, cancellationToken);
                var planoIds = versoes.Values.Select(v => v.PlanoId).Distinct().ToArray();
                var planos = await dbContext.Planos.AsNoTracking()
                    .Where(p => planoIds.Contains(p.Id) && p.OrganizacaoId == organizacaoId)
                    .ToDictionaryAsync(p => p.Id, cancellationToken);
                var matriculaIds = matriculas.Select(m => m.Id).ToArray();
                var horarios = await (from matriculaHorario in dbContext.MatriculasHorarios.AsNoTracking()
                                      join turmaHorario in dbContext.TurmasHorarios.AsNoTracking()
                                          on new
                                          {
                                              matriculaHorario.OrganizacaoId,
                                              matriculaHorario.UnidadeId,
                                              matriculaHorario.TurmaHorarioId
                                          }
                                          equals new
                                          {
                                              turmaHorario.OrganizacaoId,
                                              turmaHorario.UnidadeId,
                                              TurmaHorarioId = turmaHorario.Id
                                          }
                                      join turma in dbContext.Turmas.AsNoTracking()
                                          on new { turmaHorario.OrganizacaoId, turmaHorario.UnidadeId, turmaHorario.TurmaId }
                                          equals new
                                          {
                                              turma.OrganizacaoId,
                                              turma.UnidadeId,
                                              TurmaId = turma.Id
                                          }
                                      where matriculaHorario.OrganizacaoId == organizacaoId
                                          && matriculaHorario.UnidadeId == unidadeId
                                          && matriculaIds.Contains(matriculaHorario.MatriculaId)
                                      select new
                                      {
                                          matriculaHorario.MatriculaId,
                                          turmaHorario.DiaSemana,
                                          turmaHorario.HoraInicio,
                                          turmaHorario.HoraFim,
                                          TurmaNome = turma.Nome
                                      }).ToListAsync(cancellationToken);

        return matriculas.Select(m =>
                {
                    var versao = versoes[m.PlanoVersaoId];
                    var planoNome = planos[versao.PlanoId].Nome;
                    var horariosMatricula = horarios
                        .Where(h => h.MatriculaId == m.Id)
                        .OrderBy(h => h.DiaSemana)
                        .ThenBy(h => h.HoraInicio)
                        .Select(h => new HorarioMatriculaDto(
                            h.DiaSemana.ToString(),
                            h.HoraInicio.ToString("HH:mm"),
                            h.HoraFim.ToString("HH:mm"),
                            h.TurmaNome))
                        .ToArray();

                    return new MatriculaAlunoConsulta(
                        m.Id,
                        planoNome,
                        versao.FrequenciaSemanal,
                        m.Status,
                        m.DataInicio,
                        m.DataFimPrevista,
                        m.DataFimReal,
                        m.ValorMensalContratado,
                        horariosMatricula);
                }).ToArray();
    }

    public async Task<IReadOnlyList<(Guid AulaId, string TurmaNome, DateOnly Data, string HoraInicio, string HoraFim, string Status, bool ConfirmacaoAtiva)>> ListarAulasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        return await (from aula in dbContext.Aulas.AsNoTracking()
                      join turma in dbContext.Turmas.AsNoTracking()
                          on new { aula.OrganizacaoId, aula.UnidadeId, aula.TurmaId }
                          equals new
                          {
                              turma.OrganizacaoId,
                              turma.UnidadeId,
                              TurmaId = turma.Id
                          }
                      where aula.OrganizacaoId == organizacaoId
                          && aula.UnidadeId == unidadeId
                          && aula.Data >= dataInicio
                          && aula.Data <= dataFim
                          && dbContext.MatriculasHorarios.Any(matriculaHorario =>
                              matriculaHorario.OrganizacaoId == organizacaoId
                              && matriculaHorario.UnidadeId == unidadeId
                              && matriculaHorario.TurmaHorarioId == aula.TurmaHorarioId
                              && matriculaHorario.VigenciaInicio <= aula.Data
                              && (matriculaHorario.VigenciaFim == null
                                  || matriculaHorario.VigenciaFim >= aula.Data)
                              && dbContext.Matriculas.Any(matricula =>
                                  matricula.Id == matriculaHorario.MatriculaId
                                  && matricula.OrganizacaoId == organizacaoId
                                  && matricula.UnidadeId == unidadeId
                                  && matricula.AlunoId == alunoId
                                  && matricula.Status == StatusMatricula.Ativa
                                  && matricula.DataInicio <= aula.Data
                                  && matricula.DataFimPrevista >= aula.Data))
                      orderby aula.Data, aula.HoraInicio
                      select new ValueTuple<Guid, string, DateOnly, string, string, string, bool>(
                          aula.Id,
                          turma.Nome,
                          aula.Data,
                          aula.HoraInicio.ToString("HH:mm"),
                          aula.HoraFim.ToString("HH:mm"),
                          aula.Status.ToString(),
                          dbContext.ConfirmacoesAulaAluno.Any(confirmacao =>
                              confirmacao.OrganizacaoId == organizacaoId
                              && confirmacao.UnidadeId == unidadeId
                              && confirmacao.AulaId == aula.Id
                              && confirmacao.AlunoId == alunoId
                              && confirmacao.Ativa)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(DateOnly Data, string TurmaNome, string HoraInicio, string HoraFim, string Status, string? Observacoes)>> ListarPresencasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        return await (from presenca in dbContext.Presencas.AsNoTracking()
                      join aula in dbContext.Aulas.AsNoTracking()
                          on new { presenca.OrganizacaoId, presenca.UnidadeId, presenca.AulaId }
                          equals new { aula.OrganizacaoId, aula.UnidadeId, AulaId = aula.Id }
                      join turma in dbContext.Turmas.AsNoTracking()
                          on new { aula.OrganizacaoId, aula.UnidadeId, aula.TurmaId }
                          equals new
                          {
                              turma.OrganizacaoId,
                              turma.UnidadeId,
                              TurmaId = turma.Id
                          }
                      where presenca.OrganizacaoId == organizacaoId
                          && presenca.UnidadeId == unidadeId
                          && presenca.AlunoId == alunoId
                          && aula.Data >= dataInicio
                          && aula.Data <= dataFim
                      orderby aula.Data descending
                      select new ValueTuple<DateOnly, string, string, string, string, string?>(
                          aula.Data,
                          turma.Nome,
                          aula.HoraInicio.ToString("HH:mm"),
                          aula.HoraFim.ToString("HH:mm"),
                          presenca.Status.ToString(),
                          presenca.Observacoes))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarAulasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        return await ConsultaAulasRelacionadas(
                organizacaoId,
                unidadeId,
                alunoId,
                dataInicio,
                dataFim,
                somenteNaoCanceladas: true)
            .Select(aula => aula.AulaId)
            .CountAsync(cancellationToken);
    }

    public async Task<int> ContarPresencasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        return await (from presenca in dbContext.Presencas.AsNoTracking()
                      join aula in dbContext.Aulas.AsNoTracking()
                          on presenca.AulaId equals aula.Id
                      where presenca.OrganizacaoId == organizacaoId
                          && presenca.UnidadeId == unidadeId
                          && presenca.AlunoId == alunoId
                          && presenca.Status == StatusPresenca.Presente
                          && aula.Data >= dataInicio
                          && aula.Data <= dataFim
                      select presenca.Id)
            .CountAsync(cancellationToken);
    }

    public async Task<int> ContarAusenciasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        return await (from presenca in dbContext.Presencas.AsNoTracking()
                      join aula in dbContext.Aulas.AsNoTracking()
                          on presenca.AulaId equals aula.Id
                      where presenca.OrganizacaoId == organizacaoId
                          && presenca.UnidadeId == unidadeId
                          && presenca.AlunoId == alunoId
                          && presenca.Status == StatusPresenca.Ausente
                          && aula.Data >= dataInicio
                          && aula.Data <= dataFim
                      select presenca.Id)
            .CountAsync(cancellationToken);
    }

    public async Task<int> ContarJustificativasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken)
    {
        return await (from presenca in dbContext.Presencas.AsNoTracking()
                      join aula in dbContext.Aulas.AsNoTracking()
                          on presenca.AulaId equals aula.Id
                      where presenca.OrganizacaoId == organizacaoId
                          && presenca.UnidadeId == unidadeId
                          && presenca.AlunoId == alunoId
                          && presenca.Status == StatusPresenca.Justificado
                          && aula.Data >= dataInicio
                          && aula.Data <= dataFim
                      select presenca.Id)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Cobranca>> ListarCobrancasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly? dataInicio,
        DateOnly? dataFim,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Cobrancas.AsNoTracking()
            .Where(c => c.OrganizacaoId == organizacaoId
                && c.UnidadeId == unidadeId
                && c.AlunoId == alunoId);

        if (dataInicio is not null)
            consulta = consulta.Where(c => c.DataVencimento >= dataInicio.Value);

        if (dataFim is not null)
            consulta = consulta.Where(c => c.DataVencimento <= dataFim.Value);

        return await consulta
            .OrderByDescending(c => c.DataEmissao)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(Pagamento Pagamento, TipoCobranca Tipo)>> ListarPagamentosAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly? dataInicio,
        DateOnly? dataFim,
        CancellationToken cancellationToken)
    {
        var consulta = from pagamento in dbContext.Pagamentos.AsNoTracking()
                      join cobranca in dbContext.Cobrancas.AsNoTracking()
                          on new { pagamento.OrganizacaoId, pagamento.UnidadeId, pagamento.CobrancaId }
                          equals new { cobranca.OrganizacaoId, cobranca.UnidadeId, CobrancaId = cobranca.Id }
                      where pagamento.OrganizacaoId == organizacaoId
                          && pagamento.UnidadeId == unidadeId
                          && cobranca.AlunoId == alunoId
                      select new { Pagamento = pagamento, cobranca.Tipo };

        if (dataInicio is not null)
            consulta = consulta.Where(item => item.Pagamento.DataPagamento >= dataInicio.Value);

        if (dataFim is not null)
            consulta = consulta.Where(item => item.Pagamento.DataPagamento <= dataFim.Value);

        var registros = await consulta
            .OrderByDescending(item => item.Pagamento.DataPagamento)
            .ToListAsync(cancellationToken);

        return registros.Select(item => (item.Pagamento, item.Tipo)).ToList();
    }

    public async Task<string?> ObterNomeUnidadeAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Unidades.AsNoTracking()
            .Where(u => u.OrganizacaoId == organizacaoId && u.Id == unidadeId)
            .Select(u => u.Nome)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AulaConfirmacaoConsulta?> ObterAulaParaConfirmacaoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        Guid aulaId,
        CancellationToken cancellationToken)
    {
        var aula = await (from item in dbContext.Aulas.AsNoTracking()
                          where item.OrganizacaoId == organizacaoId
                              && item.UnidadeId == unidadeId
                              && item.Id == aulaId
                              && dbContext.MatriculasHorarios.Any(grade =>
                                  grade.OrganizacaoId == organizacaoId
                                  && grade.UnidadeId == unidadeId
                                  && grade.TurmaHorarioId == item.TurmaHorarioId
                                  && grade.VigenciaInicio <= item.Data
                                  && (grade.VigenciaFim == null || grade.VigenciaFim >= item.Data)
                                  && dbContext.Matriculas.Any(matricula =>
                                      matricula.OrganizacaoId == organizacaoId
                                      && matricula.UnidadeId == unidadeId
                                      && matricula.Id == grade.MatriculaId
                                      && matricula.AlunoId == alunoId
                                      && matricula.Status == StatusMatricula.Ativa
                                      && matricula.DataInicio <= item.Data
                                      && matricula.DataFimPrevista >= item.Data))
                          select new
                          {
                              item.Id,
                              item.OrganizacaoId,
                              item.UnidadeId,
                              item.Data,
                              item.HoraInicio,
                              item.Status
                          }).FirstOrDefaultAsync(cancellationToken);

        if (aula is null)
            return null;

        var confirmacao = await dbContext.ConfirmacoesAulaAluno.AsNoTracking()
            .FirstOrDefaultAsync(item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && item.AulaId == aulaId
                && item.AlunoId == alunoId, cancellationToken);

        return new AulaConfirmacaoConsulta(
            aula.Id,
            aula.OrganizacaoId,
            aula.UnidadeId,
            aula.Data,
            aula.HoraInicio,
            aula.Status,
            confirmacao?.Ativa == true,
            confirmacao is not null);
    }

    public async Task<bool> ConfirmarAulaAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid aulaId,
        Guid alunoId,
        DateTime agoraUtc,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO confirmacoes_aula_aluno
                (id, organizacao_id, unidade_id, aula_id, aluno_id, ativa,
                 confirmada_em_utc, criado_em_utc, atualizado_em_utc)
            VALUES
                ({id}, {organizacaoId}, {unidadeId}, {aulaId}, {alunoId}, true,
                 {agoraUtc}, {agoraUtc}, {agoraUtc})
            ON CONFLICT (organizacao_id, unidade_id, aula_id, aluno_id)
            DO UPDATE SET ativa = true,
                          confirmada_em_utc = EXCLUDED.confirmada_em_utc,
                          atualizado_em_utc = EXCLUDED.atualizado_em_utc
            """, cancellationToken);
        return true;
    }

    public async Task<bool> CancelarConfirmacaoAulaAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid aulaId,
        Guid alunoId,
        DateTime agoraUtc,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO confirmacoes_aula_aluno
                (id, organizacao_id, unidade_id, aula_id, aluno_id, ativa,
                 confirmada_em_utc, criado_em_utc, atualizado_em_utc)
            VALUES
                ({id}, {organizacaoId}, {unidadeId}, {aulaId}, {alunoId}, false,
                 NULL, {agoraUtc}, {agoraUtc})
            ON CONFLICT (organizacao_id, unidade_id, aula_id, aluno_id)
            DO UPDATE SET ativa = false,
                          atualizado_em_utc = EXCLUDED.atualizado_em_utc
            """, cancellationToken);
        return true;
    }

    private IQueryable<(Guid AulaId, string TurmaNome, DateOnly Data, string HoraInicio, string HoraFim, string Status)> ConsultaAulasRelacionadas(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        DateOnly dataInicio,
        DateOnly dataFim,
        bool somenteNaoCanceladas)
    {
        return from aula in dbContext.Aulas.AsNoTracking()
               join turma in dbContext.Turmas.AsNoTracking()
                   on new { aula.OrganizacaoId, aula.UnidadeId, aula.TurmaId }
                   equals new
                   {
                       turma.OrganizacaoId,
                       turma.UnidadeId,
                       TurmaId = turma.Id
                   }
               where aula.OrganizacaoId == organizacaoId
                   && aula.UnidadeId == unidadeId
                   && aula.Data >= dataInicio
                   && aula.Data <= dataFim
                   && (!somenteNaoCanceladas || aula.Status != StatusAula.Cancelada)
                   && dbContext.MatriculasHorarios.Any(matriculaHorario =>
                       matriculaHorario.OrganizacaoId == organizacaoId
                       && matriculaHorario.UnidadeId == unidadeId
                       && matriculaHorario.TurmaHorarioId == aula.TurmaHorarioId
                       && matriculaHorario.VigenciaInicio <= aula.Data
                       && (matriculaHorario.VigenciaFim == null
                           || matriculaHorario.VigenciaFim >= aula.Data)
                       && dbContext.Matriculas.Any(matricula =>
                           matricula.Id == matriculaHorario.MatriculaId
                           && matricula.OrganizacaoId == organizacaoId
                           && matricula.UnidadeId == unidadeId
                           && matricula.AlunoId == alunoId
                           && matricula.Status == StatusMatricula.Ativa
                           && matricula.DataInicio <= aula.Data
                           && matricula.DataFimPrevista >= aula.Data))
               orderby aula.Data, aula.HoraInicio
               select new ValueTuple<Guid, string, DateOnly, string, string, string>(
                   aula.Id,
                   turma.Nome,
                   aula.Data,
                   aula.HoraInicio.ToString("HH:mm"),
                   aula.HoraFim.ToString("HH:mm"),
                   aula.Status.ToString());
    }
}

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

    public async Task<IReadOnlyList<Matricula>> ListarMatriculasAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Matriculas.AsNoTracking()
            .Where(m => m.OrganizacaoId == organizacaoId
                && m.UnidadeId == unidadeId
                && m.AlunoId == alunoId)
            .OrderByDescending(m => m.DataInicio)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(string TurmaNome, DateOnly Data, string HoraInicio, string HoraFim, string Status)>> ListarAulasAsync(
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
                somenteNaoCanceladas: false)
            .Select(aula => new ValueTuple<string, DateOnly, string, string, string>(
                aula.TurmaNome,
                aula.Data,
                aula.HoraInicio,
                aula.HoraFim,
                aula.Status))
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
        CancellationToken cancellationToken)
    {
        return await dbContext.Cobrancas.AsNoTracking()
            .Where(c => c.OrganizacaoId == organizacaoId
                && c.UnidadeId == unidadeId
                && c.AlunoId == alunoId)
            .OrderByDescending(c => c.DataEmissao)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Pagamento>> ListarPagamentosAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        return await (from pagamento in dbContext.Pagamentos.AsNoTracking()
                      join cobranca in dbContext.Cobrancas.AsNoTracking()
                          on new { pagamento.OrganizacaoId, pagamento.UnidadeId, pagamento.CobrancaId }
                          equals new { cobranca.OrganizacaoId, cobranca.UnidadeId, CobrancaId = cobranca.Id }
                      where pagamento.OrganizacaoId == organizacaoId
                          && pagamento.UnidadeId == unidadeId
                          && cobranca.AlunoId == alunoId
                      orderby pagamento.DataPagamento descending
                      select pagamento)
            .ToListAsync(cancellationToken);
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

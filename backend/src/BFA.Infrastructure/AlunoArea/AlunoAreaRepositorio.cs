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
        return await (from aula in dbContext.Aulas.AsNoTracking()
                      join turma in dbContext.Turmas.AsNoTracking()
                          on aula.TurmaId equals turma.Id
                      where aula.OrganizacaoId == organizacaoId
                          && aula.UnidadeId == unidadeId
                          && dbContext.Matriculas.Any(m =>
                              m.OrganizacaoId == organizacaoId
                              && m.UnidadeId == unidadeId
                              && m.AlunoId == alunoId)
                          && aula.Data >= dataInicio
                          && aula.Data <= dataFim
                      orderby aula.Data, aula.HoraInicio
                      select new ValueTuple<string, DateOnly, string, string, string>(
                          turma.Nome,
                          aula.Data,
                          aula.HoraInicio.ToString("HH:mm"),
                          aula.HoraFim.ToString("HH:mm"),
                          aula.Status.ToString()))
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
                          on presenca.AulaId equals aula.Id
                      join turma in dbContext.Turmas.AsNoTracking()
                          on aula.TurmaId equals turma.Id
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
        return await (from aula in dbContext.Aulas.AsNoTracking()
                      where aula.OrganizacaoId == organizacaoId
                          && aula.UnidadeId == unidadeId
                          && dbContext.Matriculas.Any(m =>
                              m.OrganizacaoId == organizacaoId
                              && m.UnidadeId == unidadeId
                              && m.AlunoId == alunoId)
                          && aula.Data >= dataInicio
                          && aula.Data <= dataFim
                          && aula.Status != StatusAula.Cancelada
                      select aula.Id)
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
                          on pagamento.CobrancaId equals cobranca.Id
                      where pagamento.OrganizacaoId == organizacaoId
                          && pagamento.UnidadeId == unidadeId
                          && cobranca.AlunoId == alunoId
                      orderby pagamento.DataPagamento descending
                      select pagamento)
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> ObterNomeUnidadeAsync(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Unidades.AsNoTracking()
            .Where(u => u.Id == unidadeId)
            .Select(u => u.Nome)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

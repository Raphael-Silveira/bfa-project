using BFA.Application.Franqueadora;
using BFA.Domain.Acessos;
using BFA.Domain.Matriculas;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Franqueadora;

public sealed class FranqueadoraAlunosConsulta(BfaDbContext dbContext)
    : IFranqueadoraAlunosConsulta
{
    public async Task<FranqueadoraAlunosResultado> ListarAsync(
        Guid usuarioId,
        Guid? unidadeId,
        string? busca,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty)
        {
            return FranqueadoraAlunosResultado.SemAcesso();
        }

        var organizacoesAdministradas = await dbContext.VinculosAcesso
            .AsNoTracking()
            .Where(vinculo => vinculo.UsuarioId == usuarioId
                && vinculo.Ativo
                && vinculo.Perfil == PerfilAcesso.AdministradorRede
                && vinculo.UnidadeId == null)
            .Select(vinculo => vinculo.OrganizacaoId)
            .Distinct()
            .Take(2)
            .ToArrayAsync(cancellationToken);

        if (organizacoesAdministradas.Length == 0)
        {
            return FranqueadoraAlunosResultado.SemAcesso();
        }

        if (organizacoesAdministradas.Length > 1)
        {
            return FranqueadoraAlunosResultado.SelecaoOrganizacaoNecessaria();
        }

        var organizacaoId = organizacoesAdministradas[0];
        var organizacao = await dbContext.Organizacoes
            .AsNoTracking()
            .Where(item => item.Id == organizacaoId)
            .Select(item => new { item.Id, item.Nome })
            .SingleOrDefaultAsync(cancellationToken);

        if (organizacao is null)
        {
            return FranqueadoraAlunosResultado.SemAcesso();
        }

        var unidades = await dbContext.Unidades
            .AsNoTracking()
            .Where(u => u.OrganizacaoId == organizacaoId && u.Ativa)
            .Select(u => new FranqueadoraUnidadeSelecao(u.Id, u.Nome))
            .ToListAsync(cancellationToken);

        var query = from matricula in dbContext.Matriculas.AsNoTracking()
                    join aluno in dbContext.Alunos.AsNoTracking()
                        on matricula.AlunoId equals aluno.Id
                    join unidade in dbContext.Unidades.AsNoTracking()
                        on matricula.UnidadeId equals unidade.Id
                    where matricula.OrganizacaoId == organizacaoId
                       && matricula.Status == StatusMatricula.Ativa
                       && unidade.Ativa
                    select new { matricula, aluno, unidade };

        if (unidadeId.HasValue)
        {
            query = query.Where(x => x.matricula.UnidadeId == unidadeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var buscaNormalizada = busca.Trim().ToUpper();
            query = query.Where(x =>
                x.aluno.NomeCompleto.ToUpper().Contains(buscaNormalizada)
                || (x.aluno.Cpf != null && x.aluno.Cpf.Contains(buscaNormalizada))
                || (x.aluno.Email != null && x.aluno.Email.ToUpper().Contains(buscaNormalizada)));
        }

        var alunos = await query
            .Select(x => new FranqueadoraAlunoItem(
                x.aluno.Id,
                x.aluno.NomeCompleto,
                x.aluno.Cpf,
                x.aluno.Email,
                x.aluno.Telefone,
                x.aluno.Ativo,
                x.unidade.Nome,
                x.matricula.UnidadeId,
                x.aluno.DataNascimento,
                x.aluno.CriadoEmUtc))
            .OrderBy(x => x.NomeCompleto)
            .ToListAsync(cancellationToken);

        var resumo = new FranqueadoraAlunosResumo(
            organizacao.Id,
            organizacao.Nome,
            alunos.Count,
            alunos,
            unidades);

        return FranqueadoraAlunosResultado.Sucesso(resumo);
    }
}

using BFA.Application.Unidades;
using BFA.Domain.Acessos;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using UnidadeEntidade = BFA.Domain.Unidades.Unidade;

namespace BFA.Infrastructure.Unidades;

public sealed class UnidadesUsuarioConsulta(BfaDbContext dbContext)
    : IUnidadesUsuarioConsulta, IUnidadeContextoConsulta
{
    public async Task<IReadOnlyList<UnidadeAcessoResumo>> ListarAdministradasAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        return await ConsultaListagemAdministradas(usuarioId)
            .ToArrayAsync(cancellationToken);
    }

    public Task<UnidadeAcessoResumo?> ObterAdministradaAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return ConsultaAdministrada(usuarioId, unidadeId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UnidadeAcessoResumo>> ListarProfessorAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        return await UnidadesPorPerfil(usuarioId, PerfilAcesso.Professor)
            .OrderBy(unidade => unidade.Nome)
            .ThenBy(unidade => unidade.Id)
            .Select(unidade => new UnidadeAcessoResumo(
                unidade.OrganizacaoId, unidade.Id, unidade.Nome))
            .ToArrayAsync(cancellationToken);
    }

    public Task<UnidadeAcessoResumo?> ObterProfessorAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return UnidadesPorPerfil(usuarioId, PerfilAcesso.Professor)
            .Where(unidade => unidade.Id == unidadeId)
            .Select(unidade => new UnidadeAcessoResumo(
                unidade.OrganizacaoId, unidade.Id, unidade.Nome))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<UnidadeContextoResumo?> ObterAtivaAsync(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return (from unidade in dbContext.Unidades.AsNoTracking()
                join organizacao in dbContext.Organizacoes.AsNoTracking()
                    on unidade.OrganizacaoId equals organizacao.Id
                where unidade.Id == unidadeId
                    && unidade.Ativa
                    && organizacao.Ativa
                select new UnidadeContextoResumo(
                    unidade.OrganizacaoId,
                    unidade.Id,
                    unidade.Nome))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<UnidadeAcessoResumo> ConsultaListagemAdministradas(Guid usuarioId)
    {
        return UnidadesAdministradas(usuarioId)
            .OrderBy(unidade => unidade.Nome)
            .ThenBy(unidade => unidade.Id)
            .Select(unidade => new UnidadeAcessoResumo(
                unidade.OrganizacaoId,
                unidade.Id,
                unidade.Nome));
    }

    private IQueryable<UnidadeAcessoResumo> ConsultaAdministrada(
        Guid usuarioId,
        Guid unidadeId)
    {
        return UnidadesAdministradas(usuarioId)
            .Where(unidade => unidade.Id == unidadeId)
            .Select(unidade => new UnidadeAcessoResumo(
                unidade.OrganizacaoId,
                unidade.Id,
                unidade.Nome));
    }

    private IQueryable<UnidadeEntidade> UnidadesAdministradas(Guid usuarioId)
    {
        return from vinculo in dbContext.VinculosAcesso.AsNoTracking()
               join unidade in dbContext.Unidades.AsNoTracking()
                   on new { vinculo.OrganizacaoId, vinculo.UnidadeId }
                   equals new
                   {
                       unidade.OrganizacaoId,
                       UnidadeId = (Guid?)unidade.Id
                   }
               join organizacao in dbContext.Organizacoes.AsNoTracking()
                   on unidade.OrganizacaoId equals organizacao.Id
               where vinculo.UsuarioId == usuarioId
                   && vinculo.Ativo
                   && vinculo.Perfil == PerfilAcesso.AdministradorUnidade
                   && vinculo.UnidadeId != null
                   && unidade.Ativa
                   && organizacao.Ativa
               select unidade;
    }

    private IQueryable<UnidadeEntidade> UnidadesPorPerfil(
        Guid usuarioId,
        PerfilAcesso perfil)
    {
        return from vinculo in dbContext.VinculosAcesso.AsNoTracking()
               join unidade in dbContext.Unidades.AsNoTracking()
                   on new { vinculo.OrganizacaoId, vinculo.UnidadeId }
                   equals new
                   {
                       unidade.OrganizacaoId,
                       UnidadeId = (Guid?)unidade.Id
                   }
               join organizacao in dbContext.Organizacoes.AsNoTracking()
                   on unidade.OrganizacaoId equals organizacao.Id
               where vinculo.UsuarioId == usuarioId
                   && vinculo.Ativo
                   && vinculo.Perfil == perfil
                   && unidade.Ativa
                   && organizacao.Ativa
               select unidade;
    }
}

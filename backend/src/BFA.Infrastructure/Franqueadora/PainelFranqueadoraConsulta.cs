using BFA.Application.Franqueadora;
using BFA.Domain.Acessos;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Franqueadora;

public sealed class PainelFranqueadoraConsulta(BfaDbContext dbContext)
    : IPainelFranqueadoraConsulta
{
    public async Task<PainelFranqueadoraResultado> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty)
        {
            return PainelFranqueadoraResultado.SemAcesso();
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
            return PainelFranqueadoraResultado.SemAcesso();
        }

        if (organizacoesAdministradas.Length > 1)
        {
            return PainelFranqueadoraResultado.SelecaoOrganizacaoNecessaria();
        }

        var organizacaoId = organizacoesAdministradas[0];
        var organizacao = await dbContext.Organizacoes
            .AsNoTracking()
            .Where(item => item.Id == organizacaoId)
            .Select(item => new { item.Id, item.Nome })
            .SingleOrDefaultAsync(cancellationToken);

        if (organizacao is null)
        {
            return PainelFranqueadoraResultado.SemAcesso();
        }

        var totalUnidades = await dbContext.Unidades
            .AsNoTracking()
            .CountAsync(
                unidade => unidade.OrganizacaoId == organizacaoId,
                cancellationToken);
        var unidadesAtivas = await dbContext.Unidades
            .AsNoTracking()
            .CountAsync(
                unidade => unidade.OrganizacaoId == organizacaoId && unidade.Ativa,
                cancellationToken);
        var administradoresRedeAtivos = await dbContext.VinculosAcesso
            .AsNoTracking()
            .CountAsync(
                vinculo => vinculo.OrganizacaoId == organizacaoId
                    && vinculo.Ativo
                    && vinculo.Perfil == PerfilAcesso.AdministradorRede
                    && vinculo.UnidadeId == null,
                cancellationToken);
        var administradoresUnidadeAtivos = await dbContext.VinculosAcesso
            .AsNoTracking()
            .CountAsync(
                vinculo => vinculo.OrganizacaoId == organizacaoId
                    && vinculo.Ativo
                    && vinculo.Perfil == PerfilAcesso.AdministradorUnidade
                    && vinculo.UnidadeId != null,
                cancellationToken);

        return PainelFranqueadoraResultado.Disponivel(new PainelFranqueadoraResumo(
            organizacao.Id,
            organizacao.Nome,
            totalUnidades,
            unidadesAtivas,
            administradoresRedeAtivos,
            administradoresUnidadeAtivos));
    }
}

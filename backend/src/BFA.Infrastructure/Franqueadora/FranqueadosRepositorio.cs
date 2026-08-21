using BFA.Application.Franqueadora.Franqueados;
using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BFA.Infrastructure.Franqueadora;

public sealed class FranqueadosRepositorio(BfaDbContext dbContext)
    : IFranqueadosRepositorio, IDiagnosticoVinculosFranqueadoConsulta
{
    private const string RestricaoDocumentoUnico =
        "uq_franqueados_organizacao_id_documento";
    private const string RestricaoUnidadeComFranqueadoAtivo =
        "uq_franqueados_unidades_unidade_ativa";

    public async Task<IReadOnlyList<FranqueadoResumo>> ListarAsync(
        Guid organizacaoId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Franqueados
            .AsNoTracking()
            .Where(franqueado => franqueado.OrganizacaoId == organizacaoId)
            .OrderBy(franqueado => franqueado.NomeRazaoSocial)
            .ThenBy(franqueado => franqueado.Id)
            .Select(franqueado => new FranqueadoResumo(
                franqueado.Id,
                franqueado.NomeRazaoSocial,
                franqueado.NomeFantasia,
                franqueado.Documento,
                franqueado.TipoPessoa,
                dbContext.FranqueadosUnidades.Count(vinculo =>
                    vinculo.OrganizacaoId == organizacaoId
                    && vinculo.FranqueadoId == franqueado.Id
                    && vinculo.Ativo),
                franqueado.Ativo))
            .ToArrayAsync(cancellationToken);
    }

    public Task<FranqueadoDados?> ObterDadosAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        CancellationToken cancellationToken)
    {
        return dbContext.Franqueados
            .AsNoTracking()
            .Where(franqueado => franqueado.OrganizacaoId == organizacaoId
                && franqueado.Id == franqueadoId)
            .Select(franqueado => new FranqueadoDados(
                franqueado.Id,
                franqueado.OrganizacaoId,
                franqueado.TipoPessoa,
                franqueado.NomeRazaoSocial,
                franqueado.NomeFantasia,
                franqueado.Documento,
                franqueado.Telefone,
                franqueado.Email,
                franqueado.EmailFinanceiro,
                franqueado.ResponsavelLegal,
                franqueado.Logradouro,
                franqueado.Numero,
                franqueado.Complemento,
                franqueado.Bairro,
                franqueado.Cidade,
                franqueado.Estado,
                franqueado.Cep,
                franqueado.Observacoes,
                franqueado.Ativo))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FranqueadoUsuarioResumo>> ListarUsuariosAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        CancellationToken cancellationToken)
    {
        return await (
            from relacao in dbContext.FranqueadosUsuarios.AsNoTracking()
            join franqueado in dbContext.Franqueados.AsNoTracking()
                on relacao.FranqueadoId equals franqueado.Id
            join usuario in dbContext.Users.AsNoTracking()
                on relacao.UsuarioId equals usuario.Id
            join perfil in dbContext.PerfisUsuario.AsNoTracking()
                on usuario.Id equals perfil.UsuarioId into perfis
            from perfil in perfis.DefaultIfEmpty()
            where franqueado.OrganizacaoId == organizacaoId
                && franqueado.Id == franqueadoId
            orderby relacao.Principal descending,
                perfil != null ? perfil.NomeCompleto : usuario.Email
            select new FranqueadoUsuarioResumo(
                usuario.Id,
                perfil != null
                    ? perfil.NomeCompleto
                    : usuario.Email ?? usuario.UserName ?? string.Empty,
                usuario.Email ?? usuario.UserName ?? string.Empty,
                relacao.Principal,
                relacao.Ativo))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FranqueadoUnidadeResumo>> ListarUnidadesAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        CancellationToken cancellationToken)
    {
        return await (
            from relacao in dbContext.FranqueadosUnidades.AsNoTracking()
            join unidade in dbContext.Unidades.AsNoTracking()
                on new { relacao.OrganizacaoId, relacao.UnidadeId }
                equals new { unidade.OrganizacaoId, UnidadeId = unidade.Id }
            where relacao.OrganizacaoId == organizacaoId
                && relacao.FranqueadoId == franqueadoId
            orderby relacao.Ativo descending, unidade.Nome
            select new FranqueadoUnidadeResumo(
                unidade.Id,
                unidade.Nome,
                relacao.Ativo,
                unidade.Ativa,
                relacao.CriadoEmUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UnidadeDisponivelFranqueadoResumo>>
        ListarUnidadesDisponiveisAsync(
            Guid organizacaoId,
            CancellationToken cancellationToken)
    {
        return await dbContext.Unidades
            .AsNoTracking()
            .Where(unidade => unidade.OrganizacaoId == organizacaoId
                && unidade.Ativa
                && !dbContext.FranqueadosUnidades.Any(vinculo =>
                    vinculo.OrganizacaoId == organizacaoId
                    && vinculo.UnidadeId == unidade.Id
                    && vinculo.Ativo))
            .OrderBy(unidade => unidade.Nome)
            .ThenBy(unidade => unidade.Id)
            .Select(unidade => new UnidadeDisponivelFranqueadoResumo(
                unidade.Id,
                unidade.Nome))
            .ToArrayAsync(cancellationToken);
    }

    public Task<Franqueado?> ObterParaAtualizacaoAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        CancellationToken cancellationToken)
    {
        return dbContext.Franqueados.SingleOrDefaultAsync(
            franqueado => franqueado.OrganizacaoId == organizacaoId
                && franqueado.Id == franqueadoId,
            cancellationToken);
    }

    public Task<bool> ExisteDocumentoAsync(
        Guid organizacaoId,
        Guid franqueadoIdIgnorado,
        string documento,
        CancellationToken cancellationToken)
    {
        return dbContext.Franqueados.AsNoTracking().AnyAsync(
            franqueado => franqueado.OrganizacaoId == organizacaoId
                && franqueado.Id != franqueadoIdIgnorado
                && franqueado.Documento == documento,
            cancellationToken);
    }

    public Task<bool> UnidadeAtivaExisteAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return dbContext.Unidades.AsNoTracking().AnyAsync(
            unidade => unidade.OrganizacaoId == organizacaoId
                && unidade.Id == unidadeId
                && unidade.Ativa,
            cancellationToken);
    }

    public Task<bool> UnidadePossuiOutroFranqueadoAtivoAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return dbContext.FranqueadosUnidades.AsNoTracking().AnyAsync(
            vinculo => vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.FranqueadoId != franqueadoId
                && vinculo.Ativo,
            cancellationToken);
    }

    public Task<FranqueadoUsuario?> ObterUsuarioPrincipalAtivoAsync(
        Guid franqueadoId,
        CancellationToken cancellationToken)
    {
        return dbContext.FranqueadosUsuarios.SingleOrDefaultAsync(
            vinculo => vinculo.FranqueadoId == franqueadoId
                && vinculo.Principal
                && vinculo.Ativo,
            cancellationToken);
    }

    public Task<FranqueadoUnidade?> ObterVinculoUnidadeAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return dbContext.FranqueadosUnidades.SingleOrDefaultAsync(
            vinculo => vinculo.OrganizacaoId == organizacaoId
                && vinculo.FranqueadoId == franqueadoId
                && vinculo.UnidadeId == unidadeId,
            cancellationToken);
    }

    public Task<VinculoAcesso?> ObterAcessoAdministradorUnidadeAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        return dbContext.VinculosAcesso.SingleOrDefaultAsync(
            vinculo => vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.UsuarioId == usuarioId
                && vinculo.Perfil == PerfilAcesso.AdministradorUnidade,
            cancellationToken);
    }

    public void Adicionar(FranqueadoUnidade vinculo)
    {
        dbContext.FranqueadosUnidades.Add(vinculo);
    }

    public void Adicionar(VinculoAcesso vinculo)
    {
        dbContext.VinculosAcesso.Add(vinculo);
    }

    public async Task<EstadoPersistenciaFranqueado> SalvarAsync(
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return EstadoPersistenciaFranqueado.Sucesso;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.ConstraintName switch
            {
                RestricaoDocumentoUnico => EstadoPersistenciaFranqueado.DocumentoDuplicado,
                RestricaoUnidadeComFranqueadoAtivo =>
                    EstadoPersistenciaFranqueado.UnidadeOcupada,
                _ => EstadoPersistenciaFranqueado.Falha
            };
        }
    }

    public async Task<DiagnosticoVinculosFranqueado> DiagnosticarAsync(
        CancellationToken cancellationToken)
    {
        var principais = await (
            from relacao in dbContext.FranqueadosUsuarios.AsNoTracking()
            join franqueado in dbContext.Franqueados.AsNoTracking()
                on relacao.FranqueadoId equals franqueado.Id
            where relacao.Principal && relacao.Ativo
            select new PrincipalDiagnostico(
                franqueado.Id,
                franqueado.OrganizacaoId,
                franqueado.NomeRazaoSocial,
                relacao.UsuarioId))
            .ToArrayAsync(cancellationToken);

        if (principais.Length == 0)
        {
            return new([], []);
        }

        var usuariosIds = principais
            .Select(principal => principal.UsuarioId)
            .Distinct()
            .ToArray();
        var franqueadosIds = principais
            .Select(principal => principal.FranqueadoId)
            .Distinct()
            .ToArray();
        var organizacoesIds = principais
            .Select(principal => principal.OrganizacaoId)
            .Distinct()
            .ToArray();

        var acessos = await dbContext.VinculosAcesso
            .AsNoTracking()
            .Where(acesso => acesso.Ativo
                && acesso.Perfil == PerfilAcesso.AdministradorUnidade
                && acesso.UnidadeId.HasValue
                && usuariosIds.Contains(acesso.UsuarioId)
                && organizacoesIds.Contains(acesso.OrganizacaoId))
            .Select(acesso => new AcessoDiagnostico(
                acesso.UsuarioId,
                acesso.OrganizacaoId,
                acesso.UnidadeId!.Value))
            .ToArrayAsync(cancellationToken);
        var vinculosComerciais = await dbContext.FranqueadosUnidades
            .AsNoTracking()
            .Where(vinculo => vinculo.Ativo
                && franqueadosIds.Contains(vinculo.FranqueadoId))
            .Select(vinculo => new VinculoComercialDiagnostico(
                vinculo.FranqueadoId,
                vinculo.OrganizacaoId,
                vinculo.UnidadeId))
            .ToArrayAsync(cancellationToken);
        var unidadesIds = acessos
            .Select(acesso => acesso.UnidadeId)
            .Concat(vinculosComerciais.Select(vinculo => vinculo.UnidadeId))
            .Distinct()
            .ToArray();
        var unidades = await dbContext.Unidades
            .AsNoTracking()
            .Where(unidade => unidadesIds.Contains(unidade.Id)
                && organizacoesIds.Contains(unidade.OrganizacaoId))
            .Select(unidade => new UnidadeDiagnostico(
                unidade.Id,
                unidade.OrganizacaoId,
                unidade.Nome))
            .ToArrayAsync(cancellationToken);

        var acessosSemVinculo = (
            from principal in principais
            from acesso in acessos
            where acesso.UsuarioId == principal.UsuarioId
                && acesso.OrganizacaoId == principal.OrganizacaoId
                && !vinculosComerciais.Any(vinculo =>
                    vinculo.FranqueadoId == principal.FranqueadoId
                    && vinculo.OrganizacaoId == principal.OrganizacaoId
                    && vinculo.UnidadeId == acesso.UnidadeId)
            select CriarInconsistencia(principal, acesso.UnidadeId, unidades))
            .Distinct()
            .OrderBy(item => item.Franqueado, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Unidade, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var vinculosSemAcesso = (
            from principal in principais
            from vinculo in vinculosComerciais
            where vinculo.FranqueadoId == principal.FranqueadoId
                && vinculo.OrganizacaoId == principal.OrganizacaoId
                && !acessos.Any(acesso =>
                    acesso.UsuarioId == principal.UsuarioId
                    && acesso.OrganizacaoId == principal.OrganizacaoId
                    && acesso.UnidadeId == vinculo.UnidadeId)
            select CriarInconsistencia(principal, vinculo.UnidadeId, unidades))
            .Distinct()
            .OrderBy(item => item.Franqueado, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Unidade, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new(acessosSemVinculo, vinculosSemAcesso);
    }

    private static InconsistenciaVinculosFranqueado CriarInconsistencia(
        PrincipalDiagnostico principal,
        Guid unidadeId,
        IReadOnlyCollection<UnidadeDiagnostico> unidades)
    {
        var unidade = unidades.SingleOrDefault(item =>
            item.Id == unidadeId
            && item.OrganizacaoId == principal.OrganizacaoId);

        return new(
            principal.FranqueadoId,
            principal.Franqueado,
            principal.UsuarioId,
            unidadeId,
            unidade?.Nome ?? "Unidade não encontrada");
    }

    private sealed record PrincipalDiagnostico(
        Guid FranqueadoId,
        Guid OrganizacaoId,
        string Franqueado,
        Guid UsuarioId);

    private sealed record AcessoDiagnostico(
        Guid UsuarioId,
        Guid OrganizacaoId,
        Guid UnidadeId);

    private sealed record VinculoComercialDiagnostico(
        Guid FranqueadoId,
        Guid OrganizacaoId,
        Guid UnidadeId);

    private sealed record UnidadeDiagnostico(
        Guid Id,
        Guid OrganizacaoId,
        string Nome);
}

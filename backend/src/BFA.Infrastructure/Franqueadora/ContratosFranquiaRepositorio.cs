using BFA.Application.Contratos;
using BFA.Application.Franqueadora.Contratos;
using BFA.Domain.Contratos;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BFA.Infrastructure.Franqueadora;

public sealed class ContratosFranquiaRepositorio(
    BfaDbContext dbContext,
    IArmazenamentoDocumentosContrato armazenamento)
    : IContratosFranquiaRepositorio
{
    private const string RestricaoContratoAtivo =
        "uq_contratos_franquia_franqueado_unidade_ativo";
    private const string RestricaoNumeroVersao =
        "uq_contratos_franquia_versoes_contrato_numero";
    private const string RestricaoVersaoVigente =
        "uq_contratos_franquia_versoes_vigente";

    public Task<ContextoContratoFranquia?> ObterContextoAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return (
            from vinculo in dbContext.FranqueadosUnidades.AsNoTracking()
            join franqueado in dbContext.Franqueados.AsNoTracking()
                on new { vinculo.OrganizacaoId, vinculo.FranqueadoId }
                equals new { franqueado.OrganizacaoId, FranqueadoId = franqueado.Id }
            join unidade in dbContext.Unidades.AsNoTracking()
                on new { vinculo.OrganizacaoId, vinculo.UnidadeId }
                equals new { unidade.OrganizacaoId, UnidadeId = unidade.Id }
            where vinculo.OrganizacaoId == organizacaoId
                && vinculo.FranqueadoId == franqueadoId
                && vinculo.UnidadeId == unidadeId
            select new ContextoContratoFranquia(
                vinculo.Id,
                vinculo.OrganizacaoId,
                franqueado.Id,
                franqueado.NomeRazaoSocial,
                unidade.Id,
                unidade.Nome,
                vinculo.Ativo,
                unidade.Ativa))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ContratoFranquiaPainel> ObterPainelAsync(
        ContextoContratoFranquia contexto,
        CancellationToken cancellationToken)
    {
        var contrato = await dbContext.ContratosFranquia
            .AsNoTracking()
            .Where(item => item.FranqueadoUnidadeId == contexto.FranqueadoUnidadeId)
            .OrderBy(item => item.Status == StatusContratoFranquia.Ativo ? 0
                : item.Status == StatusContratoFranquia.Rascunho ? 1
                : 2)
            .ThenByDescending(item => item.CriadoEmUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (contrato is null)
        {
            return new(contexto, null, null, null, []);
        }

        var versoes = await dbContext.ContratosFranquiaVersoes
            .AsNoTracking()
            .Where(item => item.ContratoFranquiaId == contrato.Id)
            .OrderByDescending(item => item.NumeroVersao)
            .ToArrayAsync(cancellationToken);
        var versoesIds = versoes.Select(item => item.Id).ToArray();
        var documentos = await dbContext.DocumentosContratoFranquia
            .AsNoTracking()
            .Where(item => versoesIds.Contains(item.ContratoFranquiaVersaoId))
            .OrderByDescending(item => item.CriadoEmUtc)
            .ToArrayAsync(cancellationToken);
        var usuariosIds = versoes.Select(item => item.CriadoPorUsuarioId)
            .Concat(documentos.Select(item => item.EnviadoPorUsuarioId))
            .Distinct()
            .ToArray();
        var usuarios = await (
            from usuario in dbContext.Users.AsNoTracking()
            join perfil in dbContext.PerfisUsuario.AsNoTracking()
                on usuario.Id equals perfil.UsuarioId into perfis
            from perfil in perfis.DefaultIfEmpty()
            where usuariosIds.Contains(usuario.Id)
            select new
            {
                usuario.Id,
                Nome = perfil != null
                    ? perfil.NomeCompleto
                    : usuario.Email ?? usuario.UserName ?? "Usuário"
            })
            .ToDictionaryAsync(item => item.Id, item => item.Nome, cancellationToken);
        var resumos = versoes.Select(versao => new VersaoContratoFranquiaResumo(
            versao.Id,
            versao.NumeroVersao,
            versao.DataInicio,
            versao.DataFim,
            versao.PercentualRoyalties,
            versao.MensalidadeFixa,
            versao.TaxaAdesao,
            versao.DiaVencimento,
            versao.Status,
            versao.MotivoAlteracao,
            versao.Observacoes,
            versao.CriadoEmUtc,
            usuarios.GetValueOrDefault(versao.CriadoPorUsuarioId, "Usuário"),
            documentos
                .Where(documento => documento.ContratoFranquiaVersaoId == versao.Id)
                .Select(documento => new DocumentoContratoFranquiaResumo(
                    documento.Id,
                    documento.TipoDocumento,
                    documento.NomeOriginal,
                    documento.TamanhoBytes,
                    documento.CriadoEmUtc,
                    usuarios.GetValueOrDefault(documento.EnviadoPorUsuarioId, "Usuário")))
                .ToArray()))
            .ToArray();
        return new(contexto, contrato.Id, contrato.Numero, contrato.Status, resumos);
    }

    public Task<ContratoFranquia?> ObterContratoParaAtualizacaoAsync(
        Guid franqueadoUnidadeId,
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        return dbContext.ContratosFranquia.SingleOrDefaultAsync(
            contrato => contrato.Id == contratoId
                && contrato.FranqueadoUnidadeId == franqueadoUnidadeId,
            cancellationToken);
    }

    public Task<ContratoFranquiaVersao?> ObterVersaoParaAtualizacaoAsync(
        Guid contratoId,
        Guid versaoId,
        CancellationToken cancellationToken)
    {
        return dbContext.ContratosFranquiaVersoes.SingleOrDefaultAsync(
            versao => versao.Id == versaoId
                && versao.ContratoFranquiaId == contratoId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ContratoFranquiaVersao>> ListarVersoesParaAtualizacaoAsync(
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ContratosFranquiaVersoes
            .Where(versao => versao.ContratoFranquiaId == contratoId)
            .OrderByDescending(versao => versao.NumeroVersao)
            .ToArrayAsync(cancellationToken);
    }

    public Task<bool> ExisteContratoAtivoOutroAsync(
        Guid franqueadoUnidadeId,
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        return dbContext.ContratosFranquia.AsNoTracking().AnyAsync(
            contrato => contrato.FranqueadoUnidadeId == franqueadoUnidadeId
                && contrato.Id != contratoId
                && contrato.Status == StatusContratoFranquia.Ativo,
            cancellationToken);
    }

    public Task<bool> ExisteDocumentoAsync(
        Guid versaoId,
        IReadOnlyCollection<TipoDocumentoContratoFranquia> tipos,
        CancellationToken cancellationToken)
    {
        return dbContext.DocumentosContratoFranquia.AsNoTracking().AnyAsync(
            documento => documento.ContratoFranquiaVersaoId == versaoId
                && tipos.Contains(documento.TipoDocumento),
            cancellationToken);
    }

    public Task<DocumentoContratoFranquiaAcesso?> ObterDocumentoAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        Guid documentoId,
        CancellationToken cancellationToken)
    {
        return (
            from documento in dbContext.DocumentosContratoFranquia.AsNoTracking()
            join versao in dbContext.ContratosFranquiaVersoes.AsNoTracking()
                on documento.ContratoFranquiaVersaoId equals versao.Id
            join contrato in dbContext.ContratosFranquia.AsNoTracking()
                on versao.ContratoFranquiaId equals contrato.Id
            join vinculo in dbContext.FranqueadosUnidades.AsNoTracking()
                on contrato.FranqueadoUnidadeId equals vinculo.Id
            where documento.Id == documentoId
                && versao.Id == versaoId
                && contrato.Id == contratoId
                && vinculo.OrganizacaoId == organizacaoId
                && vinculo.FranqueadoId == franqueadoId
                && vinculo.UnidadeId == unidadeId
            select new DocumentoContratoFranquiaAcesso(
                documento.ChaveArmazenamento,
                documento.NomeOriginal,
                documento.ContentType))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void Adicionar(ContratoFranquia contrato) =>
        dbContext.ContratosFranquia.Add(contrato);

    public void Adicionar(ContratoFranquiaVersao versao) =>
        dbContext.ContratosFranquiaVersoes.Add(versao);

    public Task<EstadoPersistenciaContratoFranquia> SalvarTransacaoAsync(
        CancellationToken cancellationToken) => SalvarAlteracoesAsync(cancellationToken);

    public async Task<EstadoPersistenciaContratoFranquia> SalvarNovaVersaoAsync(
        ContratoFranquiaVersao versao,
        CancellationToken cancellationToken)
    {
        dbContext.ContratosFranquiaVersoes.Add(versao);
        return await SalvarAlteracoesAsync(cancellationToken);
    }

    public async Task<EstadoPersistenciaContratoFranquia> SalvarFormalizacaoAsync(
        ContratoFranquiaVersao versaoVigenteAnterior,
        ContratoFranquiaVersao novaVersaoVigente,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        try
        {
            dbContext.Entry(versaoVigenteAnterior)
                .Property(versao => versao.Status)
                .IsModified = true;
            dbContext.Entry(novaVersaoVigente).State = EntityState.Detached;
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.Attach(novaVersaoVigente);
            dbContext.Entry(novaVersaoVigente)
                .Property(versao => versao.Status)
                .IsModified = true;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return EstadoPersistenciaContratoFranquia.Sucesso;
        }
        catch (DbUpdateException exception)
        {
            return MapearExcecao(exception);
        }
    }

    public async Task<EstadoPersistenciaContratoFranquia> SalvarDocumentoAsync(
        DocumentoContratoFranquia documento,
        string identificadorTemporario,
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        var arquivoConfirmado = false;
        var commitConcluido = false;

        try
        {
            await armazenamento.ConfirmarTemporarioAsync(
                identificadorTemporario,
                documento.ChaveArmazenamento,
                cancellationToken);
            arquivoConfirmado = true;
            dbContext.DocumentosContratoFranquia.Add(documento);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            commitConcluido = true;
            return EstadoPersistenciaContratoFranquia.Sucesso;
        }
        catch (DbUpdateException exception)
        {
            return MapearExcecao(exception);
        }
        catch (IOException)
        {
            return EstadoPersistenciaContratoFranquia.Falha;
        }
        finally
        {
            if (arquivoConfirmado && !commitConcluido)
            {
                await armazenamento.DescartarArquivoNaoConfirmadoAsync(
                    documento.ChaveArmazenamento,
                    CancellationToken.None);
            }
        }
    }

    private async Task<EstadoPersistenciaContratoFranquia> SalvarAlteracoesAsync(
        CancellationToken cancellationToken)
    {
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return EstadoPersistenciaContratoFranquia.Sucesso;
        }
        catch (DbUpdateException exception)
        {
            return MapearExcecao(exception);
        }
    }

    private static EstadoPersistenciaContratoFranquia MapearExcecao(
        DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgres)
        {
            return EstadoPersistenciaContratoFranquia.Falha;
        }

        return postgres.ConstraintName switch
        {
            RestricaoContratoAtivo =>
                EstadoPersistenciaContratoFranquia.ContratoAtivoExistente,
            RestricaoNumeroVersao or RestricaoVersaoVigente =>
                EstadoPersistenciaContratoFranquia.ConflitoVersao,
            _ => EstadoPersistenciaContratoFranquia.Falha
        };
    }
}

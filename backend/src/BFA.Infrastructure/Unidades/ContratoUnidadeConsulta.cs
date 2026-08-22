using BFA.Application.Contratos;
using BFA.Application.Unidades.Contratos;
using BFA.Domain.Acessos;
using BFA.Domain.Contratos;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Unidades;

public sealed class ContratoUnidadeConsulta(
    BfaDbContext dbContext,
    IArmazenamentoDocumentosContrato armazenamento)
    : IContratoUnidadeConsulta
{
    public async Task<ResultadoConsultaContratoUnidade<PainelContratoUnidade>> ObterAtivoAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAutorizadoAsync(
            usuarioId,
            unidadeId,
            cancellationToken);

        if (contexto is null)
        {
            return new(EstadoConsultaContratoUnidade.SemAcesso, null);
        }

        var contrato = await (
            from vinculo in dbContext.FranqueadosUnidades.AsNoTracking()
            join item in dbContext.ContratosFranquia.AsNoTracking()
                on vinculo.Id equals item.FranqueadoUnidadeId
            join versao in dbContext.ContratosFranquiaVersoes.AsNoTracking()
                on item.Id equals versao.ContratoFranquiaId
            where vinculo.OrganizacaoId == contexto.OrganizacaoId
                && vinculo.UnidadeId == contexto.UnidadeId
                && vinculo.Ativo
                && item.Status == StatusContratoFranquia.Ativo
                && versao.Status == StatusVersaoContratoFranquia.Vigente
            select new
            {
                ContratoId = item.Id,
                item.Numero,
                StatusContrato = item.Status,
                VersaoId = versao.Id,
                versao.NumeroVersao,
                versao.DataInicio,
                versao.DataFim,
                versao.PercentualRoyalties,
                versao.MensalidadeFixa,
                versao.TaxaAdesao,
                versao.DiaVencimento,
                versao.Observacoes
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (contrato is null)
        {
            return new(
                EstadoConsultaContratoUnidade.Sucesso,
                new PainelContratoUnidade(
                    contexto.OrganizacaoId,
                    contexto.UnidadeId,
                    contexto.UnidadeNome,
                    null));
        }

        var documentos = await dbContext.DocumentosContratoFranquia
            .AsNoTracking()
            .Where(item => item.ContratoFranquiaVersaoId == contrato.VersaoId)
            .OrderByDescending(item => item.CriadoEmUtc)
            .Select(item => new DocumentoContratoUnidadeResumo(
                item.Id,
                item.TipoDocumento,
                item.NomeOriginal,
                item.TamanhoBytes))
            .ToArrayAsync(cancellationToken);
        var resumo = new ContratoAtivoUnidadeResumo(
            contrato.ContratoId,
            contrato.Numero,
            contrato.StatusContrato,
            contrato.VersaoId,
            contrato.NumeroVersao,
            contrato.DataInicio,
            contrato.DataFim,
            contrato.PercentualRoyalties,
            contrato.MensalidadeFixa,
            contrato.TaxaAdesao,
            contrato.DiaVencimento,
            contrato.Observacoes,
            documentos);
        return new(
            EstadoConsultaContratoUnidade.Sucesso,
            new PainelContratoUnidade(
                contexto.OrganizacaoId,
                contexto.UnidadeId,
                contexto.UnidadeNome,
                resumo));
    }

    public async Task<ResultadoConsultaContratoUnidade<DocumentoContratoUnidadeLeitura>>
        AbrirDocumentoAsync(
            Guid usuarioId,
            Guid unidadeId,
            Guid documentoId,
            CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAutorizadoAsync(
            usuarioId,
            unidadeId,
            cancellationToken);

        if (contexto is null)
        {
            return new(EstadoConsultaContratoUnidade.SemAcesso, null);
        }

        var documento = await (
            from item in dbContext.DocumentosContratoFranquia.AsNoTracking()
            join versao in dbContext.ContratosFranquiaVersoes.AsNoTracking()
                on item.ContratoFranquiaVersaoId equals versao.Id
            join contrato in dbContext.ContratosFranquia.AsNoTracking()
                on versao.ContratoFranquiaId equals contrato.Id
            join vinculo in dbContext.FranqueadosUnidades.AsNoTracking()
                on contrato.FranqueadoUnidadeId equals vinculo.Id
            where item.Id == documentoId
                && vinculo.OrganizacaoId == contexto.OrganizacaoId
                && vinculo.UnidadeId == contexto.UnidadeId
                && vinculo.Ativo
                && contrato.Status == StatusContratoFranquia.Ativo
                && versao.Status == StatusVersaoContratoFranquia.Vigente
            select new
            {
                item.ChaveArmazenamento,
                item.NomeOriginal
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (documento is null)
        {
            return new(EstadoConsultaContratoUnidade.NaoEncontrado, null);
        }

        try
        {
            if (!await armazenamento.ExisteAsync(
                    documento.ChaveArmazenamento,
                    cancellationToken))
            {
                return new(EstadoConsultaContratoUnidade.DocumentoIndisponivel, null);
            }

            var conteudo = await armazenamento.AbrirLeituraAsync(
                documento.ChaveArmazenamento,
                cancellationToken);
            return new(
                EstadoConsultaContratoUnidade.Sucesso,
                new DocumentoContratoUnidadeLeitura(conteudo, documento.NomeOriginal));
        }
        catch (IOException)
        {
            return new(EstadoConsultaContratoUnidade.DocumentoIndisponivel, null);
        }
    }

    private Task<ContextoAutorizado?> ObterContextoAutorizadoAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty || unidadeId == Guid.Empty)
        {
            return Task.FromResult<ContextoAutorizado?>(null);
        }

        return (
            from unidade in dbContext.Unidades.AsNoTracking()
            join organizacao in dbContext.Organizacoes.AsNoTracking()
                on unidade.OrganizacaoId equals organizacao.Id
            where unidade.Id == unidadeId
                && unidade.Ativa
                && organizacao.Ativa
                && dbContext.VinculosAcesso.AsNoTracking().Any(vinculo =>
                    vinculo.UsuarioId == usuarioId
                    && vinculo.OrganizacaoId == unidade.OrganizacaoId
                    && vinculo.Ativo
                    && ((vinculo.Perfil == PerfilAcesso.AdministradorUnidade
                            && vinculo.UnidadeId == unidade.Id)
                        || (vinculo.Perfil == PerfilAcesso.AdministradorRede
                            && vinculo.UnidadeId == null)))
            select new ContextoAutorizado(
                unidade.OrganizacaoId,
                unidade.Id,
                unidade.Nome))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private sealed record ContextoAutorizado(
        Guid OrganizacaoId,
        Guid UnidadeId,
        string UnidadeNome);
}

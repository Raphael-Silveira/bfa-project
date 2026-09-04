using BFA.Application.Acessos;
using BFA.Domain.Unidades;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Franqueadora.Unidades;

public sealed class UnidadesFranqueadoraServico(
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IUnidadesFranqueadoraRepositorio repositorio,
    TimeProvider timeProvider,
    ILogger<UnidadesFranqueadoraServico> logger)
    : IUnidadesFranqueadoraConsulta, IUnidadesFranqueadoraServico
{
    public async Task<ResultadoUnidadesFranqueadora<IReadOnlyList<UnidadeResumo>>> ListarAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado, null);
        }

        var unidades = await repositorio.ListarAsync(organizacaoId, cancellationToken);
        return new(EstadoGerenciamentoUnidade.Sucesso, unidades);
    }

    public async Task<ResultadoUnidadesFranqueadora<UnidadeDetalhe>> ObterAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado, null);
        }

        var unidade = await repositorio.ObterDetalheAsync(
            organizacaoId,
            unidadeId,
            cancellationToken);

        return unidade is null
            ? new(EstadoGerenciamentoUnidade.NaoEncontrada, null)
            : new(EstadoGerenciamentoUnidade.Sucesso, unidade);
    }

    public async Task<ResultadoOperacaoUnidade> CriarAsync(
        Guid usuarioId,
        CriarUnidadeSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAsync(usuarioId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado);
        }

        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;
        var unidade = new Unidade(
            Guid.NewGuid(),
            organizacaoId,
            solicitacao.Nome,
            solicitacao.Slug,
            agoraUtc);

        if (await repositorio.ExisteSlugAsync(
                organizacaoId,
                unidade.Slug,
                unidadeIgnoradaId: null,
                cancellationToken))
        {
            return new(EstadoGerenciamentoUnidade.SlugDuplicado);
        }

        repositorio.Adicionar(unidade);
        var resultado = await SalvarAsync(cancellationToken);
        if (resultado.Estado == EstadoGerenciamentoUnidade.Sucesso)
        {
            logger.LogInformation("CriarUnidade concluído para organização {OrganizacaoId}", organizacaoId);
        }
        return resultado;
    }

    public async Task<ResultadoOperacaoUnidade> AtualizarAsync(
        Guid usuarioId,
        Guid unidadeId,
        AtualizarUnidadeSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAsync(usuarioId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado);
        }

        var unidade = await repositorio.ObterParaAlteracaoAsync(
            organizacaoId,
            unidadeId,
            cancellationToken);

        if (unidade is null)
        {
            return new(EstadoGerenciamentoUnidade.NaoEncontrada);
        }

        var slugNormalizado = Unidade.NormalizarSlug(solicitacao.Slug);

        if (await repositorio.ExisteSlugAsync(
                organizacaoId,
                slugNormalizado,
                unidadeId,
                cancellationToken))
        {
            return new(EstadoGerenciamentoUnidade.SlugDuplicado);
        }

        unidade.Atualizar(
            solicitacao.Nome,
            slugNormalizado,
            timeProvider.GetUtcNow().UtcDateTime);

        var resultado = await SalvarAsync(cancellationToken);
        if (resultado.Estado == EstadoGerenciamentoUnidade.Sucesso)
        {
            logger.LogInformation("AtualizarUnidade concluído para unidade {UnidadeId}", unidadeId);
        }
        return resultado;
    }

    public Task<ResultadoOperacaoUnidade> AtivarAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return AlterarEstadoAsync(usuarioId, unidadeId, ativar: true, cancellationToken);
    }

    public Task<ResultadoOperacaoUnidade> DesativarAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return AlterarEstadoAsync(usuarioId, unidadeId, ativar: false, cancellationToken);
    }

    private async Task<ResultadoOperacaoUnidade> AlterarEstadoAsync(
        Guid usuarioId,
        Guid unidadeId,
        bool ativar,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado);
        }

        var unidade = await repositorio.ObterParaAlteracaoAsync(
            organizacaoId,
            unidadeId,
            cancellationToken);

        if (unidade is null)
        {
            return new(EstadoGerenciamentoUnidade.NaoEncontrada);
        }

        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (ativar)
        {
            unidade.Ativar(agoraUtc);
        }
        else
        {
            unidade.Desativar(agoraUtc);
        }

        var resultado = await SalvarAsync(cancellationToken);
        if (resultado.Estado == EstadoGerenciamentoUnidade.Sucesso)
        {
            var operacao = ativar ? "AtivarUnidade" : "DesativarUnidade";
            logger.LogInformation("{Operacao} concluído para unidade {UnidadeId}", operacao, unidadeId);
        }
        return resultado;
    }

    private async Task<ContextoOrganizacao> ObterContextoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty)
        {
            return new(EstadoGerenciamentoUnidade.SemAcesso, null);
        }

        var organizacoes = await acessoUsuarioConsulta
            .ListarOrganizacoesAdministradorRedeAsync(usuarioId, cancellationToken);

        return organizacoes.Count switch
        {
            0 => new(EstadoGerenciamentoUnidade.SemAcesso, null),
            1 => new(EstadoGerenciamentoUnidade.Sucesso, organizacoes[0]),
            _ => new(EstadoGerenciamentoUnidade.SelecaoOrganizacaoNecessaria, null)
        };
    }

    private async Task<ResultadoOperacaoUnidade> SalvarAsync(
        CancellationToken cancellationToken)
    {
        var resultado = await repositorio.SalvarAsync(cancellationToken);

        return resultado == ResultadoPersistenciaUnidade.SlugDuplicado
            ? new(EstadoGerenciamentoUnidade.SlugDuplicado)
            : new(EstadoGerenciamentoUnidade.Sucesso);
    }

    private sealed record ContextoOrganizacao(
        EstadoGerenciamentoUnidade Estado,
        Guid? OrganizacaoId);
}

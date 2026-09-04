using BFA.Application.Acessos;
using BFA.Domain.Acessos;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Franqueadora.AcessosUnidade;

public sealed class AcessosUnidadeServico(
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IUsuarioPorEmailConsulta usuarioPorEmailConsulta,
    IAcessosUnidadeRepositorio repositorio,
    TimeProvider timeProvider,
    ILogger<AcessosUnidadeServico> logger)
    : IAcessosUnidadeConsulta, IAcessosUnidadeServico
{
    public async Task<ResultadoAcessosUnidade<AcessosUnidadeDetalhe>> ObterAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado, null);
        }

        var unidade = await repositorio.ObterUnidadeAsync(
            organizacaoId,
            unidadeId,
            cancellationToken);

        if (unidade is null)
        {
            return new(EstadoGerenciamentoAcessoUnidade.UnidadeNaoEncontrada, null);
        }

        var administradores = await repositorio.ListarAdministradoresAsync(
            organizacaoId,
            unidadeId,
            cancellationToken);

        return new(
            EstadoGerenciamentoAcessoUnidade.Sucesso,
            new AcessosUnidadeDetalhe(unidade, administradores));
    }

    public async Task<ResultadoOperacaoAcessoUnidade> AdicionarAsync(
        Guid usuarioId,
        Guid unidadeId,
        AdicionarAdministradorUnidadeSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAsync(usuarioId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado);
        }

        if (!await UnidadeExisteAsync(organizacaoId, unidadeId, cancellationToken))
        {
            return new(EstadoGerenciamentoAcessoUnidade.UnidadeNaoEncontrada);
        }

        if (string.IsNullOrWhiteSpace(solicitacao.Email))
        {
            return new(EstadoGerenciamentoAcessoUnidade.UsuarioNaoEncontrado);
        }

        var usuario = await usuarioPorEmailConsulta.ObterAsync(
            solicitacao.Email,
            cancellationToken);

        if (usuario is null)
        {
            return new(EstadoGerenciamentoAcessoUnidade.UsuarioNaoEncontrado);
        }

        var vinculoExistente = await repositorio.ObterAdministradorPorUsuarioAsync(
            organizacaoId,
            unidadeId,
            usuario.Id,
            cancellationToken);

        if (vinculoExistente is { Ativo: true })
        {
            return new(EstadoGerenciamentoAcessoUnidade.VinculoJaAtivo);
        }

        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (vinculoExistente is not null)
        {
            vinculoExistente.Ativar(agoraUtc);
        }
        else
        {
            repositorio.Adicionar(new VinculoAcesso(
                Guid.NewGuid(),
                usuario.Id,
                organizacaoId,
                unidadeId,
                PerfilAcesso.AdministradorUnidade,
                agoraUtc));
        }

        var resultado = await SalvarAsync(cancellationToken);
        if (resultado.Estado == EstadoGerenciamentoAcessoUnidade.Sucesso)
        {
            logger.LogInformation("AdicionarAcessoUnidade concluído para unidade {UnidadeId}", unidadeId);
        }
        return resultado;
    }

    public Task<ResultadoOperacaoAcessoUnidade> AtivarAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid vinculoId,
        CancellationToken cancellationToken)
    {
        return AlterarEstadoAsync(
            usuarioId,
            unidadeId,
            vinculoId,
            ativar: true,
            cancellationToken);
    }

    public Task<ResultadoOperacaoAcessoUnidade> DesativarAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid vinculoId,
        CancellationToken cancellationToken)
    {
        return AlterarEstadoAsync(
            usuarioId,
            unidadeId,
            vinculoId,
            ativar: false,
            cancellationToken);
    }

    private async Task<ResultadoOperacaoAcessoUnidade> AlterarEstadoAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid vinculoId,
        bool ativar,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado);
        }

        if (!await UnidadeExisteAsync(organizacaoId, unidadeId, cancellationToken))
        {
            return new(EstadoGerenciamentoAcessoUnidade.UnidadeNaoEncontrada);
        }

        var vinculo = await repositorio.ObterAdministradorPorVinculoAsync(
            organizacaoId,
            unidadeId,
            vinculoId,
            cancellationToken);

        if (vinculo is null)
        {
            return new(EstadoGerenciamentoAcessoUnidade.VinculoNaoEncontrado);
        }

        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (ativar)
        {
            vinculo.Ativar(agoraUtc);
        }
        else
        {
            vinculo.Desativar(agoraUtc);
        }

        var resultado = await SalvarAsync(cancellationToken);
        if (resultado.Estado == EstadoGerenciamentoAcessoUnidade.Sucesso)
        {
            var operacao = ativar ? "AtivarAcessoUnidade" : "DesativarAcessoUnidade";
            logger.LogInformation("{Operacao} concluído para vínculo {VinculoId}", operacao, vinculoId);
        }
        return resultado;
    }

    private async Task<bool> UnidadeExisteAsync(
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        return await repositorio.ObterUnidadeAsync(
            organizacaoId,
            unidadeId,
            cancellationToken) is not null;
    }

    private async Task<ContextoOrganizacao> ObterContextoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty)
        {
            return new(EstadoGerenciamentoAcessoUnidade.SemAcesso, null);
        }

        var organizacoes = await acessoUsuarioConsulta
            .ListarOrganizacoesAdministradorRedeAsync(usuarioId, cancellationToken);

        return organizacoes.Count switch
        {
            0 => new(EstadoGerenciamentoAcessoUnidade.SemAcesso, null),
            1 => new(EstadoGerenciamentoAcessoUnidade.Sucesso, organizacoes[0]),
            _ => new(
                EstadoGerenciamentoAcessoUnidade.SelecaoOrganizacaoNecessaria,
                null)
        };
    }

    private async Task<ResultadoOperacaoAcessoUnidade> SalvarAsync(
        CancellationToken cancellationToken)
    {
        var resultado = await repositorio.SalvarAsync(cancellationToken);

        return resultado == ResultadoPersistenciaAcessoUnidade.VinculoDuplicado
            ? new(EstadoGerenciamentoAcessoUnidade.VinculoJaAtivo)
            : new(EstadoGerenciamentoAcessoUnidade.Sucesso);
    }

    private sealed record ContextoOrganizacao(
        EstadoGerenciamentoAcessoUnidade Estado,
        Guid? OrganizacaoId);
}

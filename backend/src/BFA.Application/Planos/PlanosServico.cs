using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Domain.Planos;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Planos;

public enum FiltroPlanos
{
    Ativos,
    Inativos,
    Todos
}

public enum EstadoPlanos
{
    Sucesso,
    SemAcesso,
    ContextoNaoEncontrado,
    PlanoNaoEncontrado,
    DadosInvalidos,
    VigenciaInvalida,
    SemVersaoAberta,
    ConflitoConcorrencia,
    Falha
}

public enum EstadoPersistenciaPlano
{
    Sucesso,
    PlanoNaoEncontrado,
    VigenciaInvalida,
    SemVersaoAberta,
    ConflitoConcorrencia,
    DadosInvalidos,
    Falha
}

public sealed record PlanoTermosSolicitacao(
    int DuracaoMeses,
    int FrequenciaSemanal,
    decimal ValorMensal,
    bool CobraMatricula,
    decimal? ValorMatricula,
    DateOnly VigenciaInicio);

public sealed record CriarPlanoSolicitacao(
    string Nome,
    PlanoTermosSolicitacao Termos);

public sealed record PlanoVersaoResumo(
    Guid Id,
    int NumeroVersao,
    int DuracaoMeses,
    int FrequenciaSemanal,
    decimal ValorMensal,
    bool CobraMatricula,
    decimal? ValorMatricula,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim);

public sealed record PlanoResumo(
    Guid Id,
    string Nome,
    bool Ativo,
    PlanoVersaoResumo? VersaoAtual);

public sealed record PlanoDetalheResumo(
    Guid Id,
    Guid OrganizacaoId,
    Guid? UnidadeId,
    string Nome,
    bool Ativo,
    IReadOnlyList<PlanoVersaoResumo> Versoes)
{
    public PlanoVersaoResumo? VersaoAtual =>
        Versoes.SingleOrDefault(versao => versao.VigenciaFim is null);
}

public sealed record ContextoPlanosResumo(
    Guid OrganizacaoId,
    Guid? UnidadeId,
    string? NomeUnidade,
    bool PodeGerenciar,
    bool PossuiFranqueadoAtivo);

public sealed record ListaPlanosResultado(
    ContextoPlanosResumo Contexto,
    IReadOnlyList<PlanoResumo> Planos);

public sealed record DetalhePlanoResultado(
    ContextoPlanosResumo Contexto,
    PlanoDetalheResumo Plano);

public sealed record ResultadoPlanos<T>(EstadoPlanos Estado, T? Valor = default);

public interface IPlanosRepositorio
{
    Task<IReadOnlyList<PlanoResumo>> ListarAsync(
        Guid organizacaoId,
        Guid? unidadeId,
        FiltroPlanos filtro,
        CancellationToken cancellationToken);

    Task<PlanoDetalheResumo?> ObterAsync(
        Guid organizacaoId,
        Guid? unidadeId,
        Guid planoId,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaPlano> CriarAsync(
        Plano plano,
        PlanoVersao versao,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaPlano> CriarNovaVersaoAsync(
        Guid organizacaoId,
        Guid? unidadeId,
        Guid planoId,
        PlanoTermosSolicitacao termos,
        Guid usuarioId,
        DateTime agoraUtc,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaPlano> AlterarEstadoAsync(
        Guid organizacaoId,
        Guid? unidadeId,
        Guid planoId,
        bool ativar,
        Guid usuarioId,
        DateTime agoraUtc,
        CancellationToken cancellationToken);
}

public interface IPlanosServico
{
    Task<ResultadoPlanos<ListaPlanosResultado>> ListarRedeAsync(
        Guid usuarioId, FiltroPlanos filtro, CancellationToken cancellationToken);
    Task<ResultadoPlanos<DetalhePlanoResultado>> ObterRedeAsync(
        Guid usuarioId, Guid planoId, CancellationToken cancellationToken);
    Task<ResultadoPlanos<Guid>> CriarRedeAsync(
        Guid usuarioId, CriarPlanoSolicitacao solicitacao,
        CancellationToken cancellationToken);
    Task<ResultadoPlanos<Guid>> CriarNovaVersaoRedeAsync(
        Guid usuarioId, Guid planoId, PlanoTermosSolicitacao solicitacao,
        CancellationToken cancellationToken);
    Task<ResultadoPlanos<Guid>> AlterarEstadoRedeAsync(
        Guid usuarioId, Guid planoId, bool ativar, CancellationToken cancellationToken);

    Task<ResultadoPlanos<ListaPlanosResultado>> ListarLocalAsync(
        Guid usuarioId, Guid unidadeId, FiltroPlanos filtro,
        CancellationToken cancellationToken);
    Task<ResultadoPlanos<DetalhePlanoResultado>> ObterLocalAsync(
        Guid usuarioId, Guid unidadeId, Guid planoId,
        CancellationToken cancellationToken);
    Task<ResultadoPlanos<Guid>> CriarLocalAsync(
        Guid usuarioId, Guid unidadeId, CriarPlanoSolicitacao solicitacao,
        CancellationToken cancellationToken);
    Task<ResultadoPlanos<Guid>> CriarNovaVersaoLocalAsync(
        Guid usuarioId, Guid unidadeId, Guid planoId, PlanoTermosSolicitacao solicitacao,
        CancellationToken cancellationToken);
    Task<ResultadoPlanos<Guid>> AlterarEstadoLocalAsync(
        Guid usuarioId, Guid unidadeId, Guid planoId, bool ativar,
        CancellationToken cancellationToken);
}

public sealed class PlanosServico(
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IGovernancaOperacionalUnidade governancaOperacional,
    IPlanosRepositorio repositorio,
    TimeProvider timeProvider,
    ILogger<PlanosServico> logger) : IPlanosServico
{
    public async Task<ResultadoPlanos<ListaPlanosResultado>> ListarRedeAsync(
        Guid usuarioId, FiltroPlanos filtro, CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoRedeAsync(usuarioId, cancellationToken);
        if (contexto is null) return new(EstadoPlanos.SemAcesso);
        var planos = await repositorio.ListarAsync(
            contexto.OrganizacaoId, null, filtro, cancellationToken);
        return new(EstadoPlanos.Sucesso, new(contexto, planos));
    }

    public async Task<ResultadoPlanos<DetalhePlanoResultado>> ObterRedeAsync(
        Guid usuarioId, Guid planoId, CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoRedeAsync(usuarioId, cancellationToken);
        return await ObterAsync(contexto, planoId, cancellationToken);
    }

    public async Task<ResultadoPlanos<Guid>> CriarRedeAsync(
        Guid usuarioId, CriarPlanoSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoRedeAsync(usuarioId, cancellationToken);
        return await CriarAsync(contexto, usuarioId, solicitacao, cancellationToken);
    }

    public async Task<ResultadoPlanos<Guid>> CriarNovaVersaoRedeAsync(
        Guid usuarioId, Guid planoId, PlanoTermosSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoRedeAsync(usuarioId, cancellationToken);
        return await NovaVersaoAsync(
            contexto, usuarioId, planoId, solicitacao, cancellationToken);
    }

    public async Task<ResultadoPlanos<Guid>> AlterarEstadoRedeAsync(
        Guid usuarioId, Guid planoId, bool ativar, CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoRedeAsync(usuarioId, cancellationToken);
        return await AlterarEstadoAsync(
            contexto, usuarioId, planoId, ativar, cancellationToken);
    }

    public async Task<ResultadoPlanos<ListaPlanosResultado>> ListarLocalAsync(
        Guid usuarioId, Guid unidadeId, FiltroPlanos filtro,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoLocalAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        if (contexto.Estado != EstadoPlanos.Sucesso || contexto.Valor is null)
            return new(contexto.Estado);
        var planos = await repositorio.ListarAsync(
            contexto.Valor.OrganizacaoId, unidadeId, filtro, cancellationToken);
        return new(EstadoPlanos.Sucesso, new(contexto.Valor, planos));
    }

    public async Task<ResultadoPlanos<DetalhePlanoResultado>> ObterLocalAsync(
        Guid usuarioId, Guid unidadeId, Guid planoId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoLocalAsync(
            usuarioId, unidadeId, exigirGerenciamento: false, cancellationToken);
        return await ObterAsync(contexto.Valor, planoId, cancellationToken, contexto.Estado);
    }

    public async Task<ResultadoPlanos<Guid>> CriarLocalAsync(
        Guid usuarioId, Guid unidadeId, CriarPlanoSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoLocalAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        return await CriarAsync(
            contexto.Valor, usuarioId, solicitacao, cancellationToken, contexto.Estado);
    }

    public async Task<ResultadoPlanos<Guid>> CriarNovaVersaoLocalAsync(
        Guid usuarioId, Guid unidadeId, Guid planoId, PlanoTermosSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoLocalAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        return await NovaVersaoAsync(
            contexto.Valor, usuarioId, planoId, solicitacao,
            cancellationToken, contexto.Estado);
    }

    public async Task<ResultadoPlanos<Guid>> AlterarEstadoLocalAsync(
        Guid usuarioId, Guid unidadeId, Guid planoId, bool ativar,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoLocalAsync(
            usuarioId, unidadeId, exigirGerenciamento: true, cancellationToken);
        return await AlterarEstadoAsync(
            contexto.Valor, usuarioId, planoId, ativar,
            cancellationToken, contexto.Estado);
    }

    private async Task<ContextoPlanosResumo?> ObterContextoRedeAsync(
        Guid usuarioId, CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty) return null;
        var organizacoes = await acessoUsuarioConsulta
            .ListarOrganizacoesAdministradorRedeAsync(usuarioId, cancellationToken);
        return organizacoes.Count == 1
            ? new(organizacoes[0], null, null, true, false)
            : null;
    }

    private async Task<ResultadoPlanos<ContextoPlanosResumo>> ObterContextoLocalAsync(
        Guid usuarioId, Guid unidadeId, bool exigirGerenciamento,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty || unidadeId == Guid.Empty)
            return new(EstadoPlanos.SemAcesso);
        var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId, cancellationToken);
        if (unidade is null) return new(EstadoPlanos.ContextoNaoEncontrado);
        var governanca = await governancaOperacional.ObterAsync(
            usuarioId, unidade.OrganizacaoId, unidadeId, cancellationToken);
        if (!governanca.PodeAcessar
            || exigirGerenciamento && !governanca.PodeGerenciarPlanoLocal)
            return new(EstadoPlanos.SemAcesso);
        return new(EstadoPlanos.Sucesso, new(
            unidade.OrganizacaoId,
            unidadeId,
            unidade.Nome,
            governanca.PodeGerenciarPlanoLocal,
            governanca.PossuiFranqueadoAtivo));
    }

    private async Task<ResultadoPlanos<DetalhePlanoResultado>> ObterAsync(
        ContextoPlanosResumo? contexto, Guid planoId,
        CancellationToken cancellationToken,
        EstadoPlanos estadoSemContexto = EstadoPlanos.SemAcesso)
    {
        if (contexto is null) return new(estadoSemContexto);
        var plano = await repositorio.ObterAsync(
            contexto.OrganizacaoId, contexto.UnidadeId, planoId, cancellationToken);
        return plano is null
            ? new(EstadoPlanos.PlanoNaoEncontrado)
            : new(EstadoPlanos.Sucesso, new(contexto, plano));
    }

    private async Task<ResultadoPlanos<Guid>> CriarAsync(
        ContextoPlanosResumo? contexto, Guid usuarioId, CriarPlanoSolicitacao solicitacao,
        CancellationToken cancellationToken,
        EstadoPlanos estadoSemContexto = EstadoPlanos.SemAcesso)
    {
        if (contexto is null) return new(estadoSemContexto);
        ArgumentNullException.ThrowIfNull(solicitacao);
        try
        {
            var agora = timeProvider.GetUtcNow().UtcDateTime;
            var plano = new Plano(
                Guid.NewGuid(), contexto.OrganizacaoId, contexto.UnidadeId,
                solicitacao.Nome, usuarioId, agora);
            var termos = solicitacao.Termos;
            var versao = new PlanoVersao(
                Guid.NewGuid(), contexto.OrganizacaoId, plano.Id, 1,
                termos.DuracaoMeses, termos.FrequenciaSemanal, termos.ValorMensal,
                termos.CobraMatricula, termos.ValorMatricula, termos.VigenciaInicio,
                null, usuarioId, agora);
            var resultado = MapearPersistencia(
                await repositorio.CriarAsync(plano, versao, cancellationToken), plano.Id);
            if (resultado.Estado == EstadoPlanos.Sucesso)
            {
                logger.LogInformation("CriarPlano concluído para organização {OrganizacaoId}", contexto.OrganizacaoId);
            }
            return resultado;
        }
        catch (ArgumentException)
        {
            return new(EstadoPlanos.DadosInvalidos);
        }
    }

    private async Task<ResultadoPlanos<Guid>> NovaVersaoAsync(
        ContextoPlanosResumo? contexto, Guid usuarioId, Guid planoId,
        PlanoTermosSolicitacao solicitacao, CancellationToken cancellationToken,
        EstadoPlanos estadoSemContexto = EstadoPlanos.SemAcesso)
    {
        if (contexto is null) return new(estadoSemContexto);
        ArgumentNullException.ThrowIfNull(solicitacao);
        if (!TermosValidos(solicitacao)) return new(EstadoPlanos.DadosInvalidos);
        var resultado = MapearPersistencia(
            await repositorio.CriarNovaVersaoAsync(
                contexto.OrganizacaoId, contexto.UnidadeId, planoId, solicitacao,
                usuarioId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken), planoId);
        if (resultado.Estado == EstadoPlanos.Sucesso)
        {
            logger.LogInformation("NovaVersaoPlano concluído para plano {PlanoId}", planoId);
        }
        return resultado;
    }

    private async Task<ResultadoPlanos<Guid>> AlterarEstadoAsync(
        ContextoPlanosResumo? contexto, Guid usuarioId, Guid planoId, bool ativar,
        CancellationToken cancellationToken,
        EstadoPlanos estadoSemContexto = EstadoPlanos.SemAcesso)
    {
        if (contexto is null) return new(estadoSemContexto);
        var resultado = MapearPersistencia(
            await repositorio.AlterarEstadoAsync(
                contexto.OrganizacaoId, contexto.UnidadeId, planoId, ativar,
                usuarioId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken), planoId);
        if (resultado.Estado == EstadoPlanos.Sucesso)
        {
            var operacao = ativar ? "AtivarPlano" : "DesativarPlano";
            logger.LogInformation("{Operacao} concluído para plano {PlanoId}", operacao, planoId);
        }
        return resultado;
    }

    private static bool TermosValidos(PlanoTermosSolicitacao termos) =>
        termos.DuracaoMeses > 0
        && termos.FrequenciaSemanal is >= 1 and <= 7
        && termos.ValorMensal > 0
        && termos.VigenciaInicio != default
        && (termos.CobraMatricula
            ? termos.ValorMatricula is > 0
            : termos.ValorMatricula is null);

    private static ResultadoPlanos<Guid> MapearPersistencia(
        EstadoPersistenciaPlano estado, Guid planoId) => estado switch
    {
        EstadoPersistenciaPlano.Sucesso => new(EstadoPlanos.Sucesso, planoId),
        EstadoPersistenciaPlano.PlanoNaoEncontrado => new(EstadoPlanos.PlanoNaoEncontrado),
        EstadoPersistenciaPlano.VigenciaInvalida => new(EstadoPlanos.VigenciaInvalida),
        EstadoPersistenciaPlano.SemVersaoAberta => new(EstadoPlanos.SemVersaoAberta),
        EstadoPersistenciaPlano.ConflitoConcorrencia =>
            new(EstadoPlanos.ConflitoConcorrencia),
        EstadoPersistenciaPlano.DadosInvalidos => new(EstadoPlanos.DadosInvalidos),
        _ => new(EstadoPlanos.Falha)
    };
}

using BFA.Application.Unidades;
using BFA.Domain.DayUses;
using Microsoft.Extensions.Logging;

namespace BFA.Application.DayUses;

public enum PerfilOperacaoDayUse { AdministradorUnidade, Professor }

public enum EstadoDayUse
{
    Sucesso,
    SemAcesso,
    UnidadeNaoEncontrada,
    ValorNaoConfigurado,
    DadosInvalidos,
    ParticipanteNaoEncontrado,
    DayUseDuplicado,
    DayUseNaoEncontrado,
    PagamentoInvalido,
    Falha
}

public sealed record DayUseUnidadeResumo(Guid OrganizacaoId, Guid UnidadeId, string Nome, decimal? ValorSugerido);
public sealed record DayUseAlunoResumo(Guid AlunoId, string Nome, string? Telefone);
public sealed record DayUseListaItem(
    Guid DayUseId, DateOnly DataUso, Guid? AlunoId, string Participante, bool EhAluno,
    string? Telefone, string? Email, decimal ValorSugerido, decimal ValorCobrado, bool Pago,
    Guid CriadoPorUsuarioId, string NomeRegistrador, bool PodeExcluir = false);
public sealed record DayUsePagina(IReadOnlyList<DayUseListaItem> Itens, int PaginaAtual, int TotalPaginas, int TotalItens);
public sealed record FiltroDayUses(DateOnly? DataInicial, DateOnly? DataFinal, string? Participante, int Pagina = 1, int TamanhoPagina = 10);
public sealed record RegistrarDayUseSolicitacao(
    Guid? AlunoId, string? NomeAvulso, string? TelefoneAvulso, string? EmailAvulso,
    DateOnly DataUso, decimal ValorCobrado, bool Pago);

public interface IDayUsesRepositorio
{
    Task<DayUseUnidadeResumo?> ObterUnidadeAsync(Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken);
    Task<bool> AtualizarValorSugeridoAsync(Guid organizacaoId, Guid unidadeId, decimal? valor, DateTime agoraUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<DayUseAlunoResumo>> ListarAlunosAsync(Guid organizacaoId, Guid unidadeId, string? texto, CancellationToken cancellationToken);
    Task<bool> ExisteAlunoNaUnidadeAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, CancellationToken cancellationToken);
    Task<bool> ExisteDuplicadoAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly dataUso, CancellationToken cancellationToken);
    Task<bool> CriarAsync(DayUse dayUse, CancellationToken cancellationToken);
    Task<DayUsePagina> ListarAsync(Guid organizacaoId, Guid unidadeId, FiltroDayUses filtro, CancellationToken cancellationToken);
    Task<bool> MarcarComoPagoAsync(Guid organizacaoId, Guid unidadeId, Guid dayUseId, CancellationToken cancellationToken);
    Task<bool> ExcluirAsync(Guid organizacaoId, Guid unidadeId, Guid dayUseId, Guid? criadoPorUsuarioId, CancellationToken cancellationToken);
}

public interface IDayUsesServico
{
    Task<(EstadoDayUse Estado, DayUseUnidadeResumo? Unidade)> ObterUnidadeAsync(Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, CancellationToken cancellationToken);
    Task<EstadoDayUse> ConfigurarValorAsync(Guid usuarioId, Guid unidadeId, decimal? valor, CancellationToken cancellationToken);
    Task<(EstadoDayUse Estado, IReadOnlyList<DayUseAlunoResumo> Alunos)> ListarAlunosAsync(Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, string? texto, CancellationToken cancellationToken);
    Task<EstadoDayUse> RegistrarAsync(Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, RegistrarDayUseSolicitacao solicitacao, CancellationToken cancellationToken);
    Task<(EstadoDayUse Estado, DayUsePagina? Pagina)> ListarAsync(Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, FiltroDayUses filtro, CancellationToken cancellationToken);
    Task<EstadoDayUse> MarcarComoPagoAsync(Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, Guid dayUseId, CancellationToken cancellationToken);
    Task<EstadoDayUse> ExcluirAsync(Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, Guid dayUseId, CancellationToken cancellationToken);
}

public sealed class DayUsesServico(
    IDayUsesRepositorio repositorio,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    TimeProvider timeProvider,
    ILogger<DayUsesServico> logger) : IDayUsesServico
{
    public async Task<(EstadoDayUse Estado, DayUseUnidadeResumo? Unidade)> ObterUnidadeAsync(
        Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, CancellationToken cancellationToken)
    {
        var unidade = await ResolverUnidadeAsync(usuarioId, unidadeId, perfil, cancellationToken);
        if (unidade is null) return (EstadoDayUse.SemAcesso, null);
        return (EstadoDayUse.Sucesso, await repositorio.ObterUnidadeAsync(unidade.OrganizacaoId, unidadeId, cancellationToken));
    }

    public async Task<EstadoDayUse> ConfigurarValorAsync(Guid usuarioId, Guid unidadeId, decimal? valor, CancellationToken cancellationToken)
    {
        var unidade = await ResolverUnidadeAsync(usuarioId, unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, cancellationToken);
        if (unidade is null) return EstadoDayUse.SemAcesso;
        if (valor is < 0) return EstadoDayUse.DadosInvalidos;
        return await repositorio.AtualizarValorSugeridoAsync(unidade.OrganizacaoId, unidadeId, valor, timeProvider.GetUtcNow().UtcDateTime, cancellationToken)
            ? EstadoDayUse.Sucesso : EstadoDayUse.UnidadeNaoEncontrada;
    }

    public async Task<(EstadoDayUse Estado, IReadOnlyList<DayUseAlunoResumo> Alunos)> ListarAlunosAsync(
        Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, string? texto, CancellationToken cancellationToken)
    {
        var unidade = await ResolverUnidadeAsync(usuarioId, unidadeId, perfil, cancellationToken);
        return unidade is null
            ? (EstadoDayUse.SemAcesso, [])
            : (EstadoDayUse.Sucesso, await repositorio.ListarAlunosAsync(unidade.OrganizacaoId, unidadeId, texto, cancellationToken));
    }

    public async Task<EstadoDayUse> RegistrarAsync(
        Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil,
        RegistrarDayUseSolicitacao solicitacao, CancellationToken cancellationToken)
    {
        var unidade = await ResolverUnidadeAsync(usuarioId, unidadeId, perfil, cancellationToken);
        if (unidade is null) return EstadoDayUse.SemAcesso;
        if (solicitacao.DataUso == default || solicitacao.ValorCobrado < 0) return EstadoDayUse.DadosInvalidos;
        var unidadeDados = await repositorio.ObterUnidadeAsync(unidade.OrganizacaoId, unidadeId, cancellationToken);
        if (unidadeDados?.ValorSugerido is not { } valorSugerido) return EstadoDayUse.ValorNaoConfigurado;

        if (solicitacao.AlunoId is { } alunoId)
        {
            if (!await repositorio.ExisteAlunoNaUnidadeAsync(unidade.OrganizacaoId, unidadeId, alunoId, cancellationToken))
                return EstadoDayUse.ParticipanteNaoEncontrado;
            if (await repositorio.ExisteDuplicadoAsync(unidade.OrganizacaoId, unidadeId, alunoId, solicitacao.DataUso, cancellationToken))
                return EstadoDayUse.DayUseDuplicado;
        }
        else if (string.IsNullOrWhiteSpace(solicitacao.NomeAvulso))
        {
            return EstadoDayUse.DadosInvalidos;
        }

        DayUse dayUse;
        try
        {
            dayUse = new DayUse(Guid.NewGuid(), unidade.OrganizacaoId, unidadeId,
                solicitacao.AlunoId, solicitacao.NomeAvulso, solicitacao.TelefoneAvulso,
                solicitacao.EmailAvulso, solicitacao.DataUso, valorSugerido,
                solicitacao.ValorCobrado, solicitacao.Pago, usuarioId,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (ArgumentException)
        {
            return EstadoDayUse.DadosInvalidos;
        }

        if (!await repositorio.CriarAsync(dayUse, cancellationToken))
            return solicitacao.AlunoId is not null ? EstadoDayUse.DayUseDuplicado : EstadoDayUse.Falha;

        logger.LogInformation("Day Use registrado: {DayUseId} na unidade {UnidadeId}", dayUse.Id, unidadeId);
        return EstadoDayUse.Sucesso;
    }

    public async Task<(EstadoDayUse Estado, DayUsePagina? Pagina)> ListarAsync(
        Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, FiltroDayUses filtro, CancellationToken cancellationToken)
    {
        var unidade = await ResolverUnidadeAsync(usuarioId, unidadeId, perfil, cancellationToken);
        if (unidade is null) return (EstadoDayUse.SemAcesso, null);
        var pagina = await repositorio.ListarAsync(unidade.OrganizacaoId, unidadeId, filtro, cancellationToken);
        return (EstadoDayUse.Sucesso, pagina with
        {
            Itens = pagina.Itens.Select(item => item with
            {
                PodeExcluir = perfil == PerfilOperacaoDayUse.AdministradorUnidade
                    || item.CriadoPorUsuarioId == usuarioId
            }).ToArray()
        });
    }

    public async Task<EstadoDayUse> MarcarComoPagoAsync(Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, Guid dayUseId, CancellationToken cancellationToken)
    {
        var unidade = await ResolverUnidadeAsync(usuarioId, unidadeId, perfil, cancellationToken);
        if (unidade is null) return EstadoDayUse.SemAcesso;
        return await repositorio.MarcarComoPagoAsync(unidade.OrganizacaoId, unidadeId, dayUseId, cancellationToken)
            ? EstadoDayUse.Sucesso : EstadoDayUse.DayUseNaoEncontrado;
    }

    public async Task<EstadoDayUse> ExcluirAsync(Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, Guid dayUseId, CancellationToken cancellationToken)
    {
        var unidade = await ResolverUnidadeAsync(usuarioId, unidadeId, perfil, cancellationToken);
        if (unidade is null) return EstadoDayUse.SemAcesso;
        Guid? criadorPermitido = perfil == PerfilOperacaoDayUse.Professor ? usuarioId : null;
        logger.LogDebug("Exclusão de Day Use iniciada por {UsuarioId} na unidade {UnidadeId}", usuarioId, unidadeId);
        var excluido = await repositorio.ExcluirAsync(unidade.OrganizacaoId, unidadeId, dayUseId, criadorPermitido, cancellationToken);
        if (!excluido)
        {
            logger.LogWarning("Exclusão de Day Use não realizada para {DayUseId} na unidade {UnidadeId}", dayUseId, unidadeId);
            return EstadoDayUse.DayUseNaoEncontrado;
        }
        logger.LogInformation("Day Use excluído: {DayUseId} na unidade {UnidadeId}", dayUseId, unidadeId);
        return EstadoDayUse.Sucesso;
    }

    private Task<UnidadeAcessoResumo?> ResolverUnidadeAsync(Guid usuarioId, Guid unidadeId, PerfilOperacaoDayUse perfil, CancellationToken cancellationToken) =>
        perfil == PerfilOperacaoDayUse.AdministradorUnidade
            ? unidadesUsuarioConsulta.ObterAdministradaAsync(usuarioId, unidadeId, cancellationToken)
            : unidadesUsuarioConsulta.ObterProfessorAsync(usuarioId, unidadeId, cancellationToken);
}

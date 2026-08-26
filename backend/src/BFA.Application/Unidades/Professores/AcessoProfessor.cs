using BFA.Application.Acessos;
using BFA.Domain.Acessos;

namespace BFA.Application.Unidades.Professores;

public enum EstadoAcessoProfessor
{
    Sucesso,
    SemAcesso,
    ProfessorNaoEncontrado,
    VinculoProfissionalInativo,
    NomeUsuarioInvalido,
    NomeUsuarioDuplicado,
    AcessoJaAtivo,
    AcessoNaoEncontrado,
    Falha
}

public sealed record AcessoProfessorResumo(
    Guid ProfessorId,
    string NomeCompleto,
    string? Email,
    Guid? UsuarioId,
    string? NomeUsuario,
    bool AcessoAtivo);

public sealed record ConcessaoAcessoProfessorResultado(
    EstadoAcessoProfessor Estado,
    Guid? UsuarioId = null,
    string? NomeUsuario = null,
    string? TokenDefinicaoSenha = null);

public interface IAcessoProfessorRepositorio
{
    Task<AcessoProfessorResumo?> ObterAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken);

    Task<ConcessaoAcessoProfessorResultado> ConcederAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        string nomeUsuario,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken);

    Task<EstadoAcessoProfessor> RevogarAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken);
}

public interface IAcessoProfessorServico
{
    Task<(EstadoAcessoProfessor Estado, AcessoProfessorResumo? Acesso)> ObterAsync(
        Guid administradorId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken);

    Task<ConcessaoAcessoProfessorResultado> ConcederAsync(
        Guid administradorId,
        Guid unidadeId,
        Guid professorId,
        string nomeUsuario,
        CancellationToken cancellationToken);

    Task<EstadoAcessoProfessor> RevogarAsync(
        Guid administradorId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken);
}

public sealed class AcessoProfessorServico(
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IAcessoProfessorRepositorio repositorio,
    TimeProvider timeProvider) : IAcessoProfessorServico
{
    public async Task<(EstadoAcessoProfessor Estado, AcessoProfessorResumo? Acesso)> ObterAsync(
        Guid administradorId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken)
    {
        var organizacaoId = await ObterOrganizacaoAutorizadaAsync(
            administradorId, unidadeId, cancellationToken);
        if (organizacaoId is null)
        {
            return (EstadoAcessoProfessor.SemAcesso, null);
        }

        var acesso = await repositorio.ObterAsync(
            organizacaoId.Value, unidadeId, professorId, cancellationToken);
        return acesso is null
            ? (EstadoAcessoProfessor.ProfessorNaoEncontrado, null)
            : (EstadoAcessoProfessor.Sucesso, acesso);
    }

    public async Task<ConcessaoAcessoProfessorResultado> ConcederAsync(
        Guid administradorId,
        Guid unidadeId,
        Guid professorId,
        string nomeUsuario,
        CancellationToken cancellationToken)
    {
        var organizacaoId = await ObterOrganizacaoAutorizadaAsync(
            administradorId, unidadeId, cancellationToken);
        if (organizacaoId is null)
        {
            return new(EstadoAcessoProfessor.SemAcesso);
        }

        return await repositorio.ConcederAsync(
            organizacaoId.Value,
            unidadeId,
            professorId,
            nomeUsuario,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }

    public async Task<EstadoAcessoProfessor> RevogarAsync(
        Guid administradorId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken)
    {
        var organizacaoId = await ObterOrganizacaoAutorizadaAsync(
            administradorId, unidadeId, cancellationToken);
        if (organizacaoId is null)
        {
            return EstadoAcessoProfessor.SemAcesso;
        }

        return await repositorio.RevogarAsync(
            organizacaoId.Value,
            unidadeId,
            professorId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }

    private async Task<Guid?> ObterOrganizacaoAutorizadaAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioId == Guid.Empty || unidadeId == Guid.Empty)
        {
            return null;
        }

        var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId, cancellationToken);
        if (unidade is null)
        {
            return null;
        }

        var autorizado = await acessoUsuarioConsulta.PossuiPerfilNaUnidadeAsync(
            usuarioId,
            unidade.OrganizacaoId,
            unidadeId,
            PerfilAcesso.AdministradorUnidade,
            cancellationToken);
        return autorizado ? unidade.OrganizacaoId : null;
    }
}

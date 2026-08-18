using BFA.Application.Acessos;
using BFA.Domain.Acessos;

namespace BFA.IntegrationTests;

public sealed class TestUsuarioAtual : IUsuarioAtual
{
    public bool Autenticado { get; set; }

    public Guid? UsuarioId { get; set; }
}

public sealed class TestAcessoUsuarioConsulta : IAcessoUsuarioConsulta
{
    private readonly List<TestVinculoAcesso> _vinculos = [];

    public int QuantidadeConsultasAdministradorRede { get; private set; }

    public void Limpar()
    {
        _vinculos.Clear();
        QuantidadeConsultasAdministradorRede = 0;
    }

    public void Adicionar(
        Guid usuarioId,
        Guid organizacaoId,
        Guid? unidadeId,
        PerfilAcesso perfil,
        bool ativo = true)
    {
        _vinculos.Add(new TestVinculoAcesso(
            usuarioId,
            organizacaoId,
            unidadeId,
            perfil,
            ativo));
    }

    public Task<bool> EhAdministradorRedeAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QuantidadeConsultasAdministradorRede++;
        return Task.FromResult(VinculosAtivos(usuarioId).Any(vinculo =>
            vinculo.Perfil == PerfilAcesso.AdministradorRede
            && vinculo.UnidadeId == null));
    }

    public Task<bool> EhAdministradorRedeNaOrganizacaoAsync(
        Guid usuarioId,
        Guid organizacaoId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(VinculosAtivos(usuarioId).Any(vinculo =>
            vinculo.OrganizacaoId == organizacaoId
            && vinculo.Perfil == PerfilAcesso.AdministradorRede
            && vinculo.UnidadeId == null));
    }

    public Task<IReadOnlyList<Guid>> ListarOrganizacoesAdministradorRedeAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Guid> organizacoes = VinculosAtivos(usuarioId)
            .Where(vinculo => vinculo.Perfil == PerfilAcesso.AdministradorRede
                && vinculo.UnidadeId == null)
            .Select(vinculo => vinculo.OrganizacaoId)
            .Distinct()
            .Take(2)
            .ToArray();

        return Task.FromResult(organizacoes);
    }

    public Task<bool> PossuiAlgumPerfilAsync(
        Guid usuarioId,
        IReadOnlyCollection<PerfilAcesso> perfis,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(VinculosAtivos(usuarioId).Any(vinculo =>
            perfis.Contains(vinculo.Perfil)));
    }

    public Task<bool> PossuiPerfilNaOrganizacaoAsync(
        Guid usuarioId,
        Guid organizacaoId,
        PerfilAcesso perfil,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(VinculosAtivos(usuarioId).Any(vinculo =>
            vinculo.OrganizacaoId == organizacaoId
            && vinculo.Perfil == perfil));
    }

    public Task<bool> PossuiAcessoUnidadeAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(VinculosAtivos(usuarioId).Any(vinculo =>
            vinculo.OrganizacaoId == organizacaoId
            && ((vinculo.Perfil == PerfilAcesso.AdministradorRede
                    && vinculo.UnidadeId == null)
                || vinculo.UnidadeId == unidadeId)));
    }

    public Task<bool> PossuiPerfilNaUnidadeAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        PerfilAcesso perfil,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(VinculosAtivos(usuarioId).Any(vinculo =>
            vinculo.OrganizacaoId == organizacaoId
            && vinculo.UnidadeId == unidadeId
            && vinculo.Perfil == perfil));
    }

    public Task<bool> PossuiAlgumPerfilNaUnidadeAsync(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        IReadOnlyCollection<PerfilAcesso> perfis,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(VinculosAtivos(usuarioId).Any(vinculo =>
            vinculo.OrganizacaoId == organizacaoId
            && vinculo.UnidadeId == unidadeId
            && perfis.Contains(vinculo.Perfil)));
    }

    private IEnumerable<TestVinculoAcesso> VinculosAtivos(Guid usuarioId)
    {
        return _vinculos.Where(vinculo =>
            vinculo.UsuarioId == usuarioId
            && vinculo.Ativo);
    }

    private sealed record TestVinculoAcesso(
        Guid UsuarioId,
        Guid OrganizacaoId,
        Guid? UnidadeId,
        PerfilAcesso Perfil,
        bool Ativo);
}

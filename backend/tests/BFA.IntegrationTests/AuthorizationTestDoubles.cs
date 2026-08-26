using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Domain.Acessos;
using BFA.Application.Usuarios;

namespace BFA.IntegrationTests;

public sealed class TestUsuarioApresentacaoConsulta : IUsuarioApresentacaoConsulta
{
    public Task<string?> ObterNomeCompletoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>("Professor Teste");
    }
}

public sealed class TestUsuarioAtual : IUsuarioAtual
{
    public bool Autenticado { get; set; }

    public Guid? UsuarioId { get; set; }
}

public sealed class TestUnidadesUsuarioConsulta : IUnidadesUsuarioConsulta
{
    private readonly List<TestUnidadeUsuario> _unidades = [];
    private readonly List<TestUnidadeUsuario> _unidadesProfessor = [];

    public void Limpar()
    {
        _unidades.Clear();
        _unidadesProfessor.Clear();
    }

    public void Adicionar(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        string nome,
        bool ativa = true)
    {
        _unidades.Add(new TestUnidadeUsuario(
            usuarioId,
            new UnidadeAcessoResumo(organizacaoId, unidadeId, nome),
            ativa));
    }

    public Task<IReadOnlyList<UnidadeAcessoResumo>> ListarAdministradasAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<UnidadeAcessoResumo> unidades = _unidades
            .Where(item => item.UsuarioId == usuarioId && item.Ativa)
            .Select(item => item.Unidade)
            .OrderBy(unidade => unidade.Nome)
            .ToArray();
        return Task.FromResult(unidades);
    }

    public Task<UnidadeAcessoResumo?> ObterAdministradaAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unidade = _unidades
            .Where(item => item.UsuarioId == usuarioId && item.Ativa)
            .Select(item => item.Unidade)
            .SingleOrDefault(item => item.UnidadeId == unidadeId);
        return Task.FromResult(unidade);
    }

    public void AdicionarProfessor(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        string nome,
        bool ativa = true)
    {
        _unidadesProfessor.Add(new TestUnidadeUsuario(
            usuarioId,
            new UnidadeAcessoResumo(organizacaoId, unidadeId, nome),
            ativa));
    }

    public Task<IReadOnlyList<UnidadeAcessoResumo>> ListarProfessorAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<UnidadeAcessoResumo> unidades = _unidadesProfessor
            .Where(item => item.UsuarioId == usuarioId && item.Ativa)
            .Select(item => item.Unidade)
            .OrderBy(unidade => unidade.Nome)
            .ToArray();
        return Task.FromResult(unidades);
    }

    public Task<UnidadeAcessoResumo?> ObterProfessorAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unidade = _unidadesProfessor
            .Where(item => item.UsuarioId == usuarioId && item.Ativa)
            .Select(item => item.Unidade)
            .SingleOrDefault(item => item.UnidadeId == unidadeId);
        return Task.FromResult(unidade);
    }

    private sealed record TestUnidadeUsuario(
        Guid UsuarioId,
        UnidadeAcessoResumo Unidade,
        bool Ativa);
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

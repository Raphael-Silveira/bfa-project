using BFA.Application.Acessos;
using BFA.Application.Franqueadora.Unidades;
using BFA.Domain.Acessos;
using BFA.Domain.Unidades;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.UnitTests.Unidades;

public sealed class UnidadesFranqueadoraServicoTests
{
    private static readonly DateTime AgoraUtc = new(
        2026,
        8,
        18,
        15,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public async Task Listagem_retorna_somente_unidades_da_organizacao_atual()
    {
        var contexto = CriarContexto();
        contexto.Repositorio.Adicionar(CriarUnidade(contexto.OrganizacaoId, "Tietê", "tiete"));
        contexto.Repositorio.Adicionar(CriarUnidade(contexto.OrganizacaoId, "Sorocaba", "sorocaba"));
        contexto.Repositorio.Adicionar(CriarUnidade(Guid.NewGuid(), "Externa", "externa"));

        var resultado = await contexto.Servico.ListarAsync(
            contexto.UsuarioId,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUnidade.Sucesso, resultado.Estado);
        var unidades = Assert.IsAssignableFrom<IReadOnlyList<UnidadeResumo>>(resultado.Valor);
        Assert.Equal(2, unidades.Count);
        Assert.Equal(["Sorocaba", "Tietê"], unidades.Select(unidade => unidade.Nome));
        Assert.DoesNotContain(unidades, unidade => unidade.Nome == "Externa");
    }

    [Fact]
    public async Task Listagem_nao_mistura_unidade_de_outra_organizacao_com_mesmo_slug()
    {
        var contexto = CriarContexto();
        contexto.Repositorio.Adicionar(CriarUnidade(contexto.OrganizacaoId, "Atual", "unidade"));
        contexto.Repositorio.Adicionar(CriarUnidade(Guid.NewGuid(), "Outra", "unidade"));

        var resultado = await contexto.Servico.ListarAsync(
            contexto.UsuarioId,
            CancellationToken.None);

        var unidade = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<UnidadeResumo>>(
            resultado.Valor));
        Assert.Equal("Atual", unidade.Nome);
    }

    [Fact]
    public async Task Criacao_associa_organizacao_do_vinculo_e_inicia_ativa()
    {
        var contexto = CriarContexto();

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioId,
            new CriarUnidadeSolicitacao("BFA Tietê", "BFA-TIETE"),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUnidade.Sucesso, resultado.Estado);
        var unidade = Assert.Single(contexto.Repositorio.Unidades);
        Assert.Equal(contexto.OrganizacaoId, unidade.OrganizacaoId);
        Assert.Equal("bfa-tiete", unidade.Slug);
        Assert.True(unidade.Ativa);
    }

    [Fact]
    public async Task Slug_duplicado_na_mesma_organizacao_e_rejeitado()
    {
        var contexto = CriarContexto();
        contexto.Repositorio.Adicionar(CriarUnidade(
            contexto.OrganizacaoId,
            "Existente",
            "bfa-tiete"));

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioId,
            new CriarUnidadeSolicitacao("Nova", "BFA-TIETE"),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUnidade.SlugDuplicado, resultado.Estado);
        Assert.Single(contexto.Repositorio.Unidades);
    }

    [Fact]
    public async Task Mesmo_slug_em_organizacoes_diferentes_e_permitido()
    {
        var contexto = CriarContexto();
        contexto.Repositorio.Adicionar(CriarUnidade(Guid.NewGuid(), "Outra", "bfa-tiete"));

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioId,
            new CriarUnidadeSolicitacao("Atual", "bfa-tiete"),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUnidade.Sucesso, resultado.Estado);
        Assert.Contains(
            contexto.Repositorio.Unidades,
            unidade => unidade.OrganizacaoId == contexto.OrganizacaoId
                && unidade.Slug == "bfa-tiete");
    }

    [Fact]
    public async Task Edicao_da_propria_organizacao_funciona()
    {
        var contexto = CriarContexto();
        var unidade = CriarUnidade(contexto.OrganizacaoId, "Antiga", "antiga");
        contexto.Repositorio.Adicionar(unidade);

        var resultado = await contexto.Servico.AtualizarAsync(
            contexto.UsuarioId,
            unidade.Id,
            new AtualizarUnidadeSolicitacao("Nova", "NOVA"),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUnidade.Sucesso, resultado.Estado);
        Assert.Equal("Nova", unidade.Nome);
        Assert.Equal("nova", unidade.Slug);
        Assert.Equal(AgoraUtc, unidade.AtualizadoEmUtc);
    }

    [Fact]
    public async Task Edicao_de_outra_organizacao_e_negada_sem_alterar_recurso()
    {
        var contexto = CriarContexto();
        var unidadeExterna = CriarUnidade(Guid.NewGuid(), "Externa", "externa");
        contexto.Repositorio.Adicionar(unidadeExterna);

        var resultado = await contexto.Servico.AtualizarAsync(
            contexto.UsuarioId,
            unidadeExterna.Id,
            new AtualizarUnidadeSolicitacao("Invadida", "invadida"),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUnidade.NaoEncontrada, resultado.Estado);
        Assert.Equal("Externa", unidadeExterna.Nome);
    }

    [Fact]
    public async Task Ativar_funciona_na_organizacao_atual()
    {
        var contexto = CriarContexto();
        var unidade = CriarUnidade(contexto.OrganizacaoId, "Unidade", "unidade");
        unidade.Desativar(AgoraUtc.AddHours(-1));
        contexto.Repositorio.Adicionar(unidade);

        var resultado = await contexto.Servico.AtivarAsync(
            contexto.UsuarioId,
            unidade.Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUnidade.Sucesso, resultado.Estado);
        Assert.True(unidade.Ativa);
        Assert.Equal(AgoraUtc, unidade.AtualizadoEmUtc);
    }

    [Fact]
    public async Task Desativar_funciona_na_organizacao_atual()
    {
        var contexto = CriarContexto();
        var unidade = CriarUnidade(contexto.OrganizacaoId, "Unidade", "unidade");
        contexto.Repositorio.Adicionar(unidade);

        var resultado = await contexto.Servico.DesativarAsync(
            contexto.UsuarioId,
            unidade.Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUnidade.Sucesso, resultado.Estado);
        Assert.False(unidade.Ativa);
        Assert.Equal(AgoraUtc, unidade.AtualizadoEmUtc);
    }

    [Fact]
    public void Contrato_nao_expoe_exclusao_fisica()
    {
        Assert.DoesNotContain(
            typeof(IUnidadesFranqueadoraServico).GetMethods(),
            metodo => metodo.Name.Contains("Excluir", StringComparison.OrdinalIgnoreCase)
                || metodo.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(IUnidadesFranqueadoraRepositorio).GetMethods(),
            metodo => metodo.Name.Contains("Excluir", StringComparison.OrdinalIgnoreCase)
                || metodo.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    private static ContextoTeste CriarContexto()
    {
        var usuarioId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var acessos = new AcessoUsuarioConsultaTeste();
        acessos.Organizacoes.Add(organizacaoId);
        var repositorio = new UnidadesRepositorioTeste();
        var timeProvider = new TimeProviderTeste(new DateTimeOffset(AgoraUtc));
        var servico = new UnidadesFranqueadoraServico(
            acessos,
            repositorio,
            timeProvider,
            NullLogger<UnidadesFranqueadoraServico>.Instance);
        return new ContextoTeste(usuarioId, organizacaoId, repositorio, servico);
    }

    private static Unidade CriarUnidade(Guid organizacaoId, string nome, string slug)
    {
        return new Unidade(Guid.NewGuid(), organizacaoId, nome, slug, AgoraUtc.AddDays(-1));
    }

    private sealed record ContextoTeste(
        Guid UsuarioId,
        Guid OrganizacaoId,
        UnidadesRepositorioTeste Repositorio,
        UnidadesFranqueadoraServico Servico);

    private sealed class TimeProviderTeste(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private sealed class UnidadesRepositorioTeste : IUnidadesFranqueadoraRepositorio
    {
        public List<Unidade> Unidades { get; } = [];

        public Task<IReadOnlyList<UnidadeResumo>> ListarAsync(
            Guid organizacaoId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<UnidadeResumo> unidades = Unidades
                .Where(unidade => unidade.OrganizacaoId == organizacaoId)
                .OrderBy(unidade => unidade.Nome)
                .Select(unidade => new UnidadeResumo(
                    unidade.Id,
                    unidade.Nome,
                    unidade.Slug,
                    unidade.Ativa,
                    unidade.CriadoEmUtc))
                .ToArray();
            return Task.FromResult(unidades);
        }

        public Task<UnidadeDetalhe?> ObterDetalheAsync(
            Guid organizacaoId,
            Guid unidadeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var unidade = Unidades.SingleOrDefault(item =>
                item.OrganizacaoId == organizacaoId && item.Id == unidadeId);
            var detalhe = unidade is null
                ? null
                : new UnidadeDetalhe(
                    unidade.Id,
                    unidade.Nome,
                    unidade.Slug,
                    unidade.Ativa,
                    unidade.CriadoEmUtc);
            return Task.FromResult(detalhe);
        }

        public Task<Unidade?> ObterParaAlteracaoAsync(
            Guid organizacaoId,
            Guid unidadeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Unidades.SingleOrDefault(unidade =>
                unidade.OrganizacaoId == organizacaoId && unidade.Id == unidadeId));
        }

        public Task<bool> ExisteSlugAsync(
            Guid organizacaoId,
            string slug,
            Guid? unidadeIgnoradaId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Unidades.Any(unidade =>
                unidade.OrganizacaoId == organizacaoId
                && unidade.Slug == slug
                && (!unidadeIgnoradaId.HasValue || unidade.Id != unidadeIgnoradaId.Value)));
        }

        public void Adicionar(Unidade unidade)
        {
            Unidades.Add(unidade);
        }

        public Task<ResultadoPersistenciaUnidade> SalvarAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ResultadoPersistenciaUnidade.Sucesso);
        }
    }

    private sealed class AcessoUsuarioConsultaTeste : IAcessoUsuarioConsulta
    {
        public List<Guid> Organizacoes { get; } = [];

        public Task<IReadOnlyList<Guid>> ListarOrganizacoesAdministradorRedeAsync(
            Guid usuarioId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Guid>>([.. Organizacoes]);
        }

        public Task<bool> EhAdministradorRedeAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> EhAdministradorRedeNaOrganizacaoAsync(
            Guid usuarioId,
            Guid organizacaoId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> PossuiAlgumPerfilAsync(
            Guid usuarioId,
            IReadOnlyCollection<PerfilAcesso> perfis,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> PossuiPerfilNaOrganizacaoAsync(
            Guid usuarioId,
            Guid organizacaoId,
            PerfilAcesso perfil,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> PossuiAcessoUnidadeAsync(
            Guid usuarioId,
            Guid organizacaoId,
            Guid unidadeId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> PossuiPerfilNaUnidadeAsync(
            Guid usuarioId,
            Guid organizacaoId,
            Guid unidadeId,
            PerfilAcesso perfil,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> PossuiAlgumPerfilNaUnidadeAsync(
            Guid usuarioId,
            Guid organizacaoId,
            Guid unidadeId,
            IReadOnlyCollection<PerfilAcesso> perfis,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

using BFA.Application.Acessos;
using BFA.Application.Franqueadora.AcessosUnidade;
using BFA.Domain.Acessos;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.UnitTests.Acessos;

public sealed class AcessosUnidadeServicoTests
{
    private static readonly DateTime AgoraUtc = new(
        2026,
        8,
        18,
        18,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public async Task Listagem_filtra_organizacao_unidade_e_perfil_e_ordena_por_email()
    {
        var contexto = CriarContexto();
        var outraUnidadeId = contexto.Repositorio.AdicionarUnidade(
            contexto.OrganizacaoId,
            "Outra unidade");
        var outraOrganizacaoId = Guid.NewGuid();
        var unidadeExternaId = contexto.Repositorio.AdicionarUnidade(
            outraOrganizacaoId,
            "Unidade externa");
        contexto.Repositorio.AdicionarVinculo(
            CriarVinculo(contexto.OrganizacaoId, contexto.UnidadeId, PerfilAcesso.AdministradorUnidade),
            "zeta@bfa.test");
        contexto.Repositorio.AdicionarVinculo(
            CriarVinculo(contexto.OrganizacaoId, contexto.UnidadeId, PerfilAcesso.AdministradorUnidade),
            "alfa@bfa.test");
        contexto.Repositorio.AdicionarVinculo(
            CriarVinculo(contexto.OrganizacaoId, outraUnidadeId, PerfilAcesso.AdministradorUnidade),
            "outra-unidade@bfa.test");
        contexto.Repositorio.AdicionarVinculo(
            CriarVinculo(outraOrganizacaoId, unidadeExternaId, PerfilAcesso.AdministradorUnidade),
            "outra-organizacao@bfa.test");
        contexto.Repositorio.AdicionarVinculo(
            CriarVinculo(contexto.OrganizacaoId, contexto.UnidadeId, PerfilAcesso.Professor),
            "professor@bfa.test");
        contexto.Repositorio.AdicionarVinculo(
            CriarVinculo(contexto.OrganizacaoId, contexto.UnidadeId, PerfilAcesso.Aluno),
            "aluno@bfa.test");

        var resultado = await contexto.Servico.ObterAsync(
            contexto.UsuarioAtualId,
            contexto.UnidadeId,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoAcessoUnidade.Sucesso, resultado.Estado);
        var detalhe = Assert.IsType<AcessosUnidadeDetalhe>(resultado.Valor);
        Assert.Equal(contexto.UnidadeId, detalhe.Unidade.Id);
        Assert.Equal(
            ["alfa@bfa.test", "zeta@bfa.test"],
            detalhe.Administradores.Select(item => item.Email));
    }

    [Fact]
    public async Task Adicionar_usuario_existente_cria_administrador_da_unidade_no_tenant_seguro()
    {
        var contexto = CriarContexto();
        var usuarioEncontradoId = contexto.Usuarios.Adicionar("admin@bfa.test");

        var resultado = await contexto.Servico.AdicionarAsync(
            contexto.UsuarioAtualId,
            contexto.UnidadeId,
            new AdicionarAdministradorUnidadeSolicitacao(" ADMIN@BFA.TEST "),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoAcessoUnidade.Sucesso, resultado.Estado);
        var vinculo = Assert.Single(contexto.Repositorio.Vinculos);
        Assert.Equal(usuarioEncontradoId, vinculo.UsuarioId);
        Assert.Equal(contexto.OrganizacaoId, vinculo.OrganizacaoId);
        Assert.Equal(contexto.UnidadeId, vinculo.UnidadeId);
        Assert.Equal(PerfilAcesso.AdministradorUnidade, vinculo.Perfil);
        Assert.True(vinculo.Ativo);
    }

    [Fact]
    public async Task Usuario_inexistente_retorna_estado_controlado_sem_criar_vinculo()
    {
        var contexto = CriarContexto();

        var resultado = await contexto.Servico.AdicionarAsync(
            contexto.UsuarioAtualId,
            contexto.UnidadeId,
            new AdicionarAdministradorUnidadeSolicitacao("inexistente@bfa.test"),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoAcessoUnidade.UsuarioNaoEncontrado, resultado.Estado);
        Assert.Empty(contexto.Repositorio.Vinculos);
    }

    [Fact]
    public async Task Vinculo_ativo_duplicado_nao_e_criado()
    {
        var contexto = CriarContexto();
        var usuarioId = contexto.Usuarios.Adicionar("admin@bfa.test");
        var existente = CriarVinculo(
            contexto.OrganizacaoId,
            contexto.UnidadeId,
            PerfilAcesso.AdministradorUnidade,
            usuarioId);
        contexto.Repositorio.AdicionarVinculo(existente, "admin@bfa.test");

        var resultado = await contexto.Servico.AdicionarAsync(
            contexto.UsuarioAtualId,
            contexto.UnidadeId,
            new AdicionarAdministradorUnidadeSolicitacao("admin@bfa.test"),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoAcessoUnidade.VinculoJaAtivo, resultado.Estado);
        Assert.Same(existente, Assert.Single(contexto.Repositorio.Vinculos));
        Assert.Equal(0, contexto.Repositorio.QuantidadeSalvamentos);
    }

    [Fact]
    public async Task Vinculo_inativo_equivalente_e_reativado_sem_novo_registro()
    {
        var contexto = CriarContexto();
        var usuarioId = contexto.Usuarios.Adicionar("admin@bfa.test");
        var existente = CriarVinculo(
            contexto.OrganizacaoId,
            contexto.UnidadeId,
            PerfilAcesso.AdministradorUnidade,
            usuarioId);
        existente.Desativar(AgoraUtc.AddHours(-1));
        contexto.Repositorio.AdicionarVinculo(existente, "admin@bfa.test");

        var resultado = await contexto.Servico.AdicionarAsync(
            contexto.UsuarioAtualId,
            contexto.UnidadeId,
            new AdicionarAdministradorUnidadeSolicitacao("admin@bfa.test"),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoAcessoUnidade.Sucesso, resultado.Estado);
        Assert.Same(existente, Assert.Single(contexto.Repositorio.Vinculos));
        Assert.True(existente.Ativo);
        Assert.Equal(AgoraUtc, existente.AtualizadoEmUtc);
    }

    [Fact]
    public async Task Mesmo_usuario_pode_administrar_duas_unidades_diferentes()
    {
        var contexto = CriarContexto();
        var outraUnidadeId = contexto.Repositorio.AdicionarUnidade(
            contexto.OrganizacaoId,
            "Outra unidade");
        var usuarioId = contexto.Usuarios.Adicionar("admin@bfa.test");

        var primeiro = await contexto.Servico.AdicionarAsync(
            contexto.UsuarioAtualId,
            contexto.UnidadeId,
            new AdicionarAdministradorUnidadeSolicitacao("admin@bfa.test"),
            CancellationToken.None);
        var segundo = await contexto.Servico.AdicionarAsync(
            contexto.UsuarioAtualId,
            outraUnidadeId,
            new AdicionarAdministradorUnidadeSolicitacao("admin@bfa.test"),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoAcessoUnidade.Sucesso, primeiro.Estado);
        Assert.Equal(EstadoGerenciamentoAcessoUnidade.Sucesso, segundo.Estado);
        Assert.Equal(2, contexto.Repositorio.Vinculos.Count);
        Assert.All(contexto.Repositorio.Vinculos, vinculo => Assert.Equal(usuarioId, vinculo.UsuarioId));
        Assert.Equal(2, contexto.Repositorio.Vinculos.Select(vinculo => vinculo.UnidadeId).Distinct().Count());
    }

    [Fact]
    public async Task Desativacao_altera_estado_sem_excluir_registro()
    {
        var contexto = CriarContexto();
        var existente = CriarVinculo(
            contexto.OrganizacaoId,
            contexto.UnidadeId,
            PerfilAcesso.AdministradorUnidade);
        contexto.Repositorio.AdicionarVinculo(existente, "admin@bfa.test");

        var resultado = await contexto.Servico.DesativarAsync(
            contexto.UsuarioAtualId,
            contexto.UnidadeId,
            existente.Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoAcessoUnidade.Sucesso, resultado.Estado);
        Assert.False(existente.Ativo);
        Assert.Equal(AgoraUtc, existente.AtualizadoEmUtc);
        Assert.Same(existente, Assert.Single(contexto.Repositorio.Vinculos));
    }

    [Fact]
    public async Task Ativacao_altera_estado_do_vinculo_existente()
    {
        var contexto = CriarContexto();
        var existente = CriarVinculo(
            contexto.OrganizacaoId,
            contexto.UnidadeId,
            PerfilAcesso.AdministradorUnidade);
        existente.Desativar(AgoraUtc.AddHours(-1));
        contexto.Repositorio.AdicionarVinculo(existente, "admin@bfa.test");

        var resultado = await contexto.Servico.AtivarAsync(
            contexto.UsuarioAtualId,
            contexto.UnidadeId,
            existente.Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoAcessoUnidade.Sucesso, resultado.Estado);
        Assert.True(existente.Ativo);
        Assert.Equal(AgoraUtc, existente.AtualizadoEmUtc);
    }

    [Fact]
    public async Task Vinculo_de_outro_tenant_nao_pode_ser_alterado()
    {
        var contexto = CriarContexto();
        var outraOrganizacaoId = Guid.NewGuid();
        var unidadeExternaId = contexto.Repositorio.AdicionarUnidade(
            outraOrganizacaoId,
            "Externa");
        var externo = CriarVinculo(
            outraOrganizacaoId,
            unidadeExternaId,
            PerfilAcesso.AdministradorUnidade);
        contexto.Repositorio.AdicionarVinculo(externo, "externo@bfa.test");

        var resultado = await contexto.Servico.DesativarAsync(
            contexto.UsuarioAtualId,
            unidadeExternaId,
            externo.Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoAcessoUnidade.UnidadeNaoEncontrada, resultado.Estado);
        Assert.True(externo.Ativo);
        Assert.Equal(0, contexto.Repositorio.QuantidadeSalvamentos);
    }

    [Theory]
    [InlineData(PerfilAcesso.Professor)]
    [InlineData(PerfilAcesso.Aluno)]
    public async Task Vinculo_de_outro_perfil_nao_pode_ser_alterado(PerfilAcesso perfil)
    {
        var contexto = CriarContexto();
        var outroPerfil = CriarVinculo(
            contexto.OrganizacaoId,
            contexto.UnidadeId,
            perfil);
        contexto.Repositorio.AdicionarVinculo(outroPerfil, $"{perfil}@bfa.test");

        var resultado = await contexto.Servico.DesativarAsync(
            contexto.UsuarioAtualId,
            contexto.UnidadeId,
            outroPerfil.Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoAcessoUnidade.VinculoNaoEncontrado, resultado.Estado);
        Assert.True(outroPerfil.Ativo);
    }

    [Fact]
    public void Contratos_nao_expoem_delete_fisico()
    {
        var metodos = typeof(IAcessosUnidadeServico).GetMethods()
            .Concat(typeof(IAcessosUnidadeRepositorio).GetMethods());

        Assert.DoesNotContain(metodos, metodo =>
            metodo.Name.Contains("Excluir", StringComparison.OrdinalIgnoreCase)
            || metodo.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
            || metodo.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }

    private static ContextoTeste CriarContexto()
    {
        var usuarioAtualId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var repositorio = new AcessosUnidadeRepositorioTeste();
        var unidadeId = repositorio.AdicionarUnidade(organizacaoId, "BFA Tietê");
        var acessos = new AcessoUsuarioConsultaTeste(organizacaoId);
        var usuarios = new UsuarioPorEmailConsultaTeste();
        var servico = new AcessosUnidadeServico(
            acessos,
            usuarios,
            repositorio,
            new TimeProviderTeste(new DateTimeOffset(AgoraUtc)),
            NullLogger<AcessosUnidadeServico>.Instance);

        return new(
            usuarioAtualId,
            organizacaoId,
            unidadeId,
            usuarios,
            repositorio,
            servico);
    }

    private static VinculoAcesso CriarVinculo(
        Guid organizacaoId,
        Guid unidadeId,
        PerfilAcesso perfil,
        Guid? usuarioId = null)
    {
        return new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId ?? Guid.NewGuid(),
            organizacaoId,
            unidadeId,
            perfil,
            AgoraUtc.AddDays(-1));
    }

    private sealed record ContextoTeste(
        Guid UsuarioAtualId,
        Guid OrganizacaoId,
        Guid UnidadeId,
        UsuarioPorEmailConsultaTeste Usuarios,
        AcessosUnidadeRepositorioTeste Repositorio,
        AcessosUnidadeServico Servico);

    private sealed class TimeProviderTeste(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private sealed class UsuarioPorEmailConsultaTeste : IUsuarioPorEmailConsulta
    {
        private readonly Dictionary<string, UsuarioPorEmail> _usuarios =
            new(StringComparer.OrdinalIgnoreCase);

        public Guid Adicionar(string email)
        {
            var usuario = new UsuarioPorEmail(Guid.NewGuid(), email);
            _usuarios[email] = usuario;
            return usuario.Id;
        }

        public Task<UsuarioPorEmail?> ObterAsync(
            string email,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _usuarios.TryGetValue(email.Trim(), out var usuario);
            return Task.FromResult(usuario);
        }
    }

    private sealed class AcessosUnidadeRepositorioTeste : IAcessosUnidadeRepositorio
    {
        private readonly List<UnidadeAcessosResumo> _unidades = [];
        private readonly Dictionary<Guid, string> _emails = [];

        public List<VinculoAcesso> Vinculos { get; } = [];

        public int QuantidadeSalvamentos { get; private set; }

        public Guid AdicionarUnidade(Guid organizacaoId, string nome)
        {
            var unidadeId = Guid.NewGuid();
            _unidades.Add(new UnidadeAcessosResumo(unidadeId, nome, Ativa: true));
            OrganizacoesPorUnidade[unidadeId] = organizacaoId;
            return unidadeId;
        }

        private Dictionary<Guid, Guid> OrganizacoesPorUnidade { get; } = [];

        public void AdicionarVinculo(VinculoAcesso vinculo, string email)
        {
            Vinculos.Add(vinculo);
            _emails[vinculo.UsuarioId] = email;
        }

        public Task<UnidadeAcessosResumo?> ObterUnidadeAsync(
            Guid organizacaoId,
            Guid unidadeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var unidade = _unidades.SingleOrDefault(item =>
                item.Id == unidadeId
                && OrganizacoesPorUnidade[item.Id] == organizacaoId);
            return Task.FromResult(unidade);
        }

        public Task<IReadOnlyList<AdministradorUnidadeResumo>> ListarAdministradoresAsync(
            Guid organizacaoId,
            Guid unidadeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<AdministradorUnidadeResumo> resultado = Vinculos
                .Where(vinculo => vinculo.OrganizacaoId == organizacaoId
                    && vinculo.UnidadeId == unidadeId
                    && vinculo.Perfil == PerfilAcesso.AdministradorUnidade)
                .Select(vinculo => new AdministradorUnidadeResumo(
                    vinculo.Id,
                    vinculo.UsuarioId,
                    _emails[vinculo.UsuarioId],
                    vinculo.Ativo,
                    vinculo.CriadoEmUtc))
                .OrderBy(item => item.Email)
                .ToArray();
            return Task.FromResult(resultado);
        }

        public Task<VinculoAcesso?> ObterAdministradorPorUsuarioAsync(
            Guid organizacaoId,
            Guid unidadeId,
            Guid usuarioId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Vinculos.SingleOrDefault(vinculo =>
                vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.UsuarioId == usuarioId
                && vinculo.Perfil == PerfilAcesso.AdministradorUnidade));
        }

        public Task<VinculoAcesso?> ObterAdministradorPorVinculoAsync(
            Guid organizacaoId,
            Guid unidadeId,
            Guid vinculoId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Vinculos.SingleOrDefault(vinculo =>
                vinculo.OrganizacaoId == organizacaoId
                && vinculo.UnidadeId == unidadeId
                && vinculo.Id == vinculoId
                && vinculo.Perfil == PerfilAcesso.AdministradorUnidade));
        }

        public void Adicionar(VinculoAcesso vinculo)
        {
            Vinculos.Add(vinculo);
        }

        public Task<ResultadoPersistenciaAcessoUnidade> SalvarAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QuantidadeSalvamentos++;
            return Task.FromResult(ResultadoPersistenciaAcessoUnidade.Sucesso);
        }
    }

    private sealed class AcessoUsuarioConsultaTeste(Guid organizacaoId)
        : IAcessoUsuarioConsulta
    {
        public Task<IReadOnlyList<Guid>> ListarOrganizacoesAdministradorRedeAsync(
            Guid usuarioId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Guid>>([organizacaoId]);
        }

        public Task<bool> EhAdministradorRedeAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> EhAdministradorRedeNaOrganizacaoAsync(
            Guid usuarioId,
            Guid organizacaoConsultadaId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> PossuiAlgumPerfilAsync(
            Guid usuarioId,
            IReadOnlyCollection<PerfilAcesso> perfis,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> PossuiPerfilNaOrganizacaoAsync(
            Guid usuarioId,
            Guid organizacaoConsultadaId,
            PerfilAcesso perfil,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> PossuiAcessoUnidadeAsync(
            Guid usuarioId,
            Guid organizacaoConsultadaId,
            Guid unidadeId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> PossuiPerfilNaUnidadeAsync(
            Guid usuarioId,
            Guid organizacaoConsultadaId,
            Guid unidadeId,
            PerfilAcesso perfil,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> PossuiAlgumPerfilNaUnidadeAsync(
            Guid usuarioId,
            Guid organizacaoConsultadaId,
            Guid unidadeId,
            IReadOnlyCollection<PerfilAcesso> perfis,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

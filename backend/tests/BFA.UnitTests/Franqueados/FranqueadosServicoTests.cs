using BFA.Application.Acessos;
using BFA.Application.Franqueadora.Franqueados;
using BFA.Application.Localidades;
using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.UnitTests.Franqueados;

public sealed class FranqueadosServicoTests
{
    private static readonly DateTime AgoraUtc = new(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Lista_franqueados_somente_no_contexto_da_organizacao_autorizada()
    {
        var contexto = CriarContexto();
        contexto.Repositorio.Resumos.Add(new(
            contexto.Franqueado.Id,
            contexto.Franqueado.NomeRazaoSocial,
            null,
            contexto.Franqueado.Documento,
            contexto.Franqueado.TipoPessoa,
            2,
            true));

        var resultado = await contexto.Servico.ListarAsync(
            contexto.UsuarioAtualId,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoFranqueado.Sucesso, resultado.Estado);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<FranqueadoResumo>>(resultado.Valor));
        Assert.Equal(contexto.OrganizacaoId, contexto.Repositorio.OrganizacaoListada);
    }

    [Fact]
    public async Task Franqueado_de_outro_tenant_nao_e_exposto()
    {
        var contexto = CriarContexto();
        contexto.Repositorio.Dados = null;

        var resultado = await contexto.Servico.ObterAsync(
            contexto.UsuarioAtualId,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoFranqueado.NaoEncontrado, resultado.Estado);
        Assert.Null(resultado.Valor);
        Assert.Equal(contexto.OrganizacaoId, contexto.Repositorio.OrganizacaoConsultada);
    }

    [Theory]
    [InlineData(TipoPessoaFranqueado.PessoaFisica, "123.456.789-01", "12345678901")]
    [InlineData(TipoPessoaFranqueado.PessoaJuridica, "AB.CDE.F12/3456-78", "ABCDEF12345678")]
    public async Task Edicao_preserva_regras_de_pessoa_e_normaliza_documento(
        TipoPessoaFranqueado tipoPessoa,
        string documento,
        string documentoEsperado)
    {
        var contexto = CriarContexto();

        var resultado = await contexto.Servico.AtualizarAsync(
            contexto.UsuarioAtualId,
            contexto.Franqueado.Id,
            CriarAtualizacao(tipoPessoa, documento),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoFranqueado.Sucesso, resultado.Estado);
        Assert.Equal(tipoPessoa, contexto.Franqueado.TipoPessoa);
        Assert.Equal(documentoEsperado, contexto.Franqueado.Documento);
        Assert.Equal("SP", contexto.Franqueado.Estado);
        Assert.Equal("Tietê", contexto.Franqueado.Cidade);

        if (tipoPessoa == TipoPessoaFranqueado.PessoaFisica)
        {
            Assert.Equal("franqueado@bfa.test", contexto.Franqueado.Email);
            Assert.Null(contexto.Franqueado.Telefone);
            Assert.Null(contexto.Franqueado.NomeFantasia);
            Assert.Null(contexto.Franqueado.ResponsavelLegal);
        }
    }

    [Theory]
    [InlineData(99, 3554508, EstadoGerenciamentoFranqueado.EstadoLocalidadeInvalido)]
    [InlineData(35, 9999999, EstadoGerenciamentoFranqueado.MunicipioLocalidadeInvalido)]
    public async Task Edicao_rejeita_estado_ou_municipio_fora_do_catalogo_local(
        int estadoCodigo,
        int municipioCodigo,
        EstadoGerenciamentoFranqueado estadoEsperado)
    {
        var contexto = CriarContexto();
        var solicitacao = CriarAtualizacao(
            TipoPessoaFranqueado.PessoaFisica,
            "12345678901") with
        {
            EstadoCodigoIbge = estadoCodigo,
            MunicipioCodigoIbge = municipioCodigo
        };

        var resultado = await contexto.Servico.AtualizarAsync(
            contexto.UsuarioAtualId,
            contexto.Franqueado.Id,
            solicitacao,
            CancellationToken.None);

        Assert.Equal(estadoEsperado, resultado.Estado);
        Assert.Equal(0, contexto.Repositorio.QuantidadeSalvamentos);
    }

    [Fact]
    public async Task Franqueado_pode_receber_varias_unidades_com_acesso_do_principal()
    {
        var contexto = CriarContexto();
        var segundaUnidadeId = Guid.NewGuid();
        contexto.Repositorio.UnidadesAtivas.UnionWith(
            [contexto.UnidadeId, segundaUnidadeId]);

        var primeiro = await contexto.Servico.VincularUnidadeAsync(
            contexto.UsuarioAtualId,
            contexto.Franqueado.Id,
            new(contexto.UnidadeId),
            CancellationToken.None);
        var segundo = await contexto.Servico.VincularUnidadeAsync(
            contexto.UsuarioAtualId,
            contexto.Franqueado.Id,
            new(segundaUnidadeId),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoFranqueado.Sucesso, primeiro.Estado);
        Assert.Equal(EstadoGerenciamentoFranqueado.Sucesso, segundo.Estado);
        Assert.Equal(2, contexto.Repositorio.NovosVinculosUnidade.Count);
        Assert.Equal(2, contexto.Repositorio.NovosAcessos.Count);
        Assert.All(contexto.Repositorio.NovosAcessos, acesso =>
        {
            Assert.Equal(contexto.UsuarioPrincipalId, acesso.UsuarioId);
            Assert.Equal(PerfilAcesso.AdministradorUnidade, acesso.Perfil);
            Assert.True(acesso.Ativo);
        });
    }

    [Fact]
    public async Task Unidade_ocupada_por_outro_franqueado_e_bloqueada()
    {
        var contexto = CriarContexto();
        contexto.Repositorio.UnidadesAtivas.Add(contexto.UnidadeId);
        contexto.Repositorio.UnidadesOcupadas.Add(contexto.UnidadeId);

        var resultado = await contexto.Servico.VincularUnidadeAsync(
            contexto.UsuarioAtualId,
            contexto.Franqueado.Id,
            new(contexto.UnidadeId),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoFranqueado.UnidadeOcupada, resultado.Estado);
        Assert.Equal("Esta unidade já possui um franqueado ativo.", resultado.Mensagem);
        Assert.Empty(contexto.Repositorio.NovosVinculosUnidade);
        Assert.Empty(contexto.Repositorio.NovosAcessos);
    }

    [Fact]
    public async Task Vinculos_inativos_sao_reativados_sem_duplicacao()
    {
        var contexto = CriarContexto();
        contexto.Repositorio.UnidadesAtivas.Add(contexto.UnidadeId);
        var vinculo = new FranqueadoUnidade(
            Guid.NewGuid(),
            contexto.Franqueado.Id,
            contexto.OrganizacaoId,
            contexto.UnidadeId,
            AgoraUtc.AddDays(-2));
        vinculo.Desativar(AgoraUtc.AddDays(-1));
        contexto.Repositorio.VinculosUnidade.Add(vinculo);
        var acesso = new VinculoAcesso(
            Guid.NewGuid(),
            contexto.UsuarioPrincipalId,
            contexto.OrganizacaoId,
            contexto.UnidadeId,
            PerfilAcesso.AdministradorUnidade,
            AgoraUtc.AddDays(-2));
        acesso.Desativar(AgoraUtc.AddDays(-1));
        contexto.Repositorio.Acessos.Add(acesso);

        var resultado = await contexto.Servico.VincularUnidadeAsync(
            contexto.UsuarioAtualId,
            contexto.Franqueado.Id,
            new(contexto.UnidadeId),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoFranqueado.Sucesso, resultado.Estado);
        Assert.True(vinculo.Ativo);
        Assert.True(acesso.Ativo);
        Assert.Empty(contexto.Repositorio.NovosVinculosUnidade);
        Assert.Empty(contexto.Repositorio.NovosAcessos);
        Assert.Equal(AgoraUtc, vinculo.AtualizadoEmUtc);
        Assert.Equal(AgoraUtc, acesso.AtualizadoEmUtc);
    }

    [Fact]
    public async Task Ausencia_de_usuario_principal_nao_cria_vinculo_parcial()
    {
        var contexto = CriarContexto(incluirPrincipal: false);
        contexto.Repositorio.UnidadesAtivas.Add(contexto.UnidadeId);

        var resultado = await contexto.Servico.VincularUnidadeAsync(
            contexto.UsuarioAtualId,
            contexto.Franqueado.Id,
            new(contexto.UnidadeId),
            CancellationToken.None);

        Assert.Equal(
            EstadoGerenciamentoFranqueado.UsuarioPrincipalAusente,
            resultado.Estado);
        Assert.Empty(contexto.Repositorio.NovosVinculosUnidade);
        Assert.Empty(contexto.Repositorio.NovosAcessos);
        Assert.Equal(0, contexto.Repositorio.QuantidadeSalvamentos);
    }

    [Fact]
    public async Task Falha_de_persistencia_e_reportada_como_operacao_atomica()
    {
        var contexto = CriarContexto();
        contexto.Repositorio.UnidadesAtivas.Add(contexto.UnidadeId);
        contexto.Repositorio.ResultadoPersistencia = EstadoPersistenciaFranqueado.Falha;

        var resultado = await contexto.Servico.VincularUnidadeAsync(
            contexto.UsuarioAtualId,
            contexto.Franqueado.Id,
            new(contexto.UnidadeId),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoFranqueado.FalhaPersistencia, resultado.Estado);
        Assert.Contains("Nenhuma alteração foi salva", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Equal(1, contexto.Repositorio.QuantidadeSalvamentos);
    }

    [Fact]
    public async Task Desvincular_preserva_registro_e_unidade_e_desativa_apenas_acesso_do_principal()
    {
        var contexto = CriarContexto();
        var vinculo = new FranqueadoUnidade(
            Guid.NewGuid(),
            contexto.Franqueado.Id,
            contexto.OrganizacaoId,
            contexto.UnidadeId,
            AgoraUtc.AddDays(-3));
        contexto.Repositorio.VinculosUnidade.Add(vinculo);
        var acessoPrincipal = new VinculoAcesso(
            Guid.NewGuid(),
            contexto.UsuarioPrincipalId,
            contexto.OrganizacaoId,
            contexto.UnidadeId,
            PerfilAcesso.AdministradorUnidade,
            AgoraUtc.AddDays(-3));
        var acessoOutroAdministrador = new VinculoAcesso(
            Guid.NewGuid(),
            Guid.NewGuid(),
            contexto.OrganizacaoId,
            contexto.UnidadeId,
            PerfilAcesso.AdministradorUnidade,
            AgoraUtc.AddDays(-2));
        contexto.Repositorio.Acessos.AddRange([acessoPrincipal, acessoOutroAdministrador]);
        contexto.Repositorio.UnidadesAtivas.Add(contexto.UnidadeId);

        var resultado = await contexto.Servico.DesativarUnidadeAsync(
            contexto.UsuarioAtualId,
            contexto.Franqueado.Id,
            contexto.UnidadeId,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoFranqueado.Sucesso, resultado.Estado);
        Assert.Single(contexto.Repositorio.VinculosUnidade);
        Assert.False(vinculo.Ativo);
        Assert.Contains(contexto.UnidadeId, contexto.Repositorio.UnidadesAtivas);
        Assert.False(acessoPrincipal.Ativo);
        Assert.True(acessoOutroAdministrador.Ativo);
    }

    private static ContextoTeste CriarContexto(bool incluirPrincipal = true)
    {
        var usuarioAtualId = Guid.NewGuid();
        var usuarioPrincipalId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var franqueado = new Franqueado(
            Guid.NewGuid(),
            organizacaoId,
            TipoPessoaFranqueado.PessoaFisica,
            "Franqueado Teste",
            "12345678901",
            "franqueado@bfa.test",
            AgoraUtc.AddDays(-5));
        var repositorio = new FranqueadosRepositorioTeste(franqueado);

        if (incluirPrincipal)
        {
            repositorio.Principal = new FranqueadoUsuario(
                Guid.NewGuid(),
                franqueado.Id,
                usuarioPrincipalId,
                principal: true,
                AgoraUtc.AddDays(-5));
        }

        var acessos = new AcessoUsuarioConsultaTeste();
        acessos.Organizacoes.Add(organizacaoId);
        var servico = new FranqueadosServico(
            acessos,
            repositorio,
            new LocalidadesConsultaTeste(),
            new TimeProviderTeste(new DateTimeOffset(AgoraUtc)),
            NullLogger<FranqueadosServico>.Instance);
        return new(
            usuarioAtualId,
            usuarioPrincipalId,
            organizacaoId,
            unidadeId,
            franqueado,
            repositorio,
            servico);
    }

    private static AtualizarFranqueadoSolicitacao CriarAtualizacao(
        TipoPessoaFranqueado tipoPessoa,
        string documento) => new(
            tipoPessoa,
            tipoPessoa == TipoPessoaFranqueado.PessoaFisica ? "Melissa Souza" : "BFA Franquias Ltda",
            tipoPessoa == TipoPessoaFranqueado.PessoaJuridica ? "BFA Franquias" : null,
            documento,
            "(11) 98888-8888",
            "comercial@bfa.test",
            "financeiro@bfa.test",
            tipoPessoa == TipoPessoaFranqueado.PessoaJuridica ? "Melissa Souza" : null,
            "Rua do Esporte",
            "10",
            null,
            "Centro",
            35,
            3554508,
            "18530-000",
            "Dados revisados");

    private sealed record ContextoTeste(
        Guid UsuarioAtualId,
        Guid UsuarioPrincipalId,
        Guid OrganizacaoId,
        Guid UnidadeId,
        Franqueado Franqueado,
        FranqueadosRepositorioTeste Repositorio,
        FranqueadosServico Servico);

    private sealed class FranqueadosRepositorioTeste(Franqueado franqueado)
        : IFranqueadosRepositorio
    {
        public List<FranqueadoResumo> Resumos { get; } = [];
        public FranqueadoDados? Dados { get; set; } = CriarDados(franqueado);
        public FranqueadoUsuario? Principal { get; set; }
        public HashSet<Guid> UnidadesAtivas { get; } = [];
        public HashSet<Guid> UnidadesOcupadas { get; } = [];
        public List<FranqueadoUnidade> VinculosUnidade { get; } = [];
        public List<VinculoAcesso> Acessos { get; } = [];
        public List<FranqueadoUnidade> NovosVinculosUnidade { get; } = [];
        public List<VinculoAcesso> NovosAcessos { get; } = [];
        public Guid? OrganizacaoListada { get; private set; }
        public Guid? OrganizacaoConsultada { get; private set; }
        public int QuantidadeSalvamentos { get; private set; }
        public EstadoPersistenciaFranqueado ResultadoPersistencia { get; set; } =
            EstadoPersistenciaFranqueado.Sucesso;

        public Task<IReadOnlyList<FranqueadoResumo>> ListarAsync(
            Guid organizacaoId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OrganizacaoListada = organizacaoId;
            return Task.FromResult<IReadOnlyList<FranqueadoResumo>>([.. Resumos]);
        }

        public Task<FranqueadoDados?> ObterDadosAsync(
            Guid organizacaoId,
            Guid franqueadoId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OrganizacaoConsultada = organizacaoId;
            return Task.FromResult(Dados is not null
                && Dados.OrganizacaoId == organizacaoId
                && Dados.Id == franqueadoId
                    ? Dados
                    : null);
        }

        public Task<IReadOnlyList<FranqueadoUsuarioResumo>> ListarUsuariosAsync(
            Guid organizacaoId,
            Guid franqueadoId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FranqueadoUsuarioResumo>>([]);

        public Task<IReadOnlyList<FranqueadoUnidadeResumo>> ListarUnidadesAsync(
            Guid organizacaoId,
            Guid franqueadoId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FranqueadoUnidadeResumo>>([]);

        public Task<IReadOnlyList<UnidadeDisponivelFranqueadoResumo>> ListarUnidadesDisponiveisAsync(
            Guid organizacaoId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UnidadeDisponivelFranqueadoResumo>>([]);

        public Task<Franqueado?> ObterParaAtualizacaoAsync(
            Guid organizacaoId,
            Guid franqueadoId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(franqueado.OrganizacaoId == organizacaoId
                && franqueado.Id == franqueadoId
                    ? franqueado
                    : null);
        }

        public Task<bool> ExisteDocumentoAsync(
            Guid organizacaoId,
            Guid franqueadoIdIgnorado,
            string documento,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> UnidadeAtivaExisteAsync(
            Guid organizacaoId,
            Guid unidadeId,
            CancellationToken cancellationToken) =>
            Task.FromResult(UnidadesAtivas.Contains(unidadeId));

        public Task<bool> UnidadePossuiOutroFranqueadoAtivoAsync(
            Guid organizacaoId,
            Guid franqueadoId,
            Guid unidadeId,
            CancellationToken cancellationToken) =>
            Task.FromResult(UnidadesOcupadas.Contains(unidadeId));

        public Task<FranqueadoUsuario?> ObterUsuarioPrincipalAtivoAsync(
            Guid franqueadoId,
            CancellationToken cancellationToken) => Task.FromResult(Principal);

        public Task<FranqueadoUnidade?> ObterVinculoUnidadeAsync(
            Guid organizacaoId,
            Guid franqueadoId,
            Guid unidadeId,
            CancellationToken cancellationToken) => Task.FromResult(VinculosUnidade.SingleOrDefault(
                item => item.OrganizacaoId == organizacaoId
                    && item.FranqueadoId == franqueadoId
                    && item.UnidadeId == unidadeId));

        public Task<VinculoAcesso?> ObterAcessoAdministradorUnidadeAsync(
            Guid organizacaoId,
            Guid unidadeId,
            Guid usuarioId,
            CancellationToken cancellationToken) => Task.FromResult(Acessos.SingleOrDefault(
                item => item.OrganizacaoId == organizacaoId
                    && item.UnidadeId == unidadeId
                    && item.UsuarioId == usuarioId
                    && item.Perfil == PerfilAcesso.AdministradorUnidade));

        public void Adicionar(FranqueadoUnidade vinculo)
        {
            NovosVinculosUnidade.Add(vinculo);
            VinculosUnidade.Add(vinculo);
        }

        public void Adicionar(VinculoAcesso vinculo)
        {
            NovosAcessos.Add(vinculo);
            Acessos.Add(vinculo);
        }

        public Task<EstadoPersistenciaFranqueado> SalvarAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QuantidadeSalvamentos++;
            return Task.FromResult(ResultadoPersistencia);
        }

        private static FranqueadoDados CriarDados(Franqueado item) => new(
            item.Id,
            item.OrganizacaoId,
            item.TipoPessoa,
            item.NomeRazaoSocial,
            item.NomeFantasia,
            item.Documento,
            item.Telefone,
            item.Email,
            item.EmailFinanceiro,
            item.ResponsavelLegal,
            item.Logradouro,
            item.Numero,
            item.Complemento,
            item.Bairro,
            item.Cidade,
            item.Estado,
            item.Cep,
            item.Observacoes,
            item.Ativo);
    }

    private sealed class AcessoUsuarioConsultaTeste : IAcessoUsuarioConsulta
    {
        public List<Guid> Organizacoes { get; } = [];

        public Task<IReadOnlyList<Guid>> ListarOrganizacoesAdministradorRedeAsync(
            Guid usuarioId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([.. Organizacoes]);

        public Task<bool> EhAdministradorRedeAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> EhAdministradorRedeNaOrganizacaoAsync(Guid usuarioId, Guid organizacaoId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> PossuiAlgumPerfilAsync(Guid usuarioId, IReadOnlyCollection<PerfilAcesso> perfis, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> PossuiPerfilNaOrganizacaoAsync(Guid usuarioId, Guid organizacaoId, PerfilAcesso perfil, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> PossuiAcessoUnidadeAsync(Guid usuarioId, Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> PossuiPerfilNaUnidadeAsync(Guid usuarioId, Guid organizacaoId, Guid unidadeId, PerfilAcesso perfil, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> PossuiAlgumPerfilNaUnidadeAsync(Guid usuarioId, Guid organizacaoId, Guid unidadeId, IReadOnlyCollection<PerfilAcesso> perfis, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class LocalidadesConsultaTeste : ILocalidadesConsulta
    {
        public Task<IReadOnlyList<EstadoLocalidadeResumo>> ListarEstadosAtivosAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EstadoLocalidadeResumo>>([
                new(35, "SP", "São Paulo")
            ]);

        public Task<IReadOnlyList<MunicipioLocalidadeResumo>> ListarMunicipiosAtivosAsync(
            int estadoCodigoIbge,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MunicipioLocalidadeResumo>>(
                estadoCodigoIbge == 35 ? [new(3554508, "Tietê")] : []);
    }

    private sealed class TimeProviderTeste(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}

using BFA.Application.Acessos;
using BFA.Application.Franqueadora.Usuarios;
using BFA.Application.Localidades;
using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;

namespace BFA.UnitTests.Usuarios;

public sealed class UsuariosFranqueadoraServicoTests
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
    public async Task Administrador_rede_cria_perfil_e_um_vinculo_sem_entidade_franqueado()
    {
        var contexto = CriarContexto();

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            CriarAdministrador(),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.Sucesso, resultado.Estado);
        var cadastro = Assert.Single(contexto.Repositorio.Cadastros);
        Assert.Equal("admin@bfa.test", cadastro.Email);
        Assert.Equal(cadastro.UsuarioId, cadastro.PerfilUsuario.UsuarioId);
        Assert.Null(cadastro.Franqueado);
        Assert.Null(cadastro.FranqueadoUsuario);
        Assert.Empty(cadastro.FranqueadosUnidades);
        var vinculo = Assert.Single(cadastro.VinculosAcesso);
        Assert.Equal(contexto.OrganizacaoId, vinculo.OrganizacaoId);
        Assert.Equal(PerfilAcesso.AdministradorRede, vinculo.Perfil);
        Assert.Null(vinculo.UnidadeId);
    }

    [Fact]
    public async Task Franqueado_cria_perfil_relacao_principal_unidades_e_acessos_correspondentes()
    {
        var contexto = CriarContexto();
        var primeiraUnidade = Guid.NewGuid();
        var segundaUnidade = Guid.NewGuid();
        contexto.Repositorio.UnidadesValidas.UnionWith([primeiraUnidade, segundaUnidade]);

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            CriarFranqueado([primeiraUnidade, segundaUnidade]),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.Sucesso, resultado.Estado);
        var cadastro = Assert.Single(contexto.Repositorio.Cadastros);
        var franqueado = Assert.IsType<Franqueado>(cadastro.Franqueado);
        var relacaoUsuario = Assert.IsType<FranqueadoUsuario>(cadastro.FranqueadoUsuario);
        Assert.Equal(contexto.OrganizacaoId, franqueado.OrganizacaoId);
        Assert.Equal("12345678901", franqueado.Documento);
        Assert.Equal("Pessoa Franqueada", franqueado.NomeRazaoSocial);
        Assert.Equal("franqueado@bfa.test", franqueado.Email);
        Assert.Equal("11999999999", franqueado.Telefone);
        Assert.Equal("SP", franqueado.Estado);
        Assert.Equal("Tietê", franqueado.Cidade);
        Assert.True(relacaoUsuario.Principal);
        Assert.True(relacaoUsuario.Ativo);
        Assert.Equal(2, cadastro.FranqueadosUnidades.Count);
        Assert.Equal(2, cadastro.VinculosAcesso.Count);
        Assert.All(cadastro.VinculosAcesso, vinculo =>
        {
            Assert.Equal(PerfilAcesso.AdministradorUnidade, vinculo.Perfil);
            Assert.Equal(contexto.OrganizacaoId, vinculo.OrganizacaoId);
        });
        Assert.Equal(
            [primeiraUnidade, segundaUnidade],
            cadastro.VinculosAcesso.Select(vinculo => vinculo.UnidadeId!.Value));
    }

    [Fact]
    public async Task Pessoa_fisica_deriva_dados_do_usuario_e_ignora_campos_exclusivos_de_empresa()
    {
        var contexto = CriarContexto();
        var unidadeId = Guid.NewGuid();
        contexto.Repositorio.UnidadesValidas.Add(unidadeId);
        var solicitacao = new CriarUsuarioFranqueadoraSolicitacao(
            TipoCadastroUsuario.Franqueado,
            "  Pessoa Física BFA  ",
            "  pessoa.fisica@bfa.test  ",
            "  (11) 97777-7777  ",
            new FranqueadoCadastroDados(
                TipoPessoaFranqueado.PessoaFisica,
                "Razão social hostil",
                "Fantasia hostil",
                "123.456.789-01",
                "telefone hostil",
                "email-invalido-hostil",
                "financeiro@bfa.test",
                "Representante hostil",
                null,
                null,
                null,
                null,
                35,
                3554508,
                null,
                null,
                [unidadeId]));

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            solicitacao,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.Sucesso, resultado.Estado);
        var franqueado = Assert.IsType<Franqueado>(
            Assert.Single(contexto.Repositorio.Cadastros).Franqueado);
        Assert.Equal("Pessoa Física BFA", franqueado.NomeRazaoSocial);
        Assert.Equal("pessoa.fisica@bfa.test", franqueado.Email);
        Assert.Equal("(11) 97777-7777", franqueado.Telefone);
        Assert.Null(franqueado.NomeFantasia);
        Assert.Null(franqueado.ResponsavelLegal);
        Assert.Equal("financeiro@bfa.test", franqueado.EmailFinanceiro);
    }

    [Fact]
    public async Task Pessoa_juridica_preserva_dados_comerciais_e_cnpj_alfanumerico()
    {
        var contexto = CriarContexto();
        var unidadeId = Guid.NewGuid();
        contexto.Repositorio.UnidadesValidas.Add(unidadeId);
        var solicitacao = new CriarUsuarioFranqueadoraSolicitacao(
            TipoCadastroUsuario.Franqueado,
            "Usuário da Empresa",
            "usuario@empresa.test",
            "11999999999",
            new FranqueadoCadastroDados(
                TipoPessoaFranqueado.PessoaJuridica,
                "Empresa BFA Ltda.",
                "BFA Empresa",
                "ab.cde.f12/3456-78",
                "1133334444",
                "comercial@empresa.test",
                "financeiro@empresa.test",
                "Representante BFA",
                null,
                null,
                null,
                null,
                35,
                3554508,
                null,
                null,
                [unidadeId]));

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            solicitacao,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.Sucesso, resultado.Estado);
        var cadastro = Assert.Single(contexto.Repositorio.Cadastros);
        Assert.Equal("usuario@empresa.test", cadastro.Email);
        Assert.Equal("Usuário da Empresa", cadastro.PerfilUsuario.NomeCompleto);
        Assert.Equal("11999999999", cadastro.PerfilUsuario.Telefone);
        var franqueado = Assert.IsType<Franqueado>(cadastro.Franqueado);
        Assert.Equal("ABCDEF12345678", franqueado.Documento);
        Assert.Equal("Empresa BFA Ltda.", franqueado.NomeRazaoSocial);
        Assert.Equal("BFA Empresa", franqueado.NomeFantasia);
        Assert.Equal("1133334444", franqueado.Telefone);
        Assert.Equal("comercial@empresa.test", franqueado.Email);
        Assert.Equal("financeiro@empresa.test", franqueado.EmailFinanceiro);
        Assert.Equal("Representante BFA", franqueado.ResponsavelLegal);
    }

    [Fact]
    public async Task Email_existente_e_rejeitado_sem_alterar_usuario_existente()
    {
        var contexto = CriarContexto();
        contexto.Repositorio.EmailExiste = true;

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            CriarAdministrador(),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.EmailDuplicado, resultado.Estado);
        Assert.Equal("Já existe um usuário cadastrado com este email.", resultado.Mensagem);
        Assert.Empty(contexto.Repositorio.Cadastros);
    }

    [Fact]
    public async Task Unidade_de_outra_organizacao_invalida_todo_o_cadastro()
    {
        var contexto = CriarContexto();
        var unidadeValida = Guid.NewGuid();
        var unidadeExterna = Guid.NewGuid();
        contexto.Repositorio.UnidadesValidas.Add(unidadeValida);

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            CriarFranqueado([unidadeValida, unidadeExterna]),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.UnidadesInvalidas, resultado.Estado);
        Assert.Empty(contexto.Repositorio.Cadastros);
    }

    [Fact]
    public async Task Unidade_com_franqueado_ativo_retorna_conflito_amigavel_sem_cadastro()
    {
        var contexto = CriarContexto();
        var unidadeId = Guid.NewGuid();
        contexto.Repositorio.UnidadesValidas.Add(unidadeId);
        contexto.Repositorio.UnidadeEmConflito = "BFA Tietê";

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            CriarFranqueado([unidadeId]),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.UnidadeComFranqueadoAtivo, resultado.Estado);
        Assert.Contains("BFA Tietê", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Empty(contexto.Repositorio.Cadastros);
    }

    [Fact]
    public async Task Estado_inexistente_rejeita_cadastro()
    {
        var contexto = CriarContexto();
        var unidadeId = Guid.NewGuid();
        contexto.Repositorio.UnidadesValidas.Add(unidadeId);
        var solicitacao = CriarFranqueado([unidadeId]);
        solicitacao = solicitacao with
        {
            Franqueado = solicitacao.Franqueado! with { EstadoCodigoIbge = 99 }
        };

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            solicitacao,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.EstadoLocalidadeInvalido, resultado.Estado);
        Assert.Empty(contexto.Repositorio.Cadastros);
    }

    [Fact]
    public async Task Municipio_inexistente_rejeita_cadastro()
    {
        var contexto = CriarContexto();
        var unidadeId = Guid.NewGuid();
        contexto.Repositorio.UnidadesValidas.Add(unidadeId);
        var solicitacao = CriarFranqueado([unidadeId]);
        solicitacao = solicitacao with
        {
            Franqueado = solicitacao.Franqueado! with { MunicipioCodigoIbge = 9999999 }
        };

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            solicitacao,
            CancellationToken.None);

        Assert.Equal(
            EstadoGerenciamentoUsuario.MunicipioLocalidadeInvalido,
            resultado.Estado);
        Assert.Empty(contexto.Repositorio.Cadastros);
    }

    [Fact]
    public async Task Troca_de_estado_nao_aceita_municipio_da_selecao_anterior()
    {
        var contexto = CriarContexto();
        contexto.Localidades.Estados.Add(new(41, "PR", "Paraná"));
        contexto.Localidades.Municipios[41] = [new(4106902, "Curitiba")];
        var unidadeId = Guid.NewGuid();
        contexto.Repositorio.UnidadesValidas.Add(unidadeId);
        var solicitacao = CriarFranqueado([unidadeId]);
        solicitacao = solicitacao with
        {
            Franqueado = solicitacao.Franqueado! with { EstadoCodigoIbge = 41 }
        };

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            solicitacao,
            CancellationToken.None);

        Assert.Equal(
            EstadoGerenciamentoUsuario.MunicipioLocalidadeInvalido,
            resultado.Estado);
        Assert.Empty(contexto.Repositorio.Cadastros);
    }

    [Fact]
    public async Task Catalogo_vazio_rejeita_cadastro_sem_fallback_para_texto()
    {
        var contexto = CriarContexto();
        contexto.Localidades.Estados.Clear();
        contexto.Localidades.Municipios.Clear();
        var unidadeId = Guid.NewGuid();
        contexto.Repositorio.UnidadesValidas.Add(unidadeId);

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            CriarFranqueado([unidadeId]),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.EstadoLocalidadeInvalido, resultado.Estado);
        Assert.Contains("Catálogo", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Empty(contexto.Repositorio.Cadastros);
    }

    [Theory]
    [InlineData("Identity criado sem PerfilUsuario")]
    [InlineData("PerfilUsuario criado sem vínculos")]
    public async Task Falha_em_etapa_intermediaria_nao_publica_resultado_parcial(
        string etapaSimulada)
    {
        var contexto = CriarContexto();
        contexto.Repositorio.ResultadoPersistencia = new(
            EstadoPersistenciaCadastroUsuario.Falha);

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            CriarAdministrador(),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.FalhaPersistencia, resultado.Estado);
        Assert.Null(resultado.Usuario);
        Assert.Empty(contexto.Repositorio.Cadastros);
        Assert.False(string.IsNullOrWhiteSpace(etapaSimulada));
    }

    [Fact]
    public async Task Usuario_sem_um_unico_contexto_administrador_rede_nao_pode_cadastrar()
    {
        var contexto = CriarContexto();
        contexto.Acessos.Organizacoes.Add(Guid.NewGuid());

        var resultado = await contexto.Servico.CriarAsync(
            contexto.UsuarioAtualId,
            CriarAdministrador(),
            CancellationToken.None);

        Assert.Equal(
            EstadoGerenciamentoUsuario.SelecaoOrganizacaoNecessaria,
            resultado.Estado);
        Assert.Empty(contexto.Repositorio.Cadastros);
    }

    [Fact]
    public async Task Consulta_de_edicao_exige_relacao_com_a_organizacao_atual()
    {
        var contexto = CriarContexto();
        var usuarioId = Guid.NewGuid();
        contexto.Repositorio.Edicao = new(
            usuarioId,
            "Pessoa BFA",
            "pessoa@bfa.test",
            null,
            [Guid.NewGuid()]);

        var resultado = await contexto.Servico.ObterEdicaoAsync(
            contexto.UsuarioAtualId,
            usuarioId,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.UsuarioNaoEncontrado, resultado.Estado);
        Assert.Null(resultado.Valor);
    }

    [Fact]
    public async Task Usuario_com_relacoes_ativas_em_multiplas_organizacoes_nao_pode_ser_editado()
    {
        var contexto = CriarContexto();
        var usuarioId = Guid.NewGuid();
        contexto.Repositorio.Edicao = new(
            usuarioId,
            "Pessoa compartilhada",
            "compartilhada@bfa.test",
            null,
            [contexto.OrganizacaoId, Guid.NewGuid()]);

        var resultado = await contexto.Servico.ObterEdicaoAsync(
            contexto.UsuarioAtualId,
            usuarioId,
            CancellationToken.None);

        Assert.Equal(
            EstadoGerenciamentoUsuario.UsuarioComMultiplasOrganizacoes,
            resultado.Estado);
        Assert.Contains("mais de uma Organização", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edicao_normaliza_dados_e_informa_organizacao_para_revalidacao_transacional()
    {
        var contexto = CriarContexto();
        var usuarioId = Guid.NewGuid();

        var resultado = await contexto.Servico.EditarAsync(
            contexto.UsuarioAtualId,
            new EditarUsuarioFranqueadoraSolicitacao(
                usuarioId,
                "  Pessoa Atualizada  ",
                "  atualizada@bfa.test  ",
                "  (11) 99999-9999  "),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoUsuario.Sucesso, resultado.Estado);
        var dados = Assert.Single(contexto.Repositorio.Atualizacoes);
        Assert.Equal(usuarioId, dados.UsuarioId);
        Assert.Equal(contexto.OrganizacaoId, dados.OrganizacaoId);
        Assert.Equal("Pessoa Atualizada", dados.NomeCompleto);
        Assert.Equal("atualizada@bfa.test", dados.Email);
        Assert.Equal("(11) 99999-9999", dados.Telefone);
        Assert.Equal(AgoraUtc, dados.AtualizadoEmUtc);
    }

    private static ContextoTeste CriarContexto()
    {
        var usuarioAtualId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var acessos = new AcessoUsuarioConsultaTeste();
        acessos.Organizacoes.Add(organizacaoId);
        var repositorio = new UsuariosRepositorioTeste();
        var localidades = new LocalidadesConsultaTeste();
        var servico = new UsuariosFranqueadoraServico(
            acessos,
            repositorio,
            localidades,
            new TimeProviderTeste(new DateTimeOffset(AgoraUtc)));
        return new(
            usuarioAtualId,
            organizacaoId,
            acessos,
            repositorio,
            localidades,
            servico);
    }

    private static CriarUsuarioFranqueadoraSolicitacao CriarAdministrador()
    {
        return new(
            TipoCadastroUsuario.AdministradorRede,
            "Administrador BFA",
            "admin@bfa.test",
            "11999999999",
            Franqueado: null);
    }

    private static CriarUsuarioFranqueadoraSolicitacao CriarFranqueado(Guid[] unidadesIds)
    {
        return new(
            TipoCadastroUsuario.Franqueado,
            "Pessoa Franqueada",
            "franqueado@bfa.test",
            "11999999999",
            new FranqueadoCadastroDados(
                TipoPessoaFranqueado.PessoaFisica,
                "Pessoa Franqueada",
                null,
                "123.456.789-01",
                "11988888888",
                "comercial@bfa.test",
                null,
                null,
                null,
                null,
                null,
                null,
                35,
                3554508,
                null,
                null,
                unidadesIds));
    }

    private sealed record ContextoTeste(
        Guid UsuarioAtualId,
        Guid OrganizacaoId,
        AcessoUsuarioConsultaTeste Acessos,
        UsuariosRepositorioTeste Repositorio,
        LocalidadesConsultaTeste Localidades,
        UsuariosFranqueadoraServico Servico);

    private sealed class TimeProviderTeste(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private sealed class LocalidadesConsultaTeste : ILocalidadesConsulta
    {
        public List<EstadoLocalidadeResumo> Estados { get; } =
            [new(35, "SP", "São Paulo")];

        public Dictionary<int, IReadOnlyList<MunicipioLocalidadeResumo>> Municipios { get; } =
            new()
            {
                [35] = [new(3554508, "Tietê")]
            };

        public Task<IReadOnlyList<EstadoLocalidadeResumo>> ListarEstadosAtivosAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<EstadoLocalidadeResumo>>([.. Estados]);
        }

        public Task<IReadOnlyList<MunicipioLocalidadeResumo>> ListarMunicipiosAtivosAsync(
            int estadoCodigoIbge,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Municipios.GetValueOrDefault(estadoCodigoIbge)
                ?? (IReadOnlyList<MunicipioLocalidadeResumo>)[]);
        }
    }

    private sealed class UsuariosRepositorioTeste : IUsuariosFranqueadoraRepositorio
    {
        public bool EmailExiste { get; set; }

        public bool DocumentoExiste { get; set; }

        public string? UnidadeEmConflito { get; set; }

        public HashSet<Guid> UnidadesValidas { get; } = [];

        public List<CadastroUsuarioFranqueadora> Cadastros { get; } = [];

        public List<AtualizarUsuarioFranqueadoraDados> Atualizacoes { get; } = [];

        public UsuarioFranqueadoraEdicaoContexto? Edicao { get; set; }

        public ResultadoPersistenciaCadastroUsuario ResultadoPersistencia { get; set; } =
            new(EstadoPersistenciaCadastroUsuario.Sucesso, "token-seguro");

        public ResultadoPersistenciaEdicaoUsuario ResultadoEdicao { get; set; } =
            new(EstadoPersistenciaEdicaoUsuario.Sucesso);

        public Task<IReadOnlyList<UsuarioFranqueadoraResumo>> ListarAsync(
            Guid organizacaoId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidadeSelecaoUsuarioResumo>> ListarUnidadesAtivasAsync(
            Guid organizacaoId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> ListarUnidadesValidasAsync(
            Guid organizacaoId,
            IReadOnlyCollection<Guid> unidadesIds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<Guid> validas = unidadesIds
                .Where(UnidadesValidas.Contains)
                .ToArray();
            return Task.FromResult(validas);
        }

        public Task<string?> ObterUnidadeComFranqueadoAtivoAsync(
            Guid organizacaoId,
            IReadOnlyCollection<Guid> unidadesIds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(UnidadeEmConflito);
        }

        public Task<bool> ExisteUsuarioPorEmailAsync(
            string email,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(EmailExiste);
        }

        public Task<bool> ExisteFranqueadoPorDocumentoAsync(
            Guid organizacaoId,
            string documento,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DocumentoExiste);
        }

        public Task<ResultadoPersistenciaCadastroUsuario> CriarAsync(
            CadastroUsuarioFranqueadora cadastro,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ResultadoPersistencia.Estado == EstadoPersistenciaCadastroUsuario.Sucesso)
            {
                Cadastros.Add(cadastro);
            }

            return Task.FromResult(ResultadoPersistencia);
        }

        public Task<UsuarioFranqueadoraEdicaoContexto?> ObterEdicaoAsync(
            Guid usuarioId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Edicao);
        }

        public Task<ResultadoPersistenciaEdicaoUsuario> AtualizarAsync(
            AtualizarUsuarioFranqueadoraDados dados,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ResultadoEdicao.Estado == EstadoPersistenciaEdicaoUsuario.Sucesso)
            {
                Atualizacoes.Add(dados);
            }

            return Task.FromResult(ResultadoEdicao);
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

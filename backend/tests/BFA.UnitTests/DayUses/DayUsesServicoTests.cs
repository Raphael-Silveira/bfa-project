using BFA.Application.DayUses;
using BFA.Application.Unidades;
using BFA.Domain.DayUses;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.UnitTests.DayUses;

public sealed class DayUsesServicoTests
{
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly Guid _organizacaoId = Guid.NewGuid();
    private readonly Guid _unidadeId = Guid.NewGuid();
    private readonly Guid _alunoId = Guid.NewGuid();

    [Fact]
    public async Task Professor_nao_pode_alterar_valor_sugerido()
    {
        var repositorio = new RepositorioFake { Unidade = Unidade() };
        var servico = Criar(repositorio, professor: true);

        var estado = await servico.ConfigurarValorAsync(_usuarioId, _unidadeId, 50m, CancellationToken.None);

        Assert.Equal(EstadoDayUse.SemAcesso, estado);
        Assert.False(repositorio.ConfiguracaoAlterada);
    }

    [Fact]
    public async Task Registro_e_bloqueado_sem_valor_configurado()
    {
        var repositorio = new RepositorioFake { Unidade = Unidade(null) };
        var servico = Criar(repositorio);

        var estado = await servico.RegistrarAsync(_usuarioId, _unidadeId,
            PerfilOperacaoDayUse.AdministradorUnidade,
            new(_alunoId, null, null, null, new DateOnly(2026, 9, 22), 50m, false), CancellationToken.None);

        Assert.Equal(EstadoDayUse.ValorNaoConfigurado, estado);
        Assert.Empty(repositorio.Registros);
    }

    [Fact]
    public async Task Registro_de_aluno_preserva_valores_snapshot_sem_criar_cobranca()
    {
        var repositorio = new RepositorioFake { Unidade = Unidade(50m), AlunoValido = true };
        var servico = Criar(repositorio);

        var estado = await servico.RegistrarAsync(_usuarioId, _unidadeId,
            PerfilOperacaoDayUse.AdministradorUnidade,
            new(_alunoId, null, null, null, new DateOnly(2026, 9, 22), 45m, false), CancellationToken.None);

        Assert.Equal(EstadoDayUse.Sucesso, estado);
        var registro = Assert.Single(repositorio.Registros);
        Assert.Equal(50m, registro.ValorSugerido);
        Assert.Equal(45m, registro.ValorCobrado);
        Assert.Equal(_alunoId, registro.AlunoId);
    }

    [Fact]
    public async Task Registro_do_professor_preserva_identidade_autenticada_como_criador_tecnico()
    {
        var repositorio = new RepositorioFake { Unidade = Unidade(50m), AlunoValido = true };
        var servico = Criar(repositorio, professor: true);

        var estado = await servico.RegistrarAsync(_usuarioId, _unidadeId,
            PerfilOperacaoDayUse.Professor,
            new(_alunoId, null, null, null, new DateOnly(2026, 9, 22), 50m, false), CancellationToken.None);

        Assert.Equal(EstadoDayUse.Sucesso, estado);
        Assert.Equal(_usuarioId, Assert.Single(repositorio.Registros).CriadoPorUsuarioId);
    }

    [Fact]
    public async Task Avulso_com_cortesia_e_aceito_sem_deduplicar_por_dados_pessoais()
    {
        var repositorio = new RepositorioFake { Unidade = Unidade(50m) };
        var servico = Criar(repositorio);
        var solicitacao = new RegistrarDayUseSolicitacao(null, " Maria Silva ", "5511999999999", "maria@example.com", new(2026, 9, 22), 0m, true);

        Assert.Equal(EstadoDayUse.Sucesso, await servico.RegistrarAsync(_usuarioId, _unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, solicitacao, CancellationToken.None));
        Assert.Equal(EstadoDayUse.Sucesso, await servico.RegistrarAsync(_usuarioId, _unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, solicitacao, CancellationToken.None));
        Assert.Equal(2, repositorio.Registros.Count);
        Assert.All(repositorio.Registros, item => Assert.False(item.Pago));
    }

    [Fact]
    public async Task Aluno_nao_pode_ser_registrado_duas_vezes_na_mesma_data()
    {
        var repositorio = new RepositorioFake { Unidade = Unidade(50m), AlunoValido = true, Duplicado = true };
        var servico = Criar(repositorio);

        var estado = await servico.RegistrarAsync(_usuarioId, _unidadeId,
            PerfilOperacaoDayUse.AdministradorUnidade,
            new(_alunoId, null, null, null, new DateOnly(2026, 9, 22), 50m, true), CancellationToken.None);

        Assert.Equal(EstadoDayUse.DayUseDuplicado, estado);
        Assert.Empty(repositorio.Registros);
    }

    [Fact]
    public async Task Administrador_exclui_qualquer_day_use_da_unidade_autorizada()
    {
        var repositorio = new RepositorioFake { Unidade = Unidade() };
        var dayUse = CriarDayUse(_usuarioId);
        repositorio.Registros.Add(dayUse);
        var servico = Criar(repositorio);

        var estado = await servico.ExcluirAsync(_usuarioId, _unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, dayUse.Id, CancellationToken.None);

        Assert.Equal(EstadoDayUse.Sucesso, estado);
        Assert.Empty(repositorio.Registros);
        Assert.Null(repositorio.UltimoCriadorExclusao);
    }

    [Fact]
    public async Task Administrador_nao_exclui_day_use_de_outra_unidade()
    {
        var repositorio = new RepositorioFake { Unidade = Unidade() };
        var dayUse = CriarDayUse(_usuarioId);
        repositorio.Registros.Add(dayUse);
        var servico = Criar(repositorio);

        var estado = await servico.ExcluirAsync(_usuarioId, Guid.NewGuid(), PerfilOperacaoDayUse.AdministradorUnidade, dayUse.Id, CancellationToken.None);

        Assert.Equal(EstadoDayUse.SemAcesso, estado);
        Assert.Single(repositorio.Registros);
    }

    [Fact]
    public async Task Professor_exclui_somente_day_use_criado_por_si()
    {
        var repositorio = new RepositorioFake { Unidade = Unidade() };
        var dayUseProprio = CriarDayUse(_usuarioId);
        var dayUseAlheio = CriarDayUse(Guid.NewGuid());
        repositorio.Registros.AddRange([dayUseProprio, dayUseAlheio]);
        var servico = Criar(repositorio, professor: true);

        var proprio = await servico.ExcluirAsync(_usuarioId, _unidadeId, PerfilOperacaoDayUse.Professor, dayUseProprio.Id, CancellationToken.None);
        var alheio = await servico.ExcluirAsync(_usuarioId, _unidadeId, PerfilOperacaoDayUse.Professor, dayUseAlheio.Id, CancellationToken.None);

        Assert.Equal(EstadoDayUse.Sucesso, proprio);
        Assert.Equal(EstadoDayUse.DayUseNaoEncontrado, alheio);
        Assert.Single(repositorio.Registros);
        Assert.Equal(_usuarioId, repositorio.UltimoCriadorExclusao);
    }

    [Fact]
    public async Task Professor_sem_vinculo_ativo_na_unidade_nao_exclui()
    {
        var repositorio = new RepositorioFake { Unidade = Unidade() };
        var dayUse = CriarDayUse(_usuarioId);
        repositorio.Registros.Add(dayUse);
        var servico = Criar(repositorio, professor: true);

        var estado = await servico.ExcluirAsync(_usuarioId, Guid.NewGuid(), PerfilOperacaoDayUse.Professor, dayUse.Id, CancellationToken.None);

        Assert.Equal(EstadoDayUse.SemAcesso, estado);
        Assert.Single(repositorio.Registros);
    }

    [Fact]
    public async Task Exclusao_de_identificador_inexistente_retorna_nao_encontrado()
    {
        var servico = Criar(new RepositorioFake { Unidade = Unidade() });

        var estado = await servico.ExcluirAsync(_usuarioId, _unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(EstadoDayUse.DayUseNaoEncontrado, estado);
    }

    [Fact]
    public async Task Listagem_libera_exclusao_para_admin_e_apenas_para_criador_no_professor()
    {
        var itens = new DayUsePagina([
            new(Guid.NewGuid(), new(2026, 9, 20), null, "Avulso", false, null, null, 50m, 30m, false, _usuarioId, "Raphael"),
            new(Guid.NewGuid(), new(2026, 9, 19), null, "Outro", false, null, null, 50m, 30m, false, Guid.NewGuid(), "Professor")
        ], 1, 1, 2);
        var repositorio = new RepositorioFake { Unidade = Unidade(), Pagina = itens };

        var admin = await Criar(repositorio).ListarAsync(_usuarioId, _unidadeId, PerfilOperacaoDayUse.AdministradorUnidade, new(null, null, null), CancellationToken.None);
        var professor = await Criar(repositorio, professor: true).ListarAsync(_usuarioId, _unidadeId, PerfilOperacaoDayUse.Professor, new(null, null, null), CancellationToken.None);

        Assert.All(admin.Pagina!.Itens, item => Assert.True(item.PodeExcluir));
        Assert.Equal([true, false], professor.Pagina!.Itens.Select(item => item.PodeExcluir));
    }

    private DayUsesServico Criar(RepositorioFake repositorio, bool professor = false) =>
        new(repositorio, new AcessoFake(_organizacaoId, _unidadeId, professor), TimeProvider.System, NullLogger<DayUsesServico>.Instance);

    private DayUseUnidadeResumo Unidade(decimal? valor = 50m) => new(_organizacaoId, _unidadeId, "BFA Teste", valor);

    private DayUse CriarDayUse(Guid criador) => new(Guid.NewGuid(), _organizacaoId, _unidadeId,
        null, "Participante Avulso", null, null, new(2026, 9, 20), 50m, 30m, false,
        criador, new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));

    private sealed class AcessoFake(Guid organizacaoId, Guid unidadeId, bool professor) : IUnidadesUsuarioConsulta
    {
        private UnidadeAcessoResumo Resumo => new(organizacaoId, unidadeId, "BFA Teste");
        public Task<IReadOnlyList<UnidadeAcessoResumo>> ListarAdministradasAsync(Guid usuarioId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UnidadeAcessoResumo>>([Resumo]);
        public Task<UnidadeAcessoResumo?> ObterAdministradaAsync(Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken) => Task.FromResult<UnidadeAcessoResumo?>(!professor && unidadeId == Resumo.UnidadeId ? Resumo : null);
        public Task<IReadOnlyList<UnidadeAcessoResumo>> ListarProfessorAsync(Guid usuarioId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UnidadeAcessoResumo>>([Resumo]);
        public Task<UnidadeAcessoResumo?> ObterProfessorAsync(Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken) => Task.FromResult<UnidadeAcessoResumo?>(professor && unidadeId == Resumo.UnidadeId ? Resumo : null);
        public Task<IReadOnlyList<UnidadeAcessoResumo>> ListarAlunoAsync(Guid usuarioId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UnidadeAcessoResumo>>([]);
        public Task<UnidadeAcessoResumo?> ObterAlunoAsync(Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken) => Task.FromResult<UnidadeAcessoResumo?>(null);
    }

    private sealed class RepositorioFake : IDayUsesRepositorio
    {
        public DayUseUnidadeResumo? Unidade { get; set; }
        public bool AlunoValido { get; set; }
        public bool Duplicado { get; set; }
        public bool ConfiguracaoAlterada { get; private set; }
        public List<DayUse> Registros { get; } = [];
        public DayUsePagina Pagina { get; set; } = new([], 1, 1, 0);
        public Guid? UltimoCriadorExclusao { get; private set; }
        public Task<DayUseUnidadeResumo?> ObterUnidadeAsync(Guid organizacaoId, Guid unidadeId, CancellationToken cancellationToken) => Task.FromResult(Unidade);
        public Task<bool> AtualizarValorSugeridoAsync(Guid organizacaoId, Guid unidadeId, decimal? valor, DateTime agoraUtc, CancellationToken cancellationToken) { ConfiguracaoAlterada = true; return Task.FromResult(true); }
        public Task<IReadOnlyList<DayUseAlunoResumo>> ListarAlunosAsync(Guid organizacaoId, Guid unidadeId, string? texto, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DayUseAlunoResumo>>([]);
        public Task<bool> ExisteAlunoNaUnidadeAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, CancellationToken cancellationToken) => Task.FromResult(AlunoValido);
        public Task<bool> ExisteDuplicadoAsync(Guid organizacaoId, Guid unidadeId, Guid alunoId, DateOnly dataUso, CancellationToken cancellationToken) => Task.FromResult(Duplicado);
        public Task<bool> CriarAsync(DayUse dayUse, CancellationToken cancellationToken) { Registros.Add(dayUse); return Task.FromResult(true); }
        public Task<DayUsePagina> ListarAsync(Guid organizacaoId, Guid unidadeId, FiltroDayUses filtro, CancellationToken cancellationToken) => Task.FromResult(Pagina);
        public Task<bool> MarcarComoPagoAsync(Guid organizacaoId, Guid unidadeId, Guid dayUseId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> ExcluirAsync(Guid organizacaoId, Guid unidadeId, Guid dayUseId, Guid? criadoPorUsuarioId, CancellationToken cancellationToken)
        {
            UltimoCriadorExclusao = criadoPorUsuarioId;
            var item = Registros.SingleOrDefault(registro => registro.Id == dayUseId
                && registro.OrganizacaoId == organizacaoId && registro.UnidadeId == unidadeId
                && (criadoPorUsuarioId is null || registro.CriadoPorUsuarioId == criadoPorUsuarioId));
            if (item is null) return Task.FromResult(false);
            Registros.Remove(item);
            return Task.FromResult(true);
        }
    }
}

using BFA.Application.Acessos;
using BFA.Application.Contratos;
using BFA.Application.Franqueadora.Contratos;
using BFA.Domain.Acessos;
using BFA.Domain.Contratos;

namespace BFA.UnitTests.Contratos;

public sealed class ContratosFranquiaServicoTests
{
    private static readonly DateTime AgoraUtc = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Cria_contrato_rascunho_e_versao_um_com_campos_e_auditoria()
    {
        var cenario = CriarCenario();

        var resultado = await cenario.Servico.CriarAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            Termos(),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        var contrato = Assert.Single(cenario.Repositorio.Contratos);
        var versao = Assert.Single(cenario.Repositorio.Versoes);
        Assert.Equal(StatusContratoFranquia.Rascunho, contrato.Status);
        Assert.Equal(1, versao.NumeroVersao);
        Assert.Equal(StatusVersaoContratoFranquia.Rascunho, versao.Status);
        Assert.Equal(8m, versao.PercentualRoyalties);
        Assert.Equal(500m, versao.MensalidadeFixa);
        Assert.Equal(cenario.UsuarioId, versao.CriadoPorUsuarioId);
        Assert.Equal(1, cenario.Repositorio.SalvamentosTransacionais);
    }

    [Fact]
    public async Task Outro_tenant_e_vinculo_inativo_nao_criam_contrato()
    {
        var externo = CriarCenario();
        externo.Repositorio.Contexto = null;
        var resultadoExterno = await externo.Servico.CriarAsync(
            externo.UsuarioId,
            externo.FranqueadoId,
            externo.UnidadeId,
            Termos(),
            CancellationToken.None);
        var inativo = CriarCenario(vinculoAtivo: false);
        var resultadoInativo = await inativo.Servico.CriarAsync(
            inativo.UsuarioId,
            inativo.FranqueadoId,
            inativo.UnidadeId,
            Termos(),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.NaoEncontrado, resultadoExterno.Estado);
        Assert.Equal(EstadoGerenciamentoContratoFranquia.VinculoInativo, resultadoInativo.Estado);
        Assert.Empty(externo.Repositorio.Contratos);
        Assert.Empty(inativo.Repositorio.Contratos);
    }

    [Fact]
    public async Task Ativacao_exige_documento_contrato_e_rejeita_outro_ativo()
    {
        var cenario = CriarCenarioComContrato();

        var semDocumento = await cenario.Servico.AtivarAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            CancellationToken.None);
        cenario.Repositorio.TiposDocumento.Add(TipoDocumentoContratoFranquia.Contrato);
        cenario.Repositorio.ExisteContratoAtivoOutro = true;
        var concorrente = await cenario.Servico.AtivarAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.DocumentoObrigatorio, semDocumento.Estado);
        Assert.Equal(EstadoGerenciamentoContratoFranquia.ContratoAtivoExistente, concorrente.Estado);
        Assert.Equal(StatusContratoFranquia.Rascunho, cenario.Repositorio.Contratos[0].Status);
    }

    [Fact]
    public async Task Ativacao_com_documento_formaliza_contrato_e_versao_atomicamente()
    {
        var cenario = CriarCenarioComContrato();
        cenario.Repositorio.TiposDocumento.Add(TipoDocumentoContratoFranquia.Contrato);

        var resultado = await cenario.Servico.AtivarAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        Assert.Equal(StatusContratoFranquia.Ativo, cenario.Repositorio.Contratos[0].Status);
        Assert.Equal(StatusVersaoContratoFranquia.Vigente, cenario.Repositorio.Versoes[0].Status);
        Assert.Equal(1, cenario.Repositorio.SalvamentosTransacionais);
    }

    [Fact]
    public async Task Versao_vigente_nao_pode_ser_editada()
    {
        var cenario = CriarCenarioComContrato(ativo: true);

        var resultado = await cenario.Servico.AtualizarRascunhoAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            cenario.Repositorio.Versoes[0].Id,
            Termos(mensalidade: 900m),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.EstadoInvalido, resultado.Estado);
        Assert.Equal(500m, cenario.Repositorio.Versoes[0].MensalidadeFixa);
    }

    [Fact]
    public async Task Nova_versao_rascunho_permite_editar_termos_sem_alterar_numero_do_contrato_ativo()
    {
        var cenario = CriarCenarioComContrato(ativo: true);
        var contrato = cenario.Repositorio.Contratos[0];
        var nova = NovaVersao(contrato.Id, 2, cenario.UsuarioId);
        cenario.Repositorio.Versoes.Add(nova);
        var solicitacao = Termos(mensalidade: 750m) with
        {
            NumeroContrato = "NUMERO-IGNORADO",
            MotivoAlteracao = "Reajuste anual"
        };

        var resultado = await cenario.Servico.AtualizarRascunhoAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            contrato.Id,
            nova.Id,
            solicitacao,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        Assert.Equal("BFA-001", contrato.Numero);
        Assert.Equal(750m, nova.MensalidadeFixa);
        Assert.Equal("Reajuste anual", nova.MotivoAlteracao);
    }

    [Fact]
    public async Task Cancelar_rascunho_preserva_entidades_e_marca_ambos_cancelados()
    {
        var cenario = CriarCenarioComContrato();

        var resultado = await cenario.Servico.CancelarAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        Assert.Single(cenario.Repositorio.Contratos);
        Assert.Single(cenario.Repositorio.Versoes);
        Assert.Equal(StatusContratoFranquia.Cancelado, cenario.Repositorio.Contratos[0].Status);
        Assert.Equal(StatusVersaoContratoFranquia.Cancelada, cenario.Repositorio.Versoes[0].Status);
        Assert.Equal(1, cenario.Repositorio.SalvamentosTransacionais);
    }

    [Fact]
    public async Task Cancelar_ativo_cancela_contrato_e_versao_vigente()
    {
        var cenario = CriarCenarioComContrato(ativo: true);

        var resultado = await cenario.Servico.CancelarAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        Assert.Equal(StatusContratoFranquia.Cancelado, cenario.Repositorio.Contratos[0].Status);
        Assert.Equal(StatusVersaoContratoFranquia.Cancelada, cenario.Repositorio.Versoes[0].Status);
        Assert.Equal(1, cenario.Repositorio.SalvamentosTransacionais);
    }

    [Fact]
    public async Task Cancelar_ativo_cancela_vigente_e_rascunho_pendente_em_uma_transacao()
    {
        var cenario = CriarCenarioComContrato(ativo: true);
        var contrato = cenario.Repositorio.Contratos[0];
        var vigente = cenario.Repositorio.Versoes[0];
        var rascunho = NovaVersao(contrato.Id, 2, cenario.UsuarioId);
        cenario.Repositorio.Versoes.Add(rascunho);

        var resultado = await cenario.Servico.CancelarAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            contrato.Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        Assert.Equal(StatusContratoFranquia.Cancelado, contrato.Status);
        Assert.Equal(StatusVersaoContratoFranquia.Cancelada, vigente.Status);
        Assert.Equal(StatusVersaoContratoFranquia.Cancelada, rascunho.Status);
        Assert.Equal(1, cenario.Repositorio.SalvamentosTransacionais);
    }

    [Fact]
    public async Task Cancelar_ativo_preserva_versao_substituida()
    {
        var cenario = CriarCenarioComContrato(ativo: true);
        var contrato = cenario.Repositorio.Contratos[0];
        var substituida = cenario.Repositorio.Versoes[0];
        substituida.AlterarStatus(StatusVersaoContratoFranquia.Substituida);
        var vigente = NovaVersao(contrato.Id, 2, cenario.UsuarioId);
        vigente.AlterarStatus(StatusVersaoContratoFranquia.Vigente);
        cenario.Repositorio.Versoes.Add(vigente);

        var resultado = await cenario.Servico.CancelarAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            contrato.Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        Assert.Equal(StatusVersaoContratoFranquia.Substituida, substituida.Status);
        Assert.Equal(StatusVersaoContratoFranquia.Cancelada, vigente.Status);
        Assert.Equal(1, cenario.Repositorio.SalvamentosTransacionais);
    }

    [Fact]
    public async Task Outro_tenant_nao_cancela_contrato()
    {
        var cenario = CriarCenarioComContrato(ativo: true);
        var contrato = cenario.Repositorio.Contratos[0];
        var vigente = cenario.Repositorio.Versoes[0];
        cenario.Repositorio.Contexto = cenario.Repositorio.Contexto! with
        {
            OrganizacaoId = Guid.NewGuid()
        };

        var resultado = await cenario.Servico.CancelarAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            contrato.Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.NaoEncontrado, resultado.Estado);
        Assert.Equal(StatusContratoFranquia.Ativo, contrato.Status);
        Assert.Equal(StatusVersaoContratoFranquia.Vigente, vigente.Status);
        Assert.Equal(0, cenario.Repositorio.SalvamentosTransacionais);
    }

    [Fact]
    public async Task Encerrar_contrato_mantem_ultima_versao_como_vigente()
    {
        var cenario = CriarCenarioComContrato(ativo: true);

        var resultado = await cenario.Servico.EncerrarAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        Assert.Equal(StatusContratoFranquia.Encerrado, cenario.Repositorio.Contratos[0].Status);
        Assert.Equal(StatusVersaoContratoFranquia.Vigente, cenario.Repositorio.Versoes[0].Status);
    }

    [Fact]
    public async Task Nova_versao_exige_motivo_incrementa_numero_e_copia_termos_sem_alterar_vigente()
    {
        var cenario = CriarCenarioComContrato(ativo: true);
        var vigente = cenario.Repositorio.Versoes[0];
        var semMotivo = await cenario.Servico.CriarNovaVersaoAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            " ",
            CancellationToken.None);
        var resultado = await cenario.Servico.CriarNovaVersaoAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            "Reajuste anual",
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.DadosInvalidos, semMotivo.Estado);
        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        var nova = Assert.Single(cenario.Repositorio.Versoes, item => item.NumeroVersao == 2);
        Assert.Equal(StatusVersaoContratoFranquia.Rascunho, nova.Status);
        Assert.Equal(vigente.MensalidadeFixa, nova.MensalidadeFixa);
        Assert.Equal("Reajuste anual", nova.MotivoAlteracao);
        Assert.Equal(StatusVersaoContratoFranquia.Vigente, vigente.Status);
    }

    [Fact]
    public async Task Conflito_de_numero_da_nova_versao_recebe_resultado_controlado()
    {
        var cenario = CriarCenarioComContrato(ativo: true);
        cenario.Repositorio.EstadoNovaVersao = EstadoPersistenciaContratoFranquia.ConflitoVersao;

        var resultado = await cenario.Servico.CriarNovaVersaoAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            "Reajuste",
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.ConflitoVersao, resultado.Estado);
    }

    [Fact]
    public async Task Formalizacao_exige_evidencia_e_substitui_somente_quando_documentada()
    {
        var cenario = CriarCenarioComContrato(ativo: true);
        var vigente = cenario.Repositorio.Versoes[0];
        var nova = NovaVersao(cenario.Repositorio.Contratos[0].Id, 2, cenario.UsuarioId);
        cenario.Repositorio.Versoes.Add(nova);
        var semDocumento = await cenario.Servico.FormalizarVersaoAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            nova.Id,
            CancellationToken.None);
        cenario.Repositorio.TiposDocumento.Add(TipoDocumentoContratoFranquia.Aditivo);
        var resultado = await cenario.Servico.FormalizarVersaoAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            nova.Id,
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.DocumentoObrigatorio, semDocumento.Estado);
        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        Assert.Equal(StatusVersaoContratoFranquia.Substituida, vigente.Status);
        Assert.Equal(StatusVersaoContratoFranquia.Vigente, nova.Status);
        Assert.Equal(1, cenario.Repositorio.Formalizacoes);
        Assert.Single(cenario.Repositorio.Versoes, item =>
            item.Status == StatusVersaoContratoFranquia.Vigente);
    }

    [Fact]
    public async Task Upload_pdf_valido_usa_chave_logica_auditoria_hash_e_descarta_temporario()
    {
        var cenario = CriarCenarioComContrato();
        await using var conteudo = new MemoryStream("%PDF-1.7 teste"u8.ToArray());

        var resultado = await cenario.Servico.EnviarDocumentoAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            cenario.Repositorio.Versoes[0].Id,
            new(
                TipoDocumentoContratoFranquia.Contrato,
                "contrato.pdf",
                "application/pdf",
                conteudo),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.Sucesso, resultado.Estado);
        var documento = Assert.Single(cenario.Repositorio.Documentos);
        Assert.Equal(cenario.UsuarioId, documento.EnviadoPorUsuarioId);
        Assert.Equal(new string('a', 64), documento.HashSha256);
        Assert.Matches($"^contratos/{cenario.Repositorio.Contratos[0].Id:N}/versoes/{cenario.Repositorio.Versoes[0].Id:N}/[0-9a-f]{{32}}[.]pdf$", documento.ChaveArmazenamento);
        Assert.True(cenario.Armazenamento.TemporarioDescartado);
    }

    [Theory]
    [InlineData("arquivo.txt", "application/pdf")]
    [InlineData("arquivo.pdf", "text/plain")]
    public async Task Upload_rejeita_extensao_ou_content_type_falso(string nome, string contentType)
    {
        var cenario = CriarCenarioComContrato();
        await using var conteudo = new MemoryStream("%PDF-"u8.ToArray());

        var resultado = await cenario.Servico.EnviarDocumentoAsync(
            cenario.UsuarioId,
            cenario.FranqueadoId,
            cenario.UnidadeId,
            cenario.Repositorio.Contratos[0].Id,
            cenario.Repositorio.Versoes[0].Id,
            new(TipoDocumentoContratoFranquia.Contrato, nome, contentType, conteudo),
            CancellationToken.None);

        Assert.Equal(EstadoGerenciamentoContratoFranquia.ArquivoInvalido, resultado.Estado);
        Assert.Empty(cenario.Repositorio.Documentos);
    }

    private static Cenario CriarCenario(bool vinculoAtivo = true)
    {
        var usuarioId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var franqueadoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var repositorio = new RepositorioFake
        {
            Contexto = new(
                Guid.NewGuid(),
                organizacaoId,
                franqueadoId,
                "Franqueado BFA",
                unidadeId,
                "BFA Tietê",
                vinculoAtivo,
                true)
        };
        var acesso = new AcessoFake(usuarioId, organizacaoId);
        var armazenamento = new ArmazenamentoFake();
        var servico = new ContratosFranquiaServico(
            acesso,
            repositorio,
            armazenamento,
            new TimeProviderFake(AgoraUtc));
        return new(servico, repositorio, armazenamento, usuarioId, franqueadoId, unidadeId);
    }

    private static Cenario CriarCenarioComContrato(bool ativo = false)
    {
        var cenario = CriarCenario();
        var contrato = new ContratoFranquia(
            Guid.NewGuid(),
            cenario.Repositorio.Contexto!.FranqueadoUnidadeId,
            "BFA-001",
            StatusContratoFranquia.Rascunho,
            AgoraUtc);
        var versao = new ContratoFranquiaVersao(
            Guid.NewGuid(),
            contrato.Id,
            1,
            new DateOnly(2026, 9, 1),
            null,
            8m,
            500m,
            1000m,
            10,
            StatusVersaoContratoFranquia.Rascunho,
            null,
            "Condições iniciais",
            AgoraUtc,
            cenario.UsuarioId);

        if (ativo)
        {
            contrato.AlterarStatus(StatusContratoFranquia.Ativo, AgoraUtc.AddMinutes(1));
            versao.AlterarStatus(StatusVersaoContratoFranquia.Vigente);
        }

        cenario.Repositorio.Contratos.Add(contrato);
        cenario.Repositorio.Versoes.Add(versao);
        return cenario;
    }

    private static ContratoFranquiaVersao NovaVersao(
        Guid contratoId,
        int numero,
        Guid usuarioId) => new(
            Guid.NewGuid(),
            contratoId,
            numero,
            new DateOnly(2027, 1, 1),
            null,
            9m,
            650m,
            null,
            10,
            StatusVersaoContratoFranquia.Rascunho,
            "Reajuste",
            null,
            AgoraUtc,
            usuarioId);

    private static TermosContratoFranquiaSolicitacao Termos(decimal mensalidade = 500m) => new(
        "BFA-001",
        new DateOnly(2026, 9, 1),
        null,
        8m,
        mensalidade,
        1000m,
        10,
        null,
        "Condições iniciais");

    private sealed record Cenario(
        ContratosFranquiaServico Servico,
        RepositorioFake Repositorio,
        ArmazenamentoFake Armazenamento,
        Guid UsuarioId,
        Guid FranqueadoId,
        Guid UnidadeId);

    private sealed class TimeProviderFake(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    private sealed class RepositorioFake : IContratosFranquiaRepositorio
    {
        public ContextoContratoFranquia? Contexto { get; set; }
        public List<ContratoFranquia> Contratos { get; } = [];
        public List<ContratoFranquiaVersao> Versoes { get; } = [];
        public List<DocumentoContratoFranquia> Documentos { get; } = [];
        public HashSet<TipoDocumentoContratoFranquia> TiposDocumento { get; } = [];
        public bool ExisteContratoAtivoOutro { get; set; }
        public int SalvamentosTransacionais { get; private set; }
        public int Formalizacoes { get; private set; }
        public EstadoPersistenciaContratoFranquia EstadoNovaVersao { get; set; } =
            EstadoPersistenciaContratoFranquia.Sucesso;

        public Task<ContextoContratoFranquia?> ObterContextoAsync(Guid organizacaoId, Guid franqueadoId, Guid unidadeId, CancellationToken cancellationToken) =>
            Task.FromResult(Contexto is not null
                && Contexto.OrganizacaoId == organizacaoId
                && Contexto.FranqueadoId == franqueadoId
                && Contexto.UnidadeId == unidadeId ? Contexto : null);
        public Task<ContratoFranquiaPainel> ObterPainelAsync(ContextoContratoFranquia contexto, CancellationToken cancellationToken) =>
            Task.FromResult(new ContratoFranquiaPainel(contexto, null, null, null, []));
        public Task<ContratoFranquia?> ObterContratoParaAtualizacaoAsync(Guid vinculoId, Guid contratoId, CancellationToken cancellationToken) =>
            Task.FromResult(Contratos.SingleOrDefault(item => item.Id == contratoId && item.FranqueadoUnidadeId == vinculoId));
        public Task<ContratoFranquiaVersao?> ObterVersaoParaAtualizacaoAsync(Guid contratoId, Guid versaoId, CancellationToken cancellationToken) =>
            Task.FromResult(Versoes.SingleOrDefault(item => item.Id == versaoId && item.ContratoFranquiaId == contratoId));
        public Task<IReadOnlyList<ContratoFranquiaVersao>> ListarVersoesParaAtualizacaoAsync(Guid contratoId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ContratoFranquiaVersao>>(Versoes.Where(item => item.ContratoFranquiaId == contratoId).ToArray());
        public Task<bool> ExisteContratoAtivoOutroAsync(Guid vinculoId, Guid contratoId, CancellationToken cancellationToken) => Task.FromResult(ExisteContratoAtivoOutro);
        public Task<bool> ExisteDocumentoAsync(Guid versaoId, IReadOnlyCollection<TipoDocumentoContratoFranquia> tipos, CancellationToken cancellationToken) =>
            Task.FromResult(tipos.Any(TiposDocumento.Contains));
        public Task<DocumentoContratoFranquiaAcesso?> ObterDocumentoAsync(Guid organizacaoId, Guid franqueadoId, Guid unidadeId, Guid contratoId, Guid versaoId, Guid documentoId, CancellationToken cancellationToken) => Task.FromResult<DocumentoContratoFranquiaAcesso?>(null);
        public void Adicionar(ContratoFranquia contrato) => Contratos.Add(contrato);
        public void Adicionar(ContratoFranquiaVersao versao) => Versoes.Add(versao);
        public Task<EstadoPersistenciaContratoFranquia> SalvarTransacaoAsync(CancellationToken cancellationToken)
        {
            SalvamentosTransacionais++;
            return Task.FromResult(EstadoPersistenciaContratoFranquia.Sucesso);
        }
        public Task<EstadoPersistenciaContratoFranquia> SalvarNovaVersaoAsync(ContratoFranquiaVersao versao, CancellationToken cancellationToken)
        {
            if (EstadoNovaVersao == EstadoPersistenciaContratoFranquia.Sucesso) Versoes.Add(versao);
            return Task.FromResult(EstadoNovaVersao);
        }
        public Task<EstadoPersistenciaContratoFranquia> SalvarFormalizacaoAsync(ContratoFranquiaVersao versaoVigenteAnterior, ContratoFranquiaVersao novaVersaoVigente, CancellationToken cancellationToken)
        {
            Formalizacoes++;
            return Task.FromResult(EstadoPersistenciaContratoFranquia.Sucesso);
        }
        public Task<EstadoPersistenciaContratoFranquia> SalvarDocumentoAsync(DocumentoContratoFranquia documento, string identificadorTemporario, CancellationToken cancellationToken)
        {
            Documentos.Add(documento);
            return Task.FromResult(EstadoPersistenciaContratoFranquia.Sucesso);
        }
    }

    private sealed class ArmazenamentoFake : IArmazenamentoDocumentosContrato
    {
        public bool TemporarioDescartado { get; private set; }
        public Task SalvarAsync(string chaveArmazenamento, Stream conteudo, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Stream> AbrirLeituraAsync(string chaveArmazenamento, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task<bool> ExisteAsync(string chaveArmazenamento, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<ArquivoTemporarioDocumentoContrato> SalvarTemporarioAsync(Stream conteudo, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArquivoTemporarioDocumentoContrato(".temporarios/teste.tmp", 14, new string('a', 64), true));
        public Task ConfirmarTemporarioAsync(string identificadorTemporario, string chaveArmazenamento, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DescartarTemporarioAsync(string identificadorTemporario, CancellationToken cancellationToken = default)
        {
            TemporarioDescartado = true;
            return Task.CompletedTask;
        }
        public Task DescartarArquivoNaoConfirmadoAsync(string chaveArmazenamento, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AcessoFake(Guid usuarioId, Guid organizacaoId) : IAcessoUsuarioConsulta
    {
        public Task<IReadOnlyList<Guid>> ListarOrganizacoesAdministradorRedeAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>(id == usuarioId ? [organizacaoId] : []);
        public Task<bool> EhAdministradorRedeAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(id == usuarioId);
        public Task<bool> EhAdministradorRedeNaOrganizacaoAsync(Guid id, Guid org, CancellationToken cancellationToken) => Task.FromResult(id == usuarioId && org == organizacaoId);
        public Task<bool> PossuiAlgumPerfilAsync(Guid id, IReadOnlyCollection<PerfilAcesso> perfis, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> PossuiPerfilNaOrganizacaoAsync(Guid id, Guid org, PerfilAcesso perfil, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> PossuiAcessoUnidadeAsync(Guid id, Guid org, Guid unidadeId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> PossuiPerfilNaUnidadeAsync(Guid id, Guid org, Guid unidadeId, PerfilAcesso perfil, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> PossuiAlgumPerfilNaUnidadeAsync(Guid id, Guid org, Guid unidadeId, IReadOnlyCollection<PerfilAcesso> perfis, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}

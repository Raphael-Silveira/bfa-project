using BFA.Application.Acessos;
using BFA.Application.Contratos;
using BFA.Domain.Contratos;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Franqueadora.Contratos;

public sealed class ContratosFranquiaServico(
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IContratosFranquiaRepositorio repositorio,
    IArmazenamentoDocumentosContrato armazenamento,
    TimeProvider timeProvider,
    ILogger<ContratosFranquiaServico> logger)
    : IContratosFranquiaConsulta, IContratosFranquiaServico
{
    private static readonly TipoDocumentoContratoFranquia[] DocumentoPrincipal =
        [TipoDocumentoContratoFranquia.Contrato];
    private static readonly TipoDocumentoContratoFranquia[] DocumentoFormalizacao =
        [TipoDocumentoContratoFranquia.Contrato, TipoDocumentoContratoFranquia.Aditivo];

    public async Task<ResultadoContratoFranquia<ContratoFranquiaPainel>> ObterAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            cancellationToken);

        return contexto.Valor is not { } valor
            ? new(contexto.Estado, null)
            : new(
                EstadoGerenciamentoContratoFranquia.Sucesso,
                await repositorio.ObterPainelAsync(valor, cancellationToken));
    }

    public async Task<ResultadoContratoFranquia<VersaoContratoFranquiaResumo>> ObterVersaoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        CancellationToken cancellationToken)
    {
        var painel = await ObterAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            cancellationToken);

        if (painel.Estado != EstadoGerenciamentoContratoFranquia.Sucesso
            || painel.Valor is not { } valor)
        {
            return new(painel.Estado, null);
        }

        if (valor.ContratoId != contratoId)
        {
            return new(EstadoGerenciamentoContratoFranquia.NaoEncontrado, null);
        }

        var versao = valor.Versoes.SingleOrDefault(item => item.Id == versaoId);
        return versao is null
            ? new(EstadoGerenciamentoContratoFranquia.NaoEncontrado, null)
            : new(EstadoGerenciamentoContratoFranquia.Sucesso, versao);
    }

    public async Task<ResultadoContratoFranquia<DocumentoContratoFranquiaLeitura>> AbrirDocumentoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        Guid documentoId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            cancellationToken);

        if (contexto.Valor is not { } valor)
        {
            return new(contexto.Estado, null);
        }

        var documento = await repositorio.ObterDocumentoAsync(
            valor.OrganizacaoId,
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            documentoId,
            cancellationToken);

        if (documento is null)
        {
            return new(EstadoGerenciamentoContratoFranquia.NaoEncontrado, null);
        }

        try
        {
            if (!await armazenamento.ExisteAsync(
                    documento.ChaveArmazenamento,
                    cancellationToken))
            {
                return new(
                    EstadoGerenciamentoContratoFranquia.DocumentoIndisponivel,
                    null,
                    "Documento indisponível no armazenamento.");
            }

            var stream = await armazenamento.AbrirLeituraAsync(
                documento.ChaveArmazenamento,
                cancellationToken);
            return new(
                EstadoGerenciamentoContratoFranquia.Sucesso,
                new DocumentoContratoFranquiaLeitura(
                    stream,
                    documento.NomeOriginal,
                    documento.ContentType));
        }
        catch (IOException)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.DocumentoIndisponivel,
                null,
                "Documento indisponível no armazenamento.");
        }
    }

    public async Task<ResultadoContratoFranquia<Guid>> CriarAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        TermosContratoFranquiaSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAtivoAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            cancellationToken);

        if (contexto.Valor is not { } valor)
        {
            return new(contexto.Estado, default, contexto.Mensagem);
        }

        if (await repositorio.ExisteContratoAtivoOutroAsync(
                valor.FranqueadoUnidadeId,
                Guid.Empty,
                cancellationToken))
        {
            return new(
                EstadoGerenciamentoContratoFranquia.ContratoAtivoExistente,
                default,
                "Já existe um contrato ativo para esta unidade.");
        }

        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;
        var contratoId = Guid.NewGuid();

        try
        {
            var contrato = new ContratoFranquia(
                contratoId,
                valor.FranqueadoUnidadeId,
                solicitacao.NumeroContrato,
                StatusContratoFranquia.Rascunho,
                agoraUtc);
            var versao = CriarVersao(
                Guid.NewGuid(),
                contratoId,
                1,
                solicitacao,
                usuarioAtualId,
                agoraUtc);
            repositorio.Adicionar(contrato);
            repositorio.Adicionar(versao);
        }
        catch (ArgumentException exception)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.DadosInvalidos,
                default,
                exception.Message);
        }

        var persistencia = await repositorio.SalvarTransacaoAsync(cancellationToken);
        if (persistencia == EstadoPersistenciaContratoFranquia.Sucesso)
        {
            logger.LogInformation("CriarContrato concluído para unidade {UnidadeId}", unidadeId);
        }
        return persistencia == EstadoPersistenciaContratoFranquia.Sucesso
            ? new(EstadoGerenciamentoContratoFranquia.Sucesso, contratoId)
            : MapearPersistencia<Guid>(persistencia);
    }

    public async Task<ResultadoOperacaoContratoFranquia> AtualizarRascunhoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        TermosContratoFranquiaSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var dados = await ObterEntidadesAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            exigirVinculoAtivo: true,
            cancellationToken);

        if (dados.Valor is not { } valor)
        {
            return new(dados.Estado, dados.Mensagem);
        }

        if (valor.Versao.Status != StatusVersaoContratoFranquia.Rascunho
            || valor.Contrato.Status is not (
                StatusContratoFranquia.Rascunho or StatusContratoFranquia.Ativo))
        {
            return new(
                EstadoGerenciamentoContratoFranquia.EstadoInvalido,
                "Somente contratos e versões em rascunho podem ser editados.");
        }

        try
        {
            if (valor.Contrato.Status == StatusContratoFranquia.Rascunho)
            {
                valor.Contrato.AtualizarNumeroRascunho(
                    solicitacao.NumeroContrato,
                    timeProvider.GetUtcNow().UtcDateTime);
            }

            valor.Versao.AtualizarTermosRascunho(
                solicitacao.DataInicio,
                solicitacao.DataFim,
                solicitacao.PercentualRoyalties,
                solicitacao.MensalidadeFixa,
                solicitacao.TaxaAdesao,
                solicitacao.DiaVencimento,
                solicitacao.MotivoAlteracao,
                solicitacao.Observacoes);
        }
        catch (ArgumentException exception)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.DadosInvalidos,
                exception.Message);
        }

        return MapearPersistenciaOperacao(
            await repositorio.SalvarTransacaoAsync(cancellationToken));
    }

    public async Task<ResultadoOperacaoContratoFranquia> EnviarDocumentoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        EnviarDocumentoContratoFranquiaSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var dados = await ObterEntidadesAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            exigirVinculoAtivo: true,
            cancellationToken);

        if (dados.Valor is not { } valor)
        {
            return new(dados.Estado, dados.Mensagem);
        }

        if (valor.Versao.Status != StatusVersaoContratoFranquia.Rascunho)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.EstadoInvalido,
                "Documentos somente podem ser adicionados a uma versão em rascunho.");
        }

        var nomeOriginal = Path.GetFileName(solicitacao.NomeOriginal).Trim();

        if (!Enum.IsDefined(solicitacao.TipoDocumento)
            || !string.Equals(Path.GetExtension(nomeOriginal), ".pdf", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(solicitacao.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            || nomeOriginal.Length is 0 or > DocumentoContratoFranquia.NomeOriginalTamanhoMaximo)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.ArquivoInvalido,
                "Envie um arquivo PDF válido.");
        }

        ArquivoTemporarioDocumentoContrato? temporario = null;

        try
        {
            temporario = await armazenamento.SalvarTemporarioAsync(
                solicitacao.Conteudo,
                cancellationToken);

            if (!temporario.PossuiAssinaturaPdf)
            {
                return new(
                    EstadoGerenciamentoContratoFranquia.ArquivoInvalido,
                    "O conteúdo enviado não possui uma assinatura PDF válida.");
            }

            var documentoId = Guid.NewGuid();
            var chave = $"contratos/{contratoId:N}/versoes/{versaoId:N}/{documentoId:N}.pdf";
            var documento = new DocumentoContratoFranquia(
                documentoId,
                versaoId,
                solicitacao.TipoDocumento,
                nomeOriginal,
                chave,
                "application/pdf",
                temporario.TamanhoBytes,
                temporario.HashSha256,
                timeProvider.GetUtcNow().UtcDateTime,
                usuarioAtualId);
            return MapearPersistenciaOperacao(await repositorio.SalvarDocumentoAsync(
                documento,
                temporario.Identificador,
                cancellationToken));
        }
        catch (TamanhoDocumentoContratoExcedidoException exception)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.ArquivoMuitoGrande,
                $"O arquivo excede o limite de {FormatarTamanho(exception.TamanhoMaximoBytes)}.");
        }
        catch (ArgumentException exception)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.ArquivoInvalido,
                exception.Message);
        }
        finally
        {
            if (temporario is not null)
            {
                await armazenamento.DescartarTemporarioAsync(
                    temporario.Identificador,
                    CancellationToken.None);
            }
        }
    }

    public async Task<ResultadoOperacaoContratoFranquia> AtivarAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAtivoAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            cancellationToken);

        if (contexto.Valor is not { } contextoValor)
        {
            return new(contexto.Estado, contexto.Mensagem);
        }

        var contrato = await repositorio.ObterContratoParaAtualizacaoAsync(
            contextoValor.FranqueadoUnidadeId,
            contratoId,
            cancellationToken);
        var versoes = contrato is null
            ? []
            : await repositorio.ListarVersoesParaAtualizacaoAsync(contratoId, cancellationToken);
        var versao = versoes.SingleOrDefault(item =>
            item.Status == StatusVersaoContratoFranquia.Rascunho);

        if (contrato is null || versao is null)
        {
            return new(EstadoGerenciamentoContratoFranquia.NaoEncontrado);
        }

        if (contrato.Status != StatusContratoFranquia.Rascunho)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.EstadoInvalido,
                "Somente um contrato em rascunho pode ser ativado.");
        }

        if (!await repositorio.ExisteDocumentoAsync(
                versao.Id,
                DocumentoPrincipal,
                cancellationToken))
        {
            return new(
                EstadoGerenciamentoContratoFranquia.DocumentoObrigatorio,
                "Adicione o documento do contrato antes de ativá-lo.");
        }

        if (await repositorio.ExisteContratoAtivoOutroAsync(
                contextoValor.FranqueadoUnidadeId,
                contratoId,
                cancellationToken))
        {
            return new(
                EstadoGerenciamentoContratoFranquia.ContratoAtivoExistente,
                "Já existe um contrato ativo para esta unidade.");
        }

        contrato.AlterarStatus(
            StatusContratoFranquia.Ativo,
            timeProvider.GetUtcNow().UtcDateTime);
        versao.AlterarStatus(StatusVersaoContratoFranquia.Vigente);
        var resultado = MapearPersistenciaOperacao(
            await repositorio.SalvarTransacaoAsync(cancellationToken));
        if (resultado.Estado == EstadoGerenciamentoContratoFranquia.Sucesso)
        {
            logger.LogInformation("AtivarContrato concluído para contrato {ContratoId}", contratoId);
        }
        return resultado;
    }

    public async Task<ResultadoContratoFranquia<Guid>> CriarNovaVersaoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        string motivoAlteracao,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAtivoAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            cancellationToken);

        if (contexto.Valor is not { } contextoValor)
        {
            return new(contexto.Estado, default, contexto.Mensagem);
        }

        var contrato = await repositorio.ObterContratoParaAtualizacaoAsync(
            contextoValor.FranqueadoUnidadeId,
            contratoId,
            cancellationToken);
        var versoes = contrato is null
            ? []
            : await repositorio.ListarVersoesParaAtualizacaoAsync(contratoId, cancellationToken);

        if (contrato is null)
        {
            return new(EstadoGerenciamentoContratoFranquia.NaoEncontrado, default);
        }

        var vigente = versoes.SingleOrDefault(item =>
            item.Status == StatusVersaoContratoFranquia.Vigente);

        if (contrato.Status != StatusContratoFranquia.Ativo
            || vigente is null
            || versoes.Any(item => item.Status == StatusVersaoContratoFranquia.Rascunho))
        {
            return new(
                EstadoGerenciamentoContratoFranquia.EstadoInvalido,
                default,
                "O contrato precisa estar ativo e sem outra versão em rascunho.");
        }

        if (string.IsNullOrWhiteSpace(motivoAlteracao))
        {
            return new(
                EstadoGerenciamentoContratoFranquia.DadosInvalidos,
                default,
                "Informe o motivo da alteração.");
        }

        var versaoId = Guid.NewGuid();
        ContratoFranquiaVersao novaVersao;

        try
        {
            novaVersao = new ContratoFranquiaVersao(
                versaoId,
                contratoId,
                versoes.Max(item => item.NumeroVersao) + 1,
                vigente.DataInicio,
                vigente.DataFim,
                vigente.PercentualRoyalties,
                vigente.MensalidadeFixa,
                vigente.TaxaAdesao,
                vigente.DiaVencimento,
                StatusVersaoContratoFranquia.Rascunho,
                motivoAlteracao,
                vigente.Observacoes,
                timeProvider.GetUtcNow().UtcDateTime,
                usuarioAtualId);
        }
        catch (ArgumentException exception)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.DadosInvalidos,
                default,
                exception.Message);
        }

        var persistencia = await repositorio.SalvarNovaVersaoAsync(
            novaVersao,
            cancellationToken);
        return persistencia == EstadoPersistenciaContratoFranquia.Sucesso
            ? new(EstadoGerenciamentoContratoFranquia.Sucesso, versaoId)
            : MapearPersistencia<Guid>(persistencia);
    }

    public async Task<ResultadoOperacaoContratoFranquia> FormalizarVersaoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAtivoAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            cancellationToken);

        if (contexto.Valor is not { } contextoValor)
        {
            return new(contexto.Estado, contexto.Mensagem);
        }

        var contrato = await repositorio.ObterContratoParaAtualizacaoAsync(
            contextoValor.FranqueadoUnidadeId,
            contratoId,
            cancellationToken);
        var versoes = contrato is null
            ? []
            : await repositorio.ListarVersoesParaAtualizacaoAsync(contratoId, cancellationToken);
        var nova = versoes.SingleOrDefault(item => item.Id == versaoId);
        var vigente = versoes.SingleOrDefault(item =>
            item.Status == StatusVersaoContratoFranquia.Vigente);

        if (contrato is null || nova is null)
        {
            return new(EstadoGerenciamentoContratoFranquia.NaoEncontrado);
        }

        if (contrato.Status != StatusContratoFranquia.Ativo
            || nova.Status != StatusVersaoContratoFranquia.Rascunho
            || vigente is null
            || nova.NumeroVersao <= 1)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.EstadoInvalido,
                "Somente uma nova versão em rascunho de contrato ativo pode ser formalizada.");
        }

        if (!await repositorio.ExisteDocumentoAsync(
                nova.Id,
                DocumentoFormalizacao,
                cancellationToken))
        {
            return new(
                EstadoGerenciamentoContratoFranquia.DocumentoObrigatorio,
                "Adicione um documento do tipo Contrato ou Aditivo antes de formalizar.");
        }

        vigente.AlterarStatus(StatusVersaoContratoFranquia.Substituida);
        nova.AlterarStatus(StatusVersaoContratoFranquia.Vigente);
        var resultado = MapearPersistenciaOperacao(
            await repositorio.SalvarFormalizacaoAsync(
                vigente,
                nova,
                cancellationToken));
        if (resultado.Estado == EstadoGerenciamentoContratoFranquia.Sucesso)
        {
            logger.LogInformation("FormalizarVersão concluído para contrato {ContratoId}", contratoId);
        }
        return resultado;
    }

    public async Task<ResultadoOperacaoContratoFranquia> CancelarAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            cancellationToken);

        if (contexto.Valor is not { } contextoValor)
        {
            return new(contexto.Estado);
        }

        var contrato = await repositorio.ObterContratoParaAtualizacaoAsync(
            contextoValor.FranqueadoUnidadeId,
            contratoId,
            cancellationToken);
        var versoes = contrato is null
            ? []
            : await repositorio.ListarVersoesParaAtualizacaoAsync(contratoId, cancellationToken);
        var possuiVersaoBase = contrato?.Status switch
        {
            StatusContratoFranquia.Rascunho => versoes.Any(item =>
                item.Status == StatusVersaoContratoFranquia.Rascunho),
            StatusContratoFranquia.Ativo => versoes.Any(item =>
                item.Status == StatusVersaoContratoFranquia.Vigente),
            _ => false
        };

        if (contrato is null)
        {
            return new(EstadoGerenciamentoContratoFranquia.NaoEncontrado);
        }

        if (!possuiVersaoBase)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.EstadoInvalido,
                "Este contrato não pode ser cancelado no estado atual.");
        }

        var versoesParaCancelar = contrato.Status switch
        {
            StatusContratoFranquia.Rascunho => versoes.Where(item =>
                item.Status == StatusVersaoContratoFranquia.Rascunho).ToArray(),
            StatusContratoFranquia.Ativo => versoes.Where(item =>
                item.Status is StatusVersaoContratoFranquia.Rascunho
                    or StatusVersaoContratoFranquia.Vigente).ToArray(),
            _ => []
        };
        contrato.AlterarStatus(
            StatusContratoFranquia.Cancelado,
            timeProvider.GetUtcNow().UtcDateTime);

        foreach (var versao in versoesParaCancelar)
        {
            versao.AlterarStatus(StatusVersaoContratoFranquia.Cancelada);
        }

        var resultado = MapearPersistenciaOperacao(
            await repositorio.SalvarTransacaoAsync(cancellationToken));
        if (resultado.Estado == EstadoGerenciamentoContratoFranquia.Sucesso)
        {
            logger.LogInformation("CancelarContrato concluído para contrato {ContratoId}", contratoId);
        }
        return resultado;
    }

    public async Task<ResultadoOperacaoContratoFranquia> EncerrarAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            cancellationToken);

        if (contexto.Valor is not { } contextoValor)
        {
            return new(contexto.Estado);
        }

        var contrato = await repositorio.ObterContratoParaAtualizacaoAsync(
            contextoValor.FranqueadoUnidadeId,
            contratoId,
            cancellationToken);

        if (contrato is null)
        {
            return new(EstadoGerenciamentoContratoFranquia.NaoEncontrado);
        }

        if (contrato.Status != StatusContratoFranquia.Ativo)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.EstadoInvalido,
                "Somente um contrato ativo pode ser encerrado.");
        }

        contrato.AlterarStatus(
            StatusContratoFranquia.Encerrado,
            timeProvider.GetUtcNow().UtcDateTime);
        var resultado = MapearPersistenciaOperacao(
            await repositorio.SalvarTransacaoAsync(cancellationToken));
        if (resultado.Estado == EstadoGerenciamentoContratoFranquia.Sucesso)
        {
            logger.LogInformation("EncerrarContrato concluído para contrato {ContratoId}", contratoId);
        }
        return resultado;
    }

    private async Task<ResultadoContratoFranquia<EntidadesContrato>> ObterEntidadesAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        bool exigirVinculoAtivo,
        CancellationToken cancellationToken)
    {
        var contexto = exigirVinculoAtivo
            ? await ObterContextoAtivoAsync(
                usuarioAtualId,
                franqueadoId,
                unidadeId,
                cancellationToken)
            : await ObterContextoAsync(
                usuarioAtualId,
                franqueadoId,
                unidadeId,
                cancellationToken);

        if (contexto.Valor is not { } contextoValor)
        {
            return new(contexto.Estado, null, contexto.Mensagem);
        }

        var contrato = await repositorio.ObterContratoParaAtualizacaoAsync(
            contextoValor.FranqueadoUnidadeId,
            contratoId,
            cancellationToken);
        var versao = contrato is null
            ? null
            : await repositorio.ObterVersaoParaAtualizacaoAsync(
                contratoId,
                versaoId,
                cancellationToken);
        return contrato is null || versao is null
            ? new(EstadoGerenciamentoContratoFranquia.NaoEncontrado, null)
            : new(
                EstadoGerenciamentoContratoFranquia.Sucesso,
                new EntidadesContrato(contextoValor, contrato, versao));
    }

    private async Task<ResultadoContratoFranquia<ContextoContratoFranquia>> ObterContextoAtivoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(
            usuarioAtualId,
            franqueadoId,
            unidadeId,
            cancellationToken);
        return contexto.Valor is { VinculoAtivo: false }
            ? new(
                EstadoGerenciamentoContratoFranquia.VinculoInativo,
                null,
                "O vínculo comercial está inativo e não permite esta operação.")
            : contexto;
    }

    private async Task<ResultadoContratoFranquia<ContextoContratoFranquia>> ObterContextoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtualId == Guid.Empty)
        {
            return new(EstadoGerenciamentoContratoFranquia.SemAcesso, null);
        }

        var organizacoes = await acessoUsuarioConsulta
            .ListarOrganizacoesAdministradorRedeAsync(usuarioAtualId, cancellationToken);

        if (organizacoes.Count == 0)
        {
            return new(EstadoGerenciamentoContratoFranquia.SemAcesso, null);
        }

        if (organizacoes.Count > 1)
        {
            return new(
                EstadoGerenciamentoContratoFranquia.SelecaoOrganizacaoNecessaria,
                null);
        }

        var contexto = await repositorio.ObterContextoAsync(
            organizacoes[0],
            franqueadoId,
            unidadeId,
            cancellationToken);
        return contexto is null
            ? new(EstadoGerenciamentoContratoFranquia.NaoEncontrado, null)
            : new(EstadoGerenciamentoContratoFranquia.Sucesso, contexto);
    }

    private static ContratoFranquiaVersao CriarVersao(
        Guid versaoId,
        Guid contratoId,
        int numeroVersao,
        TermosContratoFranquiaSolicitacao solicitacao,
        Guid usuarioId,
        DateTime agoraUtc) => new(
            versaoId,
            contratoId,
            numeroVersao,
            solicitacao.DataInicio,
            solicitacao.DataFim,
            solicitacao.PercentualRoyalties,
            solicitacao.MensalidadeFixa,
            solicitacao.TaxaAdesao,
            solicitacao.DiaVencimento,
            StatusVersaoContratoFranquia.Rascunho,
            solicitacao.MotivoAlteracao,
            solicitacao.Observacoes,
            agoraUtc,
            usuarioId);

    private static ResultadoOperacaoContratoFranquia MapearPersistenciaOperacao(
        EstadoPersistenciaContratoFranquia estado) => estado switch
        {
            EstadoPersistenciaContratoFranquia.Sucesso => new(
                EstadoGerenciamentoContratoFranquia.Sucesso),
            EstadoPersistenciaContratoFranquia.ContratoAtivoExistente => new(
                EstadoGerenciamentoContratoFranquia.ContratoAtivoExistente,
                "Já existe um contrato ativo para esta unidade."),
            EstadoPersistenciaContratoFranquia.ConflitoVersao => new(
                EstadoGerenciamentoContratoFranquia.ConflitoVersao,
                "Outra versão foi criada simultaneamente. Atualize a página e tente novamente."),
            _ => new(
                EstadoGerenciamentoContratoFranquia.FalhaPersistencia,
                "Não foi possível concluir a operação. Nenhuma alteração foi salva.")
        };

    private static ResultadoContratoFranquia<T> MapearPersistencia<T>(
        EstadoPersistenciaContratoFranquia estado) => estado switch
        {
            EstadoPersistenciaContratoFranquia.ContratoAtivoExistente => new(
                EstadoGerenciamentoContratoFranquia.ContratoAtivoExistente,
                default,
                "Já existe um contrato ativo para esta unidade."),
            EstadoPersistenciaContratoFranquia.ConflitoVersao => new(
                EstadoGerenciamentoContratoFranquia.ConflitoVersao,
                default,
                "Outra versão foi criada simultaneamente. Atualize a página e tente novamente."),
            _ => new(
                EstadoGerenciamentoContratoFranquia.FalhaPersistencia,
                default,
                "Não foi possível concluir a operação. Nenhuma alteração foi salva.")
        };

    private static string FormatarTamanho(long tamanhoBytes) =>
        $"{tamanhoBytes / 1024m / 1024m:N0} MB";

    private sealed record EntidadesContrato(
        ContextoContratoFranquia Contexto,
        ContratoFranquia Contrato,
        ContratoFranquiaVersao Versao);
}

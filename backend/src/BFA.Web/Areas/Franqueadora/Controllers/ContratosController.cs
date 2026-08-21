using System.Globalization;
using BFA.Application.Acessos;
using BFA.Application.Franqueadora.Contratos;
using BFA.Domain.Contratos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Franqueadora;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Franqueadora.Controllers;

[Area("Franqueadora")]
[Authorize(Policy = PoliticasAcesso.AdministradorRede)]
[Route("franqueadora/franqueados/{franqueadoId:guid}/unidades/{unidadeId:guid}/contrato")]
public sealed class ContratosController(
    IUsuarioAtual usuarioAtual,
    IContratosFranquiaConsulta consulta,
    IContratosFranquiaServico servico,
    IConfiguration configuration,
    ILogger<ContratosController> logger) : Controller
{
    public const string MensagemSucesso = "ContratoMensagemSucesso";
    public const string MensagemErro = "ContratoMensagemErro";
    private readonly long _tamanhoMaximoDocumentoBytes = configuration.GetValue<long?>(
        "Armazenamento:Documentos:TamanhoMaximoBytes") ?? 20 * 1024 * 1024;

    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var resultado = await ObterPainelAsync(franqueadoId, unidadeId, cancellationToken);
        return resultado.Resultado ?? View("Index", MontarPainel(resultado.Valor!));
    }

    [HttpGet("novo")]
    public async Task<IActionResult> Novo(
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var painel = await ObterPainelAsync(franqueadoId, unidadeId, cancellationToken);

        if (painel.Resultado is not null)
        {
            return painel.Resultado;
        }

        if (!painel.Valor!.Contexto.VinculoAtivo)
        {
            return Conflict("O vínculo comercial está inativo.");
        }

        var model = new ContratoFranquiaFormViewModel
        {
            FranqueadoId = franqueadoId,
            FranqueadoNome = painel.Valor.Contexto.FranqueadoNome,
            UnidadeId = unidadeId,
            UnidadeNome = painel.Valor.Contexto.UnidadeNome,
            NumeroVersao = 1,
            DataInicio = DateOnly.FromDateTime(DateTime.Today),
            PercentualRoyalties = "0,00",
            MensalidadeFixa = "0,00"
        };
        return View("Formulario", model);
    }

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(
        Guid franqueadoId,
        Guid unidadeId,
        ContratoFranquiaFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.FranqueadoId = franqueadoId;
        model.UnidadeId = unidadeId;
        model.NumeroVersao = 1;

        if (!ModelState.IsValid || !TentarMontarSolicitacao(model, out var solicitacao))
        {
            return await ExibirFormularioAsync(model, cancellationToken);
        }

        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await servico.CriarAsync(
            usuarioId,
            franqueadoId,
            unidadeId,
            solicitacao!,
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoContratoFranquia.Sucesso)
        {
            TempData[MensagemSucesso] = "Contrato criado como rascunho.";
            return Redirect(RotaContrato(franqueadoId, unidadeId));
        }

        var resposta = MapearEstadoHttp(resultado.Estado);

        if (resposta is not null)
        {
            return resposta;
        }

        ModelState.AddModelError(string.Empty, resultado.Mensagem ?? "Não foi possível criar o contrato.");
        return await ExibirFormularioAsync(model, cancellationToken);
    }

    [HttpGet("{contratoId:guid}/versoes/{versaoId:guid}/editar")]
    public async Task<IActionResult> Editar(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        CancellationToken cancellationToken)
    {
        var resultado = await ObterFormularioEdicaoAsync(
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            cancellationToken);
        return resultado.Resultado ?? View("Formulario", resultado.Valor);
    }

    [HttpPost("{contratoId:guid}/versoes/{versaoId:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        ContratoFranquiaFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.FranqueadoId = franqueadoId;
        model.UnidadeId = unidadeId;
        model.ContratoId = contratoId;
        model.VersaoId = versaoId;

        if (!ModelState.IsValid || !TentarMontarSolicitacao(model, out var solicitacao))
        {
            return await ExibirFormularioAsync(model, cancellationToken);
        }

        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await servico.AtualizarRascunhoAsync(
            usuarioId,
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            solicitacao!,
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoContratoFranquia.Sucesso)
        {
            TempData[MensagemSucesso] = "Rascunho atualizado.";
            return Redirect(RotaContrato(franqueadoId, unidadeId));
        }

        var resposta = MapearEstadoHttp(resultado.Estado);

        if (resposta is not null)
        {
            return resposta;
        }

        ModelState.AddModelError(string.Empty, resultado.Mensagem ?? "Não foi possível atualizar o rascunho.");
        return await ExibirFormularioAsync(model, cancellationToken);
    }

    [HttpPost("{contratoId:guid}/versoes/{versaoId:guid}/documentos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarDocumento(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        UploadDocumentoContratoFranquiaViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid || model.Arquivo is null || model.TipoDocumento is null)
        {
            TempData[MensagemErro] = "Selecione o tipo e um arquivo PDF.";
            return Redirect(RotaContrato(franqueadoId, unidadeId));
        }

        await using var stream = model.Arquivo.OpenReadStream();
        var resultado = await servico.EnviarDocumentoAsync(
            usuarioId,
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            new EnviarDocumentoContratoFranquiaSolicitacao(
                model.TipoDocumento.Value,
                model.Arquivo.FileName,
                model.Arquivo.ContentType,
                stream),
            cancellationToken);
        return RedirecionarOperacao(
            franqueadoId,
            unidadeId,
            resultado,
            "Documento enviado com sucesso.");
    }

    [HttpPost("{contratoId:guid}/ativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ativar(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        CancellationToken cancellationToken) =>
        await ExecutarOperacaoAsync(
            franqueadoId,
            unidadeId,
            servico.AtivarAsync,
            contratoId,
            "Contrato ativado.",
            cancellationToken);

    [HttpPost("{contratoId:guid}/versoes/nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NovaVersao(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        NovaVersaoContratoFranquiaViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData[MensagemErro] = "Informe o motivo da alteração.";
            return Redirect(RotaContrato(franqueadoId, unidadeId));
        }

        var resultado = await servico.CriarNovaVersaoAsync(
            usuarioId,
            franqueadoId,
            unidadeId,
            contratoId,
            model.MotivoAlteracao,
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoContratoFranquia.Sucesso)
        {
            TempData[MensagemSucesso] = "Nova versão criada como rascunho.";
            return Redirect($"{RotaContrato(franqueadoId, unidadeId)}/{contratoId}/versoes/{resultado.Valor}/editar");
        }

        return RedirecionarOperacao(
            franqueadoId,
            unidadeId,
            new ResultadoOperacaoContratoFranquia(resultado.Estado, resultado.Mensagem),
            string.Empty);
    }

    [HttpPost("{contratoId:guid}/versoes/{versaoId:guid}/formalizar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Formalizar(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await servico.FormalizarVersaoAsync(
            usuarioId,
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            cancellationToken);
        return RedirecionarOperacao(
            franqueadoId,
            unidadeId,
            resultado,
            "Nova versão formalizada.");
    }

    [HttpPost("{contratoId:guid}/cancelar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        CancellationToken cancellationToken) =>
        await ExecutarOperacaoAsync(
            franqueadoId,
            unidadeId,
            servico.CancelarAsync,
            contratoId,
            "Contrato cancelado. O histórico foi preservado.",
            cancellationToken);

    [HttpPost("{contratoId:guid}/encerrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Encerrar(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        CancellationToken cancellationToken) =>
        await ExecutarOperacaoAsync(
            franqueadoId,
            unidadeId,
            servico.EncerrarAsync,
            contratoId,
            "Contrato encerrado.",
            cancellationToken);

    [HttpGet("{contratoId:guid}/versoes/{versaoId:guid}")]
    public async Task<IActionResult> Versao(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await consulta.ObterVersaoAsync(
            usuarioId,
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            cancellationToken);
        var resposta = MapearEstadoHttp(resultado.Estado);

        if (resposta is not null)
        {
            return resposta;
        }

        var painel = await ObterPainelAsync(franqueadoId, unidadeId, cancellationToken);
        return painel.Resultado ?? View("Versao", new ContratoVersaoDetalheViewModel
        {
            FranqueadoId = franqueadoId,
            FranqueadoNome = painel.Valor!.Contexto.FranqueadoNome,
            UnidadeId = unidadeId,
            UnidadeNome = painel.Valor.Contexto.UnidadeNome,
            ContratoId = contratoId,
            NumeroContrato = painel.Valor.Numero,
            Versao = MapearVersao(resultado.Valor!)
        });
    }

    [HttpGet("{contratoId:guid}/versoes/{versaoId:guid}/documentos/{documentoId:guid}/visualizar")]
    public Task<IActionResult> VisualizarDocumento(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        Guid documentoId,
        CancellationToken cancellationToken) => AbrirDocumentoAsync(
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            documentoId,
            baixar: false,
            cancellationToken);

    [HttpGet("{contratoId:guid}/versoes/{versaoId:guid}/documentos/{documentoId:guid}/baixar")]
    public Task<IActionResult> BaixarDocumento(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        Guid documentoId,
        CancellationToken cancellationToken) => AbrirDocumentoAsync(
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            documentoId,
            baixar: true,
            cancellationToken);

    private async Task<IActionResult> AbrirDocumentoAsync(
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        Guid documentoId,
        bool baixar,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await consulta.AbrirDocumentoAsync(
            usuarioId,
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            documentoId,
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoContratoFranquia.DocumentoIndisponivel)
        {
            logger.LogError(
                "Documento contratual {DocumentoId} indisponível no armazenamento para contrato {ContratoId}.",
                documentoId,
                contratoId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Documento indisponível no armazenamento.");
        }

        var resposta = MapearEstadoHttp(resultado.Estado);

        if (resposta is not null)
        {
            return resposta;
        }

        var documento = resultado.Valor!;
        return baixar
            ? File(
                documento.Conteudo,
                "application/pdf",
                SanitizarNomeDownload(documento.NomeOriginal),
                enableRangeProcessing: true)
            : File(documento.Conteudo, "application/pdf", enableRangeProcessing: true);
    }

    private async Task<IActionResult> ExecutarOperacaoAsync(
        Guid franqueadoId,
        Guid unidadeId,
        Func<Guid, Guid, Guid, Guid, CancellationToken, Task<ResultadoOperacaoContratoFranquia>> operacao,
        Guid contratoId,
        string mensagemSucesso,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await operacao(
            usuarioId,
            franqueadoId,
            unidadeId,
            contratoId,
            cancellationToken);
        return RedirecionarOperacao(franqueadoId, unidadeId, resultado, mensagemSucesso);
    }

    private IActionResult RedirecionarOperacao(
        Guid franqueadoId,
        Guid unidadeId,
        ResultadoOperacaoContratoFranquia resultado,
        string mensagemSucesso)
    {
        var resposta = MapearEstadoHttp(resultado.Estado);

        if (resposta is not null)
        {
            return resposta;
        }

        TempData[resultado.Estado == EstadoGerenciamentoContratoFranquia.Sucesso
            ? MensagemSucesso
            : MensagemErro] = resultado.Estado == EstadoGerenciamentoContratoFranquia.Sucesso
            ? mensagemSucesso
            : resultado.Mensagem ?? "Não foi possível concluir a operação.";
        return Redirect(RotaContrato(franqueadoId, unidadeId));
    }

    private async Task<IActionResult> ExibirFormularioAsync(
        ContratoFranquiaFormViewModel model,
        CancellationToken cancellationToken)
    {
        var painel = await ObterPainelAsync(model.FranqueadoId, model.UnidadeId, cancellationToken);

        if (painel.Resultado is not null)
        {
            return painel.Resultado;
        }

        model.FranqueadoNome = painel.Valor!.Contexto.FranqueadoNome;
        model.UnidadeNome = painel.Valor.Contexto.UnidadeNome;

        if (model.VersaoId is { } versaoId
            && painel.Valor.Versoes.SingleOrDefault(item => item.Id == versaoId) is { } versao)
        {
            model.NumeroVersao = versao.NumeroVersao;
        }

        return View("Formulario", model);
    }

    private async Task<(ContratoFranquiaFormViewModel? Valor, IActionResult? Resultado)>
        ObterFormularioEdicaoAsync(
            Guid franqueadoId,
            Guid unidadeId,
            Guid contratoId,
            Guid versaoId,
            CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return (null, Forbid());
        }

        var versao = await consulta.ObterVersaoAsync(
            usuarioId,
            franqueadoId,
            unidadeId,
            contratoId,
            versaoId,
            cancellationToken);
        var resposta = MapearEstadoHttp(versao.Estado);

        if (resposta is not null)
        {
            return (null, resposta);
        }

        if (versao.Valor!.Status != StatusVersaoContratoFranquia.Rascunho)
        {
            return (null, Conflict("Somente versões em rascunho podem ser editadas."));
        }

        var painel = await ObterPainelAsync(franqueadoId, unidadeId, cancellationToken);

        if (painel.Resultado is not null)
        {
            return (null, painel.Resultado);
        }

        var item = versao.Valor;
        return (new ContratoFranquiaFormViewModel
        {
            FranqueadoId = franqueadoId,
            FranqueadoNome = painel.Valor!.Contexto.FranqueadoNome,
            UnidadeId = unidadeId,
            UnidadeNome = painel.Valor.Contexto.UnidadeNome,
            ContratoId = contratoId,
            VersaoId = versaoId,
            NumeroVersao = item.NumeroVersao,
            NumeroContrato = painel.Valor.Numero,
            DataInicio = item.DataInicio,
            DataFim = item.DataFim,
            PercentualRoyalties = item.PercentualRoyalties.ToString("N2", CulturaPtBr),
            MensalidadeFixa = item.MensalidadeFixa.ToString("N2", CulturaPtBr),
            TaxaAdesao = item.TaxaAdesao?.ToString("N2", CulturaPtBr),
            DiaVencimento = item.DiaVencimento,
            MotivoAlteracao = item.MotivoAlteracao,
            Observacoes = item.Observacoes
        }, null);
    }

    private async Task<(ContratoFranquiaPainel? Valor, IActionResult? Resultado)> ObterPainelAsync(
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return (null, Forbid());
        }

        var resultado = await consulta.ObterAsync(
            usuarioId,
            franqueadoId,
            unidadeId,
            cancellationToken);
        return (resultado.Valor, MapearEstadoHttp(resultado.Estado));
    }

    private IActionResult? MapearEstadoHttp(EstadoGerenciamentoContratoFranquia estado) =>
        estado switch
        {
            EstadoGerenciamentoContratoFranquia.SemAcesso or
                EstadoGerenciamentoContratoFranquia.SelecaoOrganizacaoNecessaria => Forbid(),
            EstadoGerenciamentoContratoFranquia.NaoEncontrado => NotFound(),
            _ => null
        };

    private static bool TentarMontarSolicitacao(
        ContratoFranquiaFormViewModel model,
        out TermosContratoFranquiaSolicitacao? solicitacao)
    {
        if (model.DataInicio is not { } inicio
            || !ContratoFranquiaFormViewModel.TentarDecimal(
                model.PercentualRoyalties,
                out var royalties)
            || !ContratoFranquiaFormViewModel.TentarDecimal(
                model.MensalidadeFixa,
                out var mensalidade))
        {
            solicitacao = null;
            return false;
        }

        decimal? taxa = null;

        if (!string.IsNullOrWhiteSpace(model.TaxaAdesao))
        {
            if (!ContratoFranquiaFormViewModel.TentarDecimal(model.TaxaAdesao, out var taxaValor))
            {
                solicitacao = null;
                return false;
            }

            taxa = taxaValor;
        }

        solicitacao = new(
            model.NumeroContrato,
            inicio,
            model.DataFim,
            royalties,
            mensalidade,
            taxa,
            model.DiaVencimento,
            model.MotivoAlteracao,
            model.Observacoes);
        return true;
    }

    private ContratoFranquiaPainelViewModel MontarPainel(ContratoFranquiaPainel painel)
    {
        var versoes = painel.Versoes.Select(MapearVersao).ToArray();
        var versaoAtualId = painel.VersaoAtual?.Id;
        return new()
        {
            FranqueadoId = painel.Contexto.FranqueadoId,
            FranqueadoNome = painel.Contexto.FranqueadoNome,
            UnidadeId = painel.Contexto.UnidadeId,
            UnidadeNome = painel.Contexto.UnidadeNome,
            VinculoAtivo = painel.Contexto.VinculoAtivo,
            ContratoId = painel.ContratoId,
            Numero = painel.Numero,
            Status = painel.Status,
            TamanhoMaximoDocumentoBytes = _tamanhoMaximoDocumentoBytes,
            Versoes = versoes,
            VersaoAtual = versoes.SingleOrDefault(item => item.Id == versaoAtualId)
        };
    }

    private static VersaoContratoFranquiaViewModel MapearVersao(
        VersaoContratoFranquiaResumo versao) => new(
            versao.Id,
            versao.NumeroVersao,
            versao.DataInicio,
            versao.DataFim,
            versao.PercentualRoyalties,
            versao.MensalidadeFixa,
            versao.TaxaAdesao,
            versao.DiaVencimento,
            versao.Status,
            versao.MotivoAlteracao,
            versao.Observacoes,
            versao.CriadoEmUtc,
            versao.CriadoPor,
            versao.Documentos.Select(documento => new DocumentoContratoFranquiaViewModel(
                documento.Id,
                documento.TipoDocumento,
                documento.NomeOriginal,
                documento.TamanhoBytes,
                documento.CriadoEmUtc,
                documento.EnviadoPor)).ToArray());

    private static string SanitizarNomeDownload(string nomeOriginal)
    {
        var nome = Path.GetFileName(new string(nomeOriginal
            .Where(caractere => !char.IsControl(caractere))
            .ToArray())).Trim();
        return string.IsNullOrWhiteSpace(nome) ? "documento.pdf" : nome;
    }

    private static string RotaContrato(Guid franqueadoId, Guid unidadeId) =>
        $"/franqueadora/franqueados/{franqueadoId}/unidades/{unidadeId}/contrato";

    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");
}

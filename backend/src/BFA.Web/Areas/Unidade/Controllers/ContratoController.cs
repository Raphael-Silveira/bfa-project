using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Application.Unidades.Contratos;
using BFA.Domain.Acessos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[ServiceFilter(typeof(GovernancaOperacionalUnidadeResultFilter))]
[Route("unidade/{unidadeId:guid}/contrato")]
public sealed class ContratoController(
    IUsuarioAtual usuarioAtual,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IContratoUnidadeConsulta contratoUnidadeConsulta,
    IAuthorizationService authorizationService,
    ILogger<ContratoController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);

        if (acesso.Resultado is not null)
        {
            return acesso.Resultado;
        }

        var resultado = await contratoUnidadeConsulta.ObterAtivoAsync(
            acesso.UsuarioId,
            unidadeId,
            cancellationToken);

        if (resultado.Estado == EstadoConsultaContratoUnidade.SemAcesso)
        {
            return Forbid();
        }

        if (resultado.Valor is not { } painel)
        {
            return NotFound();
        }

        var unidadesAdministradas = await unidadesUsuarioConsulta.ListarAdministradasAsync(
            acesso.UsuarioId,
            cancellationToken);
        return View(new ContratoUnidadeDetalheViewModel
        {
            OrganizacaoId = painel.OrganizacaoId,
            UnidadeId = painel.UnidadeId,
            NomeUnidade = painel.UnidadeNome,
            PodeTrocarUnidade = unidadesAdministradas.Count > 1,
            Contrato = ContratoUnidadeViewModelMapper.Mapear(painel.Contrato)
        });
    }

    [HttpGet("documentos/{documentoId:guid}/visualizar")]
    public Task<IActionResult> VisualizarDocumento(
        Guid unidadeId,
        Guid documentoId,
        CancellationToken cancellationToken) => AbrirDocumentoAsync(
            unidadeId,
            documentoId,
            baixar: false,
            cancellationToken);

    [HttpGet("documentos/{documentoId:guid}/baixar")]
    public Task<IActionResult> BaixarDocumento(
        Guid unidadeId,
        Guid documentoId,
        CancellationToken cancellationToken) => AbrirDocumentoAsync(
            unidadeId,
            documentoId,
            baixar: true,
            cancellationToken);

    private async Task<IActionResult> AbrirDocumentoAsync(
        Guid unidadeId,
        Guid documentoId,
        bool baixar,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);

        if (acesso.Resultado is not null)
        {
            return acesso.Resultado;
        }

        var resultado = await contratoUnidadeConsulta.AbrirDocumentoAsync(
            acesso.UsuarioId,
            unidadeId,
            documentoId,
            cancellationToken);

        if (resultado.Estado == EstadoConsultaContratoUnidade.SemAcesso)
        {
            return Forbid();
        }

        if (resultado.Estado == EstadoConsultaContratoUnidade.NaoEncontrado)
        {
            return NotFound();
        }

        if (resultado.Estado == EstadoConsultaContratoUnidade.DocumentoIndisponivel)
        {
            logger.LogError(
                "Documento contratual {DocumentoId} indisponível para a Unidade {UnidadeId}.",
                documentoId,
                unidadeId);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Documento indisponível no armazenamento.");
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

    private async Task<(Guid UsuarioId, IActionResult? Resultado)> ValidarAcessoAsync(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return (Guid.Empty, Forbid());
        }

        var unidade = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId,
            cancellationToken);

        if (unidade is null)
        {
            return (Guid.Empty, NotFound());
        }

        var autorizacao = await authorizationService.AuthorizeAsync(
            User,
            new ContextoUnidade(unidade.OrganizacaoId, unidade.UnidadeId),
            new AcessoUnidadePorPerfilRequirement(PerfilAcesso.AdministradorUnidade));
        return autorizacao.Succeeded
            ? (usuarioId, null)
            : (Guid.Empty, Forbid());
    }

    private static string SanitizarNomeDownload(string nomeOriginal)
    {
        var nome = Path.GetFileName(new string(nomeOriginal
            .Where(caractere => !char.IsControl(caractere))
            .ToArray())).Trim();
        return string.IsNullOrWhiteSpace(nome) ? "documento.pdf" : nome;
    }
}

using BFA.Application.Acessos;
using BFA.Application.Cobrancas;
using BFA.Application.Unidades;
using BFA.Domain.Cobrancas;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[ServiceFilter(typeof(GovernancaOperacionalUnidadeResultFilter))]
[Route("unidade/{unidadeId:guid}/cobrancas")]
public sealed class CobrancasController(
    IUsuarioAtual usuarioAtual,
    ICobrancasServico cobrancasServico,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    ILogger<CobrancasController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        [FromQuery] Guid? alunoId,
        [FromQuery] string? status,
        [FromQuery] string? tipo,
        [FromQuery] DateOnly? dataVencimentoInicio,
        [FromQuery] DateOnly? dataVencimentoFim,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var filtro = new FiltroCobrancas(
            alunoId,
            ParseStatus(status),
            ParseTipo(tipo),
            dataVencimentoInicio,
            dataVencimentoFim);

        var (estado, itens) = await cobrancasServico.ListarAsync(
            usuarioId, unidadeId, filtro);

        if (estado == EstadoCobrancas.UnidadeNaoEncontrada)
            return NotFound();
        if (estado != EstadoCobrancas.Sucesso)
            return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        return View(CobrancaViewModelMapper.MapearLista(
            contexto, itens, alunoId, status, tipo, dataVencimentoInicio, dataVencimentoFim));
    }

    [HttpGet("nova")]
    public async Task<IActionResult> Nova(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        var (estado, alunos) = await cobrancasServico.ListarAlunosAsync(usuarioId, unidadeId);
        if (estado != EstadoCobrancas.Sucesso) return Forbid();

        return View(CobrancaViewModelMapper.MapearFormularioCriacao(contexto, alunos));
    }

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nova(
        Guid unidadeId,
        CobrancaFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        if (!ModelState.IsValid)
        {
            var (estadoAlunos, alunos) = await cobrancasServico.ListarAlunosAsync(usuarioId, unidadeId);
            if (estadoAlunos != EstadoCobrancas.Sucesso) return Forbid();

            return View(CobrancaViewModelMapper.ReconstituirFormularioCriacao(contexto, model, alunos));
        }

        if (model.AlunoId is not { } alunoId
            || model.Tipo is not { } tipoStr
            || model.Valor is not { } valor
            || model.DataVencimento is not { } dataVencimento
            || string.IsNullOrWhiteSpace(model.Descricao))
        {
            return BadRequest();
        }

        var alunoSelecionado = model.Alunos.FirstOrDefault(a => a.AlunoId == alunoId);
        if (alunoSelecionado is null) return BadRequest();

        var solicitacao = new CriarCobrancaSolicitacao(
            alunoId,
            alunoSelecionado.MatriculaId,
            ParseTipo(tipoStr)!.Value,
            model.Descricao,
            valor,
            dataVencimento,
            model.Observacoes);

        var (estado, item) = await cobrancasServico.CriarAsync(usuarioId, unidadeId, solicitacao);

        if (estado == EstadoCobrancas.DadosInvalidos)
        {
            ModelState.AddModelError(string.Empty, "Dados invalidos para criacao da cobranca.");
            var (estadoAlunos, alunos) = await cobrancasServico.ListarAlunosAsync(usuarioId, unidadeId);
            if (estadoAlunos != EstadoCobrancas.Sucesso) return Forbid();
            return View(CobrancaViewModelMapper.ReconstituirFormularioCriacao(contexto, model, alunos));
        }

        if (estado != EstadoCobrancas.Sucesso || item is null)
            return Forbid();

        return RedirectToAction(nameof(Detalhes), new { unidadeId, cobrancaId = item.CobrancaId });
    }

    [HttpGet("detalhes/{cobrancaId:guid}")]
    public async Task<IActionResult> Detalhes(
        Guid unidadeId,
        Guid cobrancaId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var (estado, detalhe) = await cobrancasServico.ObterAsync(usuarioId, unidadeId, cobrancaId);

        if (estado == EstadoCobrancas.UnidadeNaoEncontrada)
            return NotFound();
        if (estado == EstadoCobrancas.CobrancaNaoEncontrada)
            return NotFound();
        if (estado != EstadoCobrancas.Sucesso || detalhe is null)
            return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        return View(CobrancaViewModelMapper.MapearDetalhe(contexto, detalhe));
    }

    [HttpPost("cancelar/{cobrancaId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(
        Guid unidadeId,
        Guid cobrancaId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var estado = await cobrancasServico.CancelarAsync(usuarioId, unidadeId, cobrancaId);

        if (estado == EstadoCobrancas.UnidadeNaoEncontrada)
            return NotFound();
        if (estado == EstadoCobrancas.CobrancaNaoEncontrada)
            return NotFound();
        if (estado != EstadoCobrancas.Sucesso)
            return Forbid();

        return RedirectToAction(nameof(Detalhes), new { unidadeId, cobrancaId });
    }

    [HttpPost("pagamento/{cobrancaId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPagamento(
        Guid unidadeId,
        Guid cobrancaId,
        PagamentoFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Detalhes), new { unidadeId, cobrancaId });

        if (model.Valor is not { } valor
            || model.DataPagamento is not { } dataPagamento
            || model.FormaPagamento is not { } formaStr
            || ParseFormaPagamento(formaStr) is not { } forma)
        {
            return BadRequest();
        }

        var solicitacao = new RegistrarPagamentoSolicitacao(
            valor,
            dataPagamento,
            forma,
            model.Observacoes);

        var (estado, pagamento) = await cobrancasServico.RegistrarPagamentoAsync(
            usuarioId, unidadeId, cobrancaId, solicitacao);

        if (estado == EstadoCobrancas.CobrancaNaoPendente)
        {
            TempData["Erro"] = "Esta cobranca nao pode receber pagamentos.";
        }
        else if (estado == EstadoCobrancas.ValorExcedeSaldo)
        {
            TempData["Erro"] = "O valor informado excede o saldo devedor.";
        }

        return RedirectToAction(nameof(Detalhes), new { unidadeId, cobrancaId });
    }

    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var (estado, resumo) = await cobrancasServico.ObterResumoFinanceiroAsync(usuarioId, unidadeId);

        if (estado == EstadoCobrancas.UnidadeNaoEncontrada)
            return NotFound();
        if (estado != EstadoCobrancas.Sucesso || resumo is null)
            return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        return View(CobrancaViewModelMapper.MapearResumoFinanceiro(contexto, resumo));
    }

    private async Task<UnidadeAcessoResumo?> ObterContextoAsync(
        Guid usuarioId, Guid unidadeId, CancellationToken cancellationToken)
    {
        var unidade = await unidadesUsuarioConsulta.ObterAdministradaAsync(
            usuarioId, unidadeId, cancellationToken);

        return unidade;
    }

    private static StatusCobranca? ParseStatus(string? valor) => valor switch
    {
        "Pendente" => StatusCobranca.Pendente,
        "Paga" => StatusCobranca.Paga,
        "Atrasada" => StatusCobranca.Atrasada,
        "Cancelada" => StatusCobranca.Cancelada,
        _ => null
    };

    private static TipoCobranca? ParseTipo(string? valor) => valor switch
    {
        "Matricula" => TipoCobranca.Matricula,
        "Mensalidade" => TipoCobranca.Mensalidade,
        "Avulso" => TipoCobranca.Avulso,
        _ => null
    };

    private static FormaPagamento? ParseFormaPagamento(string? valor) => valor switch
    {
        "Dinheiro" => FormaPagamento.Dinheiro,
        "Pix" => FormaPagamento.Pix,
        "CartaoCredito" => FormaPagamento.CartaoCredito,
        "CartaoDebito" => FormaPagamento.CartaoDebito,
        "Boleto" => FormaPagamento.Boleto,
        "Transferencia" => FormaPagamento.Transferencia,
        "Outros" => FormaPagamento.Outros,
        _ => null
    };
}

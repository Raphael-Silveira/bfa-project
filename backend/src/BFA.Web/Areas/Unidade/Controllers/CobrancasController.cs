using BFA.Application.Acessos;
using BFA.Application.Cobrancas;
using BFA.Application.Unidades;
using BFA.Domain.Cobrancas;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Globalization;

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
        [FromQuery] string? alunoNome,
        [FromQuery] string? status,
        [FromQuery] string? tipo,
        [FromQuery] string? dataInicio,
        [FromQuery] string? dataFim,
        [FromQuery] int? pagina,
        [FromQuery] int? tamanhoPagina,
        CancellationToken cancellationToken)
    {
        var usuario = ObterUsuarioOuForbid();
        if (usuario.Resultado is not null) return usuario.Resultado;

        var (inicio, fim) = ObterPeriodoConsulta(dataInicio, dataFim);

        var filtro = new FiltroCobrancas(
            alunoId,
            alunoNome,
            ParseStatus(status),
            ParseTipo(tipo),
            inicio,
            fim);

        var (estado, itens) = await cobrancasServico.ListarAsync(
            usuario.UsuarioId, unidadeId, filtro);

        if (estado == EstadoCobrancas.UnidadeNaoEncontrada)
            return NotFound();
        if (estado != EstadoCobrancas.Sucesso)
            return Forbid();

        var contexto = await ObterContextoOuForbidAsync(usuario.UsuarioId, unidadeId, cancellationToken);
        if (contexto.Resultado is not null) return contexto.Resultado;

        var grupos = itens
            .GroupBy(i => i.AlunoId)
            .Select(g => CobrancaViewModelMapper.MapearGrupo(
                g.Key,
                g.First().AlunoNome,
                g.OrderByDescending(i => i.DataVencimento).ToList()))
            .OrderByDescending(g => g.DataVencimento)
            .ToList();

        var totalItens = grupos.Count;
        var (paginaAtual, tamanho) = ObterPaginacao(pagina, tamanhoPagina, totalItens);

        var gruposPagina = grupos
            .Skip((paginaAtual - 1) * tamanho)
            .Take(tamanho)
            .ToList();

        return View(CobrancaViewModelMapper.MapearListaAgrupada(
            contexto.Valor!, gruposPagina, alunoId, alunoNome, status, tipo,
            inicio, fim,
            paginaAtual, tamanho, totalItens));
    }

    [HttpGet("nova")]
    public async Task<IActionResult> Nova(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var usuario = ObterUsuarioOuForbid();
        if (usuario.Resultado is not null) return usuario.Resultado;

        var contexto = await ObterContextoOuForbidAsync(usuario.UsuarioId, unidadeId, cancellationToken);
        if (contexto.Resultado is not null) return contexto.Resultado;

        var (estado, alunos) = await cobrancasServico.ListarAlunosAsync(usuario.UsuarioId, unidadeId);
        if (estado != EstadoCobrancas.Sucesso) return Forbid();

        return View(CobrancaViewModelMapper.MapearFormularioCriacao(contexto.Valor!, alunos));
    }

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nova(
        Guid unidadeId,
        CobrancaFormViewModel model,
        CancellationToken cancellationToken)
    {
        var usuario = ObterUsuarioOuForbid();
        if (usuario.Resultado is not null) return usuario.Resultado;

        var contexto = await ObterContextoOuForbidAsync(usuario.UsuarioId, unidadeId, cancellationToken);
        if (contexto.Resultado is not null) return contexto.Resultado;

        if (!ModelState.IsValid)
        {
            var (estadoAlunos, alunos) = await cobrancasServico.ListarAlunosAsync(usuario.UsuarioId, unidadeId);
            if (estadoAlunos != EstadoCobrancas.Sucesso) return Forbid();

            return View(CobrancaViewModelMapper.ReconstituirFormularioCriacao(contexto.Valor!, model, alunos));
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

        var (estado, item) = await cobrancasServico.CriarAsync(usuario.UsuarioId, unidadeId, solicitacao);

        if (estado == EstadoCobrancas.DadosInvalidos)
        {
            ModelState.AddModelError(string.Empty, "Dados invalidos para criacao da cobranca.");
            var (estadoAlunos, alunos) = await cobrancasServico.ListarAlunosAsync(usuario.UsuarioId, unidadeId);
            if (estadoAlunos != EstadoCobrancas.Sucesso) return Forbid();
            return View(CobrancaViewModelMapper.ReconstituirFormularioCriacao(contexto.Valor!, model, alunos));
        }

        if (estado != EstadoCobrancas.Sucesso || item is null)
            return Forbid();

        return RedirectToAction(nameof(DetalhesAluno), new { unidadeId, alunoId = item.AlunoId });
    }

    [HttpGet("detalhes/aluno/{alunoId:guid}")]
    public async Task<IActionResult> DetalhesAluno(
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        return RedirectToAction(nameof(Index), new { unidadeId, alunoId });
    }

    [HttpPost("cancelar/{cobrancaId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(
        Guid unidadeId,
        Guid alunoId,
        Guid cobrancaId,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var usuario = ObterUsuarioOuForbid();
        if (usuario.Resultado is not null) return usuario.Resultado;

        var estado = await cobrancasServico.CancelarAsync(usuario.UsuarioId, unidadeId, cobrancaId);

        if (estado == EstadoCobrancas.UnidadeNaoEncontrada)
            return NotFound();
        if (estado == EstadoCobrancas.CobrancaNaoEncontrada)
            return NotFound();
        if (estado != EstadoCobrancas.Sucesso)
            return Forbid();

        return RedirecionarParaOrigemOuIndex(returnUrl, unidadeId, alunoId);
    }

    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var usuario = ObterUsuarioOuForbid();
        if (usuario.Resultado is not null) return usuario.Resultado;

        var (estado, resumo) = await cobrancasServico.ObterResumoFinanceiroAsync(usuario.UsuarioId, unidadeId);

        if (estado == EstadoCobrancas.UnidadeNaoEncontrada)
            return NotFound();
        if (estado != EstadoCobrancas.Sucesso || resumo is null)
            return Forbid();

        var contexto = await ObterContextoOuForbidAsync(usuario.UsuarioId, unidadeId, cancellationToken);
        if (contexto.Resultado is not null) return contexto.Resultado;

        return View(CobrancaViewModelMapper.MapearResumoFinanceiro(contexto.Valor!, resumo));
    }

    [HttpPost("pagamento-consolidado/{alunoId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPagamentoConsolidado(
        Guid unidadeId,
        Guid alunoId,
        IReadOnlyList<Guid> cobrancaIds,
        DateOnly dataPagamento,
        string formaPagamento,
        string? observacoes,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var usuario = ObterUsuarioOuForbid();
        if (usuario.Resultado is not null) return usuario.Resultado;

        if (cobrancaIds == null || cobrancaIds.Count == 0)
            return BadRequest("Nenhuma cobrança selecionada.");

        if (dataPagamento == default)
            return BadRequest("Data de pagamento inválida.");

        if (ParseFormaPagamento(formaPagamento) is not { } forma)
            return BadRequest("Forma de pagamento inválida.");

        var solicitacao = new RegistrarPagamentoConsolidadoSolicitacao(
            cobrancaIds,
            dataPagamento,
            forma,
            observacoes);

        var (estado, _) = await cobrancasServico.RegistrarPagamentoConsolidadoAsync(
            usuario.UsuarioId, unidadeId, solicitacao);

        if (estado == EstadoCobrancas.Falha)
            TempData["Erro"] = "Não foi possível registrar o pagamento consolidado. Verifique se todas as cobranças estão pendentes e o valor não excede o saldo.";
        else if (estado == EstadoCobrancas.DadosInvalidos)
            TempData["Erro"] = "Dados inválidos para pagamento consolidado.";
        else if (estado == EstadoCobrancas.Sucesso)
            TempData["Sucesso"] = "Pagamento consolidado registrado com sucesso.";

        return RedirecionarParaOrigemOuIndex(returnUrl, unidadeId, alunoId);
    }

    private async Task<(UnidadeAcessoResumo? Valor, IActionResult? Resultado)> ObterContextoOuForbidAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var unidade = await unidadesUsuarioConsulta.ObterAdministradaAsync(
            usuarioId, unidadeId, cancellationToken);

        return unidade is null ? (null, Forbid()) : (unidade, null);
    }

    private static (DateOnly Inicio, DateOnly Fim) ObterPeriodoConsulta(
        string? dataInicio,
        string? dataFim)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var styles = DateTimeStyles.None;

        var inicio = DateOnly.TryParseExact(dataInicio, "dd/MM/yyyy", culture, styles, out var d1)
            ? d1 : hoje;
        var fim = DateOnly.TryParseExact(dataFim, "dd/MM/yyyy", culture, styles, out var d2)
            ? d2 : inicio;

        return (inicio, fim);
    }

    private static (int PaginaAtual, int TamanhoPagina) ObterPaginacao(
        int? pagina,
        int? tamanhoPagina,
        int totalItens)
    {
        var tamanho = Math.Clamp(tamanhoPagina ?? 10, 5, 50);
        var paginaAtual = Math.Max(1, pagina ?? 1);
        var totalPaginas = (int)Math.Ceiling((double)totalItens / tamanho);

        if (paginaAtual > totalPaginas && totalPaginas > 0)
        {
            paginaAtual = totalPaginas;
        }

        return (paginaAtual, tamanho);
    }

    private (Guid UsuarioId, IActionResult? Resultado) ObterUsuarioOuForbid() =>
        usuarioAtual.UsuarioId is { } usuarioId
            ? (usuarioId, null)
            : (Guid.Empty, Forbid());

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

    private IActionResult RedirecionarParaOrigemOuIndex(string? returnUrl, Guid unidadeId, Guid alunoId)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index), new { unidadeId, alunoId });
    }
}

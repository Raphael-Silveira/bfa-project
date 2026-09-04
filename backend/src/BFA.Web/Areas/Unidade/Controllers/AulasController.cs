using BFA.Application.Acessos;
using BFA.Application.Aulas;
using BFA.Application.Unidades;
using BFA.Domain.Aulas;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[ServiceFilter(typeof(GovernancaOperacionalUnidadeResultFilter))]
[Route("unidade/{unidadeId:guid}/aulas")]
public sealed class AulasController(
    IUsuarioAtual usuarioAtual,
    IAulasServico aulasServico,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    ILogger<AulasController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        DateOnly? dataInicio,
        DateOnly? dataFim,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicio = dataInicio ?? hoje.AddDays(-(int)hoje.DayOfWeek + 1);
        var fim = dataFim ?? inicio.AddDays(6);

        var resultado = await aulasServico.ListarAsync(
            usuarioId, unidadeId, inicio, fim, cancellationToken);

        if (resultado.Estado == EstadoAulasUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoAulasUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        return View(AulasViewModelMapper.MapearLista(
            resultado.Contexto,
            resultado.Valor,
            inicio,
            fim));
    }

    [HttpGet("nova")]
    public async Task<IActionResult> Nova(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        return View(AulasViewModelMapper.MapearFormularioCriacao(contexto));
    }

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nova(
        Guid unidadeId,
        AulaFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var contexto = await ObterContextoAsync(usuarioId, unidadeId, cancellationToken);
        if (contexto is null) return Forbid();

        if (!ModelState.IsValid)
        {
            return View(AulasViewModelMapper.ReconstituirFormularioCriacao(contexto, model));
        }

        var solicitacao = new CriarAulaSolicitacao(
            model.TurmaHorarioId!.Value,
            model.Data!.Value,
            model.HoraInicio!.Value,
            model.HoraFim!.Value,
            model.Observacoes);

        var resultado = await aulasServico.CriarAsync(
            usuarioId, unidadeId, solicitacao, cancellationToken);

        if (resultado.Estado == EstadoAulasUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAulasUnidade.DadosInvalidos
            || resultado.Estado == EstadoAulasUnidade.ConflitoHorario)
        {
            ModelState.AddModelError(string.Empty, resultado.Estado switch
            {
                EstadoAulasUnidade.ConflitoHorario =>
                    "Ja existe uma aula programada neste horario para a turma.",
                _ => "Dados invalidos. Verifique os campos informados."
            });
            return View(AulasViewModelMapper.ReconstituirFormularioCriacao(contexto, model));
        }
        if (resultado.Estado != EstadoAulasUnidade.Sucesso)
            return Forbid();

        return RedirectToAction(nameof(Detalhes), new { unidadeId, aulaId = resultado.Valor });
    }

    [HttpGet("{aulaId:guid}")]
    public async Task<IActionResult> Detalhes(
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var resultado = await aulasServico.ObterAsync(
            usuarioId, unidadeId, aulaId, cancellationToken);

        if (resultado.Estado == EstadoAulasUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAulasUnidade.AulaNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoAulasUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        return View(AulasViewModelMapper.MapearDetalhe(
            resultado.Contexto,
            resultado.Valor));
    }

    [HttpGet("{aulaId:guid}/editar")]
    public async Task<IActionResult> Editar(
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var resultado = await aulasServico.ObterAsync(
            usuarioId, unidadeId, aulaId, cancellationToken);

        if (resultado.Estado == EstadoAulasUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAulasUnidade.AulaNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoAulasUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        return View(AulasViewModelMapper.MapearFormularioEdicao(
            resultado.Contexto,
            resultado.Valor));
    }

    [HttpPost("{aulaId:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        Guid unidadeId,
        Guid aulaId,
        AulaEdicaoFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var solicitacao = new AtualizarAulaSolicitacao(
            model.Status,
            model.Observacoes);

        var resultado = await aulasServico.AtualizarAsync(
            usuarioId, unidadeId, aulaId, solicitacao, cancellationToken);

        if (resultado.Estado == EstadoAulasUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAulasUnidade.AulaNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoAulasUnidade.Sucesso)
            return Forbid();

        return RedirectToAction(nameof(Detalhes), new { unidadeId, aulaId });
    }

    [HttpPost("{aulaId:guid}/concluir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Concluir(
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var resultado = await aulasServico.ConcluirAsync(
            usuarioId, unidadeId, aulaId, cancellationToken);

        if (resultado.Estado == EstadoAulasUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAulasUnidade.AulaNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoAulasUnidade.Sucesso)
            return Forbid();

        return RedirectToAction(nameof(Detalhes), new { unidadeId, aulaId });
    }

    [HttpPost("{aulaId:guid}/cancelar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var resultado = await aulasServico.CancelarAsync(
            usuarioId, unidadeId, aulaId, cancellationToken);

        if (resultado.Estado == EstadoAulasUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAulasUnidade.AulaNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoAulasUnidade.Sucesso)
            return Forbid();

        return RedirectToAction(nameof(Detalhes), new { unidadeId, aulaId });
    }

    [HttpGet("{aulaId:guid}/chamada")]
    public async Task<IActionResult> Chamada(
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var detalhe = await aulasServico.ObterAsync(
            usuarioId, unidadeId, aulaId, cancellationToken);

        if (detalhe.Estado == EstadoAulasUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (detalhe.Estado == EstadoAulasUnidade.AulaNaoEncontrada)
            return NotFound();
        if (detalhe.Estado != EstadoAulasUnidade.Sucesso
            || detalhe.Valor is null
            || detalhe.Contexto is null)
            return Forbid();

        var alunos = await aulasServico.ListarAlunosParaChamadaAsync(
            usuarioId, unidadeId, aulaId, cancellationToken);

        if (alunos.Estado != EstadoAulasUnidade.Sucesso || alunos.Valor is null)
            return Forbid();

        return View(AulasViewModelMapper.MapearChamada(
            detalhe.Contexto,
            detalhe.Valor,
            alunos.Valor));
    }

    [HttpPost("{aulaId:guid}/chamada")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Chamada(
        Guid unidadeId,
        Guid aulaId,
        ChamadaFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        if (model.Registros is null || model.Registros.Count == 0)
            return RedirectToAction(nameof(Chamada), new { unidadeId, aulaId });

        var registros = model.Registros
            .Where(r => r.AlunoId.HasValue)
            .Select(r => new RegistroPresencaLoteItem(
                r.AlunoId!.Value,
                r.Status,
                null, null,
                null))
            .ToList();

        var resultado = await aulasServico.RegistrarPresencasEmLoteAsync(
            usuarioId, unidadeId, aulaId, registros, cancellationToken);

        if (resultado.Estado != EstadoAulasUnidade.Sucesso)
            return Forbid();

        return RedirectToAction(nameof(Detalhes), new { unidadeId, aulaId });
    }

    [HttpGet("frequencia")]
    public async Task<IActionResult> Frequencia(
        Guid unidadeId,
        Guid? turmaId,
        DateOnly? dataInicio,
        DateOnly? dataFim,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicio = dataInicio ?? hoje.AddDays(-30);
        var fim = dataFim ?? hoje;

        var resultado = await aulasServico.ObterFrequenciaAsync(
            usuarioId, unidadeId, turmaId, inicio, fim, cancellationToken);

        if (resultado.Estado == EstadoAulasUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoAulasUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        return View(AulasViewModelMapper.MapearFrequencia(
            resultado.Contexto,
            resultado.Valor,
            turmaId,
            inicio,
            fim));
    }

    private async Task<ContextoAulasResumo?> ObterContextoAsync(
        Guid usuarioId, Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var resultado = await aulasServico.ListarAsync(
            usuarioId, unidadeId,
            DateOnly.FromDateTime(DateTime.Today),
            DateOnly.FromDateTime(DateTime.Today),
            cancellationToken);
        return resultado.Contexto;
    }
}

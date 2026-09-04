using BFA.Application.Alunos;
using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[ServiceFilter(typeof(GovernancaOperacionalUnidadeResultFilter))]
[Route("unidade/{unidadeId:guid}/alunos")]
public sealed class AlunosController(
    IUsuarioAtual usuarioAtual,
    IAlunosServico alunosServico,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    ILogger<AlunosController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        string? texto,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var textoNormalizado = NormalizarTexto(texto);
        var resultado = await alunosServico.ListarAsync(
            usuarioId, unidadeId, textoNormalizado, cancellationToken);

        if (resultado.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoAlunosUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        return View(AlunosViewModelMapper.MapearLista(
            resultado.Contexto,
            resultado.Valor,
            textoNormalizado,
            await PodeTrocarAsync(usuarioId, cancellationToken)));
    }

    [HttpGet("{alunoId:guid}")]
    public async Task<IActionResult> Detalhes(
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var resultado = await alunosServico.ObterAsync(
            usuarioId, unidadeId, alunoId, cancellationToken);

        if (resultado.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAlunosUnidade.AlunoNaoEncontrado)
            return NotFound();
        if (resultado.Estado != EstadoAlunosUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        return View(AlunosViewModelMapper.MapearDetalhe(
            resultado.Contexto,
            resultado.Valor,
            await PodeTrocarAsync(usuarioId, cancellationToken)));
    }

    [HttpGet("{alunoId:guid}/editar")]
    public async Task<IActionResult> Editar(
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var resultado = await alunosServico.ObterDadosEdicaoAsync(
            usuarioId, unidadeId, alunoId, cancellationToken);

        if (resultado.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAlunosUnidade.AlunoNaoEncontrado)
            return NotFound();
        if (resultado.Estado == EstadoAlunosUnidade.SemAcesso)
            return Forbid();
        if (resultado.Estado != EstadoAlunosUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        return View(EditarAlunoMapper.Mapear(
            resultado.Contexto,
            resultado.Valor.Aluno,
            await PodeTrocarAsync(usuarioId, cancellationToken)));
    }

    [HttpPost("{alunoId:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        Guid unidadeId,
        Guid alunoId,
        EditarAlunoViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            logger.LogWarning("{Controller} {Action} negado para {UsuarioId}", "Alunos", "Editar", (object?)null);
            return Forbid();
        }

        var dadosExistentes = await alunosServico.ObterDadosEdicaoAsync(
            usuarioId, unidadeId, alunoId, cancellationToken);
        if (dadosExistentes.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (dadosExistentes.Estado == EstadoAlunosUnidade.AlunoNaoEncontrado)
            return NotFound();
        if (dadosExistentes.Estado == EstadoAlunosUnidade.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Alunos", "Editar", dadosExistentes.Estado);
            return Forbid();
        }
        if (dadosExistentes.Estado != EstadoAlunosUnidade.Sucesso
            || dadosExistentes.Valor is null
            || dadosExistentes.Contexto is null)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Alunos", "Editar", dadosExistentes.Estado);
            return Forbid();
        }

        var contexto = dadosExistentes.Contexto;
        var dadosAluno = dadosExistentes.Valor.Aluno;
        model = EditarAlunoMapper.Mapear(contexto, dadosAluno,
            await PodeTrocarAsync(usuarioId, cancellationToken));
        model.NomeCompleto = Request.Form["NomeCompleto"].FirstOrDefault()
            ?? model.NomeCompleto;

        if (DateOnly.TryParse(Request.Form["DataNascimento"].FirstOrDefault(), out var dataNasc))
            model.DataNascimento = dataNasc;
        else
            model.DataNascimento = null;

        model.Telefone = Request.Form["Telefone"].FirstOrDefault();
        model.Email = Request.Form["Email"].FirstOrDefault();

        if (!ModelState.IsValid)
            return View(model);

        if (model.DataNascimento is not { } dataNascimento)
        {
            ModelState.AddModelError(nameof(model.DataNascimento),
                "A data de nascimento deve ser informada.");
            return View(model);
        }

        var resultado = await alunosServico.AtualizarDadosAsync(
            usuarioId,
            unidadeId,
            alunoId,
            model.NomeCompleto,
            dataNascimento,
            model.Telefone,
            model.Email,
            cancellationToken);

        if (resultado.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAlunosUnidade.AlunoNaoEncontrado)
            return NotFound();
        if (resultado.Estado == EstadoAlunosUnidade.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Alunos", "Editar", resultado.Estado);
            return Forbid();
        }
        if (resultado.Estado == EstadoAlunosUnidade.DadosInvalidos)
        {
            ModelState.AddModelError(string.Empty,
                "Revise os dados informados.");
            return View(model);
        }
        if (resultado.Estado == EstadoAlunosUnidade.MenorSemResponsavel)
        {
            ModelState.AddModelError(nameof(model.DataNascimento),
                "Para alterar a data de nascimento, cadastre primeiro um responsável ativo para este aluno.");
            return View(model);
        }
        if (resultado.Estado != EstadoAlunosUnidade.Sucesso)
        {
            ModelState.AddModelError(string.Empty,
                "Não foi possível atualizar os dados do aluno. Tente novamente.");
            return View(model);
        }

        TempData["Sucesso"] = "Dados do aluno atualizados com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Alunos", "Editar", alunoId);
        return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}");
    }

    private async Task<bool> PodeTrocarAsync(
        Guid usuarioId, CancellationToken cancellationToken)
    {
        var unidades = await unidadesUsuarioConsulta.ListarAdministradasAsync(
            usuarioId, cancellationToken);
        return unidades.Count > 1;
    }

    private static string? NormalizarTexto(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

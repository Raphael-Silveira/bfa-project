using BFA.Application.Alunos;
using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Domain.Alunos;
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

    [HttpGet("{alunoId:guid}/responsaveis")]
    public async Task<IActionResult> Responsaveis(
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            logger.LogWarning("{Controller} {Action} negado: usuario nao identificado", "Alunos", "Responsaveis");
            return Forbid();
        }

        var resultado = await alunosServico.ListarResponsaveisAsync(
            usuarioId, unidadeId, alunoId, cancellationToken);

        if (resultado.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAlunosUnidade.AlunoNaoEncontrado
            || resultado.Estado == EstadoAlunosUnidade.AlunoNaoRelacionadoUnidade)
            return NotFound();
        if (resultado.Estado != EstadoAlunosUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        var aluno = await alunosServico.ObterAsync(
            usuarioId, unidadeId, alunoId, cancellationToken);

        if (aluno.Estado != EstadoAlunosUnidade.Sucesso
            || aluno.Valor is null)
            return NotFound();

        return View(AlunosViewModelMapper.MapearListaResponsaveis(
            resultado.Contexto,
            alunoId,
            aluno.Valor.NomeCompleto,
            resultado.Valor,
            await PodeTrocarAsync(usuarioId, cancellationToken)));
    }

    [HttpGet("{alunoId:guid}/responsaveis/novo")]
    public async Task<IActionResult> NovoResponsavel(
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            logger.LogWarning("{Controller} {Action} negado: usuario nao identificado", "Alunos", "NovoResponsavel");
            return Forbid();
        }

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

        return View("ResponsavelForm", AlunosViewModelMapper.MapearFormResponsavel(
            resultado.Contexto,
            alunoId,
            resultado.Valor.NomeCompleto,
            existente: null,
            await PodeTrocarAsync(usuarioId, cancellationToken)));
    }

    [HttpPost("{alunoId:guid}/responsaveis/novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoResponsavel(
        Guid unidadeId,
        Guid alunoId,
        ResponsavelFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            logger.LogWarning("{Controller} {Action} negado: usuario nao identificado", "Alunos", "NovoResponsavel");
            return Forbid();
        }

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

        model = AlunosViewModelMapper.MapearFormResponsavel(
            resultado.Contexto,
            alunoId,
            resultado.Valor.NomeCompleto,
            existente: null,
            await PodeTrocarAsync(usuarioId, cancellationToken));
        model.NomeCompleto = Request.Form["NomeCompleto"].FirstOrDefault()
            ?? model.NomeCompleto;
        model.Cpf = Request.Form["Cpf"].FirstOrDefault();
        model.Telefone = Request.Form["Telefone"].FirstOrDefault();
        model.Email = Request.Form["Email"].FirstOrDefault();

        if (Enum.TryParse<TipoRelacaoResponsavel>(Request.Form["TipoRelacao"].FirstOrDefault(), out var tipoRelacao))
            model.TipoRelacao = tipoRelacao;

        model.DescricaoRelacao = Request.Form["DescricaoRelacao"].FirstOrDefault();
        model.PrincipalContato = Request.Form.ContainsKey("PrincipalContato");
        model.ResponsavelFinanceiro = Request.Form.ContainsKey("ResponsavelFinanceiro");

        if (!ModelState.IsValid)
            return View("ResponsavelForm", model);

        var agoraUtc = DateTime.UtcNow;

        var resultadoCriar = await alunosServico.CriarResponsavelAsync(
            usuarioId,
            unidadeId,
            alunoId,
            model.NomeCompleto,
            model.Cpf,
            model.Telefone,
            model.Email,
            model.TipoRelacao,
            model.DescricaoRelacao,
            model.PrincipalContato,
            model.ResponsavelFinanceiro,
            cancellationToken);

        if (resultadoCriar.Estado == EstadoAlunosUnidade.DadosInvalidos)
        {
            ModelState.AddModelError(string.Empty,
                "Revise os dados informados.");
            return View("ResponsavelForm", model);
        }
        if (resultadoCriar.Estado == EstadoAlunosUnidade.ResponsavelJaVinculado)
        {
            ModelState.AddModelError(string.Empty,
                "Este responsável já está vinculado ao aluno.");
            return View("ResponsavelForm", model);
        }
        if (resultadoCriar.Estado != EstadoAlunosUnidade.Sucesso)
        {
            ModelState.AddModelError(string.Empty,
                "Não foi possível cadastrar o responsável. Tente novamente.");
            return View("ResponsavelForm", model);
        }

        var novoResponsavelId = resultadoCriar.Valor;
        TempData["Sucesso"] = "Responsável cadastrado com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {ResponsavelId}", "Alunos", "NovoResponsavel", novoResponsavelId);
        return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis/{novoResponsavelId:D}");
    }

    [HttpGet("{alunoId:guid}/responsaveis/{responsavelId:guid}")]
    public async Task<IActionResult> DetalhesResponsavel(
        Guid unidadeId,
        Guid alunoId,
        Guid responsavelId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            logger.LogWarning("{Controller} {Action} negado: usuario nao identificado", "Alunos", "DetalhesResponsavel");
            return Forbid();
        }

        var resultado = await alunosServico.ObterResponsavelAsync(
            usuarioId, unidadeId, alunoId, responsavelId, cancellationToken);

        if (resultado.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAlunosUnidade.AlunoNaoEncontrado
            || resultado.Estado == EstadoAlunosUnidade.ResponsavelNaoEncontrado
            || resultado.Estado == EstadoAlunosUnidade.AlunoNaoRelacionadoUnidade)
            return NotFound();
        if (resultado.Estado != EstadoAlunosUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        var aluno = await alunosServico.ObterAsync(
            usuarioId, unidadeId, alunoId, cancellationToken);

        if (aluno.Estado != EstadoAlunosUnidade.Sucesso
            || aluno.Valor is null)
            return NotFound();

        return View(AlunosViewModelMapper.MapearDetalheResponsavel(
            resultado.Contexto,
            alunoId,
            aluno.Valor.NomeCompleto,
            resultado.Valor,
            await PodeTrocarAsync(usuarioId, cancellationToken)));
    }

    [HttpGet("{alunoId:guid}/responsaveis/{responsavelId:guid}/editar")]
    public async Task<IActionResult> EditarResponsavel(
        Guid unidadeId,
        Guid alunoId,
        Guid responsavelId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            logger.LogWarning("{Controller} {Action} negado: usuario nao identificado", "Alunos", "EditarResponsavel");
            return Forbid();
        }

        var resultado = await alunosServico.ObterResponsavelAsync(
            usuarioId, unidadeId, alunoId, responsavelId, cancellationToken);

        if (resultado.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAlunosUnidade.AlunoNaoEncontrado
            || resultado.Estado == EstadoAlunosUnidade.ResponsavelNaoEncontrado
            || resultado.Estado == EstadoAlunosUnidade.AlunoNaoRelacionadoUnidade)
            return NotFound();
        if (resultado.Estado != EstadoAlunosUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        var aluno = await alunosServico.ObterAsync(
            usuarioId, unidadeId, alunoId, cancellationToken);

        if (aluno.Estado != EstadoAlunosUnidade.Sucesso
            || aluno.Valor is null)
            return NotFound();

        return View("ResponsavelForm", AlunosViewModelMapper.MapearFormResponsavel(
            resultado.Contexto,
            alunoId,
            aluno.Valor.NomeCompleto,
            resultado.Valor,
            await PodeTrocarAsync(usuarioId, cancellationToken)));
    }

    [HttpPost("{alunoId:guid}/responsaveis/{responsavelId:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarResponsavel(
        Guid unidadeId,
        Guid alunoId,
        Guid responsavelId,
        ResponsavelFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            logger.LogWarning("{Controller} {Action} negado: usuario nao identificado", "Alunos", "EditarResponsavel");
            return Forbid();
        }

        var resultado = await alunosServico.ObterResponsavelAsync(
            usuarioId, unidadeId, alunoId, responsavelId, cancellationToken);

        if (resultado.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultado.Estado == EstadoAlunosUnidade.AlunoNaoEncontrado
            || resultado.Estado == EstadoAlunosUnidade.ResponsavelNaoEncontrado
            || resultado.Estado == EstadoAlunosUnidade.AlunoNaoRelacionadoUnidade)
            return NotFound();
        if (resultado.Estado != EstadoAlunosUnidade.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        var aluno = await alunosServico.ObterAsync(
            usuarioId, unidadeId, alunoId, cancellationToken);

        if (aluno.Estado != EstadoAlunosUnidade.Sucesso
            || aluno.Valor is null)
            return NotFound();

        model = AlunosViewModelMapper.MapearFormResponsavel(
            resultado.Contexto,
            alunoId,
            aluno.Valor.NomeCompleto,
            resultado.Valor,
            await PodeTrocarAsync(usuarioId, cancellationToken));
        model.NomeCompleto = Request.Form["NomeCompleto"].FirstOrDefault()
            ?? model.NomeCompleto;

        var cpfForm = Request.Form["Cpf"].FirstOrDefault();
        model.Cpf = !string.IsNullOrWhiteSpace(cpfForm) ? cpfForm : model.Cpf;

        model.Telefone = Request.Form["Telefone"].FirstOrDefault();
        model.Email = Request.Form["Email"].FirstOrDefault();

        if (Enum.TryParse<TipoRelacaoResponsavel>(Request.Form["TipoRelacao"].FirstOrDefault(), out var tipoRelacao))
            model.TipoRelacao = tipoRelacao;

        model.DescricaoRelacao = Request.Form["DescricaoRelacao"].FirstOrDefault();
        model.PrincipalContato = Request.Form.ContainsKey("PrincipalContato");
        model.ResponsavelFinanceiro = Request.Form.ContainsKey("ResponsavelFinanceiro");

        if (!ModelState.IsValid)
            return View("ResponsavelForm", model);

        var agoraUtc = DateTime.UtcNow;

        var resultadoAtualizar = await alunosServico.AtualizarResponsavelAsync(
            usuarioId,
            unidadeId,
            alunoId,
            responsavelId,
            model.NomeCompleto,
            model.Cpf,
            model.Telefone,
            model.Email,
            model.TipoRelacao,
            model.DescricaoRelacao,
            model.PrincipalContato,
            model.ResponsavelFinanceiro,
            cancellationToken);

        if (resultadoAtualizar.Estado == EstadoAlunosUnidade.DadosInvalidos)
        {
            ModelState.AddModelError(string.Empty,
                "Revise os dados informados.");
            return View("ResponsavelForm", model);
        }
        if (resultadoAtualizar.Estado != EstadoAlunosUnidade.Sucesso)
        {
            ModelState.AddModelError(string.Empty,
                "Não foi possível atualizar os dados do responsável. Tente novamente.");
            return View("ResponsavelForm", model);
        }

        TempData["Sucesso"] = "Dados do responsável atualizados com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {ResponsavelId}", "Alunos", "EditarResponsavel", responsavelId);
        return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis/{responsavelId:D}");
    }

    [HttpPost("{alunoId:guid}/responsaveis/{responsavelId:guid}/desativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesativarResponsavel(
        Guid unidadeId,
        Guid alunoId,
        Guid responsavelId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            logger.LogWarning("{Controller} {Action} negado: usuario nao identificado", "Alunos", "DesativarResponsavel");
            return Forbid();
        }

        var resultadoDesativar = await alunosServico.DesativarVinculoAsync(
            usuarioId, unidadeId, alunoId, responsavelId, cancellationToken);

        if (resultadoDesativar.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultadoDesativar.Estado == EstadoAlunosUnidade.AlunoNaoRelacionadoUnidade
            || resultadoDesativar.Estado == EstadoAlunosUnidade.ResponsavelNaoEncontrado)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Alunos", "DesativarResponsavel", resultadoDesativar.Estado);
            TempData["Erro"] = "Não foi possível desativar o vínculo do responsável.";
            return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis");
        }
        if (resultadoDesativar.Estado == EstadoAlunosUnidade.MenorSemResponsavel)
        {
            logger.LogWarning("{Controller} {Action} negado: menor sem responsavel", "Alunos", "DesativarResponsavel");
            TempData["Erro"] = "Este vínculo não pode ser inativo porque o aluno precisa manter pelo menos um responsável ativo.";
            return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis/{responsavelId:D}");
        }
        if (resultadoDesativar.Estado == EstadoAlunosUnidade.VinculoJaInativo)
        {
            TempData["Erro"] = "Este vínculo já está inativo.";
            return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis/{responsavelId:D}");
        }
        if (resultadoDesativar.Estado != EstadoAlunosUnidade.Sucesso)
        {
            logger.LogWarning("{Controller} {Action} falhou: {Estado}", "Alunos", "DesativarResponsavel", resultadoDesativar.Estado);
            TempData["Erro"] = "Não foi possível desativar o vínculo do responsável.";
            return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis");
        }

        TempData["Sucesso"] = "Vínculo do responsável desativado com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {ResponsavelId}", "Alunos", "DesativarResponsavel", responsavelId);
        return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis");
    }

    [HttpPost("{alunoId:guid}/responsaveis/{responsavelId:guid}/reativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReativarResponsavel(
        Guid unidadeId,
        Guid alunoId,
        Guid responsavelId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            logger.LogWarning("{Controller} {Action} negado: usuario nao identificado", "Alunos", "ReativarResponsavel");
            return Forbid();
        }

        var resultadoReativar = await alunosServico.ReativarVinculoAsync(
            usuarioId, unidadeId, alunoId, responsavelId, cancellationToken);

        if (resultadoReativar.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultadoReativar.Estado == EstadoAlunosUnidade.AlunoNaoRelacionadoUnidade
            || resultadoReativar.Estado == EstadoAlunosUnidade.ResponsavelNaoEncontrado)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Alunos", "ReativarResponsavel", resultadoReativar.Estado);
            TempData["Erro"] = "Não foi possível reativar o vínculo do responsável.";
            return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis");
        }
        if (resultadoReativar.Estado == EstadoAlunosUnidade.VinculoJaAtivo)
        {
            TempData["Erro"] = "Este vínculo já está ativo.";
            return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis/{responsavelId:D}");
        }
        if (resultadoReativar.Estado != EstadoAlunosUnidade.Sucesso)
        {
            logger.LogWarning("{Controller} {Action} falhou: {Estado}", "Alunos", "ReativarResponsavel", resultadoReativar.Estado);
            TempData["Erro"] = "Não foi possível reativar o vínculo do responsável.";
            return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis");
        }

        TempData["Sucesso"] = "Vínculo reativado com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {ResponsavelId}", "Alunos", "ReativarResponsavel", responsavelId);
        return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis/{responsavelId:D}");
    }

    [HttpPost("{alunoId:guid}/responsaveis/{responsavelId:guid}/ativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtivarResponsavel(
        Guid unidadeId,
        Guid alunoId,
        Guid responsavelId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            logger.LogWarning("{Controller} {Action} negado: usuario nao identificado", "Alunos", "AtivarResponsavel");
            return Forbid();
        }

        var contexto = await alunosServico.ListarAsync(
            usuarioId, unidadeId, null, cancellationToken);

        if (contexto.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (contexto.Estado != EstadoAlunosUnidade.Sucesso
            || contexto.Contexto is null)
            return Forbid();

        var resultadoAtivar = await alunosServico.AtivarVinculoAsync(
            usuarioId, unidadeId, alunoId, responsavelId, cancellationToken);

        if (resultadoAtivar.Estado == EstadoAlunosUnidade.UnidadeNaoEncontrada)
            return NotFound();
        if (resultadoAtivar.Estado == EstadoAlunosUnidade.AlunoNaoRelacionadoUnidade
            || resultadoAtivar.Estado == EstadoAlunosUnidade.ResponsavelNaoEncontrado)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Alunos", "AtivarResponsavel", resultadoAtivar.Estado);
            TempData["Erro"] = "Não foi possível ativar o vínculo do responsável.";
            return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis/{responsavelId:D}");
        }
        if (resultadoAtivar.Estado != EstadoAlunosUnidade.Sucesso)
        {
            logger.LogWarning("{Controller} {Action} falhou: {Estado}", "Alunos", "AtivarResponsavel", resultadoAtivar.Estado);
            TempData["Erro"] = "Não foi possível ativar o vínculo do responsável.";
            return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis/{responsavelId:D}");
        }

        TempData["Sucesso"] = "Vínculo do responsável ativado com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {ResponsavelId}", "Alunos", "AtivarResponsavel", responsavelId);
        return Redirect($"/unidade/{unidadeId:D}/alunos/{alunoId:D}/responsaveis/{responsavelId:D}");
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

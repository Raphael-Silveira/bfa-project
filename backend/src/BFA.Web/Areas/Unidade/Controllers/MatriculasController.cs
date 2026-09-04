using BFA.Application.Acessos;
using BFA.Application.Matriculas;
using BFA.Application.Unidades;
using BFA.Domain.Matriculas;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[ServiceFilter(typeof(GovernancaOperacionalUnidadeResultFilter))]
[Route("unidade/{unidadeId:guid}/matriculas")]
public sealed class MatriculasController(
    IUsuarioAtual usuarioAtual,
    IMatriculasServico matriculasServico,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    TimeProvider timeProvider,
    ILogger<MatriculasController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        string? texto,
        string? status,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var textoNormalizado = NormalizarTexto(texto);
        var statusNormalizado = ParseStatus(status);
        var resultado = await matriculasServico.ListarAsync(
            usuarioId,
            unidadeId,
            textoNormalizado,
            statusNormalizado,
            cancellationToken);

        if (resultado.Estado == EstadoMatriculas.UnidadeNaoEncontrada) return NotFound();
        if (resultado.Estado != EstadoMatriculas.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        return View(MatriculasViewModelMapper.Lista(
            resultado.Contexto,
            resultado.Valor,
            await PodeTrocarAsync(usuarioId, cancellationToken),
            textoNormalizado,
            statusNormalizado));
    }

    [HttpGet("nova")]
    public async Task<IActionResult> Nova(
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var alunos = await matriculasServico.ListarAlunosRelacionadosAsync(
            usuarioId, unidadeId, null, cancellationToken);
        if (alunos.Estado == EstadoMatriculas.UnidadeNaoEncontrada) return NotFound();
        if (alunos.Estado != EstadoMatriculas.Sucesso
            || alunos.Valor is null
            || alunos.Contexto is null)
            return Forbid();

        var dataInicio = DataCivilAtual();
        var planos = await matriculasServico.ListarPlanosElegiveisAsync(
            usuarioId, unidadeId, dataInicio, cancellationToken);
        if (planos.Estado != EstadoMatriculas.Sucesso || planos.Valor is null)
            return Forbid();

        var model = new NovaMatriculaViewModel
        {
            OrganizacaoId = alunos.Contexto.OrganizacaoId,
            UnidadeId = unidadeId,
            NomeUnidade = alunos.Contexto.NomeUnidade,
            PodeTrocarUnidade = false,
            DataInicioTexto = NovaMatriculaViewModelMapper.FormatarData(dataInicio)
        };
        NovaMatriculaViewModelMapper.PreencherOpcoes(
            model,
            alunos.Contexto,
            await PodeTrocarAsync(usuarioId, cancellationToken),
            alunos.Valor,
            planos.Valor,
            []);
        return View(model);
    }

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nova(
        Guid unidadeId,
        NovaMatriculaViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var mapeamento = NovaMatriculaViewModelMapper.MapearSolicitacao(
            model, DataCivilAtual());
        foreach (var (campo, mensagem) in mapeamento.Erros)
            ModelState.AddModelError(campo, mensagem);

        if (!ModelState.IsValid || mapeamento.Solicitacao is null)
        {
            model.PassoInicial = PrimeiroPassoComErro(model);
            return await ReexibirNovaAsync(
                usuarioId, unidadeId, model, cancellationToken);
        }

        var resultado = await matriculasServico.CriarAsync(
            usuarioId, unidadeId, mapeamento.Solicitacao, cancellationToken);
        if (resultado.Estado == EstadoMatriculas.Sucesso
            && resultado.Valor is not null)
        {
            TempData["Sucesso"] = "Matrícula criada com sucesso.";
            logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Matriculas", "Nova", resultado.Valor.MatriculaId);
            return RedirectToAction(nameof(Detalhes), new
            {
                unidadeId,
                matriculaId = resultado.Valor.MatriculaId
            });
        }

        if (resultado.Estado == EstadoMatriculas.UnidadeNaoEncontrada) return NotFound();
        if (resultado.Estado == EstadoMatriculas.SemAcesso) return Forbid();

        ModelState.AddModelError(string.Empty, MensagemErro(resultado.Estado));
        model.PassoInicial = PassoDoErro(resultado.Estado);
        return await ReexibirNovaAsync(
            usuarioId, unidadeId, model, cancellationToken);
    }

    [HttpGet("nova/planos")]
    public async Task<IActionResult> PlanosElegiveis(
        Guid unidadeId,
        string? dataInicio,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var data = NovaMatriculaViewModelMapper.ParseData(dataInicio);
        if (data is null) return BadRequest(new { mensagem = "Informe uma data de início válida." });

        var resultado = await matriculasServico.ListarPlanosElegiveisAsync(
            usuarioId, unidadeId, data.Value, cancellationToken);
        if (resultado.Estado == EstadoMatriculas.UnidadeNaoEncontrada) return NotFound();
        if (resultado.Estado != EstadoMatriculas.Sucesso || resultado.Valor is null)
            return Forbid();

        return Json(new
        {
            planos = resultado.Valor.Select(NovaMatriculaViewModelMapper.MapearPlano)
        });
    }

    [HttpGet("nova/horarios")]
    public async Task<IActionResult> HorariosElegiveis(
        Guid unidadeId,
        string? dataInicio,
        Guid planoVersaoId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var data = NovaMatriculaViewModelMapper.ParseData(dataInicio);
        if (data is null || planoVersaoId == Guid.Empty)
            return BadRequest(new { mensagem = "Selecione uma data e um plano válidos." });

        var planos = await matriculasServico.ListarPlanosElegiveisAsync(
            usuarioId, unidadeId, data.Value, cancellationToken);
        if (planos.Estado == EstadoMatriculas.UnidadeNaoEncontrada) return NotFound();
        if (planos.Estado != EstadoMatriculas.Sucesso || planos.Valor is null)
            return Forbid();
        var plano = planos.Valor.SingleOrDefault(item => item.PlanoVersaoId == planoVersaoId);
        if (plano is null)
            return BadRequest(new { mensagem = "O plano selecionado não está mais disponível." });

        var dataFim = Matricula.CalcularDataFimPrevista(data.Value, plano.DuracaoMeses);
        var horarios = await matriculasServico.ListarHorariosElegiveisAsync(
            usuarioId, unidadeId, data.Value, dataFim, cancellationToken);
        if (horarios.Estado != EstadoMatriculas.Sucesso || horarios.Valor is null)
            return Forbid();

        return Json(new
        {
            plano = NovaMatriculaViewModelMapper.MapearPlano(plano),
            dataFimPrevista = NovaMatriculaViewModelMapper.FormatarData(dataFim),
            horarios = horarios.Valor.Select(NovaMatriculaViewModelMapper.MapearHorario)
        });
    }

    [HttpGet("{matriculaId:guid}")]
    public async Task<IActionResult> Detalhes(
        Guid unidadeId,
        Guid matriculaId,
        string? texto,
        string? status,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();

        var resultado = await matriculasServico.ObterAsync(
            usuarioId, unidadeId, matriculaId, cancellationToken);
        if (resultado.Estado is EstadoMatriculas.UnidadeNaoEncontrada
            or EstadoMatriculas.MatriculaNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoMatriculas.Sucesso
            || resultado.Valor is null
            || resultado.Contexto is null)
            return Forbid();

        return View(MatriculasViewModelMapper.Detalhe(
            resultado.Contexto,
            resultado.Valor,
            await PodeTrocarAsync(usuarioId, cancellationToken),
            NormalizarTexto(texto),
            ParseStatus(status)));
    }

    [HttpGet("{matriculaId:guid}/alterar-grade")]
    public async Task<IActionResult> AlterarGrade(
        Guid unidadeId,
        Guid matriculaId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var detalhe = await matriculasServico.ObterAsync(
            usuarioId, unidadeId, matriculaId, cancellationToken);
        if (detalhe.Estado is EstadoMatriculas.UnidadeNaoEncontrada
            or EstadoMatriculas.MatriculaNaoEncontrada)
            return NotFound();
        if (detalhe.Estado != EstadoMatriculas.Sucesso
            || detalhe.Valor is null
            || detalhe.Contexto is null)
            return Forbid();
        if (!detalhe.Contexto.PodeGerenciar || detalhe.Valor.Status != StatusMatricula.Ativa)
            return Forbid();

        return await ReexibirAlterarGradeAsync(
            usuarioId, unidadeId, detalhe.Valor, detalhe.Contexto,
            DataInicialParaOpcoes(detalhe.Valor, detalhe.Valor.DataInicio),
            [], cancellationToken);
    }

    [HttpPost("{matriculaId:guid}/alterar-grade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarGrade(
        Guid unidadeId,
        Guid matriculaId,
        AlterarGradeMatriculaViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var detalhe = await matriculasServico.ObterAsync(
            usuarioId, unidadeId, matriculaId, cancellationToken);
        if (detalhe.Estado is EstadoMatriculas.UnidadeNaoEncontrada
            or EstadoMatriculas.MatriculaNaoEncontrada)
            return NotFound();
        if (detalhe.Estado != EstadoMatriculas.Sucesso
            || detalhe.Valor is null
            || detalhe.Contexto is null
            || !detalhe.Contexto.PodeGerenciar
            || detalhe.Valor.Status != StatusMatricula.Ativa)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Matriculas", "AlterarGrade", detalhe.Estado);
            return Forbid();
        }

        var data = NovaMatriculaViewModelMapper.ParseData(model.DataInicioTexto);
        if (data is null)
            ModelState.AddModelError(nameof(model.DataInicioTexto),
                "Informe uma data de início válida.");
        if (model.TurmaHorarioIds.Count == 0)
            ModelState.AddModelError(nameof(model.TurmaHorarioIds),
                "Selecione ao menos um horário para a Grade.");

        if (ModelState.IsValid && data is { } dataValida)
        {
            var resultado = await matriculasServico.AlterarGradeAsync(
                usuarioId,
                unidadeId,
                matriculaId,
                new AlterarGradeMatriculaSolicitacao(
                    dataValida,
                    model.TurmaHorarioIds.Distinct().ToArray()),
                cancellationToken);
            if (resultado.Estado == EstadoMatriculas.Sucesso && resultado.Valor is not null)
            {
                TempData["Sucesso"] = resultado.Valor.HorariosEncerrados == 0
                        && resultado.Valor.HorariosCriados == 0
                    ? "Nenhuma alteração foi identificada na Grade."
                    : "Grade alterada com sucesso.";
                logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Matriculas", "AlterarGrade", matriculaId);
                return RedirectToAction(nameof(Detalhes), new { unidadeId, matriculaId });
            }
            if (resultado.Estado is EstadoMatriculas.UnidadeNaoEncontrada
                or EstadoMatriculas.MatriculaNaoEncontrada)
                return NotFound();
            if (resultado.Estado == EstadoMatriculas.SemAcesso)
            {
                logger.LogWarning("{Controller} {Action} negado: {Estado}", "Matriculas", "AlterarGrade", resultado.Estado);
                return Forbid();
            }
            ModelState.AddModelError(string.Empty, MensagemErroAlterarGrade(resultado.Estado));
        }

        var dataParaOpcoes = data is { } dataParaOpcoesValida
            ? dataParaOpcoesValida < detalhe.Valor.DataInicio
                ? detalhe.Valor.DataInicio
                : dataParaOpcoesValida > detalhe.Valor.DataFimPrevista
                    ? detalhe.Valor.DataFimPrevista
                    : dataParaOpcoesValida
            : detalhe.Valor.DataInicio;
        dataParaOpcoes = DataInicialParaOpcoes(detalhe.Valor, dataParaOpcoes);
        return await ReexibirAlterarGradeAsync(
            usuarioId,
            unidadeId,
            detalhe.Valor,
            detalhe.Contexto,
            dataParaOpcoes,
            model.TurmaHorarioIds,
            cancellationToken,
            model);
    }

    private static DateOnly DataInicialParaOpcoes(
        MatriculaDetalhe matricula, DateOnly dataSolicitada)
    {
        var inicioGradeAtual = matricula.GradeAtual
            .Where(item => item.VigenciaFim is null)
            .Select(item => item.VigenciaInicio)
            .DefaultIfEmpty(dataSolicitada)
            .Max();
        return inicioGradeAtual > dataSolicitada ? inicioGradeAtual : dataSolicitada;
    }

    [HttpGet("{matriculaId:guid}/encerrar")]
    public Task<IActionResult> Encerrar(
        Guid unidadeId, Guid matriculaId, CancellationToken cancellationToken) =>
        ExibirFinalizacaoAsync(unidadeId, matriculaId, cancelar: false, cancellationToken);

    [HttpPost("{matriculaId:guid}/encerrar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Encerrar(
        Guid unidadeId,
        Guid matriculaId,
        FinalizarMatriculaViewModel model,
        CancellationToken cancellationToken) =>
        FinalizarAsync(unidadeId, matriculaId, model, cancelar: false, cancellationToken);

    [HttpGet("{matriculaId:guid}/cancelar")]
    public Task<IActionResult> Cancelar(
        Guid unidadeId, Guid matriculaId, CancellationToken cancellationToken) =>
        ExibirFinalizacaoAsync(unidadeId, matriculaId, cancelar: true, cancellationToken);

    [HttpPost("{matriculaId:guid}/cancelar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancelar(
        Guid unidadeId,
        Guid matriculaId,
        FinalizarMatriculaViewModel model,
        CancellationToken cancellationToken) =>
        FinalizarAsync(unidadeId, matriculaId, model, cancelar: true, cancellationToken);

    private async Task<IActionResult> ExibirFinalizacaoAsync(
        Guid unidadeId,
        Guid matriculaId,
        bool cancelar,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var detalhe = await matriculasServico.ObterAsync(
            usuarioId, unidadeId, matriculaId, cancellationToken);
        if (detalhe.Estado is EstadoMatriculas.UnidadeNaoEncontrada
            or EstadoMatriculas.MatriculaNaoEncontrada)
            return NotFound();
        if (detalhe.Estado != EstadoMatriculas.Sucesso
            || detalhe.Valor is null
            || detalhe.Contexto is null
            || !detalhe.Contexto.PodeGerenciar
            || detalhe.Valor.Status != StatusMatricula.Ativa)
            return Forbid();
        return View(cancelar ? "Cancelar" : "Encerrar",
            MatriculasViewModelMapper.Finalizar(detalhe.Contexto, detalhe.Valor, cancelar));
    }

    private async Task<IActionResult> FinalizarAsync(
        Guid unidadeId,
        Guid matriculaId,
        FinalizarMatriculaViewModel model,
        bool cancelar,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return Forbid();
        var detalhe = await matriculasServico.ObterAsync(
            usuarioId, unidadeId, matriculaId, cancellationToken);
        if (detalhe.Estado is EstadoMatriculas.UnidadeNaoEncontrada
            or EstadoMatriculas.MatriculaNaoEncontrada)
            return NotFound();
        if (detalhe.Estado != EstadoMatriculas.Sucesso
            || detalhe.Valor is null
            || detalhe.Contexto is null
            || !detalhe.Contexto.PodeGerenciar
            || detalhe.Valor.Status != StatusMatricula.Ativa)
            return Forbid();

        var data = NovaMatriculaViewModelMapper.ParseData(model.DataFinalTexto);
        if (data is null)
            ModelState.AddModelError(nameof(model.DataFinalTexto),
                "Informe uma data final válida.");
        if (ModelState.IsValid && data is { } dataValida)
        {
            var resultado = cancelar
                ? await matriculasServico.CancelarAsync(
                    usuarioId, unidadeId, matriculaId, dataValida, cancellationToken)
                : await matriculasServico.EncerrarAsync(
                    usuarioId, unidadeId, matriculaId, dataValida, cancellationToken);
            if (resultado.Estado == EstadoMatriculas.Sucesso)
            {
                TempData["Sucesso"] = cancelar
                    ? "Matrícula cancelada. O histórico foi preservado."
                    : "Matrícula encerrada com sucesso.";
                logger.LogInformation("{Controller} {Action} concluído: {EntityId}",
                    "Matriculas", cancelar ? "Cancelar" : "Encerrar", matriculaId);
                return RedirectToAction(nameof(Detalhes), new { unidadeId, matriculaId });
            }
            if (resultado.Estado is EstadoMatriculas.UnidadeNaoEncontrada
                or EstadoMatriculas.MatriculaNaoEncontrada)
                return NotFound();
            if (resultado.Estado == EstadoMatriculas.SemAcesso)
            {
                logger.LogWarning("{Controller} {Action} negado: {Estado}",
                    "Matriculas", cancelar ? "Cancelar" : "Encerrar", resultado.Estado);
                return Forbid();
            }
            ModelState.AddModelError(string.Empty, MensagemErro(resultado.Estado));
        }

        var viewModel = MatriculasViewModelMapper.Finalizar(
            detalhe.Contexto, detalhe.Valor, cancelar);
        viewModel.DataFinalTexto = model.DataFinalTexto;
        return View(cancelar ? "Cancelar" : "Encerrar", viewModel);
    }

    private async Task<IActionResult> ReexibirAlterarGradeAsync(
        Guid usuarioId,
        Guid unidadeId,
        MatriculaDetalhe matricula,
        ContextoMatriculasResumo contexto,
        DateOnly dataInicio,
        IReadOnlyList<Guid> idsSelecionados,
        CancellationToken cancellationToken,
        AlterarGradeMatriculaViewModel? model = null)
    {
        var horarios = await matriculasServico.ListarHorariosElegiveisAsync(
            usuarioId, unidadeId, dataInicio, matricula.DataFimPrevista, cancellationToken);
        if (horarios.Estado != EstadoMatriculas.Sucesso || horarios.Valor is null)
            return Forbid();
        var viewModel = MatriculasViewModelMapper.AlterarGrade(
            contexto, matricula, horarios.Valor, dataInicio);
        viewModel.DataInicioTexto = model?.DataInicioTexto
            ?? NovaMatriculaViewModelMapper.FormatarData(dataInicio);
        viewModel.TurmaHorarioIds = idsSelecionados.Count > 0
            ? idsSelecionados.Distinct().ToList()
            : matricula.GradeAtual
                .Where(item => item.VigenciaFim is null)
                .Select(item => item.TurmaHorarioId)
                .ToList();
        return View(viewModel);
    }

    private async Task<bool> PodeTrocarAsync(
        Guid usuarioId, CancellationToken cancellationToken) =>
        (await unidadesUsuarioConsulta.ListarAdministradasAsync(
            usuarioId, cancellationToken)).Count > 1;

    private static string? NormalizarTexto(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static StatusMatricula? ParseStatus(string? status) =>
        Enum.TryParse<StatusMatricula>(status, ignoreCase: true, out var valor)
        && Enum.IsDefined(valor)
            ? valor
            : null;

    private async Task<IActionResult> ReexibirNovaAsync(
        Guid usuarioId,
        Guid unidadeId,
        NovaMatriculaViewModel model,
        CancellationToken cancellationToken)
    {
        var alunos = await matriculasServico.ListarAlunosRelacionadosAsync(
            usuarioId, unidadeId, null, cancellationToken);
        if (alunos.Estado == EstadoMatriculas.UnidadeNaoEncontrada) return NotFound();
        if (alunos.Estado != EstadoMatriculas.Sucesso
            || alunos.Valor is null
            || alunos.Contexto is null)
            return Forbid();

        IReadOnlyList<PlanoElegivelMatriculaResumo> planos = [];
        IReadOnlyList<HorarioElegivelMatriculaResumo> horarios = [];
        if (NovaMatriculaViewModelMapper.ParseData(model.DataInicioTexto) is { } dataInicio)
        {
            var resultadoPlanos = await matriculasServico.ListarPlanosElegiveisAsync(
                usuarioId, unidadeId, dataInicio, cancellationToken);
            if (resultadoPlanos.Estado == EstadoMatriculas.Sucesso
                && resultadoPlanos.Valor is not null)
            {
                planos = resultadoPlanos.Valor;
                var plano = planos.SingleOrDefault(
                    item => item.PlanoVersaoId == model.PlanoVersaoId);
                if (plano is not null)
                {
                    var dataFim = Matricula.CalcularDataFimPrevista(
                        dataInicio, plano.DuracaoMeses);
                    var resultadoHorarios = await matriculasServico.ListarHorariosElegiveisAsync(
                        usuarioId, unidadeId, dataInicio, dataFim, cancellationToken);
                    if (resultadoHorarios.Estado == EstadoMatriculas.Sucesso
                        && resultadoHorarios.Valor is not null)
                        horarios = resultadoHorarios.Valor;
                }
            }
        }

        NovaMatriculaViewModelMapper.PreencherOpcoes(
            model,
            alunos.Contexto,
            await PodeTrocarAsync(usuarioId, cancellationToken),
            alunos.Valor,
            planos,
            horarios);
        return View(nameof(Nova), model);
    }

    private DateOnly DataCivilAtual() =>
        DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private static int PrimeiroPassoComErro(NovaMatriculaViewModel model)
    {
        if (!string.Equals(model.AlunoModo, "existente", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(model.AlunoModo, "novo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(model.AlunoModo, "existente", StringComparison.OrdinalIgnoreCase)
                && model.AlunoId is null
            || string.Equals(model.AlunoModo, "novo", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(model.NovoAluno.NomeCompleto)
                    || NovaMatriculaViewModelMapper.ParseData(
                        model.NovoAluno.DataNascimentoTexto) is null))
            return 1;
        if (model.Responsaveis.Count(item => item.PrincipalContato) > 1
            || model.Responsaveis.Any(item => string.IsNullOrWhiteSpace(item.NomeCompleto)
                || string.IsNullOrWhiteSpace(item.Telefone)
                    && string.IsNullOrWhiteSpace(item.Email)))
            return 2;
        if (NovaMatriculaViewModelMapper.ParseData(model.DataInicioTexto) is null
            || model.PlanoVersaoId == Guid.Empty
            || NovaMatriculaViewModelMapper.ParseMoeda(
                model.ValorMensalContratadoTexto) is not > 0)
            return 3;
        return 4;
    }

    private static int PassoDoErro(EstadoMatriculas estado) => estado switch
    {
        EstadoMatriculas.AlunoNaoEncontrado
            or EstadoMatriculas.AlunoNaoRelacionadoUnidade
            or EstadoMatriculas.AlunoDuplicado
            or EstadoMatriculas.MatriculaAtivaExistente => 1,
        EstadoMatriculas.ResponsavelInvalido
            or EstadoMatriculas.ResponsavelDuplicado
            or EstadoMatriculas.MenorSemResponsavel => 2,
        EstadoMatriculas.PlanoNaoElegivel
            or EstadoMatriculas.DataInvalida => 3,
        _ => 4
    };

    private static string MensagemErro(EstadoMatriculas estado) => estado switch
    {
        EstadoMatriculas.CapacidadeEsgotada =>
            "Um dos horários selecionados acabou de ficar sem vagas. Revise sua Grade.",
        EstadoMatriculas.ConflitoHorarioAluno =>
            "O aluno possui outro horário conflitante com a Grade selecionada.",
        EstadoMatriculas.PlanoNaoElegivel =>
            "O plano selecionado não está mais disponível.",
        EstadoMatriculas.MatriculaAtivaExistente =>
            "O aluno já possui uma matrícula ativa nesta unidade.",
        EstadoMatriculas.MenorSemResponsavel =>
            "Informe ao menos um responsável para o aluno menor de idade.",
        EstadoMatriculas.AlunoDuplicado or EstadoMatriculas.ResponsavelDuplicado =>
            "Já existe um cadastro com o CPF informado.",
        EstadoMatriculas.ResponsavelInvalido =>
            "Revise os responsáveis e mantenha somente um Principal contato.",
        EstadoMatriculas.FrequenciaExcedida =>
            "A Grade ultrapassa o limite semanal do plano selecionado.",
        EstadoMatriculas.HorarioDuplicado =>
            "A Grade contém horários repetidos.",
        EstadoMatriculas.HorarioNaoElegivel =>
            "Um dos horários selecionados não está mais disponível. Revise sua Grade.",
        EstadoMatriculas.AlunoNaoEncontrado
            or EstadoMatriculas.AlunoNaoRelacionadoUnidade =>
            "O aluno selecionado não está disponível nesta unidade.",
        EstadoMatriculas.DataInvalida =>
            "A data final não pode ser anterior ao início da grade atual.",
        _ => "Não foi possível concluir a matrícula. Tente novamente."
    };

    private static string MensagemErroAlterarGrade(EstadoMatriculas estado) => estado switch
    {
        EstadoMatriculas.DataInvalida =>
            "A nova Grade deve começar após o início da Grade atual.",
        EstadoMatriculas.EstadoTerminal =>
            "Esta matrícula não está mais ativa e não pode ter a Grade alterada.",
        EstadoMatriculas.ConflitoConcorrencia =>
            "A matrícula foi alterada por outra pessoa. Revise os dados e tente novamente.",
        EstadoMatriculas.CapacidadeEsgotada
            or EstadoMatriculas.ConflitoHorarioAluno
            or EstadoMatriculas.FrequenciaExcedida
            or EstadoMatriculas.HorarioDuplicado
            or EstadoMatriculas.HorarioNaoElegivel => MensagemErro(estado),
        _ => "Não foi possível alterar a Grade. Tente novamente."
    };
}

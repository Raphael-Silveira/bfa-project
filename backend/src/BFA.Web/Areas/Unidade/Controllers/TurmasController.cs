using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Application.Unidades.Turmas;
using BFA.Domain.Acessos;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[ServiceFilter(typeof(GovernancaOperacionalUnidadeResultFilter))]
[Route("unidade/{unidadeId:guid}/turmas")]
public sealed class TurmasController(
    IUsuarioAtual usuarioAtual,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    ITurmasUnidadeConsulta consulta,
    ITurmasUnidadeServico servico,
    IAjusteHorariosTurmaServico ajusteHorariosServico,
    ITrocaProfessorTurmaServico trocaProfessorServico,
    IAuthorizationService authorizationService,
    TimeProvider timeProvider,
    ILogger<TurmasController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId, CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resultado = await consulta.ListarAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, cancellationToken);
        if (resultado.Estado == EstadoTurmasUnidade.SemAcesso) return Forbid();

        return View(new TurmasUnidadeIndexViewModel
        {
            OrganizacaoId = acesso.Contexto!.OrganizacaoId,
            UnidadeId = unidadeId,
            NomeUnidade = acesso.Contexto.Nome,
            PodeTrocarUnidade = acesso.PodeTrocar,
            Turmas = resultado.Valor ?? []
        });
    }

    [HttpGet("nova")]
    public async Task<IActionResult> Nova(
        Guid unidadeId, CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var professores = await consulta.ListarProfessoresAtivosAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, cancellationToken);
        if (professores.Estado == EstadoTurmasUnidade.SemAcesso) return Forbid();
        var hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return View(new TurmaNovaViewModel
        {
            OrganizacaoId = acesso.Contexto!.OrganizacaoId,
            UnidadeId = unidadeId,
            NomeUnidade = acesso.Contexto.Nome,
            PodeTrocarUnidade = acesso.PodeTrocar,
            Professores = professores.Valor ?? [],
            Horarios = [new() { VigenciaInicioTexto = hoje.ToString("dd/MM/yyyy") }]
        });
    }

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nova(
        Guid unidadeId, TurmaNovaViewModel model,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        await PreencherNovaAsync(model, acesso.Contexto!, acesso.PodeTrocar,
            cancellationToken);

        if (model.Horarios.Count == 0)
            ModelState.AddModelError(nameof(model.Horarios),
                "Adicione pelo menos um horário recorrente.");
        if (!model.TryCriarSolicitacao(out var solicitacao))
            ModelState.AddModelError(string.Empty,
                "Revise os dados da turma e os horários recorrentes.");
        if (!ModelState.IsValid || solicitacao is null) return View(model);

        var resultado = await servico.CriarAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, solicitacao, cancellationToken);
        if (resultado.Estado == EstadoTurmasUnidade.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Turmas", "Nova", resultado.Estado);
            return Forbid();
        }
        if (resultado.Estado is EstadoTurmasUnidade.ProfessorNaoEncontrado
            or EstadoTurmasUnidade.ProfessorInativo)
        {
            ModelState.AddModelError(nameof(model.ProfessorUnidadeId),
                "Selecione um professor ativo vinculado a esta unidade.");
            return View(model);
        }
        if (resultado.Estado == EstadoTurmasUnidade.ConflitoHorario)
        {
            ModelState.AddModelError(nameof(model.Horarios),
                MensagemConflito(resultado.Conflito,
                    model.Professores.FirstOrDefault(item =>
                        item.ProfessorUnidadeId == model.ProfessorUnidadeId)?.NomeCompleto));
            return View(model);
        }
        if (resultado.Estado != EstadoTurmasUnidade.Sucesso)
        {
            ModelState.AddModelError(string.Empty,
                "Não foi possível cadastrar a turma. Revise os dados e tente novamente.");
            return View(model);
        }

        TempData["Sucesso"] = "Turma cadastrada com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Turmas", "Nova", (object?)null);
        return Redirect($"/unidade/{unidadeId:D}/turmas");
    }

    [HttpGet("{turmaId:guid}/editar")]
    public async Task<IActionResult> Editar(
        Guid unidadeId, Guid turmaId, CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resultado = await consulta.ObterEdicaoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId, cancellationToken);
        if (resultado.Estado == EstadoTurmasUnidade.SemAcesso) return Forbid();
        if (resultado.Valor is null) return NotFound();
        return View(MapearEdicao(resultado.Valor, acesso.Contexto!, acesso.PodeTrocar));
    }

    [HttpPost("{turmaId:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        Guid unidadeId, Guid turmaId, TurmaEditarViewModel model,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var atual = await consulta.ObterEdicaoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId, cancellationToken);
        if (atual.Estado == EstadoTurmasUnidade.SemAcesso) return Forbid();
        if (atual.Valor is null) return NotFound();
        PreencherEdicao(model, atual.Valor, acesso.Contexto!, acesso.PodeTrocar);
        if (!ModelState.IsValid) return View(model);

        var resultado = await servico.AtualizarAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId,
            new(model.Nome, model.Capacidade!.Value), cancellationToken);
        if (resultado.Estado == EstadoTurmasUnidade.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Turmas", "Editar", resultado.Estado);
            return Forbid();
        }
        if (resultado.Estado == EstadoTurmasUnidade.TurmaNaoEncontrada) return NotFound();
        if (resultado.Estado != EstadoTurmasUnidade.Sucesso)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível atualizar a turma.");
            return View(model);
        }
        TempData["Sucesso"] = "Turma atualizada com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Turmas", "Editar", turmaId);
        return Redirect($"/unidade/{unidadeId:D}/turmas");
    }

    [HttpGet("{turmaId:guid}/horarios")]
    public async Task<IActionResult> Horarios(
        Guid unidadeId, Guid turmaId, CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resultado = await ajusteHorariosServico.ObterAdministracaoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId, cancellationToken);
        if (resultado.Estado == EstadoAjusteHorariosTurma.SemAcesso) return Forbid();
        if (resultado.Valor is null) return NotFound();
        return View(MapearHorarios(resultado.Valor, acesso.Contexto!,
            acesso.PodeTrocar, resultado.MenorVigenciaPermitida, false));
    }

    [HttpPost("{turmaId:guid}/horarios")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Horarios(
        Guid unidadeId, Guid turmaId,
        BFA.Web.ViewModels.Professor.AjustarHorariosTurmaViewModel model,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var atual = await ajusteHorariosServico.ObterAdministracaoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId, cancellationToken);
        if (atual.Estado == EstadoAjusteHorariosTurma.SemAcesso) return Forbid();
        if (atual.Valor is null) return NotFound();
        PreencherHorarios(model, atual.Valor, acesso.Contexto!, acesso.PodeTrocar, false);
        if (!model.TryCriarSolicitacao(out var solicitacao) || solicitacao is null)
            ModelState.AddModelError(string.Empty,
                "Revise a data e os horários da nova programação.");
        if (!ModelState.IsValid) return View(model);
        var resultado = await ajusteHorariosServico.AjustarAdministracaoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId,
            solicitacao!, cancellationToken);
        if (resultado.Estado == EstadoAjusteHorariosTurma.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Turmas", "Horarios", resultado.Estado);
            return Forbid();
        }
        if (resultado.Estado == EstadoAjusteHorariosTurma.TurmaNaoEncontrada)
            return NotFound();
        if (AdicionarErroAjuste(ModelState, resultado)) return View(model);
        TempData["Sucesso"] = "Programação recorrente ajustada com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Turmas", "Horarios", turmaId);
        return Redirect($"/unidade/{unidadeId:D}/turmas");
    }

    [HttpGet("{turmaId:guid}/professor")]
    public async Task<IActionResult> Professor(
        Guid unidadeId, Guid turmaId, CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resultado = await trocaProfessorServico.ObterAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId, cancellationToken);
        if (resultado.Estado == EstadoTrocaProfessorTurma.SemAcesso) return Forbid();
        if (resultado.Valor is null) return NotFound();
        return View(MapearTroca(resultado.Valor, acesso.Contexto!,
            acesso.PodeTrocar, resultado.MenorDataTroca));
    }

    [HttpPost("{turmaId:guid}/professor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Professor(
        Guid unidadeId, Guid turmaId, TrocarProfessorTurmaViewModel model,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var atual = await trocaProfessorServico.ObterAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId, cancellationToken);
        if (atual.Estado == EstadoTrocaProfessorTurma.SemAcesso) return Forbid();
        if (atual.Valor is null) return NotFound();
        PreencherTroca(model, atual.Valor, acesso.Contexto!, acesso.PodeTrocar);
        if (!model.TryCriarSolicitacao(out var solicitacao) || solicitacao is null)
            ModelState.AddModelError(string.Empty,
                "Selecione o novo professor e informe uma data válida.");
        if (!ModelState.IsValid) return View(model);
        var resultado = await trocaProfessorServico.TrocarAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, turmaId,
            solicitacao!, cancellationToken);
        if (resultado.Estado == EstadoTrocaProfessorTurma.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Turmas", "Professor", resultado.Estado);
            return Forbid();
        }
        if (resultado.Estado == EstadoTrocaProfessorTurma.TurmaNaoEncontrada)
            return NotFound();
        if (resultado.Estado != EstadoTrocaProfessorTurma.Sucesso)
        {
            var mensagem = resultado.Estado switch
            {
                EstadoTrocaProfessorTurma.ProfessorNaoEncontrado =>
                    "Selecione um professor ativo vinculado a esta unidade.",
                EstadoTrocaProfessorTurma.MesmoProfessor =>
                    "Selecione um professor diferente do responsável atual.",
                EstadoTrocaProfessorTurma.VigenciaInvalida =>
                    $"A troca deve ocorrer em {resultado.MenorDataTroca:dd/MM/yyyy} ou depois.",
                EstadoTrocaProfessorTurma.ConflitoHorario =>
                    "O novo professor já possui outra turma nesse horário.",
                EstadoTrocaProfessorTurma.MigracaoGradeInvalida =>
                    "Não foi possível migrar a Grade dos alunos. Nenhuma alteração foi realizada.",
                _ => "Não foi possível trocar o professor. Tente novamente."
            };
            ModelState.AddModelError(string.Empty, mensagem);
            return View(model);
        }
        TempData["Sucesso"] = resultado.GradesMigradas == 0
            ? "Professor responsável alterado com sucesso."
            : $"Professor alterado com sucesso. {resultado.HorariosMigrados} horário(s) e "
                + $"{resultado.GradesMigradas} alocação(ões) de Grade migrados.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Turmas", "Professor", turmaId);
        return Redirect($"/unidade/{unidadeId:D}/turmas");
    }

    private async Task<(UnidadeContextoResumo? Contexto, bool PodeTrocar,
        IActionResult? Resultado)> ValidarAcessoAsync(
        Guid unidadeId, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return (null, false, Forbid());
        var contexto = await unidadeContextoConsulta.ObterAtivaAsync(
            unidadeId, cancellationToken);
        if (contexto is null) return (null, false, NotFound());
        var autorizacao = await authorizationService.AuthorizeAsync(
            User, new ContextoUnidade(contexto.OrganizacaoId, unidadeId),
            new AcessoUnidadePorPerfilRequirement(PerfilAcesso.AdministradorUnidade));
        if (!autorizacao.Succeeded) return (null, false, Forbid());
        var unidades = await unidadesUsuarioConsulta.ListarAdministradasAsync(
            usuarioId, cancellationToken);
        return (contexto, unidades.Count > 1, null);
    }

    private async Task PreencherNovaAsync(
        TurmaNovaViewModel model, UnidadeContextoResumo contexto,
        bool podeTrocar, CancellationToken cancellationToken)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.NomeUnidade = contexto.Nome;
        model.PodeTrocarUnidade = podeTrocar;
        var professores = await consulta.ListarProfessoresAtivosAsync(
            usuarioAtual.UsuarioId!.Value, contexto.UnidadeId, cancellationToken);
        model.Professores = professores.Valor ?? [];
    }

    private static TurmaEditarViewModel MapearEdicao(
        TurmaEdicaoResumo turma, UnidadeContextoResumo contexto, bool podeTrocar)
    {
        var model = new TurmaEditarViewModel
        {
            Nome = turma.Nome,
            Capacidade = turma.Capacidade
        };
        PreencherEdicao(model, turma, contexto, podeTrocar);
        return model;
    }

    private static void PreencherEdicao(
        TurmaEditarViewModel model, TurmaEdicaoResumo turma,
        UnidadeContextoResumo contexto, bool podeTrocar)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.TurmaId = turma.Id;
        model.NomeUnidade = contexto.Nome;
        model.PodeTrocarUnidade = podeTrocar;
        model.NomeProfessor = turma.NomeProfessor;
        model.Horarios = turma.Horarios;
    }

    private static string MensagemConflito(
        ConflitoHorarioProfessor? conflito, string? nomeProfessor)
    {
        var nome = conflito?.NomeProfessor ?? nomeProfessor ?? "O professor selecionado";
        if (conflito is null) return $"{nome} já possui outra turma nesse horário.";
        return $"{nome} já possui outra turma nesse horário: {NomeDia(conflito.DiaSemana)}, "
            + $"{conflito.HoraInicio:HH\\:mm}–{conflito.HoraFim:HH\\:mm}, "
            + $"{conflito.NomeTurma} em {conflito.NomeUnidade}.";
    }

    private static string NomeDia(BFA.Domain.Turmas.DiaSemana dia) => dia switch
    {
        BFA.Domain.Turmas.DiaSemana.Segunda => "segunda-feira",
        BFA.Domain.Turmas.DiaSemana.Terca => "terça-feira",
        BFA.Domain.Turmas.DiaSemana.Quarta => "quarta-feira",
        BFA.Domain.Turmas.DiaSemana.Quinta => "quinta-feira",
        BFA.Domain.Turmas.DiaSemana.Sexta => "sexta-feira",
        BFA.Domain.Turmas.DiaSemana.Sabado => "sábado",
        _ => "domingo"
    };

    private static BFA.Web.ViewModels.Professor.AjustarHorariosTurmaViewModel
        MapearHorarios(ProgramacaoTurmaResumo turma, UnidadeContextoResumo contexto,
            bool podeTrocar, DateOnly? menorVigencia, bool areaProfessor)
    {
        var model = new BFA.Web.ViewModels.Professor.AjustarHorariosTurmaViewModel
        {
            NovaVigenciaInicioTexto = menorVigencia?.ToString("dd/MM/yyyy") ?? string.Empty,
            Horarios = turma.HorariosAtuais.Select(item =>
                new BFA.Web.ViewModels.Professor.NovoHorarioTurmaFormViewModel
                {
                    DiaSemana = item.DiaSemana,
                    HoraInicio = item.HoraInicio.ToString("HH:mm"),
                    HoraFim = item.HoraFim.ToString("HH:mm")
                }).ToList()
        };
        PreencherHorarios(model, turma, contexto, podeTrocar, areaProfessor);
        return model;
    }

    private static void PreencherHorarios(
        BFA.Web.ViewModels.Professor.AjustarHorariosTurmaViewModel model,
        ProgramacaoTurmaResumo turma, UnidadeContextoResumo contexto,
        bool podeTrocar, bool areaProfessor)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.TurmaId = turma.TurmaId;
        model.NomeUnidade = contexto.Nome;
        model.PodeTrocarUnidade = podeTrocar;
        model.NomeTurma = turma.NomeTurma;
        model.NomeProfessor = turma.NomeProfessor;
        model.AreaProfessor = areaProfessor;
        model.HorariosAtuais = turma.HorariosAtuais;
        if (model.Horarios.Count == 0) model.Horarios.Add(new());
    }

    internal static bool AdicionarErroAjuste(
        Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState,
        ResultadoAjusteHorariosTurma<Guid> resultado)
    {
        if (resultado.Estado == EstadoAjusteHorariosTurma.Sucesso) return false;
        var mensagem = resultado.Estado switch
        {
            EstadoAjusteHorariosTurma.VigenciaInvalida =>
                $"A nova programação deve iniciar em "
                + $"{resultado.MenorVigenciaPermitida:dd/MM/yyyy} ou depois.",
            EstadoAjusteHorariosTurma.ConflitoHorario =>
                MensagemConflito(resultado.Conflito, null),
            EstadoAjusteHorariosTurma.ExisteGradeAfetada =>
                "Este horário possui alunos alocados. Ajuste a Grade dos alunos antes de alterá-lo ou removê-lo.",
            EstadoAjusteHorariosTurma.SemHorarios =>
                "Adicione pelo menos um horário à nova programação.",
            _ => "Não foi possível ajustar os horários. Revise os dados e tente novamente."
        };
        modelState.AddModelError(string.Empty, mensagem);
        return true;
    }

    private static TrocarProfessorTurmaViewModel MapearTroca(
        TrocaProfessorTurmaResumo turma, UnidadeContextoResumo contexto,
        bool podeTrocar, DateOnly? menorData)
    {
        var model = new TrocarProfessorTurmaViewModel
        {
            DataTrocaTexto = menorData?.ToString("dd/MM/yyyy") ?? string.Empty
        };
        PreencherTroca(model, turma, contexto, podeTrocar);
        return model;
    }

    private static void PreencherTroca(
        TrocarProfessorTurmaViewModel model, TrocaProfessorTurmaResumo turma,
        UnidadeContextoResumo contexto, bool podeTrocar)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.TurmaId = turma.TurmaId;
        model.NomeUnidade = contexto.Nome;
        model.PodeTrocarUnidade = podeTrocar;
        model.NomeTurma = turma.NomeTurma;
        model.NomeProfessorAtual = turma.NomeProfessorAtual;
        model.HorariosAtuais = turma.HorariosAtuais;
        model.ProfessoresDisponiveis = turma.ProfessoresDisponiveis;
    }
}

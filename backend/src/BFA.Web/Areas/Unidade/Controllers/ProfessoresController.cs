using BFA.Application.Acessos;
using BFA.Application.Unidades;
using BFA.Application.Unidades.Professores;
using BFA.Domain.Acessos;
using BFA.Domain.Professores;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Unidade;
using BFA.Web.Identidade;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BFA.Web.Areas.Unidade.Controllers;

[Area("Unidade")]
[Authorize]
[ServiceFilter(typeof(GovernancaOperacionalUnidadeResultFilter))]
[Route("unidade/{unidadeId:guid}/professores")]
public sealed class ProfessoresController(
    IUsuarioAtual usuarioAtual,
    IUnidadeContextoConsulta unidadeContextoConsulta,
    IUnidadesUsuarioConsulta unidadesUsuarioConsulta,
    IProfessoresUnidadeConsulta consulta,
    IProfessoresUnidadeServico servico,
    IAcessoProfessorServico acessoProfessorServico,
    IAuthorizationService authorizationService,
    TimeProvider timeProvider,
    ILogger<ProfessoresController> logger) : Controller
{
    [HttpGet("{professorId:guid}/acesso")]
    public async Task<IActionResult> Acesso(
        Guid unidadeId, Guid professorId, CancellationToken cancellationToken)
    {
        var acessoUnidade = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acessoUnidade.Resultado is not null) return acessoUnidade.Resultado;
        var resultado = await acessoProfessorServico.ObterAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, professorId, cancellationToken);
        if (resultado.Estado == EstadoAcessoProfessor.SemAcesso) return Forbid();
        if (resultado.Acesso is null) return NotFound();
        return View(MapearAcesso(resultado.Acesso, acessoUnidade.Contexto!, acessoUnidade.PodeTrocar));
    }

    [HttpPost("{professorId:guid}/acesso")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acesso(
        Guid unidadeId,
        Guid professorId,
        ProfessorAcessoViewModel model,
        CancellationToken cancellationToken)
    {
        var acessoUnidade = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acessoUnidade.Resultado is not null) return acessoUnidade.Resultado;
        var atual = await acessoProfessorServico.ObterAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, professorId, cancellationToken);
        if (atual.Estado == EstadoAcessoProfessor.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Professores", "Acesso", atual.Estado);
            return Forbid();
        }
        if (atual.Acesso is null) return NotFound();
        PreencherAcesso(model, atual.Acesso, acessoUnidade.Contexto!, acessoUnidade.PodeTrocar);
        if (!ModelState.IsValid) return View(model);

        var resultado = await acessoProfessorServico.ConcederAsync(
            usuarioAtual.UsuarioId.Value,
            unidadeId,
            professorId,
            model.NomeUsuario,
            cancellationToken);
        if (resultado.Estado == EstadoAcessoProfessor.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Professores", "Acesso", resultado.Estado);
            return Forbid();
        }
        if (resultado.Estado == EstadoAcessoProfessor.ProfessorNaoEncontrado) return NotFound();
        if (resultado.Estado is EstadoAcessoProfessor.NomeUsuarioDuplicado
            or EstadoAcessoProfessor.NomeUsuarioInvalido)
        {
            ModelState.AddModelError(nameof(model.NomeUsuario),
                resultado.Estado == EstadoAcessoProfessor.NomeUsuarioDuplicado
                    ? "Este nome de usuário já está em uso."
                    : "Informe um nome de usuário válido.");
            return View(model);
        }
        if (resultado.Estado == EstadoAcessoProfessor.VinculoProfissionalInativo)
        {
            ModelState.AddModelError(string.Empty,
                "O vínculo profissional precisa estar ativo para conceder acesso.");
            return View(model);
        }
        if (resultado.Estado != EstadoAcessoProfessor.Sucesso)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível conceder o acesso.");
            return View(model);
        }

        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Professores", "Acesso", professorId);

        string? link = null;
        if (resultado.UsuarioId is { } usuarioId
            && !string.IsNullOrWhiteSpace(resultado.TokenDefinicaoSenha))
        {
            link = Url.Action(
                "DefinirSenha",
                "PrimeiroAcesso",
                new
                {
                    usuarioId,
                    token = TokenPrimeiroAcesso.Codificar(resultado.TokenDefinicaoSenha)
                },
                Request.Scheme);
        }

        return View("AcessoConcedido", new ProfessorAcessoConcedidoViewModel(
            unidadeId,
            acessoUnidade.Contexto!.Nome,
            acessoUnidade.PodeTrocar,
            atual.Acesso.NomeCompleto,
            resultado.NomeUsuario ?? model.NomeUsuario.Trim(),
            link));
    }

    [HttpPost("{professorId:guid}/acesso/revogar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevogarAcesso(
        Guid unidadeId, Guid professorId, CancellationToken cancellationToken)
    {
        var acessoUnidade = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acessoUnidade.Resultado is not null) return acessoUnidade.Resultado;
        var estado = await acessoProfessorServico.RevogarAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, professorId, cancellationToken);
        if (estado == EstadoAcessoProfessor.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Professores", "RevogarAcesso", estado);
            return Forbid();
        }
        if (estado is EstadoAcessoProfessor.ProfessorNaoEncontrado
            or EstadoAcessoProfessor.AcessoNaoEncontrado) return NotFound();
        if (estado != EstadoAcessoProfessor.Sucesso)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        TempData["Sucesso"] = "Acesso do professor revogado nesta unidade.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Professores", "RevogarAcesso", professorId);
        return Redirect($"/unidade/{unidadeId:D}/professores");
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid unidadeId,
        string? filtro,
        string? termo,
        int? pagina,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;

        var filtroAplicado = filtro?.ToLowerInvariant() switch
        {
            "encerrados" => FiltroProfessoresUnidade.Encerrados,
            "todos" => FiltroProfessoresUnidade.Todos,
            _ => FiltroProfessoresUnidade.Ativos
        };

        var resultadoTodos = await consulta.ListarAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, FiltroProfessoresUnidade.Todos, cancellationToken);
        if (resultadoTodos.Estado == EstadoProfessoresUnidade.SemAcesso) return Forbid();

        var todos = resultadoTodos.Valor ?? [];
        var totalAtivos = todos.Count(p => p.VinculoAtivo);
        var totalEncerrados = todos.Count(p => !p.VinculoAtivo);

        IEnumerable<ProfessorUnidadeResumo> filtrados = filtroAplicado switch
        {
            FiltroProfessoresUnidade.Ativos => todos.Where(p => p.VinculoAtivo),
            FiltroProfessoresUnidade.Encerrados => todos.Where(p => !p.VinculoAtivo),
            _ => todos
        };

        if (!string.IsNullOrWhiteSpace(termo))
        {
            var termoNormalizado = termo.Trim().ToLowerInvariant();
            filtrados = filtrados.Where(p =>
                (p.NomeCompleto?.ToLowerInvariant().Contains(termoNormalizado) == true) ||
                (p.Cpf?.Contains(termoNormalizado) == true) ||
                (p.Email?.ToLowerInvariant().Contains(termoNormalizado) == true) ||
                (p.Telefone?.Contains(termoNormalizado) == true));
        }

        var listaFiltrada = filtrados.ToList();
        var totalItens = listaFiltrada.Count;
        const int tamanhoPagina = 10;
        var paginaAtual = Math.Max(1, pagina ?? 1);
        var totalPaginas = (int)Math.Ceiling((double)totalItens / tamanhoPagina);
        if (paginaAtual > totalPaginas && totalPaginas > 0) paginaAtual = totalPaginas;

        var itensPagina = listaFiltrada
            .Skip((paginaAtual - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToList();

        return View(new ProfessoresUnidadeIndexViewModel
        {
            OrganizacaoId = acesso.Contexto!.OrganizacaoId,
            UnidadeId = unidadeId,
            NomeUnidade = acesso.Contexto.Nome,
            PodeTrocarUnidade = acesso.PodeTrocar,
            Professores = itensPagina,
            Filtro = filtroAplicado,
            TermoBusca = termo,
            PaginaAtual = paginaAtual,
            TamanhoPagina = tamanhoPagina,
            TotalItens = totalItens,
            TotalAtivos = totalAtivos,
            TotalEncerrados = totalEncerrados
        });
    }

    [HttpGet("novo")]
    public async Task<IActionResult> Novo(Guid unidadeId, CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;

        return View(new ProfessorUnidadeNovoViewModel
        {
            OrganizacaoId = acesso.Contexto!.OrganizacaoId,
            UnidadeId = unidadeId,
            NomeUnidade = acesso.Contexto.Nome,
            PodeTrocarUnidade = acesso.PodeTrocar,
            Modalidade = ModalidadeRemuneracaoProfessor.Mensal,
            VigenciaInicioTexto = DateOnly.FromDateTime(
                timeProvider.GetLocalNow().DateTime).ToString("dd/MM/yyyy")
        });
    }

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(
        Guid unidadeId,
        ProfessorUnidadeNovoViewModel model,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        PreencherContexto(model, acesso.Contexto!, acesso.PodeTrocar);

        if (!model.TryCriarSolicitacao(out var solicitacao))
        {
            ModelState.AddModelError(string.Empty, "Revise a remuneração e a data de vigência.");
        }

        if (!ModelState.IsValid || solicitacao is null) return View(model);

        var resultado = await servico.CriarAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, solicitacao, cancellationToken);
        if (resultado.Estado == EstadoProfessoresUnidade.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Professores", "Novo", resultado.Estado);
            return Forbid();
        }
        if (resultado.Estado == EstadoProfessoresUnidade.CpfDuplicado)
        {
            model.CpfJaCadastradoNaRede = true;
            ModelState.AddModelError(nameof(model.Cpf),
                "Este professor já está cadastrado na rede.");
            return View(model);
        }
        if (resultado.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível cadastrar o professor. Revise os dados.");
            return View(model);
        }

        TempData["Sucesso"] = "Professor cadastrado com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Professores", "Novo", (object?)null);
        return Redirect($"/unidade/{unidadeId:D}/professores");
    }

    [HttpGet("vincular")]
    public async Task<IActionResult> Vincular(
        Guid unidadeId,
        string? termo,
        Guid? professorId,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;

        var model = CriarModeloVinculo(acesso.Contexto!, acesso.PodeTrocar);
        model.Termo = termo;
        model.ProfessorId = professorId;

        if (professorId.HasValue)
        {
            var selecionado = await consulta.ObterExistenteAsync(
                usuarioAtual.UsuarioId!.Value, unidadeId, professorId.Value, cancellationToken);
            if (selecionado.Estado == EstadoProfessoresUnidade.ProfessorNaoEncontrado)
            {
                return NotFound();
            }
            if (selecionado.Estado == EstadoProfessoresUnidade.SemAcesso) return Forbid();
            model.ProfessorSelecionado = selecionado.Valor;
            PreencherOrientacaoReativacao(model, sobrescreverVigencia: true);
        }
        else if (!string.IsNullOrWhiteSpace(termo))
        {
            var busca = await consulta.BuscarExistentesAsync(
                usuarioAtual.UsuarioId!.Value, unidadeId, termo, cancellationToken);
            if (busca.Estado == EstadoProfessoresUnidade.SemAcesso) return Forbid();
            model.Resultados = busca.Valor ?? [];
        }

        return View(model);
    }

    [HttpPost("vincular")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vincular(
        Guid unidadeId,
        ProfessorUnidadeVincularViewModel model,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        PreencherContexto(model, acesso.Contexto!, acesso.PodeTrocar);

        if (model.ProfessorId is { } professorId)
        {
            var selecionado = await consulta.ObterExistenteAsync(
                usuarioAtual.UsuarioId!.Value, unidadeId, professorId, cancellationToken);
            if (selecionado.Estado == EstadoProfessoresUnidade.ProfessorNaoEncontrado)
            {
                return NotFound();
            }
            model.ProfessorSelecionado = selecionado.Valor;
            PreencherOrientacaoReativacao(model, sobrescreverVigencia: false);
        }

        if (!model.TryCriarSolicitacao(out var solicitacao))
        {
            ModelState.AddModelError(string.Empty, "Revise a remuneração e a data de vigência.");
        }

        if (!ModelState.IsValid || solicitacao is null) return View(model);
        var resultado = await servico.VincularExistenteAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, solicitacao, cancellationToken);
        if (resultado.Estado == EstadoProfessoresUnidade.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Professores", "Vincular", resultado.Estado);
            return Forbid();
        }
        if (resultado.Estado == EstadoProfessoresUnidade.ProfessorNaoEncontrado) return NotFound();
        if (resultado.Estado == EstadoProfessoresUnidade.ProfessorInativo)
        {
            ModelState.AddModelError(string.Empty,
                "O professor está inativo e não pode ser vinculado.");
            return View(model);
        }
        if (resultado.Estado == EstadoProfessoresUnidade.JaVinculado)
        {
            ModelState.AddModelError(string.Empty,
                "Este professor já está vinculado a esta unidade.");
            return View(model);
        }
        if (resultado.Estado == EstadoProfessoresUnidade.VigenciaInicioInvalida)
        {
            var termino = model.ProfessorSelecionado?.UltimaVigenciaFim;
            ModelState.AddModelError(nameof(model.VigenciaInicioTexto), termino is { } data
                ? $"A nova remuneração deve iniciar após o término da remuneração anterior ({data:dd/MM/yyyy})."
                : "A nova remuneração deve iniciar após o término da remuneração anterior.");
            return View(model);
        }
        if (resultado.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível vincular o professor.");
            return View(model);
        }

        TempData["Sucesso"] = "Professor vinculado à unidade com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Professores", "Vincular", (object?)null);
        return Redirect($"/unidade/{unidadeId:D}/professores");
    }

    [HttpGet("{professorId:guid}/editar")]
    public async Task<IActionResult> Editar(
        Guid unidadeId, Guid professorId, CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resultado = await consulta.ObterGerenciamentoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, professorId, cancellationToken);
        if (resultado.Estado == EstadoProfessoresUnidade.VinculoNaoEncontrado) return NotFound();
        var professor = resultado.Valor!;
        return View(new ProfessorUnidadeEditarViewModel
        {
            OrganizacaoId = acesso.Contexto!.OrganizacaoId,
            UnidadeId = unidadeId,
            ProfessorId = professorId,
            NomeUnidade = acesso.Contexto.Nome,
            PodeTrocarUnidade = acesso.PodeTrocar,
            NomeCompleto = professor.NomeCompleto,
            Cpf = professor.Cpf,
            Telefone = professor.Telefone,
            Email = professor.Email
        });
    }

    [HttpGet("{professorId:guid}/remuneracao")]
    public async Task<IActionResult> Remuneracao(
        Guid unidadeId, Guid professorId, CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resultado = await consulta.ObterRemuneracaoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, professorId, cancellationToken);
        if (resultado.Estado == EstadoProfessoresUnidade.VinculoNaoEncontrado
            || resultado.Valor is not { VinculoAtivo: true, RemuneracaoAtual: not null })
        {
            return NotFound();
        }

        var model = new ProfessorRemuneracaoAlterarViewModel();
        PreencherRemuneracao(
            model, acesso.Contexto!, acesso.PodeTrocar, resultado.Valor, sobrescreverCampos: true);
        return View(model);
    }

    [HttpPost("{professorId:guid}/remuneracao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remuneracao(
        Guid unidadeId,
        Guid professorId,
        ProfessorRemuneracaoAlterarViewModel model,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resumo = await consulta.ObterRemuneracaoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, professorId, cancellationToken);
        if (resumo.Estado == EstadoProfessoresUnidade.VinculoNaoEncontrado
            || resumo.Valor is not { VinculoAtivo: true, RemuneracaoAtual: not null })
        {
            return NotFound();
        }

        PreencherRemuneracao(
            model, acesso.Contexto!, acesso.PodeTrocar, resumo.Valor, sobrescreverCampos: false);
        if (!model.TryCriarSolicitacao(out var solicitacao))
        {
            ModelState.AddModelError(string.Empty, "Revise a nova remuneração e a data de vigência.");
        }
        if (!ModelState.IsValid || solicitacao is null) return View(model);

        var resultado = await servico.AlterarRemuneracaoAsync(
            usuarioAtual.UsuarioId!.Value,
            unidadeId,
            professorId,
            solicitacao,
            cancellationToken);
        if (resultado.Estado == EstadoProfessoresUnidade.SemAcesso)
        {
            logger.LogWarning("{Controller} {Action} negado: {Estado}", "Professores", "Remuneracao", resultado.Estado);
            return Forbid();
        }
        if (resultado.Estado is EstadoProfessoresUnidade.VinculoNaoEncontrado
            or EstadoProfessoresUnidade.VinculoJaEncerrado
            or EstadoProfessoresUnidade.RemuneracaoNaoEncontrada)
        {
            return NotFound();
        }
        if (resultado.Estado == EstadoProfessoresUnidade.VigenciaInicioInvalida)
        {
            ModelState.AddModelError(nameof(model.VigenciaInicioTexto),
                $"A nova remuneração deve iniciar após {resumo.Valor.RemuneracaoAtual.VigenciaInicio:dd/MM/yyyy}.");
            return View(model);
        }
        if (resultado.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível alterar a remuneração.");
            return View(model);
        }

        TempData["Sucesso"] = "Nova remuneração cadastrada e histórico preservado.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Professores", "Remuneracao", professorId);
        return Redirect($"/unidade/{unidadeId:D}/professores/{professorId:D}/remuneracao");
    }

    [HttpPost("{professorId:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        Guid unidadeId,
        Guid professorId,
        ProfessorUnidadeEditarViewModel model,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        PreencherContexto(model, acesso.Contexto!, acesso.PodeTrocar, professorId);
        if (!ModelState.IsValid) return View(model);
        var resultado = await servico.AtualizarCadastroAsync(
            usuarioAtual.UsuarioId!.Value,
            unidadeId,
            professorId,
            new(model.NomeCompleto, model.Cpf, model.Telefone, model.Email),
            cancellationToken);
        if (resultado.Estado == EstadoProfessoresUnidade.VinculoNaoEncontrado) return NotFound();
        if (resultado.Estado == EstadoProfessoresUnidade.CpfDuplicado)
        {
            ModelState.AddModelError(nameof(model.Cpf), "Este CPF já pertence a outro professor da rede.");
            return View(model);
        }
        if (resultado.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível atualizar o professor.");
            return View(model);
        }
        TempData["Sucesso"] = "Dados cadastrais atualizados com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Professores", "Editar", professorId);
        return Redirect($"/unidade/{unidadeId:D}/professores");
    }

    [HttpGet("{professorId:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(
        Guid unidadeId, Guid professorId, CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resultado = await consulta.ObterGerenciamentoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, professorId, cancellationToken);
        if (resultado.Estado == EstadoProfessoresUnidade.VinculoNaoEncontrado) return NotFound();
        return View(MapearEncerramento(
            acesso.Contexto!, acesso.PodeTrocar, resultado.Valor!));
    }

    [HttpPost("{professorId:guid}/encerrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Encerrar(
        Guid unidadeId,
        Guid professorId,
        ProfessorUnidadeEncerrarViewModel model,
        CancellationToken cancellationToken)
    {
        var acesso = await ValidarAcessoAsync(unidadeId, cancellationToken);
        if (acesso.Resultado is not null) return acesso.Resultado;
        var resumo = await consulta.ObterGerenciamentoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, professorId, cancellationToken);
        if (resumo.Estado == EstadoProfessoresUnidade.VinculoNaoEncontrado) return NotFound();
        PreencherEncerramento(model, acesso.Contexto!, acesso.PodeTrocar, resumo.Valor!);
        if (!model.TryObterData(out var data))
        {
            ModelState.AddModelError(nameof(model.DataEncerramentoTexto),
                "Informe uma data válida no formato dd/mm/aaaa.");
        }
        if (!ModelState.IsValid) return View(model);
        var resultado = await servico.EncerrarVinculoAsync(
            usuarioAtual.UsuarioId!.Value, unidadeId, professorId, data, cancellationToken);
        if (resultado.Estado == EstadoProfessoresUnidade.VinculoJaEncerrado)
        {
            ModelState.AddModelError(string.Empty, "Este vínculo já está encerrado.");
            model.VinculoAtivo = false;
            return View(model);
        }
        if (resultado.Estado == EstadoProfessoresUnidade.DataEncerramentoInvalida)
        {
            ModelState.AddModelError(nameof(model.DataEncerramentoTexto),
                "A data de encerramento não pode ser anterior ao início da remuneração atual.");
            return View(model);
        }
        if (resultado.Estado != EstadoProfessoresUnidade.Sucesso)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível encerrar o vínculo.");
            return View(model);
        }
        TempData["Sucesso"] = "Vínculo profissional encerrado com sucesso.";
        logger.LogInformation("{Controller} {Action} concluído: {EntityId}", "Professores", "Encerrar", professorId);
        return Redirect($"/unidade/{unidadeId:D}/professores?filtro=encerrados");
    }

    private async Task<(UnidadeContextoResumo? Contexto, bool PodeTrocar, IActionResult? Resultado)>
        ValidarAcessoAsync(Guid unidadeId, CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId) return (null, false, Forbid());
        var contexto = await unidadeContextoConsulta.ObterAtivaAsync(unidadeId, cancellationToken);
        if (contexto is null) return (null, false, NotFound());
        var autorizacao = await authorizationService.AuthorizeAsync(
            User,
            new ContextoUnidade(contexto.OrganizacaoId, unidadeId),
            new AcessoUnidadePorPerfilRequirement(PerfilAcesso.AdministradorUnidade));
        if (!autorizacao.Succeeded) return (null, false, Forbid());
        var unidades = await unidadesUsuarioConsulta.ListarAdministradasAsync(
            usuarioId, cancellationToken);
        return (contexto, unidades.Count > 1, null);
    }

    private static void PreencherContexto(
        ProfessorUnidadeNovoViewModel model, UnidadeContextoResumo contexto, bool podeTrocar)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.NomeUnidade = contexto.Nome;
        model.PodeTrocarUnidade = podeTrocar;
    }

    private static ProfessorAcessoViewModel MapearAcesso(
        AcessoProfessorResumo acesso,
        UnidadeContextoResumo contexto,
        bool podeTrocar)
    {
        var model = new ProfessorAcessoViewModel();
        PreencherAcesso(model, acesso, contexto, podeTrocar);
        return model;
    }

    private static void PreencherAcesso(
        ProfessorAcessoViewModel model,
        AcessoProfessorResumo acesso,
        UnidadeContextoResumo contexto,
        bool podeTrocar)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.ProfessorId = acesso.ProfessorId;
        model.NomeUnidade = contexto.Nome;
        model.PodeTrocarUnidade = podeTrocar;
        model.NomeProfessor = acesso.NomeCompleto;
        model.Email = acesso.Email;
        model.UsuarioExistente = acesso.UsuarioId.HasValue;
        if (acesso.UsuarioId.HasValue)
        {
            model.NomeUsuario = acesso.NomeUsuario ?? string.Empty;
        }
    }

    private static void PreencherContexto(
        ProfessorUnidadeVincularViewModel model,
        UnidadeContextoResumo contexto,
        bool podeTrocar)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.NomeUnidade = contexto.Nome;
        model.PodeTrocarUnidade = podeTrocar;
    }

    private static void PreencherContexto(
        ProfessorUnidadeEditarViewModel model,
        UnidadeContextoResumo contexto,
        bool podeTrocar,
        Guid professorId)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.ProfessorId = professorId;
        model.NomeUnidade = contexto.Nome;
        model.PodeTrocarUnidade = podeTrocar;
    }

    private static ProfessorUnidadeEncerrarViewModel MapearEncerramento(
        UnidadeContextoResumo contexto,
        bool podeTrocar,
        ProfessorUnidadeGerenciamentoResumo professor)
    {
        var model = new ProfessorUnidadeEncerrarViewModel();
        PreencherEncerramento(model, contexto, podeTrocar, professor);
        return model;
    }

    private static void PreencherEncerramento(
        ProfessorUnidadeEncerrarViewModel model,
        UnidadeContextoResumo contexto,
        bool podeTrocar,
        ProfessorUnidadeGerenciamentoResumo professor)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.ProfessorId = professor.ProfessorId;
        model.NomeUnidade = contexto.Nome;
        model.PodeTrocarUnidade = podeTrocar;
        model.NomeProfessor = professor.NomeCompleto;
        model.VinculoAtivo = professor.VinculoAtivo;
        model.ModalidadeAtual = professor.ModalidadeAtual;
        model.ValorAtual = professor.ValorAtual;
        model.VigenciaInicioAtual = professor.VigenciaInicioAtual;
    }

    private void PreencherRemuneracao(
        ProfessorRemuneracaoAlterarViewModel model,
        UnidadeContextoResumo contexto,
        bool podeTrocar,
        ProfessorRemuneracaoGerenciamentoResumo professor,
        bool sobrescreverCampos)
    {
        model.OrganizacaoId = contexto.OrganizacaoId;
        model.UnidadeId = contexto.UnidadeId;
        model.ProfessorId = professor.ProfessorId;
        model.NomeUnidade = contexto.Nome;
        model.PodeTrocarUnidade = podeTrocar;
        model.NomeProfessor = professor.NomeCompleto;
        model.VinculoAtivo = professor.VinculoAtivo;
        model.RemuneracaoAtual = professor.RemuneracaoAtual;
        model.Historico = professor.Historico;
        if (professor.RemuneracaoAtual is not { } atual)
        {
            return;
        }

        model.VigenciaInicioMinima = atual.VigenciaInicio.AddDays(1);
        if (!sobrescreverCampos)
        {
            return;
        }

        model.Modalidade = atual.Modalidade;
        model.ValorTexto = atual.Valor.ToString(
            "N2", CultureInfo.GetCultureInfo("pt-BR"));
        var hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var sugestao = hoje > atual.VigenciaInicio
            ? hoje
            : model.VigenciaInicioMinima.Value;
        model.VigenciaInicioTexto = sugestao.ToString("dd/MM/yyyy");
    }

    private ProfessorUnidadeVincularViewModel CriarModeloVinculo(
        UnidadeContextoResumo contexto,
        bool podeTrocar) => new()
    {
        OrganizacaoId = contexto.OrganizacaoId,
        UnidadeId = contexto.UnidadeId,
        NomeUnidade = contexto.Nome,
        PodeTrocarUnidade = podeTrocar,
        Modalidade = ModalidadeRemuneracaoProfessor.Mensal,
        VigenciaInicioTexto = DateOnly.FromDateTime(
            timeProvider.GetLocalNow().DateTime).ToString("dd/MM/yyyy")
    };

    private static void PreencherOrientacaoReativacao(
        ProfessorUnidadeVincularViewModel model,
        bool sobrescreverVigencia)
    {
        if (model.ProfessorSelecionado is not
            { EstadoVinculo: EstadoVinculoProfessorExistente.Inativo,
              UltimaVigenciaFim: { } ultimaVigenciaFim })
        {
            return;
        }

        model.VigenciaInicioMinima = ultimaVigenciaFim.AddDays(1);
        if (sobrescreverVigencia)
        {
            model.VigenciaInicioTexto = model.VigenciaInicioMinima.Value.ToString("dd/MM/yyyy");
        }
    }
}

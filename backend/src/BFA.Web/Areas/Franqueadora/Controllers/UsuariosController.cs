using BFA.Application.Acessos;
using BFA.Application.Franqueadora.Usuarios;
using BFA.Web.Authorization;
using BFA.Web.Identidade;
using BFA.Web.ViewModels.Franqueadora;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Franqueadora.Controllers;

[Area("Franqueadora")]
[Authorize(Policy = PoliticasAcesso.AdministradorRede)]
[Route("franqueadora/usuarios")]
public sealed class UsuariosController(
    IUsuarioAtual usuarioAtual,
    IUsuariosFranqueadoraConsulta consulta,
    IUsuariosFranqueadoraServico servico) : Controller
{
    public const string MensagemUsuarioAtualizado = "Usuário atualizado com sucesso.";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await consulta.ListarAsync(usuarioId, cancellationToken);

        if (resultado.Estado != EstadoGerenciamentoUsuario.Sucesso
            || resultado.Valor is not { } usuarios)
        {
            return Forbid();
        }

        return View(new UsuariosFranqueadoraIndexViewModel
        {
            Usuarios = usuarios
                .Select(usuario => new UsuarioFranqueadoraItemViewModel(
                    usuario.Id,
                    usuario.Nome,
                    usuario.Email,
                    usuario.Funcoes,
                    usuario.Unidades,
                    usuario.Ativo))
                .ToArray()
        });
    }

    [HttpGet("novo")]
    public async Task<IActionResult> Novo(CancellationToken cancellationToken)
    {
        var model = new NovoUsuarioFranqueadoraViewModel();
        return await ExibirFormularioAsync(model, cancellationToken);
    }

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Novo(
        NovoUsuarioFranqueadoraViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ExibirFormularioAsync(model, cancellationToken);
        }

        if (usuarioAtual.UsuarioId is not { } usuarioId
            || model.TipoCadastro is not { } tipoCadastro)
        {
            return Forbid();
        }

        var resultado = await servico.CriarAsync(
            usuarioId,
            new CriarUsuarioFranqueadoraSolicitacao(
                tipoCadastro,
                model.NomeCompleto,
                model.Email,
                model.Telefone,
                MontarDadosFranqueado(model)),
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoUsuario.Sucesso
            && resultado.Usuario is { } usuarioCriado)
        {
            var tokenCodificado = TokenPrimeiroAcesso.Codificar(
                usuarioCriado.TokenDefinicaoSenha);
            var link = Url.Action(
                "DefinirSenha",
                "PrimeiroAcesso",
                new { usuarioId = usuarioCriado.UsuarioId, token = tokenCodificado },
                Request.Scheme);

            if (string.IsNullOrWhiteSpace(link))
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return View("Criado", new UsuarioFranqueadoraCriadoViewModel(
                usuarioCriado.NomeCompleto,
                usuarioCriado.Email,
                NomeTipo(usuarioCriado.TipoCadastro),
                link));
        }

        if (resultado.Estado is EstadoGerenciamentoUsuario.SemAcesso
            or EstadoGerenciamentoUsuario.SelecaoOrganizacaoNecessaria)
        {
            return Forbid();
        }

        var campo = resultado.Estado switch
        {
            EstadoGerenciamentoUsuario.EmailDuplicado => nameof(model.Email),
            EstadoGerenciamentoUsuario.DocumentoDuplicado => nameof(model.Documento),
            EstadoGerenciamentoUsuario.UnidadesInvalidas or
                EstadoGerenciamentoUsuario.UnidadeComFranqueadoAtivo => nameof(model.UnidadesIds),
            _ => string.Empty
        };
        ModelState.AddModelError(
            campo,
            resultado.Mensagem ?? "Não foi possível concluir o cadastro.");
        return await ExibirFormularioAsync(model, cancellationToken);
    }

    [HttpGet("{usuarioId:guid}/editar")]
    public async Task<IActionResult> Editar(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioAtualId)
        {
            return Forbid();
        }

        return await ExibirEdicaoAsync(
            usuarioAtualId,
            usuarioId,
            modelPostado: null,
            cancellationToken);
    }

    [HttpPost("{usuarioId:guid}/editar")]
    [ValidateAntiForgeryToken]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Editar(
        Guid usuarioId,
        EditarUsuarioFranqueadoraViewModel model,
        CancellationToken cancellationToken)
    {
        model.UsuarioId = usuarioId;

        if (usuarioAtual.UsuarioId is not { } usuarioAtualId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return await ExibirEdicaoAsync(
                usuarioAtualId,
                usuarioId,
                model,
                cancellationToken);
        }

        var resultado = await servico.EditarAsync(
            usuarioAtualId,
            new EditarUsuarioFranqueadoraSolicitacao(
                usuarioId,
                model.NomeCompleto,
                model.Email,
                model.Telefone),
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoUsuario.Sucesso)
        {
            TempData[nameof(MensagemUsuarioAtualizado)] = MensagemUsuarioAtualizado;
            return Redirect("/franqueadora/usuarios");
        }

        if (resultado.Estado is EstadoGerenciamentoUsuario.SemAcesso
            or EstadoGerenciamentoUsuario.SelecaoOrganizacaoNecessaria)
        {
            return Forbid();
        }

        if (resultado.Estado == EstadoGerenciamentoUsuario.UsuarioNaoEncontrado)
        {
            return NotFound();
        }

        if (resultado.Estado == EstadoGerenciamentoUsuario.UsuarioComMultiplasOrganizacoes)
        {
            return ExibirEdicaoBloqueada(usuarioId, resultado.Mensagem);
        }

        var campo = resultado.Estado == EstadoGerenciamentoUsuario.EmailDuplicado
            ? nameof(model.Email)
            : string.Empty;
        ModelState.AddModelError(
            campo,
            resultado.Mensagem ?? "Não foi possível atualizar o usuário.");
        return View("Editar", model);
    }

    private async Task<IActionResult> ExibirFormularioAsync(
        NovoUsuarioFranqueadoraViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await consulta.ListarUnidadesAsync(usuarioId, cancellationToken);

        if (resultado.Estado != EstadoGerenciamentoUsuario.Sucesso
            || resultado.Valor is not { } unidades)
        {
            return Forbid();
        }

        var selecionadas = model.UnidadesIds.ToHashSet();
        model.Unidades = unidades
            .Select(unidade => new UnidadeSelecaoUsuarioViewModel(
                unidade.Id,
                unidade.Nome,
                selecionadas.Contains(unidade.Id)))
            .ToArray();
        return View("Novo", model);
    }

    private async Task<IActionResult> ExibirEdicaoAsync(
        Guid usuarioAtualId,
        Guid usuarioId,
        EditarUsuarioFranqueadoraViewModel? modelPostado,
        CancellationToken cancellationToken)
    {
        var resultado = await consulta.ObterEdicaoAsync(
            usuarioAtualId,
            usuarioId,
            cancellationToken);

        if (resultado.Estado is EstadoGerenciamentoUsuario.SemAcesso
            or EstadoGerenciamentoUsuario.SelecaoOrganizacaoNecessaria)
        {
            return Forbid();
        }

        if (resultado.Estado == EstadoGerenciamentoUsuario.UsuarioNaoEncontrado)
        {
            return NotFound();
        }

        if (resultado.Estado == EstadoGerenciamentoUsuario.UsuarioComMultiplasOrganizacoes)
        {
            return ExibirEdicaoBloqueada(usuarioId, resultado.Mensagem);
        }

        if (resultado.Estado != EstadoGerenciamentoUsuario.Sucesso
            || resultado.Valor is not { } usuario)
        {
            return Forbid();
        }

        var model = modelPostado ?? new EditarUsuarioFranqueadoraViewModel
        {
            UsuarioId = usuario.UsuarioId,
            NomeCompleto = usuario.NomeCompleto,
            Email = usuario.Email,
            Telefone = usuario.Telefone
        };
        model.UsuarioId = usuarioId;
        return View("Editar", model);
    }

    private IActionResult ExibirEdicaoBloqueada(Guid usuarioId, string? mensagem)
    {
        Response.StatusCode = StatusCodes.Status409Conflict;
        return View("Editar", new EditarUsuarioFranqueadoraViewModel
        {
            UsuarioId = usuarioId,
            MensagemBloqueio = mensagem
                ?? "Este usuário não pode ser editado por esta tela."
        });
    }

    private static FranqueadoCadastroDados? MontarDadosFranqueado(
        NovoUsuarioFranqueadoraViewModel model)
    {
        if (model.TipoCadastro != TipoCadastroUsuario.Franqueado
            || model.TipoPessoa is not { } tipoPessoa)
        {
            return null;
        }

        return new FranqueadoCadastroDados(
            tipoPessoa,
            model.NomeRazaoSocial ?? string.Empty,
            model.NomeFantasia,
            model.Documento ?? string.Empty,
            model.TelefoneFranqueado,
            model.EmailFranqueado ?? string.Empty,
            model.EmailFinanceiro,
            model.ResponsavelLegal,
            model.Logradouro,
            model.Numero,
            model.Complemento,
            model.Bairro,
            model.Cidade,
            model.Estado,
            model.Cep,
            model.Observacoes,
            model.UnidadesIds);
    }

    private static string NomeTipo(TipoCadastroUsuario tipoCadastro)
    {
        return tipoCadastro == TipoCadastroUsuario.AdministradorRede
            ? "Administrador de rede"
            : "Franqueado";
    }
}

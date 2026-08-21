using BFA.Application.Acessos;
using BFA.Application.Franqueadora.Franqueados;
using BFA.Application.Localidades;
using BFA.Domain.Franqueados;
using BFA.Web.Authorization;
using BFA.Web.ViewModels.Franqueadora;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFA.Web.Areas.Franqueadora.Controllers;

[Area("Franqueadora")]
[Authorize(Policy = PoliticasAcesso.AdministradorRede)]
[Route("franqueadora/franqueados")]
public sealed class FranqueadosController(
    IUsuarioAtual usuarioAtual,
    IFranqueadosConsulta consulta,
    IFranqueadosServico servico,
    ILocalidadesConsulta localidadesConsulta) : Controller
{
    public const string MensagemSucesso = "Operação concluída com sucesso.";
    public const string MensagemErro = "Não foi possível concluir a operação.";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await consulta.ListarAsync(usuarioId, cancellationToken);

        if (resultado.Estado != EstadoGerenciamentoFranqueado.Sucesso
            || resultado.Valor is not { } franqueados)
        {
            return Forbid();
        }

        return View(new FranqueadosIndexViewModel
        {
            Franqueados = franqueados.Select(franqueado => new FranqueadoItemViewModel(
                franqueado.Id,
                franqueado.NomeRazaoSocial,
                franqueado.NomeFantasia,
                FormatarDocumento(franqueado.Documento, franqueado.TipoPessoa),
                NomeTipoPessoa(franqueado.TipoPessoa),
                franqueado.QuantidadeUnidadesAtivas,
                franqueado.Ativo)).ToArray()
        });
    }

    [HttpGet("{franqueadoId:guid}")]
    public async Task<IActionResult> Detalhe(
        Guid franqueadoId,
        CancellationToken cancellationToken)
    {
        return await ExibirDetalheAsync(franqueadoId, cancellationToken);
    }

    [HttpGet("{franqueadoId:guid}/editar")]
    public async Task<IActionResult> Editar(
        Guid franqueadoId,
        CancellationToken cancellationToken)
    {
        return await ExibirEdicaoAsync(
            franqueadoId,
            modelPostado: null,
            cancellationToken);
    }

    [HttpPost("{franqueadoId:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        Guid franqueadoId,
        EditarFranqueadoViewModel model,
        CancellationToken cancellationToken)
    {
        model.Id = franqueadoId;

        if (!ModelState.IsValid)
        {
            return await ExibirEdicaoAsync(franqueadoId, model, cancellationToken);
        }

        if (usuarioAtual.UsuarioId is not { } usuarioId
            || model.TipoPessoa is not { } tipoPessoa)
        {
            return Forbid();
        }

        var resultado = await servico.AtualizarAsync(
            usuarioId,
            franqueadoId,
            new AtualizarFranqueadoSolicitacao(
                tipoPessoa,
                model.NomeRazaoSocial,
                model.NomeFantasia,
                model.Documento,
                model.Telefone,
                model.Email,
                model.EmailFinanceiro,
                model.ResponsavelLegal,
                model.Logradouro,
                model.Numero,
                model.Complemento,
                model.Bairro,
                model.EstadoCodigoIbge,
                model.MunicipioCodigoIbge,
                model.Cep,
                model.Observacoes),
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoFranqueado.Sucesso)
        {
            TempData[nameof(MensagemSucesso)] = "Dados do franqueado atualizados.";
            return Redirect($"/franqueadora/franqueados/{franqueadoId}");
        }

        var resposta = MapearEstadoHttp(resultado.Estado);

        if (resposta is not null)
        {
            return resposta;
        }

        var campo = resultado.Estado switch
        {
            EstadoGerenciamentoFranqueado.DocumentoDuplicado => nameof(model.Documento),
            EstadoGerenciamentoFranqueado.EstadoLocalidadeInvalido =>
                nameof(model.EstadoCodigoIbge),
            EstadoGerenciamentoFranqueado.MunicipioLocalidadeInvalido =>
                nameof(model.MunicipioCodigoIbge),
            _ => string.Empty
        };
        ModelState.AddModelError(campo, resultado.Mensagem ?? MensagemErro);
        return await ExibirEdicaoAsync(franqueadoId, model, cancellationToken);
    }

    [HttpPost("{franqueadoId:guid}/unidades/adicionar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdicionarUnidade(
        Guid franqueadoId,
        [Bind(nameof(FranqueadoDetalheViewModel.UnidadeId))]
        FranqueadoDetalheViewModel model,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await servico.VincularUnidadeAsync(
            usuarioId,
            franqueadoId,
            new VincularUnidadeFranqueadoSolicitacao(model.UnidadeId ?? Guid.Empty),
            cancellationToken);
        return MapearOperacaoUnidade(franqueadoId, resultado, "Unidade vinculada ao franqueado.");
    }

    [HttpPost("{franqueadoId:guid}/unidades/{unidadeId:guid}/desativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesativarUnidade(
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await servico.DesativarUnidadeAsync(
            usuarioId,
            franqueadoId,
            unidadeId,
            cancellationToken);
        return MapearOperacaoUnidade(
            franqueadoId,
            resultado,
            "Vínculo com a unidade desativado.");
    }

    private async Task<IActionResult> ExibirDetalheAsync(
        Guid franqueadoId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await consulta.ObterAsync(
            usuarioId,
            franqueadoId,
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoFranqueado.NaoEncontrado)
        {
            return NotFound();
        }

        if (resultado.Estado != EstadoGerenciamentoFranqueado.Sucesso
            || resultado.Valor is not { } franqueado)
        {
            return Forbid();
        }

        return View("Detalhe", MontarDetalhe(franqueado));
    }

    private async Task<IActionResult> ExibirEdicaoAsync(
        Guid franqueadoId,
        EditarFranqueadoViewModel? modelPostado,
        CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } usuarioId)
        {
            return Forbid();
        }

        var resultado = await consulta.ObterAsync(
            usuarioId,
            franqueadoId,
            cancellationToken);

        if (resultado.Estado == EstadoGerenciamentoFranqueado.NaoEncontrado)
        {
            return NotFound();
        }

        if (resultado.Estado != EstadoGerenciamentoFranqueado.Sucesso
            || resultado.Valor is not { } franqueado)
        {
            return Forbid();
        }

        var dados = franqueado.Dados;
        var model = modelPostado ?? new EditarFranqueadoViewModel
        {
            Id = dados.Id,
            TipoPessoa = dados.TipoPessoa,
            NomeRazaoSocial = dados.NomeRazaoSocial,
            NomeFantasia = dados.NomeFantasia,
            Documento = dados.Documento,
            Telefone = dados.Telefone,
            Email = dados.Email,
            EmailFinanceiro = dados.EmailFinanceiro,
            ResponsavelLegal = dados.ResponsavelLegal,
            Logradouro = dados.Logradouro,
            Numero = dados.Numero,
            Complemento = dados.Complemento,
            Bairro = dados.Bairro,
            EstadoCodigoIbge = franqueado.EstadoCodigoIbge,
            MunicipioCodigoIbge = franqueado.MunicipioCodigoIbge,
            Cep = dados.Cep,
            Observacoes = dados.Observacoes,
            Ativo = dados.Ativo
        };
        model.Id = dados.Id;
        model.Ativo = dados.Ativo;
        var estados = await localidadesConsulta.ListarEstadosAtivosAsync(cancellationToken);
        model.Estados = estados.Select(estado => new EstadoSelecaoLocalidadeViewModel(
            estado.CodigoIbge,
            estado.Sigla,
            estado.Nome)).ToArray();
        model.Municipios = [];

        if (model.EstadoCodigoIbge is > 0
            && estados.Any(estado => estado.CodigoIbge == model.EstadoCodigoIbge.Value))
        {
            var municipios = await localidadesConsulta.ListarMunicipiosAtivosAsync(
                model.EstadoCodigoIbge.Value,
                cancellationToken);
            model.Municipios = municipios.Select(municipio =>
                new MunicipioSelecaoLocalidadeViewModel(
                    municipio.CodigoIbge,
                    municipio.Nome)).ToArray();
        }

        return View("Editar", model);
    }

    private IActionResult MapearOperacaoUnidade(
        Guid franqueadoId,
        ResultadoOperacaoFranqueado resultado,
        string mensagemSucesso)
    {
        var resposta = MapearEstadoHttp(resultado.Estado);

        if (resposta is not null)
        {
            return resposta;
        }

        TempData[resultado.Estado == EstadoGerenciamentoFranqueado.Sucesso
            ? nameof(MensagemSucesso)
            : nameof(MensagemErro)] = resultado.Estado == EstadoGerenciamentoFranqueado.Sucesso
            ? mensagemSucesso
            : resultado.Mensagem ?? MensagemErro;
        return Redirect($"/franqueadora/franqueados/{franqueadoId}");
    }

    private IActionResult? MapearEstadoHttp(EstadoGerenciamentoFranqueado estado)
    {
        return estado switch
        {
            EstadoGerenciamentoFranqueado.SemAcesso or
                EstadoGerenciamentoFranqueado.SelecaoOrganizacaoNecessaria => Forbid(),
            EstadoGerenciamentoFranqueado.NaoEncontrado or
                EstadoGerenciamentoFranqueado.VinculoNaoEncontrado => NotFound(),
            _ => null
        };
    }

    private static FranqueadoDetalheViewModel MontarDetalhe(FranqueadoDetalhe franqueado)
    {
        var dados = franqueado.Dados;
        return new FranqueadoDetalheViewModel
        {
            Id = dados.Id,
            NomeRazaoSocial = dados.NomeRazaoSocial,
            NomeFantasia = dados.NomeFantasia,
            DocumentoFormatado = FormatarDocumento(dados.Documento, dados.TipoPessoa),
            TipoPessoa = NomeTipoPessoa(dados.TipoPessoa),
            Telefone = dados.Telefone,
            Email = dados.Email,
            EmailFinanceiro = dados.EmailFinanceiro,
            ResponsavelLegal = dados.ResponsavelLegal,
            Endereco = MontarEndereco(dados),
            Observacoes = dados.Observacoes,
            Ativo = dados.Ativo,
            Usuarios = franqueado.Usuarios.Select(usuario =>
                new FranqueadoUsuarioItemViewModel(
                    usuario.UsuarioId,
                    usuario.Nome,
                    usuario.Email,
                    usuario.Principal,
                    usuario.Ativo)).ToArray(),
            Unidades = franqueado.Unidades.Select(unidade =>
                new FranqueadoUnidadeItemViewModel(
                    unidade.UnidadeId,
                    unidade.Nome,
                    unidade.VinculoAtivo,
                    unidade.UnidadeAtiva,
                    unidade.CriadoEmUtc,
                    unidade.StatusContrato)).ToArray(),
            UnidadesDisponiveis = franqueado.UnidadesDisponiveis.Select(unidade =>
                new UnidadeDisponivelFranqueadoViewModel(unidade.Id, unidade.Nome)).ToArray()
        };
    }

    private static string NomeTipoPessoa(TipoPessoaFranqueado tipoPessoa) =>
        tipoPessoa == TipoPessoaFranqueado.PessoaFisica
            ? "Pessoa física"
            : "Pessoa jurídica";

    private static string FormatarDocumento(
        string documento,
        TipoPessoaFranqueado tipoPessoa)
    {
        if (tipoPessoa == TipoPessoaFranqueado.PessoaFisica && documento.Length == 11)
        {
            return $"{documento[..3]}.{documento[3..6]}.{documento[6..9]}-{documento[9..]}";
        }

        return documento.Length == 14
            ? $"{documento[..2]}.{documento[2..5]}.{documento[5..8]}/{documento[8..12]}-{documento[12..]}"
            : documento;
    }

    private static string MontarEndereco(FranqueadoDados dados)
    {
        var linha = string.Join(", ", new[] { dados.Logradouro, dados.Numero }
            .Where(valor => !string.IsNullOrWhiteSpace(valor)));
        var localidade = string.Join(" - ", new[] { dados.Cidade, dados.Estado }
            .Where(valor => !string.IsNullOrWhiteSpace(valor)));
        return string.Join(" · ", new[] { linha, dados.Complemento, dados.Bairro, localidade, dados.Cep }
            .Where(valor => !string.IsNullOrWhiteSpace(valor)));
    }
}

using System.ComponentModel.DataAnnotations;
using BFA.Application.Acessos;
using BFA.Application.Localidades;
using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using Microsoft.Extensions.Logging;

namespace BFA.Application.Franqueadora.Franqueados;

public sealed class FranqueadosServico(
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IFranqueadosRepositorio repositorio,
    ILocalidadesConsulta localidadesConsulta,
    TimeProvider timeProvider,
    ILogger<FranqueadosServico> logger)
    : IFranqueadosConsulta, IFranqueadosServico
{
    public async Task<ResultadoFranqueado<IReadOnlyList<FranqueadoResumo>>> ListarAsync(
        Guid usuarioAtualId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioAtualId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado, null);
        }

        var franqueados = await repositorio.ListarAsync(organizacaoId, cancellationToken);
        return new(EstadoGerenciamentoFranqueado.Sucesso, franqueados);
    }

    public async Task<ResultadoFranqueado<FranqueadoDetalhe>> ObterAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioAtualId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado, null);
        }

        if (franqueadoId == Guid.Empty)
        {
            return new(EstadoGerenciamentoFranqueado.NaoEncontrado, null);
        }

        var dados = await repositorio.ObterDadosAsync(
            organizacaoId,
            franqueadoId,
            cancellationToken);

        if (dados is null)
        {
            return new(EstadoGerenciamentoFranqueado.NaoEncontrado, null);
        }

        var usuarios = await repositorio.ListarUsuariosAsync(
            organizacaoId,
            franqueadoId,
            cancellationToken);
        var unidades = await repositorio.ListarUnidadesAsync(
            organizacaoId,
            franqueadoId,
            cancellationToken);
        var disponiveis = await repositorio.ListarUnidadesDisponiveisAsync(
            organizacaoId,
            cancellationToken);
        var codigosLocalidade = await ObterCodigosLocalidadeAsync(
            dados.Estado,
            dados.Cidade,
            cancellationToken);

        return new(
            EstadoGerenciamentoFranqueado.Sucesso,
            new FranqueadoDetalhe(
                dados,
                codigosLocalidade.EstadoCodigoIbge,
                codigosLocalidade.MunicipioCodigoIbge,
                usuarios,
                unidades,
                disponiveis));
    }

    public async Task<ResultadoOperacaoFranqueado> AtualizarAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        AtualizarFranqueadoSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAsync(usuarioAtualId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado);
        }

        var franqueado = await repositorio.ObterParaAtualizacaoAsync(
            organizacaoId,
            franqueadoId,
            cancellationToken);

        if (franqueado is null)
        {
            return new(EstadoGerenciamentoFranqueado.NaoEncontrado);
        }

        var erro = ValidarDados(solicitacao);

        if (erro is not null)
        {
            return new(EstadoGerenciamentoFranqueado.DadosInvalidos, erro);
        }

        var localidade = await ValidarLocalidadeAsync(solicitacao, cancellationToken);

        if (localidade.Estado != EstadoGerenciamentoFranqueado.Sucesso
            || localidade.Valor is not { } endereco)
        {
            return new(localidade.Estado, localidade.Mensagem);
        }

        try
        {
            var pessoaFisica = solicitacao.TipoPessoa == TipoPessoaFranqueado.PessoaFisica;
            franqueado.AtualizarDados(
                solicitacao.TipoPessoa,
                solicitacao.NomeRazaoSocial,
                pessoaFisica ? null : solicitacao.NomeFantasia,
                solicitacao.Documento,
                pessoaFisica ? franqueado.Telefone : solicitacao.Telefone,
                pessoaFisica ? franqueado.Email : solicitacao.Email,
                solicitacao.EmailFinanceiro,
                pessoaFisica ? null : solicitacao.ResponsavelLegal,
                solicitacao.Logradouro,
                solicitacao.Numero,
                solicitacao.Complemento,
                solicitacao.Bairro,
                endereco.Municipio.Nome,
                endereco.Estado.Sigla,
                solicitacao.Cep,
                solicitacao.Observacoes,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (ArgumentException exception)
        {
            return new(EstadoGerenciamentoFranqueado.DadosInvalidos, exception.Message);
        }

        if (await repositorio.ExisteDocumentoAsync(
                organizacaoId,
                franqueadoId,
                franqueado.Documento,
                cancellationToken))
        {
            return new(
                EstadoGerenciamentoFranqueado.DocumentoDuplicado,
                "Já existe um franqueado cadastrado com este documento.");
        }

        var resultado = MapearPersistencia(await repositorio.SalvarAsync(cancellationToken));
        if (resultado.Estado == EstadoGerenciamentoFranqueado.Sucesso)
        {
            logger.LogInformation("EditarFranqueado concluído para franqueado {FranqueadoId}", franqueadoId);
        }
        return resultado;
    }

    public async Task<ResultadoOperacaoFranqueado> VincularUnidadeAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        VincularUnidadeFranqueadoSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAsync(usuarioAtualId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado);
        }

        var franqueado = await repositorio.ObterParaAtualizacaoAsync(
            organizacaoId,
            franqueadoId,
            cancellationToken);

        if (franqueado is null)
        {
            return new(EstadoGerenciamentoFranqueado.NaoEncontrado);
        }

        if (solicitacao.UnidadeId == Guid.Empty
            || !await repositorio.UnidadeAtivaExisteAsync(
                organizacaoId,
                solicitacao.UnidadeId,
                cancellationToken))
        {
            return new(
                EstadoGerenciamentoFranqueado.UnidadeInvalida,
                "Selecione uma unidade ativa da Organização atual.");
        }

        if (await repositorio.UnidadePossuiOutroFranqueadoAtivoAsync(
                organizacaoId,
                franqueadoId,
                solicitacao.UnidadeId,
                cancellationToken))
        {
            return new(
                EstadoGerenciamentoFranqueado.UnidadeOcupada,
                "Esta unidade já possui um franqueado ativo.");
        }

        var principal = await repositorio.ObterUsuarioPrincipalAtivoAsync(
            franqueadoId,
            cancellationToken);

        if (principal is null)
        {
            return new(
                EstadoGerenciamentoFranqueado.UsuarioPrincipalAusente,
                "O franqueado precisa ter um usuário principal ativo antes de receber uma unidade.");
        }

        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;
        var vinculo = await repositorio.ObterVinculoUnidadeAsync(
            organizacaoId,
            franqueadoId,
            solicitacao.UnidadeId,
            cancellationToken);

        var acesso = await repositorio.ObterAcessoAdministradorUnidadeAsync(
            organizacaoId,
            solicitacao.UnidadeId,
            principal.UsuarioId,
            cancellationToken);

        var resultadoVinculos = RegraVinculosFranqueadoUnidade.GarantirAtivos(
            franqueadoId,
            organizacaoId,
            solicitacao.UnidadeId,
            principal.UsuarioId,
            vinculo,
            acesso,
            agoraUtc);

        if (resultadoVinculos.VinculoComercialCriado)
        {
            repositorio.Adicionar(resultadoVinculos.VinculoComercial);
        }

        if (resultadoVinculos.AcessoCriado)
        {
            repositorio.Adicionar(resultadoVinculos.AcessoAdministradorUnidade);
        }

        var resultado = MapearPersistencia(await repositorio.SalvarAsync(cancellationToken));
        if (resultado.Estado == EstadoGerenciamentoFranqueado.Sucesso)
        {
            logger.LogInformation("VincularUnidadeFranqueado concluído para franqueado {FranqueadoId}", franqueadoId);
        }
        return resultado;
    }

    public async Task<ResultadoOperacaoFranqueado> DesativarUnidadeAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioAtualId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado);
        }

        if (await repositorio.ObterParaAtualizacaoAsync(
                organizacaoId,
                franqueadoId,
                cancellationToken) is null)
        {
            return new(EstadoGerenciamentoFranqueado.NaoEncontrado);
        }

        var vinculo = await repositorio.ObterVinculoUnidadeAsync(
            organizacaoId,
            franqueadoId,
            unidadeId,
            cancellationToken);

        if (vinculo is null)
        {
            return new(EstadoGerenciamentoFranqueado.VinculoNaoEncontrado);
        }

        if (!vinculo.Ativo)
        {
            return new(EstadoGerenciamentoFranqueado.Sucesso);
        }

        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;
        vinculo.Desativar(agoraUtc);
        var principal = await repositorio.ObterUsuarioPrincipalAtivoAsync(
            franqueadoId,
            cancellationToken);

        if (principal is not null)
        {
            var acesso = await repositorio.ObterAcessoAdministradorUnidadeAsync(
                organizacaoId,
                unidadeId,
                principal.UsuarioId,
                cancellationToken);

            if (acesso?.Ativo == true)
            {
                acesso.Desativar(agoraUtc);
            }
        }

        var resultado = MapearPersistencia(await repositorio.SalvarAsync(cancellationToken));
        if (resultado.Estado == EstadoGerenciamentoFranqueado.Sucesso)
        {
            logger.LogInformation("DesativarUnidadeFranqueado concluído para franqueado {FranqueadoId}", franqueadoId);
        }
        return resultado;
    }

    private async Task<ResultadoFranqueado<LocalidadeValidada>> ValidarLocalidadeAsync(
        AtualizarFranqueadoSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        if (solicitacao.EstadoCodigoIbge is not > 0)
        {
            return new(
                EstadoGerenciamentoFranqueado.EstadoLocalidadeInvalido,
                null,
                "Selecione um Estado válido.");
        }

        var estados = await localidadesConsulta.ListarEstadosAtivosAsync(cancellationToken);
        var estado = estados.SingleOrDefault(item =>
            item.CodigoIbge == solicitacao.EstadoCodigoIbge.Value);

        if (estado is null)
        {
            return new(
                EstadoGerenciamentoFranqueado.EstadoLocalidadeInvalido,
                null,
                "Selecione um Estado ativo do catálogo de localidades.");
        }

        if (solicitacao.MunicipioCodigoIbge is not > 0)
        {
            return new(
                EstadoGerenciamentoFranqueado.MunicipioLocalidadeInvalido,
                null,
                "Selecione um Município válido.");
        }

        var municipios = await localidadesConsulta.ListarMunicipiosAtivosAsync(
            estado.CodigoIbge,
            cancellationToken);
        var municipio = municipios.SingleOrDefault(item =>
            item.CodigoIbge == solicitacao.MunicipioCodigoIbge.Value);

        return municipio is null
            ? new(
                EstadoGerenciamentoFranqueado.MunicipioLocalidadeInvalido,
                null,
                "Selecione um Município ativo pertencente ao Estado informado.")
            : new(
                EstadoGerenciamentoFranqueado.Sucesso,
                new LocalidadeValidada(estado, municipio));
    }

    private async Task<CodigosLocalidade> ObterCodigosLocalidadeAsync(
        string? siglaEstado,
        string? nomeMunicipio,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(siglaEstado))
        {
            return new(null, null);
        }

        var estados = await localidadesConsulta.ListarEstadosAtivosAsync(cancellationToken);
        var estado = estados.SingleOrDefault(item =>
            string.Equals(item.Sigla, siglaEstado, StringComparison.OrdinalIgnoreCase));

        if (estado is null || string.IsNullOrWhiteSpace(nomeMunicipio))
        {
            return new(estado?.CodigoIbge, null);
        }

        var municipios = await localidadesConsulta.ListarMunicipiosAtivosAsync(
            estado.CodigoIbge,
            cancellationToken);
        var municipio = municipios.SingleOrDefault(item =>
            string.Equals(item.Nome, nomeMunicipio, StringComparison.OrdinalIgnoreCase));
        return new(estado.CodigoIbge, municipio?.CodigoIbge);
    }

    private async Task<ContextoOrganizacao> ObterContextoAsync(
        Guid usuarioAtualId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtualId == Guid.Empty)
        {
            return new(EstadoGerenciamentoFranqueado.SemAcesso, null);
        }

        var organizacoes = await acessoUsuarioConsulta
            .ListarOrganizacoesAdministradorRedeAsync(usuarioAtualId, cancellationToken);

        return organizacoes.Count switch
        {
            0 => new(EstadoGerenciamentoFranqueado.SemAcesso, null),
            1 => new(EstadoGerenciamentoFranqueado.Sucesso, organizacoes[0]),
            _ => new(EstadoGerenciamentoFranqueado.SelecaoOrganizacaoNecessaria, null)
        };
    }

    private static string? ValidarDados(AtualizarFranqueadoSolicitacao solicitacao)
    {
        if (!Enum.IsDefined(solicitacao.TipoPessoa))
        {
            return "Selecione um tipo de pessoa válido.";
        }

        if (string.IsNullOrWhiteSpace(solicitacao.NomeRazaoSocial))
        {
            return solicitacao.TipoPessoa == TipoPessoaFranqueado.PessoaFisica
                ? "Informe o nome do franqueado."
                : "Informe a razão social.";
        }

        if (string.IsNullOrWhiteSpace(solicitacao.Documento))
        {
            return "Informe o CPF ou CNPJ.";
        }

        var emailFinanceiro = solicitacao.EmailFinanceiro?.Trim();

        if (!string.IsNullOrWhiteSpace(emailFinanceiro)
            && (emailFinanceiro.Length > Franqueado.EmailFinanceiroTamanhoMaximo
                || !new EmailAddressAttribute().IsValid(emailFinanceiro)))
        {
            return "Informe um email financeiro válido.";
        }

        if (solicitacao.TipoPessoa == TipoPessoaFranqueado.PessoaFisica)
        {
            return null;
        }

        var email = solicitacao.Email.Trim();

        if (email.Length == 0
            || email.Length > Franqueado.EmailTamanhoMaximo
            || !new EmailAddressAttribute().IsValid(email))
        {
            return "Informe um email comercial válido.";
        }

        return null;
    }

    private static ResultadoOperacaoFranqueado MapearPersistencia(
        EstadoPersistenciaFranqueado estado)
    {
        return estado switch
        {
            EstadoPersistenciaFranqueado.Sucesso => new(
                EstadoGerenciamentoFranqueado.Sucesso),
            EstadoPersistenciaFranqueado.DocumentoDuplicado => new(
                EstadoGerenciamentoFranqueado.DocumentoDuplicado,
                "Já existe um franqueado cadastrado com este documento."),
            EstadoPersistenciaFranqueado.UnidadeOcupada => new(
                EstadoGerenciamentoFranqueado.UnidadeOcupada,
                "Esta unidade já possui um franqueado ativo."),
            _ => new(
                EstadoGerenciamentoFranqueado.FalhaPersistencia,
                "Não foi possível concluir a operação. Nenhuma alteração foi salva.")
        };
    }

    private sealed record ContextoOrganizacao(
        EstadoGerenciamentoFranqueado Estado,
        Guid? OrganizacaoId);

    private sealed record LocalidadeValidada(
        EstadoLocalidadeResumo Estado,
        MunicipioLocalidadeResumo Municipio);

    private sealed record CodigosLocalidade(
        int? EstadoCodigoIbge,
        int? MunicipioCodigoIbge);
}

using System.ComponentModel.DataAnnotations;
using BFA.Application.Acessos;
using BFA.Application.Franqueadora.Franqueados;
using BFA.Application.Localidades;
using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using BFA.Domain.Usuarios;

namespace BFA.Application.Franqueadora.Usuarios;

public sealed class UsuariosFranqueadoraServico(
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    IUsuariosFranqueadoraRepositorio repositorio,
    ILocalidadesConsulta localidadesConsulta,
    TimeProvider timeProvider)
    : IUsuariosFranqueadoraConsulta, IUsuariosFranqueadoraServico
{
    public async Task<ResultadoUsuariosFranqueadora<IReadOnlyList<UsuarioFranqueadoraResumo>>> ListarAsync(
        Guid usuarioAtualId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioAtualId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado, null);
        }

        var usuarios = await repositorio.ListarAsync(organizacaoId, cancellationToken);
        return new(EstadoGerenciamentoUsuario.Sucesso, usuarios);
    }

    public async Task<ResultadoUsuariosFranqueadora<IReadOnlyList<UnidadeSelecaoUsuarioResumo>>> ListarUnidadesAsync(
        Guid usuarioAtualId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioAtualId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado, null);
        }

        var unidades = await repositorio.ListarUnidadesAtivasAsync(
            organizacaoId,
            cancellationToken);
        return new(EstadoGerenciamentoUsuario.Sucesso, unidades);
    }

    public async Task<ResultadoUsuariosFranqueadora<UsuarioFranqueadoraEdicao>> ObterEdicaoAsync(
        Guid usuarioAtualId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var contexto = await ObterContextoAsync(usuarioAtualId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado, null);
        }

        if (usuarioId == Guid.Empty)
        {
            return new(EstadoGerenciamentoUsuario.UsuarioNaoEncontrado, null);
        }

        var usuario = await repositorio.ObterEdicaoAsync(usuarioId, cancellationToken);

        if (usuario is null
            || !usuario.OrganizacoesAtivasIds.Contains(organizacaoId))
        {
            return new(EstadoGerenciamentoUsuario.UsuarioNaoEncontrado, null);
        }

        if (usuario.OrganizacoesAtivasIds.Count > 1)
        {
            return new(
                EstadoGerenciamentoUsuario.UsuarioComMultiplasOrganizacoes,
                null,
                "Este usuário possui vínculos ativos com mais de uma Organização e não pode ser editado por esta tela.");
        }

        var franqueados = await repositorio.ListarFranqueadosUsuarioAsync(
            organizacaoId,
            usuarioId,
            cancellationToken);

        return new(
            EstadoGerenciamentoUsuario.Sucesso,
            new UsuarioFranqueadoraEdicao(
                usuario.UsuarioId,
                usuario.NomeCompleto ?? string.Empty,
                usuario.Email,
                usuario.Telefone,
                franqueados));
    }

    public async Task<ResultadoCriacaoUsuarioFranqueadora> CriarAsync(
        Guid usuarioAtualId,
        CriarUsuarioFranqueadoraSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAsync(usuarioAtualId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado);
        }

        var erroComum = ValidarDadosComuns(solicitacao);

        if (erroComum is not null)
        {
            return new(EstadoGerenciamentoUsuario.DadosInvalidos, Mensagem: erroComum);
        }

        var email = solicitacao.Email.Trim();

        if (await repositorio.ExisteUsuarioPorEmailAsync(email, cancellationToken))
        {
            return new(
                EstadoGerenciamentoUsuario.EmailDuplicado,
                Mensagem: "Já existe um usuário cadastrado com este email.");
        }

        return solicitacao.TipoCadastro switch
        {
            TipoCadastroUsuario.AdministradorRede => await CriarAdministradorRedeAsync(
                organizacaoId,
                solicitacao,
                email,
                cancellationToken),
            TipoCadastroUsuario.Franqueado => await CriarFranqueadoAsync(
                organizacaoId,
                solicitacao,
                email,
                cancellationToken),
            _ => new(
                EstadoGerenciamentoUsuario.DadosInvalidos,
                Mensagem: "Selecione um tipo de cadastro válido.")
        };
    }

    public async Task<ResultadoAtualizacaoUsuarioFranqueadora> EditarAsync(
        Guid usuarioAtualId,
        EditarUsuarioFranqueadoraSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var contexto = await ObterContextoAsync(usuarioAtualId, cancellationToken);

        if (contexto.OrganizacaoId is not { } organizacaoId)
        {
            return new(contexto.Estado);
        }

        var erro = ValidarEdicao(solicitacao);

        if (erro is not null)
        {
            return new(EstadoGerenciamentoUsuario.DadosInvalidos, erro);
        }

        var dados = new AtualizarUsuarioFranqueadoraDados(
            solicitacao.UsuarioId,
            organizacaoId,
            solicitacao.NomeCompleto.Trim(),
            solicitacao.Email.Trim(),
            string.IsNullOrWhiteSpace(solicitacao.Telefone)
                ? null
                : solicitacao.Telefone.Trim(),
            timeProvider.GetUtcNow().UtcDateTime);
        var resultado = await repositorio.AtualizarAsync(dados, cancellationToken);

        return resultado.Estado switch
        {
            EstadoPersistenciaEdicaoUsuario.Sucesso => new(
                EstadoGerenciamentoUsuario.Sucesso),
            EstadoPersistenciaEdicaoUsuario.UsuarioNaoEncontrado => new(
                EstadoGerenciamentoUsuario.UsuarioNaoEncontrado),
            EstadoPersistenciaEdicaoUsuario.UsuarioComMultiplasOrganizacoes => new(
                EstadoGerenciamentoUsuario.UsuarioComMultiplasOrganizacoes,
                "Este usuário possui vínculos ativos com mais de uma Organização e não pode ser editado por esta tela."),
            EstadoPersistenciaEdicaoUsuario.EmailDuplicado => new(
                EstadoGerenciamentoUsuario.EmailDuplicado,
                "Já existe um usuário cadastrado com este email."),
            EstadoPersistenciaEdicaoUsuario.DadosInvalidos => new(
                EstadoGerenciamentoUsuario.DadosInvalidos,
                "Não foi possível validar os dados do usuário."),
            _ => new(
                EstadoGerenciamentoUsuario.FalhaPersistencia,
                "Não foi possível atualizar o usuário. Nenhuma alteração foi salva.")
        };
    }

    private async Task<ResultadoCriacaoUsuarioFranqueadora> CriarAdministradorRedeAsync(
        Guid organizacaoId,
        CriarUsuarioFranqueadoraSolicitacao solicitacao,
        string email,
        CancellationToken cancellationToken)
    {
        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;
        var usuarioId = Guid.NewGuid();

        try
        {
            var perfil = new PerfilUsuario(
                Guid.NewGuid(),
                usuarioId,
                solicitacao.NomeCompleto,
                solicitacao.Telefone,
                agoraUtc);
            var vinculo = new VinculoAcesso(
                Guid.NewGuid(),
                usuarioId,
                organizacaoId,
                unidadeId: null,
                PerfilAcesso.AdministradorRede,
                agoraUtc);
            var cadastro = new CadastroUsuarioFranqueadora(
                usuarioId,
                email,
                perfil,
                Franqueado: null,
                FranqueadoUsuario: null,
                FranqueadosUnidades: [],
                VinculosAcesso: [vinculo]);

            return await PersistirAsync(
                cadastro,
                TipoCadastroUsuario.AdministradorRede,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return new(
                EstadoGerenciamentoUsuario.DadosInvalidos,
                Mensagem: exception.Message);
        }
    }

    private async Task<ResultadoCriacaoUsuarioFranqueadora> CriarFranqueadoAsync(
        Guid organizacaoId,
        CriarUsuarioFranqueadoraSolicitacao solicitacao,
        string email,
        CancellationToken cancellationToken)
    {
        if (solicitacao.Franqueado is not { } dados)
        {
            return new(
                EstadoGerenciamentoUsuario.DadosInvalidos,
                Mensagem: "Informe os dados do franqueado.");
        }

        var localidade = await ValidarLocalidadeAsync(dados, cancellationToken);

        if (localidade.Estado != EstadoGerenciamentoUsuario.Sucesso
            || localidade.Valor is not { } endereco)
        {
            return new(localidade.Estado, Mensagem: localidade.Mensagem);
        }

        var unidadesIds = dados.UnidadesIds
            .Where(unidadeId => unidadeId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (unidadesIds.Length == 0)
        {
            return new(
                EstadoGerenciamentoUsuario.UnidadesInvalidas,
                Mensagem: "Selecione ao menos uma unidade da Organização atual.");
        }

        var unidadesValidas = await repositorio.ListarUnidadesValidasAsync(
            organizacaoId,
            unidadesIds,
            cancellationToken);

        if (unidadesValidas.Count != unidadesIds.Length)
        {
            return new(
                EstadoGerenciamentoUsuario.UnidadesInvalidas,
                Mensagem: "Uma ou mais unidades selecionadas são inválidas para esta Organização.");
        }

        var unidadeEmConflito = await repositorio.ObterUnidadeComFranqueadoAtivoAsync(
            organizacaoId,
            unidadesIds,
            cancellationToken);

        if (unidadeEmConflito is not null)
        {
            return new(
                EstadoGerenciamentoUsuario.UnidadeComFranqueadoAtivo,
                Mensagem: $"A unidade {unidadeEmConflito} já possui um franqueado ativo.");
        }

        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;
        var usuarioId = Guid.NewGuid();
        var franqueadoId = Guid.NewGuid();

        try
        {
            var perfil = new PerfilUsuario(
                Guid.NewGuid(),
                usuarioId,
                solicitacao.NomeCompleto,
                solicitacao.Telefone,
                agoraUtc);
            var pessoaFisica = dados.TipoPessoa == TipoPessoaFranqueado.PessoaFisica;
            var franqueado = new Franqueado(
                franqueadoId,
                organizacaoId,
                dados.TipoPessoa,
                pessoaFisica ? perfil.NomeCompleto : dados.NomeRazaoSocial,
                dados.Documento,
                pessoaFisica ? email : dados.Email,
                agoraUtc,
                pessoaFisica ? null : dados.NomeFantasia,
                pessoaFisica ? perfil.Telefone : dados.Telefone,
                dados.EmailFinanceiro,
                pessoaFisica ? null : dados.ResponsavelLegal,
                dados.Logradouro,
                dados.Numero,
                dados.Complemento,
                dados.Bairro,
                endereco.Municipio.Nome,
                endereco.Estado.Sigla,
                dados.Cep,
                dados.Observacoes);

            if (await repositorio.ExisteFranqueadoPorDocumentoAsync(
                    organizacaoId,
                    franqueado.Documento,
                    cancellationToken))
            {
                return new(
                    EstadoGerenciamentoUsuario.DocumentoDuplicado,
                    Mensagem: "Já existe um franqueado cadastrado com este documento.");
            }

            var franqueadoUsuario = new FranqueadoUsuario(
                Guid.NewGuid(),
                franqueadoId,
                usuarioId,
                principal: true,
                agoraUtc);
            var vinculosFranquia = unidadesIds
                .Select(unidadeId => RegraVinculosFranqueadoUnidade.GarantirAtivos(
                    franqueadoId,
                    organizacaoId,
                    unidadeId,
                    usuarioId,
                    vinculoComercial: null,
                    acessoAdministradorUnidade: null,
                    agoraUtc))
                .ToArray();
            var franqueadosUnidades = vinculosFranquia
                .Select(resultado => resultado.VinculoComercial)
                .ToArray();
            var vinculos = vinculosFranquia
                .Select(resultado => resultado.AcessoAdministradorUnidade)
                .ToArray();
            var cadastro = new CadastroUsuarioFranqueadora(
                usuarioId,
                email,
                perfil,
                franqueado,
                franqueadoUsuario,
                franqueadosUnidades,
                vinculos);

            return await PersistirAsync(
                cadastro,
                TipoCadastroUsuario.Franqueado,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return new(
                EstadoGerenciamentoUsuario.DadosInvalidos,
                Mensagem: exception.Message);
        }
    }

    private async Task<ResultadoCriacaoUsuarioFranqueadora> PersistirAsync(
        CadastroUsuarioFranqueadora cadastro,
        TipoCadastroUsuario tipoCadastro,
        CancellationToken cancellationToken)
    {
        var resultado = await repositorio.CriarAsync(cadastro, cancellationToken);

        if (resultado.Estado == EstadoPersistenciaCadastroUsuario.Sucesso
            && !string.IsNullOrWhiteSpace(resultado.TokenDefinicaoSenha))
        {
            return new(
                EstadoGerenciamentoUsuario.Sucesso,
                new UsuarioFranqueadoraCriado(
                    cadastro.UsuarioId,
                    cadastro.PerfilUsuario.NomeCompleto,
                    cadastro.Email,
                    tipoCadastro,
                    resultado.TokenDefinicaoSenha));
        }

        return resultado.Estado switch
        {
            EstadoPersistenciaCadastroUsuario.EmailDuplicado => new(
                EstadoGerenciamentoUsuario.EmailDuplicado,
                Mensagem: "Já existe um usuário cadastrado com este email."),
            EstadoPersistenciaCadastroUsuario.DocumentoDuplicado => new(
                EstadoGerenciamentoUsuario.DocumentoDuplicado,
                Mensagem: "Já existe um franqueado cadastrado com este documento."),
            EstadoPersistenciaCadastroUsuario.UnidadeComFranqueadoAtivo => new(
                EstadoGerenciamentoUsuario.UnidadeComFranqueadoAtivo,
                Mensagem: "Uma das unidades selecionadas já possui um franqueado ativo."),
            EstadoPersistenciaCadastroUsuario.DadosInvalidos => new(
                EstadoGerenciamentoUsuario.DadosInvalidos,
                Mensagem: "Não foi possível validar os dados do usuário."),
            _ => new(
                EstadoGerenciamentoUsuario.FalhaPersistencia,
                Mensagem: "Não foi possível concluir o cadastro. Nenhuma alteração foi salva.")
        };
    }

    private async Task<ResultadoUsuariosFranqueadora<LocalidadeEnderecoValidada>>
        ValidarLocalidadeAsync(
            FranqueadoCadastroDados dados,
            CancellationToken cancellationToken)
    {
        if (dados.EstadoCodigoIbge is not > 0)
        {
            return new(
                EstadoGerenciamentoUsuario.EstadoLocalidadeInvalido,
                null,
                "Selecione um Estado válido.");
        }

        var estados = await localidadesConsulta.ListarEstadosAtivosAsync(cancellationToken);

        if (estados.Count == 0)
        {
            return new(
                EstadoGerenciamentoUsuario.EstadoLocalidadeInvalido,
                null,
                "Catálogo de localidades não carregado.");
        }

        var estado = estados.SingleOrDefault(item =>
            item.CodigoIbge == dados.EstadoCodigoIbge.Value);

        if (estado is null)
        {
            return new(
                EstadoGerenciamentoUsuario.EstadoLocalidadeInvalido,
                null,
                "Selecione um Estado ativo do catálogo de localidades.");
        }

        if (dados.MunicipioCodigoIbge is not > 0)
        {
            return new(
                EstadoGerenciamentoUsuario.MunicipioLocalidadeInvalido,
                null,
                "Selecione um Município válido.");
        }

        var municipios = await localidadesConsulta.ListarMunicipiosAtivosAsync(
            estado.CodigoIbge,
            cancellationToken);
        var municipio = municipios.SingleOrDefault(item =>
            item.CodigoIbge == dados.MunicipioCodigoIbge.Value);

        if (municipio is null)
        {
            return new(
                EstadoGerenciamentoUsuario.MunicipioLocalidadeInvalido,
                null,
                "Selecione um Município ativo pertencente ao Estado informado.");
        }

        return new(
            EstadoGerenciamentoUsuario.Sucesso,
            new LocalidadeEnderecoValidada(estado, municipio));
    }

    private async Task<ContextoOrganizacao> ObterContextoAsync(
        Guid usuarioAtualId,
        CancellationToken cancellationToken)
    {
        if (usuarioAtualId == Guid.Empty)
        {
            return new(EstadoGerenciamentoUsuario.SemAcesso, null);
        }

        var organizacoes = await acessoUsuarioConsulta
            .ListarOrganizacoesAdministradorRedeAsync(usuarioAtualId, cancellationToken);

        return organizacoes.Count switch
        {
            0 => new(EstadoGerenciamentoUsuario.SemAcesso, null),
            1 => new(EstadoGerenciamentoUsuario.Sucesso, organizacoes[0]),
            _ => new(EstadoGerenciamentoUsuario.SelecaoOrganizacaoNecessaria, null)
        };
    }

    private static string? ValidarDadosComuns(CriarUsuarioFranqueadoraSolicitacao solicitacao)
    {
        if (!Enum.IsDefined(solicitacao.TipoCadastro))
        {
            return "Selecione um tipo de cadastro válido.";
        }

        if (string.IsNullOrWhiteSpace(solicitacao.NomeCompleto))
        {
            return "Informe o nome completo.";
        }

        var email = solicitacao.Email.Trim();

        if (email.Length == 0
            || email.Length > 256
            || !new EmailAddressAttribute().IsValid(email))
        {
            return "Informe um email válido.";
        }

        return string.IsNullOrWhiteSpace(solicitacao.Telefone)
            ? "Informe o telefone."
            : null;
    }

    private static string? ValidarEdicao(EditarUsuarioFranqueadoraSolicitacao solicitacao)
    {
        if (solicitacao.UsuarioId == Guid.Empty)
        {
            return "O usuário deve ser informado.";
        }

        var nome = solicitacao.NomeCompleto.Trim();

        if (nome.Length == 0 || nome.Length > PerfilUsuario.NomeCompletoTamanhoMaximo)
        {
            return "Informe um nome completo válido.";
        }

        var email = solicitacao.Email.Trim();

        if (email.Length == 0
            || email.Length > 256
            || !new EmailAddressAttribute().IsValid(email))
        {
            return "Informe um email válido.";
        }

        if (!string.IsNullOrWhiteSpace(solicitacao.Telefone)
            && solicitacao.Telefone.Trim().Length > PerfilUsuario.TelefoneTamanhoMaximo)
        {
            return $"O telefone deve possuir no máximo {PerfilUsuario.TelefoneTamanhoMaximo} caracteres.";
        }

        return null;
    }

    private sealed record ContextoOrganizacao(
        EstadoGerenciamentoUsuario Estado,
        Guid? OrganizacaoId);

    private sealed record LocalidadeEnderecoValidada(
        EstadoLocalidadeResumo Estado,
        MunicipioLocalidadeResumo Municipio);
}

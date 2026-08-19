using BFA.Application.Franqueadora.Usuarios;
using BFA.Domain.Acessos;
using BFA.Domain.Usuarios;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BFA.Infrastructure.Franqueadora;

public sealed class UsuariosFranqueadoraRepositorio(
    BfaDbContext dbContext,
    UserManager<UsuarioIdentity> userManager)
    : IUsuariosFranqueadoraRepositorio
{
    private const string RestricaoDocumentoUnico =
        "uq_franqueados_organizacao_id_documento";
    private const string RestricaoUnidadeComFranqueadoAtivo =
        "uq_franqueados_unidades_unidade_ativa";
    private const string RestricaoNomeUsuarioUnico =
        "ix_usuarios_nome_usuario_normalizado";

    public async Task<IReadOnlyList<UsuarioFranqueadoraResumo>> ListarAsync(
        Guid organizacaoId,
        CancellationToken cancellationToken)
    {
        var vinculos = await dbContext.VinculosAcesso
            .AsNoTracking()
            .Where(vinculo => vinculo.OrganizacaoId == organizacaoId)
            .Select(vinculo => new VinculoListagem(
                vinculo.UsuarioId,
                vinculo.UnidadeId,
                vinculo.Perfil,
                vinculo.Ativo))
            .ToArrayAsync(cancellationToken);

        var relacoesComerciais = await (
            from relacao in dbContext.FranqueadosUsuarios.AsNoTracking()
            join franqueado in dbContext.Franqueados.AsNoTracking()
                on relacao.FranqueadoId equals franqueado.Id
            where franqueado.OrganizacaoId == organizacaoId
            select new RelacaoComercialListagem(
                relacao.UsuarioId,
                relacao.FranqueadoId,
                relacao.Ativo && franqueado.Ativo))
            .ToArrayAsync(cancellationToken);

        var usuariosIds = vinculos
            .Select(vinculo => vinculo.UsuarioId)
            .Concat(relacoesComerciais.Select(relacao => relacao.UsuarioId))
            .Distinct()
            .ToArray();

        if (usuariosIds.Length == 0)
        {
            return [];
        }

        var usuarios = await dbContext.Users
            .AsNoTracking()
            .Where(usuario => usuariosIds.Contains(usuario.Id))
            .Select(usuario => new UsuarioIdentityListagem(
                usuario.Id,
                usuario.Email ?? usuario.UserName ?? string.Empty))
            .ToArrayAsync(cancellationToken);
        var perfis = await dbContext.PerfisUsuario
            .AsNoTracking()
            .Where(perfil => usuariosIds.Contains(perfil.UsuarioId))
            .Select(perfil => new PerfilListagem(
                perfil.UsuarioId,
                perfil.NomeCompleto,
                perfil.Ativo))
            .ToDictionaryAsync(perfil => perfil.UsuarioId, cancellationToken);

        var unidadesAcessoIds = vinculos
            .Where(vinculo => vinculo.Ativo && vinculo.UnidadeId.HasValue)
            .Select(vinculo => vinculo.UnidadeId!.Value);
        var franqueadosAtivosIds = relacoesComerciais
            .Where(relacao => relacao.Ativo)
            .Select(relacao => relacao.FranqueadoId)
            .Distinct()
            .ToArray();
        var unidadesComerciais = await dbContext.FranqueadosUnidades
            .AsNoTracking()
            .Where(relacao => relacao.OrganizacaoId == organizacaoId
                && relacao.Ativo
                && franqueadosAtivosIds.Contains(relacao.FranqueadoId))
            .Select(relacao => new UnidadeComercialListagem(
                relacao.FranqueadoId,
                relacao.UnidadeId))
            .ToArrayAsync(cancellationToken);
        var unidadesIds = unidadesAcessoIds
            .Concat(unidadesComerciais.Select(relacao => relacao.UnidadeId))
            .Distinct()
            .ToArray();
        var nomesUnidades = await dbContext.Unidades
            .AsNoTracking()
            .Where(unidade => unidade.OrganizacaoId == organizacaoId
                && unidadesIds.Contains(unidade.Id))
            .ToDictionaryAsync(
                unidade => unidade.Id,
                unidade => unidade.Nome,
                cancellationToken);

        return usuarios
            .Select(usuario => MontarResumo(
                usuario,
                perfis.GetValueOrDefault(usuario.Id),
                vinculos,
                relacoesComerciais,
                unidadesComerciais,
                nomesUnidades))
            .OrderBy(usuario => usuario.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(usuario => usuario.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<UnidadeSelecaoUsuarioResumo>> ListarUnidadesAtivasAsync(
        Guid organizacaoId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Unidades
            .AsNoTracking()
            .Where(unidade => unidade.OrganizacaoId == organizacaoId && unidade.Ativa)
            .OrderBy(unidade => unidade.Nome)
            .Select(unidade => new UnidadeSelecaoUsuarioResumo(unidade.Id, unidade.Nome))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListarUnidadesValidasAsync(
        Guid organizacaoId,
        IReadOnlyCollection<Guid> unidadesIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.Unidades
            .AsNoTracking()
            .Where(unidade => unidade.OrganizacaoId == organizacaoId
                && unidade.Ativa
                && unidadesIds.Contains(unidade.Id))
            .Select(unidade => unidade.Id)
            .ToArrayAsync(cancellationToken);
    }

    public Task<string?> ObterUnidadeComFranqueadoAtivoAsync(
        Guid organizacaoId,
        IReadOnlyCollection<Guid> unidadesIds,
        CancellationToken cancellationToken)
    {
        return (
            from relacao in dbContext.FranqueadosUnidades.AsNoTracking()
            join unidade in dbContext.Unidades.AsNoTracking()
                on new { relacao.OrganizacaoId, relacao.UnidadeId }
                equals new { unidade.OrganizacaoId, UnidadeId = unidade.Id }
            where relacao.OrganizacaoId == organizacaoId
                && relacao.Ativo
                && unidadesIds.Contains(relacao.UnidadeId)
            orderby unidade.Nome
            select unidade.Nome)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExisteUsuarioPorEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await userManager.FindByEmailAsync(email) is not null;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    public Task<bool> ExisteFranqueadoPorDocumentoAsync(
        Guid organizacaoId,
        string documento,
        CancellationToken cancellationToken)
    {
        return dbContext.Franqueados
            .AsNoTracking()
            .AnyAsync(
                franqueado => franqueado.OrganizacaoId == organizacaoId
                    && franqueado.Documento == documento,
                cancellationToken);
    }

    public async Task<ResultadoPersistenciaCadastroUsuario> CriarAsync(
        CadastroUsuarioFranqueadora cadastro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cadastro);
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        var usuario = new UsuarioIdentity
        {
            Id = cadastro.UsuarioId,
            UserName = cadastro.Email,
            Email = cadastro.Email,
            EmailConfirmed = false
        };

        try
        {
            var resultadoIdentity = await userManager.CreateAsync(usuario);

            if (!resultadoIdentity.Succeeded)
            {
                return MapearFalhaIdentity(resultadoIdentity);
            }

            dbContext.PerfisUsuario.Add(cadastro.PerfilUsuario);
            dbContext.VinculosAcesso.AddRange(cadastro.VinculosAcesso);

            if (cadastro.Franqueado is not null)
            {
                dbContext.Franqueados.Add(cadastro.Franqueado);
            }

            if (cadastro.FranqueadoUsuario is not null)
            {
                dbContext.FranqueadosUsuarios.Add(cadastro.FranqueadoUsuario);
            }

            dbContext.FranqueadosUnidades.AddRange(cadastro.FranqueadosUnidades);
            await dbContext.SaveChangesAsync(cancellationToken);

            var token = await userManager.GeneratePasswordResetTokenAsync(usuario);

            if (string.IsNullOrWhiteSpace(token))
            {
                return new(EstadoPersistenciaCadastroUsuario.Falha);
            }

            await transacao.CommitAsync(cancellationToken);
            return new(EstadoPersistenciaCadastroUsuario.Sucesso, token);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.ConstraintName switch
            {
                RestricaoDocumentoUnico => new(
                    EstadoPersistenciaCadastroUsuario.DocumentoDuplicado),
                RestricaoUnidadeComFranqueadoAtivo => new(
                    EstadoPersistenciaCadastroUsuario.UnidadeComFranqueadoAtivo),
                RestricaoNomeUsuarioUnico => new(
                    EstadoPersistenciaCadastroUsuario.EmailDuplicado),
                _ => new(EstadoPersistenciaCadastroUsuario.Falha)
            };
        }
    }

    public async Task<UsuarioFranqueadoraEdicaoContexto?> ObterEdicaoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var usuario = await dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == usuarioId)
            .Select(item => new
            {
                item.Id,
                Email = item.Email ?? item.UserName ?? string.Empty
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (usuario is null)
        {
            return null;
        }

        var perfil = await dbContext.PerfisUsuario
            .AsNoTracking()
            .Where(item => item.UsuarioId == usuarioId)
            .Select(item => new { item.NomeCompleto, item.Telefone })
            .SingleOrDefaultAsync(cancellationToken);
        var organizacoesIds = await ListarOrganizacoesAtivasAsync(
            usuarioId,
            cancellationToken);

        return new UsuarioFranqueadoraEdicaoContexto(
            usuario.Id,
            perfil?.NomeCompleto,
            usuario.Email,
            perfil?.Telefone,
            organizacoesIds);
    }

    public async Task<ResultadoPersistenciaEdicaoUsuario> AtualizarAsync(
        AtualizarUsuarioFranqueadoraDados dados,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);
        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var usuario = await userManager.FindByIdAsync(dados.UsuarioId.ToString());

            if (usuario is null)
            {
                return new(EstadoPersistenciaEdicaoUsuario.UsuarioNaoEncontrado);
            }

            var organizacoesIds = await ListarOrganizacoesAtivasAsync(
                dados.UsuarioId,
                cancellationToken);

            if (!organizacoesIds.Contains(dados.OrganizacaoId))
            {
                return new(EstadoPersistenciaEdicaoUsuario.UsuarioNaoEncontrado);
            }

            if (organizacoesIds.Count > 1)
            {
                return new(
                    EstadoPersistenciaEdicaoUsuario.UsuarioComMultiplasOrganizacoes);
            }

            UsuarioIdentity? usuarioComEmail;

            try
            {
                usuarioComEmail = await userManager.FindByEmailAsync(dados.Email);
            }
            catch (InvalidOperationException)
            {
                return new(EstadoPersistenciaEdicaoUsuario.EmailDuplicado);
            }

            if (usuarioComEmail is not null && usuarioComEmail.Id != dados.UsuarioId)
            {
                return new(EstadoPersistenciaEdicaoUsuario.EmailDuplicado);
            }

            var resultadoEmail = await userManager.SetEmailAsync(usuario, dados.Email);

            if (!resultadoEmail.Succeeded)
            {
                return MapearFalhaEdicaoIdentity(resultadoEmail);
            }

            var resultadoNomeUsuario = await userManager.SetUserNameAsync(
                usuario,
                dados.Email);

            if (!resultadoNomeUsuario.Succeeded)
            {
                return MapearFalhaEdicaoIdentity(resultadoNomeUsuario);
            }

            var perfil = await dbContext.PerfisUsuario
                .SingleOrDefaultAsync(
                    item => item.UsuarioId == dados.UsuarioId,
                    cancellationToken);

            if (perfil is null)
            {
                perfil = new PerfilUsuario(
                    Guid.NewGuid(),
                    dados.UsuarioId,
                    dados.NomeCompleto,
                    dados.Telefone,
                    dados.AtualizadoEmUtc);
                dbContext.PerfisUsuario.Add(perfil);
            }
            else
            {
                perfil.AtualizarDados(
                    dados.NomeCompleto,
                    dados.Telefone,
                    dados.AtualizadoEmUtc);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return new(EstadoPersistenciaEdicaoUsuario.Sucesso);
        }
        catch (ArgumentException)
        {
            return new(EstadoPersistenciaEdicaoUsuario.DadosInvalidos);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.ConstraintName == RestricaoNomeUsuarioUnico
                ? new(EstadoPersistenciaEdicaoUsuario.EmailDuplicado)
                : new(EstadoPersistenciaEdicaoUsuario.Falha);
        }
    }

    private async Task<IReadOnlyList<Guid>> ListarOrganizacoesAtivasAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var organizacoesPorAcesso = await dbContext.VinculosAcesso
            .AsNoTracking()
            .Where(item => item.UsuarioId == usuarioId && item.Ativo)
            .Select(item => item.OrganizacaoId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var organizacoesPorFranqueado = await (
            from relacao in dbContext.FranqueadosUsuarios.AsNoTracking()
            join franqueado in dbContext.Franqueados.AsNoTracking()
                on relacao.FranqueadoId equals franqueado.Id
            where relacao.UsuarioId == usuarioId
                && relacao.Ativo
                && franqueado.Ativo
            select franqueado.OrganizacaoId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return organizacoesPorAcesso
            .Concat(organizacoesPorFranqueado)
            .Distinct()
            .ToArray();
    }

    private static UsuarioFranqueadoraResumo MontarResumo(
        UsuarioIdentityListagem usuario,
        PerfilListagem? perfil,
        IReadOnlyCollection<VinculoListagem> vinculos,
        IReadOnlyCollection<RelacaoComercialListagem> relacoesComerciais,
        IReadOnlyCollection<UnidadeComercialListagem> unidadesComerciais,
        IReadOnlyDictionary<Guid, string> nomesUnidades)
    {
        var vinculosUsuario = vinculos
            .Where(vinculo => vinculo.UsuarioId == usuario.Id && vinculo.Ativo)
            .ToArray();
        var relacoesUsuario = relacoesComerciais
            .Where(relacao => relacao.UsuarioId == usuario.Id && relacao.Ativo)
            .ToArray();
        var funcoes = vinculosUsuario
            .Select(vinculo => NomePerfil(vinculo.Perfil))
            .Concat(relacoesUsuario.Length > 0 ? ["Franqueado"] : [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(funcao => funcao, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var franqueadosIds = relacoesUsuario
            .Select(relacao => relacao.FranqueadoId)
            .ToHashSet();
        var unidadesIds = vinculosUsuario
            .Where(vinculo => vinculo.UnidadeId.HasValue)
            .Select(vinculo => vinculo.UnidadeId!.Value)
            .Concat(unidadesComerciais
                .Where(relacao => franqueadosIds.Contains(relacao.FranqueadoId))
                .Select(relacao => relacao.UnidadeId))
            .Distinct();
        var unidades = unidadesIds
            .Where(nomesUnidades.ContainsKey)
            .Select(unidadeId => nomesUnidades[unidadeId])
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(nome => nome, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var email = usuario.Email;
        var nome = string.IsNullOrWhiteSpace(perfil?.NomeCompleto)
            ? email
            : perfil.NomeCompleto;

        return new UsuarioFranqueadoraResumo(
            usuario.Id,
            nome,
            email,
            funcoes.Length == 0 ? ["Sem função ativa"] : funcoes,
            unidades,
            perfil?.Ativo ?? true);
    }

    private static string NomePerfil(PerfilAcesso perfil)
    {
        return perfil switch
        {
            PerfilAcesso.AdministradorRede => "Administrador de rede",
            PerfilAcesso.AdministradorUnidade => "Administrador de unidade",
            PerfilAcesso.Professor => "Professor",
            PerfilAcesso.Aluno => "Aluno",
            PerfilAcesso.Responsavel => "Responsável",
            _ => perfil.ToString()
        };
    }

    private static ResultadoPersistenciaCadastroUsuario MapearFalhaIdentity(
        IdentityResult resultado)
    {
        var emailDuplicado = resultado.Errors.Any(erro =>
            erro.Code is "DuplicateEmail" or "DuplicateUserName");

        return emailDuplicado
            ? new(EstadoPersistenciaCadastroUsuario.EmailDuplicado)
            : new(EstadoPersistenciaCadastroUsuario.DadosInvalidos);
    }

    private static ResultadoPersistenciaEdicaoUsuario MapearFalhaEdicaoIdentity(
        IdentityResult resultado)
    {
        var emailDuplicado = resultado.Errors.Any(erro =>
            erro.Code is "DuplicateEmail" or "DuplicateUserName");

        return emailDuplicado
            ? new(EstadoPersistenciaEdicaoUsuario.EmailDuplicado)
            : new(EstadoPersistenciaEdicaoUsuario.DadosInvalidos);
    }

    private sealed record VinculoListagem(
        Guid UsuarioId,
        Guid? UnidadeId,
        PerfilAcesso Perfil,
        bool Ativo);

    private sealed record RelacaoComercialListagem(
        Guid UsuarioId,
        Guid FranqueadoId,
        bool Ativo);

    private sealed record UnidadeComercialListagem(
        Guid FranqueadoId,
        Guid UnidadeId);

    private sealed record UsuarioIdentityListagem(Guid Id, string Email);

    private sealed record PerfilListagem(Guid UsuarioId, string NomeCompleto, bool Ativo);
}

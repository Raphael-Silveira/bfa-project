using BFA.Application.Unidades.Professores;
using BFA.Domain.Acessos;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Unidades;

public sealed class AcessoProfessorRepositorio(
    BfaDbContext dbContext,
    UserManager<UsuarioIdentity> userManager) : IAcessoProfessorRepositorio
{
    public async Task<AcessoProfessorResumo?> ObterAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken)
    {
        return await (
            from professor in dbContext.Professores.AsNoTracking()
            join profissional in dbContext.ProfessoresUnidades.AsNoTracking()
                on professor.Id equals profissional.ProfessorId
            join usuario in dbContext.Users.AsNoTracking()
                on professor.UsuarioId equals (Guid?)usuario.Id into usuarios
            from usuario in usuarios.DefaultIfEmpty()
            join acesso in dbContext.VinculosAcesso.AsNoTracking()
                    .Where(item => item.OrganizacaoId == organizacaoId
                        && item.UnidadeId == unidadeId
                        && item.Perfil == PerfilAcesso.Professor)
                on professor.UsuarioId equals (Guid?)acesso.UsuarioId into acessos
            from acesso in acessos.DefaultIfEmpty()
            where professor.OrganizacaoId == organizacaoId
                && professor.Id == professorId
                && profissional.OrganizacaoId == organizacaoId
                && profissional.UnidadeId == unidadeId
            select new AcessoProfessorResumo(
                professor.Id,
                professor.NomeCompleto,
                professor.Email,
                professor.UsuarioId,
                usuario == null ? null : usuario.UserName,
                acesso != null && acesso.Ativo))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ConcessaoAcessoProfessorResultado> ConcederAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        string nomeUsuario,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken)
    {
        var nomeNormalizado = nomeUsuario?.Trim();
        if (string.IsNullOrWhiteSpace(nomeNormalizado))
        {
            return new(EstadoAcessoProfessor.NomeUsuarioInvalido);
        }

        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        var professor = await dbContext.Professores.SingleOrDefaultAsync(
            item => item.OrganizacaoId == organizacaoId && item.Id == professorId,
            cancellationToken);
        var vinculoProfissional = await dbContext.ProfessoresUnidades
            .SingleOrDefaultAsync(
                item => item.OrganizacaoId == organizacaoId
                    && item.UnidadeId == unidadeId
                    && item.ProfessorId == professorId,
                cancellationToken);

        if (professor is null || vinculoProfissional is null)
        {
            return new(EstadoAcessoProfessor.ProfessorNaoEncontrado);
        }

        if (!professor.Ativo || !vinculoProfissional.Ativo)
        {
            return new(EstadoAcessoProfessor.VinculoProfissionalInativo);
        }

        UsuarioIdentity usuario;
        string? token = null;
        var usuarioCriado = false;
        if (professor.UsuarioId is { } usuarioId)
        {
            usuario = await userManager.FindByIdAsync(usuarioId.ToString())
                ?? throw new InvalidOperationException(
                    "O usuário associado ao professor não foi encontrado.");
        }
        else
        {
            if (await userManager.FindByNameAsync(nomeNormalizado) is not null)
            {
                return new(EstadoAcessoProfessor.NomeUsuarioDuplicado);
            }

            usuario = new UsuarioIdentity
            {
                Id = Guid.NewGuid(),
                UserName = nomeNormalizado,
                Email = string.IsNullOrWhiteSpace(professor.Email) ? null : professor.Email,
                EmailConfirmed = false
            };
            var criacao = await userManager.CreateAsync(usuario);
            if (!criacao.Succeeded)
            {
                return MapearFalhaIdentity(criacao);
            }

            professor.AlterarUsuario(usuario.Id, atualizadoEmUtc);
            usuarioCriado = true;
        }

        var vinculo = await dbContext.VinculosAcesso.SingleOrDefaultAsync(
            item => item.UsuarioId == usuario.Id
                && item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && item.Perfil == PerfilAcesso.Professor,
            cancellationToken);
        if (vinculo is { Ativo: true })
        {
            return new(EstadoAcessoProfessor.AcessoJaAtivo, usuario.Id, usuario.UserName);
        }

        if (vinculo is null)
        {
            dbContext.VinculosAcesso.Add(new VinculoAcesso(
                Guid.NewGuid(),
                usuario.Id,
                organizacaoId,
                unidadeId,
                PerfilAcesso.Professor,
                atualizadoEmUtc));
        }
        else
        {
            vinculo.Ativar(atualizadoEmUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (usuarioCriado)
        {
            token = await userManager.GeneratePasswordResetTokenAsync(usuario);
            if (string.IsNullOrWhiteSpace(token))
            {
                return new(EstadoAcessoProfessor.Falha);
            }
        }

        await transacao.CommitAsync(cancellationToken);
        return new(EstadoAcessoProfessor.Sucesso, usuario.Id, usuario.UserName, token);
    }

    public async Task<EstadoAcessoProfessor> RevogarAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken)
    {
        var professor = await dbContext.Professores.AsNoTracking().SingleOrDefaultAsync(
            item => item.OrganizacaoId == organizacaoId && item.Id == professorId,
            cancellationToken);
        var profissionalExiste = await dbContext.ProfessoresUnidades.AsNoTracking().AnyAsync(
            item => item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && item.ProfessorId == professorId,
            cancellationToken);
        if (professor is null || !profissionalExiste || professor.UsuarioId is null)
        {
            return EstadoAcessoProfessor.ProfessorNaoEncontrado;
        }

        var vinculo = await dbContext.VinculosAcesso.SingleOrDefaultAsync(
            item => item.UsuarioId == professor.UsuarioId
                && item.OrganizacaoId == organizacaoId
                && item.UnidadeId == unidadeId
                && item.Perfil == PerfilAcesso.Professor,
            cancellationToken);
        if (vinculo is null || !vinculo.Ativo)
        {
            return EstadoAcessoProfessor.AcessoNaoEncontrado;
        }

        vinculo.Desativar(atualizadoEmUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return EstadoAcessoProfessor.Sucesso;
    }

    private static ConcessaoAcessoProfessorResultado MapearFalhaIdentity(
        IdentityResult resultado)
    {
        if (resultado.Errors.Any(erro => erro.Code is "DuplicateUserName"))
        {
            return new(EstadoAcessoProfessor.NomeUsuarioDuplicado);
        }

        if (resultado.Errors.Any(erro => erro.Code is "InvalidUserName"))
        {
            return new(EstadoAcessoProfessor.NomeUsuarioInvalido);
        }

        return new(EstadoAcessoProfessor.Falha);
    }
}

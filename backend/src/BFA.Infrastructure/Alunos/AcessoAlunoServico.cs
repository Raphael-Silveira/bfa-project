using System.Security.Cryptography;
using BFA.Application.Alunos;
using BFA.Application.Acessos;
using BFA.Application.Identidade;
using BFA.Domain.Acessos;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BFA.Infrastructure.Alunos;

public sealed class AcessoAlunoServico(
    BfaDbContext dbContext,
    UserManager<UsuarioIdentity> userManager,
    IAcessoUsuarioConsulta acessoUsuarioConsulta,
    ILogger<AcessoAlunoServico> logger) : IAcessoAlunoServico
{
    public async Task<ResultadoAcessoAluno> ConcederAsync(
        Guid usuarioOperadorId,
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        if (usuarioOperadorId == Guid.Empty || unidadeId == Guid.Empty || alunoId == Guid.Empty)
        {
            return new(EstadoAcessoAluno.SemAcesso);
        }

        var aluno = await dbContext.Alunos
            .SingleOrDefaultAsync(item => item.Id == alunoId, cancellationToken);
        var unidade = aluno is null
            ? null
            : await dbContext.Unidades.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.OrganizacaoId == aluno.OrganizacaoId
                        && item.Id == unidadeId
                        && item.Ativa,
                    cancellationToken);

        if (aluno is null || unidade is null)
        {
            return new(EstadoAcessoAluno.AlunoNaoEncontrado);
        }

        var autorizado = await acessoUsuarioConsulta.EhAdministradorRedeNaOrganizacaoAsync(
                usuarioOperadorId,
                aluno.OrganizacaoId,
                cancellationToken)
            || await acessoUsuarioConsulta.PossuiPerfilNaUnidadeAsync(
                usuarioOperadorId,
                aluno.OrganizacaoId,
                unidadeId,
                PerfilAcesso.AdministradorUnidade,
                cancellationToken);

        if (!autorizado)
        {
            return new(EstadoAcessoAluno.SemAcesso);
        }

        if (!CpfIdentificador.TentarNormalizar(aluno.Cpf, out var cpf))
        {
            return new(EstadoAcessoAluno.CpfNaoInformado);
        }

        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        UsuarioIdentity? usuario;
        string? senhaTemporaria = null;
        var acessoJaExistente = aluno.UsuarioId.HasValue;
        if (aluno.UsuarioId is { } usuarioId)
        {
            usuario = await userManager.FindByIdAsync(usuarioId.ToString());
            if (usuario is null)
            {
                return new(EstadoAcessoAluno.UsuarioIncompativel);
            }
        }
        else
        {
            usuario = await userManager.FindByNameAsync(cpf);
            if (usuario is not null)
            {
                var possuiOutroAluno = await dbContext.Alunos.AnyAsync(
                    item => item.OrganizacaoId == aluno.OrganizacaoId
                        && item.Id != aluno.Id
                        && item.UsuarioId == usuario.Id,
                    cancellationToken);
                if (possuiOutroAluno)
                {
                    return new(EstadoAcessoAluno.CpfDuplicado);
                }

                return new(EstadoAcessoAluno.UsuarioIncompativel);
            }

            senhaTemporaria = GerarSenhaTemporaria();
            usuario = new UsuarioIdentity
            {
                Id = Guid.NewGuid(),
                UserName = cpf,
                Email = string.IsNullOrWhiteSpace(aluno.Email) ? null : aluno.Email,
                EmailConfirmed = false
            };

            var criacao = await userManager.CreateAsync(usuario, senhaTemporaria);
            if (!criacao.Succeeded)
            {
                logger.LogWarning(
                    "Provisionamento de acesso do aluno rejeitado: {Erros}",
                    string.Join(", ", criacao.Errors.Select(error => error.Code)));
                return new(EstadoAcessoAluno.Falha);
            }

            var claim = await userManager.AddClaimAsync(
                usuario,
                new(IdentidadeClaims.TrocaSenhaObrigatoria, "true"));
            if (!claim.Succeeded)
            {
                logger.LogWarning("Claim de troca obrigatória não pôde ser criada");
                return new(EstadoAcessoAluno.Falha);
            }

            aluno.AlterarUsuario(usuario.Id, DateTime.UtcNow);
        }

        var vinculo = await dbContext.VinculosAcesso.SingleOrDefaultAsync(
            item => item.UsuarioId == usuario.Id
                && item.OrganizacaoId == aluno.OrganizacaoId
                && item.UnidadeId == unidadeId
                && item.Perfil == PerfilAcesso.Aluno,
            cancellationToken);

        if (vinculo is null)
        {
            dbContext.VinculosAcesso.Add(new VinculoAcesso(
                Guid.NewGuid(),
                usuario.Id,
                aluno.OrganizacaoId,
                unidadeId,
                PerfilAcesso.Aluno,
                DateTime.UtcNow));
        }
        else if (!vinculo.Ativo)
        {
            vinculo.Ativar(DateTime.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Acesso de aluno provisionado: {AlunoId} na unidade {UnidadeId}",
            aluno.Id,
            unidadeId);

        return new(
            acessoJaExistente
                ? EstadoAcessoAluno.AcessoJaExistente
                : EstadoAcessoAluno.Sucesso,
            usuario.Id,
            usuario.UserName,
            senhaTemporaria);
    }

    public async Task<ResultadoAcessoAluno> RedefinirSenhaAsync(
        Guid usuarioOperadorId,
        Guid unidadeId,
        Guid alunoId,
        CancellationToken cancellationToken)
    {
        if (usuarioOperadorId == Guid.Empty || unidadeId == Guid.Empty || alunoId == Guid.Empty)
        {
            return new(EstadoAcessoAluno.SemAcesso);
        }

        var aluno = await dbContext.Alunos
            .SingleOrDefaultAsync(item => item.Id == alunoId, cancellationToken);
        var unidade = aluno is null
            ? null
            : await dbContext.Unidades.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.OrganizacaoId == aluno.OrganizacaoId
                        && item.Id == unidadeId
                        && item.Ativa,
                    cancellationToken);
        if (aluno is null || unidade is null)
        {
            return new(EstadoAcessoAluno.AlunoNaoEncontrado);
        }

        var relacionado = await dbContext.Matriculas.AsNoTracking().AnyAsync(
            item => item.OrganizacaoId == aluno.OrganizacaoId
                && item.UnidadeId == unidadeId
                && item.AlunoId == alunoId,
            cancellationToken);
        if (!relacionado)
        {
            return new(EstadoAcessoAluno.AlunoNaoEncontrado);
        }

        var autorizado = await acessoUsuarioConsulta.EhAdministradorRedeNaOrganizacaoAsync(
                usuarioOperadorId,
                aluno.OrganizacaoId,
                cancellationToken)
            || await acessoUsuarioConsulta.PossuiPerfilNaUnidadeAsync(
                usuarioOperadorId,
                aluno.OrganizacaoId,
                unidadeId,
                PerfilAcesso.AdministradorUnidade,
                cancellationToken);
        if (!autorizado)
        {
            return new(EstadoAcessoAluno.SemAcesso);
        }

        if (aluno.UsuarioId is not { } usuarioId)
        {
            return new(EstadoAcessoAluno.UsuarioIncompativel);
        }

        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
        {
            return new(EstadoAcessoAluno.UsuarioIncompativel);
        }

        await using var transacao = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        var senhaTemporaria = GerarSenhaTemporaria();
        var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
        var reset = await userManager.ResetPasswordAsync(usuario, token, senhaTemporaria);
        if (!reset.Succeeded)
        {
            logger.LogWarning(
                "Reset de senha do aluno rejeitado: {Erros}",
                string.Join(", ", reset.Errors.Select(error => error.Code)));
            return new(EstadoAcessoAluno.Falha);
        }

        var claims = await userManager.GetClaimsAsync(usuario);
        var claim = claims.FirstOrDefault(item =>
            item.Type == IdentidadeClaims.TrocaSenhaObrigatoria);
        var claimResult = claim is null
            ? await userManager.AddClaimAsync(
                usuario,
                new(IdentidadeClaims.TrocaSenhaObrigatoria, "true"))
            : await userManager.ReplaceClaimAsync(
                usuario,
                claim,
                new(IdentidadeClaims.TrocaSenhaObrigatoria, "true"));
        if (!claimResult.Succeeded)
        {
            logger.LogWarning(
                "Claim de troca obrigatória não pôde ser restaurada para o aluno {AlunoId}",
                alunoId);
            return new(EstadoAcessoAluno.Falha);
        }

        var stamp = await userManager.UpdateSecurityStampAsync(usuario);
        if (!stamp.Succeeded)
        {
            logger.LogWarning(
                "Security stamp não pôde ser atualizado para o aluno {AlunoId}",
                alunoId);
            return new(EstadoAcessoAluno.Falha);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Senha temporária do aluno redefinida: {AlunoId} na unidade {UnidadeId}",
            alunoId,
            unidadeId);

        return new(
            EstadoAcessoAluno.Sucesso,
            usuario.Id,
            usuario.UserName,
            senhaTemporaria);
    }

    private static string GerarSenhaTemporaria()
    {
        var numero = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return $"Bfa-{numero:D6}";
    }
}

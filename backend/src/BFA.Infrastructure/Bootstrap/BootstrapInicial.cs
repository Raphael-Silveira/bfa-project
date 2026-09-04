using System.ComponentModel.DataAnnotations;
using BFA.Application.Bootstrap;
using BFA.Domain.Acessos;
using BFA.Domain.Organizacoes;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BFA.Infrastructure.Bootstrap;

public sealed class BootstrapInicial(
    BfaDbContext dbContext,
    UserManager<UsuarioIdentity> userManager,
    ILogger<BootstrapInicial> logger) : IBootstrapInicial
{
    private const string NomeOrganizacao = "Brazilian Footvolley Academy";
    private const string SlugOrganizacao = "bfa";

    public async Task<BootstrapInicialResultado> ExecutarAsync(
        BootstrapInicialSolicitacao solicitacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);

        var credenciais = ValidarCredenciais(solicitacao);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var (organizacao, organizacaoCriada) = await ObterOrganizacaoAsync(
            cancellationToken);
        var resultados = new List<AdministradorBootstrapResultado>(credenciais.Count);

        foreach (var credencial in credenciais)
        {
            var (usuario, usuarioCriado) = await ObterUsuarioAsync(
                credencial,
                cancellationToken);
            var vinculoCriado = await GarantirVinculoAsync(
                usuario,
                organizacao,
                credencial.Numero,
                cancellationToken);

            resultados.Add(new AdministradorBootstrapResultado(
                credencial.Numero,
                usuarioCriado,
                vinculoCriado));
        }

        await transaction.CommitAsync(cancellationToken);

        return new BootstrapInicialResultado(organizacaoCriada, resultados);
    }

    private IReadOnlyList<CredencialValidada> ValidarCredenciais(
        BootstrapInicialSolicitacao solicitacao)
    {
        var credenciais = new[]
        {
            ValidarCredencial(1, solicitacao.Administrador1),
            ValidarCredencial(2, solicitacao.Administrador2)
        };

        var primeiroEmailNormalizado = userManager.NormalizeEmail(credenciais[0].Email);
        var segundoEmailNormalizado = userManager.NormalizeEmail(credenciais[1].Email);

        if (string.Equals(
                primeiroEmailNormalizado,
                segundoEmailNormalizado,
                StringComparison.Ordinal))
        {
            throw new BootstrapInicialException(
                "Os emails dos dois administradores devem ser diferentes.");
        }

        return credenciais;
    }

    private static CredencialValidada ValidarCredencial(
        int numero,
        CredenciaisAdministradorBootstrap credencial)
    {
        var email = credencial.Email.Trim();

        if (email.Length == 0)
        {
            throw new BootstrapInicialException(
                $"O email do Administrador {numero} deve ser informado.");
        }

        if (email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
        {
            throw new BootstrapInicialException(
                $"O email do Administrador {numero} é inválido.");
        }

        if (string.IsNullOrWhiteSpace(credencial.Senha))
        {
            throw new BootstrapInicialException(
                $"A senha do Administrador {numero} deve ser informada.");
        }

        return new CredencialValidada(numero, email, credencial.Senha);
    }

    private async Task<(Organizacao Organizacao, bool Criada)> ObterOrganizacaoAsync(
        CancellationToken cancellationToken)
    {
        var organizacao = await dbContext.Organizacoes.SingleOrDefaultAsync(
            item => item.Slug == SlugOrganizacao,
            cancellationToken);

        if (organizacao is not null)
        {
            if (!organizacao.Ativa
                || !string.Equals(organizacao.Nome, NomeOrganizacao, StringComparison.Ordinal))
            {
                throw new BootstrapInicialException(
                    "A organização existente com slug bfa é incompatível com o bootstrap inicial.");
            }

            return (organizacao, false);
        }

        organizacao = new Organizacao(
            Guid.NewGuid(),
            NomeOrganizacao,
            SlugOrganizacao,
            DateTime.UtcNow);
        dbContext.Organizacoes.Add(organizacao);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (organizacao, true);
    }

    private async Task<(UsuarioIdentity Usuario, bool Criado)> ObterUsuarioAsync(
        CredencialValidada credencial,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        UsuarioIdentity? usuario;

        try
        {
            usuario = await userManager.FindByEmailAsync(credencial.Email);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception,
                "Falha ao identificar Administrador {Numero} por email", credencial.Numero);
            throw new BootstrapInicialException(
                $"Não foi possível identificar unicamente o Administrador {credencial.Numero}.");
        }

        if (usuario is not null)
        {
            if (usuario.Id == Guid.Empty)
            {
                throw new BootstrapInicialException(
                    $"O Administrador {credencial.Numero} existente possui identificador inválido.");
            }

            return (usuario, false);
        }

        usuario = new UsuarioIdentity
        {
            Id = Guid.NewGuid(),
            UserName = credencial.Email,
            Email = credencial.Email
        };

        var resultado = await userManager.CreateAsync(usuario, credencial.Senha);

        if (!resultado.Succeeded)
        {
            var codigos = string.Join(
                ", ",
                resultado.Errors
                    .Select(erro => erro.Code)
                    .Where(codigo => !string.IsNullOrWhiteSpace(codigo))
                    .Distinct(StringComparer.Ordinal));

            if (codigos.Length == 0)
            {
                codigos = "erro não especificado";
            }

            logger.LogError(
                "Falha ao criar Administrador {Numero}: {Codigos}",
                credencial.Numero, codigos);
            throw new BootstrapInicialException(
                $"Não foi possível criar o Administrador {credencial.Numero}: {codigos}.");
        }

        return (usuario, true);
    }

    private async Task<bool> GarantirVinculoAsync(
        UsuarioIdentity usuario,
        Organizacao organizacao,
        int numeroAdministrador,
        CancellationToken cancellationToken)
    {
        var vinculos = await dbContext.VinculosAcesso
            .Where(vinculo => vinculo.UsuarioId == usuario.Id
                && vinculo.OrganizacaoId == organizacao.Id
                && vinculo.UnidadeId == null
                && vinculo.Perfil == PerfilAcesso.AdministradorRede)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (vinculos.Count > 1)
        {
            throw new BootstrapInicialException(
                $"O Administrador {numeroAdministrador} possui vínculos AdministradorRede duplicados.");
        }

        if (vinculos.SingleOrDefault() is { } vinculoExistente)
        {
            if (!vinculoExistente.Ativo)
            {
                throw new BootstrapInicialException(
                    $"O Administrador {numeroAdministrador} possui vínculo AdministradorRede inativo.");
            }

            return false;
        }

        var vinculo = new VinculoAcesso(
            Guid.NewGuid(),
            usuario.Id,
            organizacao.Id,
            null,
            PerfilAcesso.AdministradorRede,
            DateTime.UtcNow);
        dbContext.VinculosAcesso.Add(vinculo);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private sealed class CredencialValidada(int numero, string email, string senha)
    {
        public int Numero { get; } = numero;

        public string Email { get; } = email;

        public string Senha { get; } = senha;
    }
}

using System.Security.Cryptography;
using BFA.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BFA.IntegrationTests;

public sealed class LoginWebApplicationFactory : BfaWebApplicationFactory
{
    public TestUsuarioStore UsuarioStore => Services.GetRequiredService<TestUsuarioStore>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserStore<UsuarioIdentity>>();
            services.AddSingleton<TestUsuarioStore>();
            services.AddSingleton<IUserStore<UsuarioIdentity>>(serviceProvider =>
                serviceProvider.GetRequiredService<TestUsuarioStore>());
        });
    }
}

public sealed class TestUsuarioStore :
    IUserEmailStore<UsuarioIdentity>,
    IUserPasswordStore<UsuarioIdentity>
{
    public TestUsuarioStore()
    {
        Email = $"login-{Guid.NewGuid():N}@example.invalid";
        Senha = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Usuario = new UsuarioIdentity
        {
            Id = Guid.NewGuid(),
            Email = Email,
            NormalizedEmail = Email.ToUpperInvariant(),
            UserName = Email,
            NormalizedUserName = Email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString()
        };
        Usuario.PasswordHash = new PasswordHasher<UsuarioIdentity>()
            .HashPassword(Usuario, Senha);
    }

    public string Email { get; }

    public string Senha { get; }

    public UsuarioIdentity Usuario { get; }

    public Task<IdentityResult> CreateAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IdentityResult.Success);
    }

    public void Dispose()
    {
    }

    public Task<UsuarioIdentity?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = string.Equals(
            normalizedEmail,
            Usuario.NormalizedEmail,
            StringComparison.Ordinal)
            ? Usuario
            : null;

        return Task.FromResult(usuario);
    }

    public Task<UsuarioIdentity?> FindByIdAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = string.Equals(
            userId,
            Usuario.Id.ToString(),
            StringComparison.Ordinal)
            ? Usuario
            : null;

        return Task.FromResult(usuario);
    }

    public Task<UsuarioIdentity?> FindByNameAsync(
        string normalizedUserName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = string.Equals(
            normalizedUserName,
            Usuario.NormalizedUserName,
            StringComparison.Ordinal)
            ? Usuario
            : null;

        return Task.FromResult(usuario);
    }

    public Task<string?> GetEmailAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.EmailConfirmed);
    }

    public Task<string?> GetNormalizedEmailAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.NormalizedEmail);
    }

    public Task<string?> GetNormalizedUserNameAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.NormalizedUserName);
    }

    public Task<string?> GetPasswordHashAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.PasswordHash);
    }

    public Task<string> GetUserIdAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.Id.ToString());
    }

    public Task<string?> GetUserNameAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.UserName);
    }

    public Task<bool> HasPasswordAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
    }

    public Task SetEmailAsync(
        UsuarioIdentity user,
        string? email,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task SetEmailConfirmedAsync(
        UsuarioIdentity user,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task SetNormalizedEmailAsync(
        UsuarioIdentity user,
        string? normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public Task SetNormalizedUserNameAsync(
        UsuarioIdentity user,
        string? normalizedName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetPasswordHashAsync(
        UsuarioIdentity user,
        string? passwordHash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task SetUserNameAsync(
        UsuarioIdentity user,
        string? userName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<IdentityResult> UpdateAsync(
        UsuarioIdentity user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IdentityResult.Success);
    }
}

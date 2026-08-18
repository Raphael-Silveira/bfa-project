using BFA.Application.Acessos;
using BFA.Domain.Acessos;
using BFA.Web.Acessos;
using BFA.Web.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace BFA.Web;

public static class AuthorizationDependencyInjection
{
    public static IServiceCollection AddBfaAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddScoped<IUsuarioAtual, UsuarioAtual>();
        services.AddScoped<IDestinoPosLogin, DestinoPosLogin>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(PoliticasAcesso.AdministradorRede, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new AdministradorRedeRequirement());
            });
            options.AddPolicy(PoliticasAcesso.Administracao, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PerfilAcessoRequirement(
                    PerfilAcesso.AdministradorRede,
                    PerfilAcesso.AdministradorUnidade));
            });
            options.AddPolicy(PoliticasAcesso.Professor, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PerfilAcessoRequirement(PerfilAcesso.Professor));
            });
            options.AddPolicy(PoliticasAcesso.Aluno, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PerfilAcessoRequirement(PerfilAcesso.Aluno));
            });
            options.AddPolicy(PoliticasAcesso.Responsavel, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PerfilAcessoRequirement(PerfilAcesso.Responsavel));
            });
            options.AddPolicy(PoliticasAcesso.AcessoUnidade, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new AcessoUnidadeRequirement());
            });
        });

        services.AddScoped<IAuthorizationHandler, AdministradorRedeHandler>();
        services.AddScoped<IAuthorizationHandler, PerfilAcessoHandler>();
        services.AddScoped<IAuthorizationHandler, AcessoUnidadeHandler>();
        services.AddScoped<IAuthorizationHandler, AcessoUnidadePorPerfilHandler>();

        return services;
    }
}

using BFA.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed class AuthorizationRegistrationTests : IClassFixture<BfaWebApplicationFactory>
{
    private readonly IAuthorizationPolicyProvider _policyProvider;

    public AuthorizationRegistrationTests(BfaWebApplicationFactory application)
    {
        _policyProvider = application.Services.GetRequiredService<IAuthorizationPolicyProvider>();
    }

    [Theory]
    [InlineData(PoliticasAcesso.AdministradorRede)]
    [InlineData(PoliticasAcesso.Administracao)]
    [InlineData(PoliticasAcesso.Professor)]
    [InlineData(PoliticasAcesso.Aluno)]
    [InlineData(PoliticasAcesso.Responsavel)]
    [InlineData(PoliticasAcesso.AcessoUnidade)]
    public async Task Policy_esta_registrada_e_exige_usuario_autenticado(string policyName)
    {
        var policy = await _policyProvider.GetPolicyAsync(policyName);

        Assert.NotNull(policy);
        Assert.Contains(
            policy.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }
}

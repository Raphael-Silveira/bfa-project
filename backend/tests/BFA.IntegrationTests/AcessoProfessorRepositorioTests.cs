using BFA.Application.Unidades.Professores;
using BFA.Domain.Acessos;
using BFA.Domain.Professores;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Unidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed class AcessoProfessorRepositorioTests
{
    private static readonly DateTime Agora = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Concessao_sem_email_cria_conta_sem_senha_e_vinculo_da_unidade()
    {
        await using var cenario = await CriarCenarioAsync(email: null);
        var resultado = await cenario.Repositorio.ConcederAsync(
            cenario.OrganizacaoId,
            cenario.UnidadeId,
            cenario.ProfessorId,
            "  professor.cerquilho  ",
            Agora.AddMinutes(1),
            CancellationToken.None);

        Assert.Equal(EstadoAcessoProfessor.Sucesso, resultado.Estado);
        Assert.Equal("professor.cerquilho", resultado.NomeUsuario);
        Assert.False(string.IsNullOrWhiteSpace(resultado.TokenDefinicaoSenha));
        var professor = await cenario.DbContext.Professores.SingleAsync();
        Assert.Equal(resultado.UsuarioId, professor.UsuarioId);
        var usuario = await cenario.UserManager.FindByIdAsync(resultado.UsuarioId!.Value.ToString());
        Assert.NotNull(usuario);
        Assert.Null(usuario.Email);
        Assert.False(await cenario.UserManager.HasPasswordAsync(usuario));
        Assert.True(await cenario.UserManager.VerifyUserTokenAsync(
            usuario,
            TokenOptions.DefaultProvider,
            UserManager<UsuarioIdentity>.ResetPasswordTokenPurpose,
            resultado.TokenDefinicaoSenha!));
        var acesso = await cenario.DbContext.VinculosAcesso.SingleAsync();
        Assert.Equal(PerfilAcesso.Professor, acesso.Perfil);
        Assert.Equal(cenario.UnidadeId, acesso.UnidadeId);
        Assert.True(acesso.Ativo);
    }

    [Fact]
    public async Task Segunda_unidade_reutiliza_conta_sem_gerar_novo_primeiro_acesso()
    {
        await using var cenario = await CriarCenarioAsync("professor@bfa.test");
        var primeira = await cenario.Repositorio.ConcederAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.ProfessorId,
            "professor.rede", Agora.AddMinutes(1), CancellationToken.None);
        var outraUnidadeId = Guid.NewGuid();
        cenario.DbContext.ProfessoresUnidades.Add(new ProfessorUnidade(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.ProfessorId,
            outraUnidadeId, Agora));
        await cenario.DbContext.SaveChangesAsync();

        var segunda = await cenario.Repositorio.ConcederAsync(
            cenario.OrganizacaoId, outraUnidadeId, cenario.ProfessorId,
            "nome-ignorado", Agora.AddMinutes(2), CancellationToken.None);

        Assert.Equal(EstadoAcessoProfessor.Sucesso, segunda.Estado);
        Assert.Equal(primeira.UsuarioId, segunda.UsuarioId);
        Assert.Null(segunda.TokenDefinicaoSenha);
        Assert.Equal(2, await cenario.DbContext.VinculosAcesso.CountAsync());
        Assert.Single(await cenario.DbContext.Users.ToArrayAsync());
    }

    [Fact]
    public async Task Revogacao_afeta_somente_a_unidade_e_preserva_vinculo_profissional()
    {
        await using var cenario = await CriarCenarioAsync(null);
        var primeira = await cenario.Repositorio.ConcederAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.ProfessorId,
            "professor.unico", Agora.AddMinutes(1), CancellationToken.None);
        var outraUnidadeId = Guid.NewGuid();
        cenario.DbContext.ProfessoresUnidades.Add(new ProfessorUnidade(
            Guid.NewGuid(), cenario.OrganizacaoId, cenario.ProfessorId,
            outraUnidadeId, Agora));
        cenario.DbContext.VinculosAcesso.Add(new VinculoAcesso(
            Guid.NewGuid(), primeira.UsuarioId!.Value, cenario.OrganizacaoId,
            outraUnidadeId, PerfilAcesso.Professor, Agora));
        await cenario.DbContext.SaveChangesAsync();

        var estado = await cenario.Repositorio.RevogarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.ProfessorId,
            Agora.AddMinutes(3), CancellationToken.None);

        Assert.Equal(EstadoAcessoProfessor.Sucesso, estado);
        var acessos = await cenario.DbContext.VinculosAcesso.OrderBy(x => x.UnidadeId).ToArrayAsync();
        Assert.False(acessos.Single(x => x.UnidadeId == cenario.UnidadeId).Ativo);
        Assert.True(acessos.Single(x => x.UnidadeId == outraUnidadeId).Ativo);
        Assert.All(await cenario.DbContext.ProfessoresUnidades.ToArrayAsync(), item => Assert.True(item.Ativo));
        Assert.Single(await cenario.DbContext.Users.ToArrayAsync());
    }

    [Fact]
    public async Task Nome_usuario_duplicado_retorna_erro_amigavel_sem_associar_professor()
    {
        await using var cenario = await CriarCenarioAsync(null);
        await cenario.UserManager.CreateAsync(new UsuarioIdentity
        {
            Id = Guid.NewGuid(),
            UserName = "usuario.existente"
        });

        var resultado = await cenario.Repositorio.ConcederAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.ProfessorId,
            " usuario.existente ", Agora.AddMinutes(1), CancellationToken.None);

        Assert.Equal(EstadoAcessoProfessor.NomeUsuarioDuplicado, resultado.Estado);
        Assert.Null((await cenario.DbContext.Professores.SingleAsync()).UsuarioId);
        Assert.Empty(await cenario.DbContext.VinculosAcesso.ToArrayAsync());
    }

    [Fact]
    public async Task Nova_concessao_reativa_mesmo_vinculo_revogado()
    {
        await using var cenario = await CriarCenarioAsync(null);
        var primeira = await cenario.Repositorio.ConcederAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.ProfessorId,
            "professor.reativado", Agora.AddMinutes(1), CancellationToken.None);
        var vinculoId = (await cenario.DbContext.VinculosAcesso.SingleAsync()).Id;
        await cenario.Repositorio.RevogarAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.ProfessorId,
            Agora.AddMinutes(2), CancellationToken.None);

        var segunda = await cenario.Repositorio.ConcederAsync(
            cenario.OrganizacaoId, cenario.UnidadeId, cenario.ProfessorId,
            "professor.reativado", Agora.AddMinutes(3), CancellationToken.None);

        Assert.Equal(EstadoAcessoProfessor.Sucesso, segunda.Estado);
        Assert.Equal(primeira.UsuarioId, segunda.UsuarioId);
        Assert.Null(segunda.TokenDefinicaoSenha);
        var vinculo = await cenario.DbContext.VinculosAcesso.SingleAsync();
        Assert.Equal(vinculoId, vinculo.Id);
        Assert.True(vinculo.Ativo);
    }

    [Fact]
    public async Task Outro_tenant_nao_localiza_professor_para_conceder_ou_revogar()
    {
        await using var cenario = await CriarCenarioAsync(null);
        var outraOrganizacao = Guid.NewGuid();

        var concessao = await cenario.Repositorio.ConcederAsync(
            outraOrganizacao, cenario.UnidadeId, cenario.ProfessorId,
            "professor.bloqueado", Agora.AddMinutes(1), CancellationToken.None);
        var revogacao = await cenario.Repositorio.RevogarAsync(
            outraOrganizacao, cenario.UnidadeId, cenario.ProfessorId,
            Agora.AddMinutes(2), CancellationToken.None);

        Assert.Equal(EstadoAcessoProfessor.ProfessorNaoEncontrado, concessao.Estado);
        Assert.Equal(EstadoAcessoProfessor.ProfessorNaoEncontrado, revogacao);
        Assert.Null((await cenario.DbContext.Professores.SingleAsync()).UsuarioId);
        Assert.Empty(await cenario.DbContext.VinculosAcesso.ToArrayAsync());
    }

    private static async Task<Cenario> CriarCenarioAsync(string? email)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddDbContext<BfaDbContext>(options => options
            .UseInMemoryDatabase($"acesso-professor-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(
                InMemoryEventId.TransactionIgnoredWarning)));
        services.AddIdentityCore<UsuarioIdentity>()
            .AddEntityFrameworkStores<BfaDbContext>()
            .AddDefaultTokenProviders();
        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<BfaDbContext>();
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var professorId = Guid.NewGuid();
        dbContext.Professores.Add(new Professor(
            professorId, organizacaoId, "Professor BFA", Agora, email: email));
        dbContext.ProfessoresUnidades.Add(new ProfessorUnidade(
            Guid.NewGuid(), organizacaoId, professorId, unidadeId, Agora));
        await dbContext.SaveChangesAsync();
        return new Cenario(
            provider,
            dbContext,
            provider.GetRequiredService<UserManager<UsuarioIdentity>>(),
            new AcessoProfessorRepositorio(
                dbContext,
                provider.GetRequiredService<UserManager<UsuarioIdentity>>()),
            organizacaoId,
            unidadeId,
            professorId);
    }

    private sealed record Cenario(
        ServiceProvider Provider,
        BfaDbContext DbContext,
        UserManager<UsuarioIdentity> UserManager,
        AcessoProfessorRepositorio Repositorio,
        Guid OrganizacaoId,
        Guid UnidadeId,
        Guid ProfessorId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
        }
    }
}

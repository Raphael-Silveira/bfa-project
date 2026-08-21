using BFA.Application.Franqueadora.Franqueados;
using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.IntegrationTests;

public sealed class DiagnosticoVinculosFranqueadoConsultaTests
{
    [Fact]
    public async Task Relata_as_duas_direcoes_sem_inferir_relacao_de_administrador_comum()
    {
        using var application = new UsuariosFranqueadoraWebApplicationFactory();
        var organizacaoId = await application.InicializarAdministradorAsync();
        var agoraUtc = DateTime.UtcNow;
        var franqueadoId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var administradorComumId = Guid.NewGuid();
        var unidadeConsistente = NovaUnidade(organizacaoId, "Unidade consistente", agoraUtc);
        var unidadeSemComercial = NovaUnidade(organizacaoId, "Unidade sem comercial", agoraUtc);
        var unidadeSemAcesso = NovaUnidade(organizacaoId, "Unidade sem acesso", agoraUtc);
        var unidadeAdministradorComum = NovaUnidade(
            organizacaoId,
            "Unidade do administrador comum",
            agoraUtc);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BfaDbContext>();
            dbContext.Unidades.AddRange(
                unidadeConsistente,
                unidadeSemComercial,
                unidadeSemAcesso,
                unidadeAdministradorComum);
            dbContext.Franqueados.Add(new Franqueado(
                franqueadoId,
                organizacaoId,
                TipoPessoaFranqueado.PessoaJuridica,
                "Franqueado diagnóstico",
                "12345678000195",
                "diagnostico@bfa.test",
                agoraUtc));
            dbContext.FranqueadosUsuarios.Add(new FranqueadoUsuario(
                Guid.NewGuid(),
                franqueadoId,
                principalId,
                principal: true,
                agoraUtc));
            dbContext.FranqueadosUnidades.AddRange(
                NovoVinculo(franqueadoId, organizacaoId, unidadeConsistente.Id, agoraUtc),
                NovoVinculo(franqueadoId, organizacaoId, unidadeSemAcesso.Id, agoraUtc));
            dbContext.VinculosAcesso.AddRange(
                NovoAcesso(principalId, organizacaoId, unidadeConsistente.Id, agoraUtc),
                NovoAcesso(principalId, organizacaoId, unidadeSemComercial.Id, agoraUtc),
                NovoAcesso(
                    administradorComumId,
                    organizacaoId,
                    unidadeAdministradorComum.Id,
                    agoraUtc));
            await dbContext.SaveChangesAsync();
        }

        await using var consultaScope = application.Services.CreateAsyncScope();
        var consulta = consultaScope.ServiceProvider
            .GetRequiredService<IDiagnosticoVinculosFranqueadoConsulta>();
        var resultado = await consulta.DiagnosticarAsync(CancellationToken.None);

        var acessoSemComercial = Assert.Single(resultado.AcessosSemVinculoComercial);
        Assert.Equal(unidadeSemComercial.Id, acessoSemComercial.UnidadeId);
        var comercialSemAcesso = Assert.Single(
            resultado.VinculosComerciaisSemAcessoPrincipal);
        Assert.Equal(unidadeSemAcesso.Id, comercialSemAcesso.UnidadeId);
        Assert.DoesNotContain(
            resultado.AcessosSemVinculoComercial,
            item => item.UnidadeId == unidadeAdministradorComum.Id);

        var dbContextLeitura = consultaScope.ServiceProvider.GetRequiredService<BfaDbContext>();
        Assert.False(await dbContextLeitura.FranqueadosUnidades.AnyAsync(vinculo =>
            vinculo.UnidadeId == unidadeAdministradorComum.Id));
    }

    private static Unidade NovaUnidade(
        Guid organizacaoId,
        string nome,
        DateTime agoraUtc) =>
        new(Guid.NewGuid(), organizacaoId, nome, $"unidade-{Guid.NewGuid():N}", agoraUtc);

    private static FranqueadoUnidade NovoVinculo(
        Guid franqueadoId,
        Guid organizacaoId,
        Guid unidadeId,
        DateTime agoraUtc) =>
        new(Guid.NewGuid(), franqueadoId, organizacaoId, unidadeId, agoraUtc);

    private static VinculoAcesso NovoAcesso(
        Guid usuarioId,
        Guid organizacaoId,
        Guid unidadeId,
        DateTime agoraUtc) =>
        new(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            unidadeId,
            PerfilAcesso.AdministradorUnidade,
            agoraUtc);
}

using BFA.Domain.Acessos;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Unidades;
using Microsoft.EntityFrameworkCore;

namespace BFA.IntegrationTests;

public sealed class UnidadesUsuarioConsultaTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        20,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Lista_todas_as_unidades_administradas_pelo_usuario(
        int quantidadeUnidades)
    {
        await using var context = CreateContext();
        var usuarioId = Guid.NewGuid();
        var organizacao = NovaOrganizacao("BFA", "bfa");
        var unidades = Enumerable.Range(1, quantidadeUnidades)
            .Select(indice => NovaUnidade(
                organizacao.Id,
                $"BFA Unidade {quantidadeUnidades - indice + 1}"))
            .ToArray();
        context.Organizacoes.Add(organizacao);
        context.Unidades.AddRange(unidades);
        context.VinculosAcesso.AddRange(
            unidades.Select(unidade => NovoVinculo(usuarioId, unidade)));
        await context.SaveChangesAsync();
        var consulta = new UnidadesUsuarioConsulta(context);

        var resultado = await consulta.ListarAdministradasAsync(
            usuarioId,
            CancellationToken.None);

        Assert.Equal(quantidadeUnidades, resultado.Count);
        Assert.Equal(
            unidades.OrderBy(unidade => unidade.Nome).Select(unidade => unidade.Id),
            resultado.Select(unidade => unidade.UnidadeId));
    }

    [Fact]
    public async Task Lista_somente_unidades_ativas_administradas_pelo_usuario()
    {
        await using var context = CreateContext();
        var usuarioId = Guid.NewGuid();
        var outroUsuarioId = Guid.NewGuid();
        var organizacao = NovaOrganizacao("BFA", "bfa");
        var outraOrganizacao = NovaOrganizacao("Outra", "outra");
        var organizacaoInativa = NovaOrganizacao("Inativa", "inativa");
        context.Entry(organizacaoInativa).Property(item => item.Ativa).CurrentValue = false;
        var permitida = NovaUnidade(organizacao.Id, "BFA Tietê");
        var vinculoInativo = NovaUnidade(organizacao.Id, "BFA Sorocaba");
        var unidadeInativa = NovaUnidade(organizacao.Id, "BFA Campinas");
        var outroUsuario = NovaUnidade(organizacao.Id, "BFA Santos");
        var outroTenant = NovaUnidade(outraOrganizacao.Id, "Unidade externa");
        var organizacaoDesativada = NovaUnidade(
            organizacaoInativa.Id,
            "Unidade de organização inativa");
        unidadeInativa.Desativar(CriadoEmUtc.AddHours(2));
        context.AddRange(
            organizacao,
            outraOrganizacao,
            organizacaoInativa,
            permitida,
            vinculoInativo,
            unidadeInativa,
            outroUsuario,
            outroTenant,
            organizacaoDesativada);
        context.VinculosAcesso.AddRange(
            NovoVinculo(usuarioId, permitida),
            NovoVinculo(usuarioId, vinculoInativo, ativo: false),
            NovoVinculo(usuarioId, unidadeInativa),
            NovoVinculo(usuarioId, organizacaoDesativada),
            NovoVinculo(outroUsuarioId, outroUsuario),
            NovoVinculo(outroUsuarioId, outroTenant));
        await context.SaveChangesAsync();
        var consulta = new UnidadesUsuarioConsulta(context);

        var resultado = await consulta.ListarAdministradasAsync(
            usuarioId,
            CancellationToken.None);

        var unidade = Assert.Single(resultado);
        Assert.Equal(permitida.Id, unidade.UnidadeId);
        Assert.Equal(organizacao.Id, unidade.OrganizacaoId);
        Assert.Equal(permitida.Nome, unidade.Nome);
    }

    [Fact]
    public async Task Perfil_diferente_de_administrador_unidade_nao_entra_na_lista()
    {
        await using var context = CreateContext();
        var usuarioId = Guid.NewGuid();
        var organizacao = NovaOrganizacao("BFA", "bfa");
        var unidade = NovaUnidade(organizacao.Id, "BFA Tietê");
        context.AddRange(organizacao, unidade);
        context.VinculosAcesso.Add(new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacao.Id,
            unidade.Id,
            PerfilAcesso.Professor,
            CriadoEmUtc));
        await context.SaveChangesAsync();
        var consulta = new UnidadesUsuarioConsulta(context);

        var resultado = await consulta.ListarAdministradasAsync(
            usuarioId,
            CancellationToken.None);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Administrador_rede_sem_unidade_nao_entra_na_lista()
    {
        await using var context = CreateContext();
        var usuarioId = Guid.NewGuid();
        var organizacao = NovaOrganizacao("BFA", "bfa");
        context.Organizacoes.Add(organizacao);
        context.VinculosAcesso.Add(new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacao.Id,
            null,
            PerfilAcesso.AdministradorRede,
            CriadoEmUtc));
        await context.SaveChangesAsync();
        var consulta = new UnidadesUsuarioConsulta(context);

        var resultado = await consulta.ListarAdministradasAsync(
            usuarioId,
            CancellationToken.None);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Join_composto_impede_cruzamento_entre_organizacao_e_unidade()
    {
        await using var context = CreateContext();
        var usuarioId = Guid.NewGuid();
        var outroUsuarioId = Guid.NewGuid();
        var organizacao = NovaOrganizacao("BFA", "bfa");
        var outraOrganizacao = NovaOrganizacao("Outra", "outra");
        var permitida = NovaUnidade(organizacao.Id, "BFA Tietê");
        var externa = NovaUnidade(outraOrganizacao.Id, "Unidade externa");
        context.AddRange(organizacao, outraOrganizacao, permitida, externa);
        context.VinculosAcesso.AddRange(
            NovoVinculo(usuarioId, permitida),
            new VinculoAcesso(
                Guid.NewGuid(),
                usuarioId,
                organizacao.Id,
                externa.Id,
                PerfilAcesso.AdministradorUnidade,
                CriadoEmUtc),
            NovoVinculo(outroUsuarioId, externa));
        await context.SaveChangesAsync();
        var consulta = new UnidadesUsuarioConsulta(context);

        var resultado = await consulta.ListarAdministradasAsync(
            usuarioId,
            CancellationToken.None);

        var unidade = Assert.Single(resultado);
        Assert.Equal(permitida.Id, unidade.UnidadeId);
        Assert.Equal(organizacao.Id, unidade.OrganizacaoId);
    }

    [Fact]
    public async Task Obter_administrada_retorna_somente_unidade_permitida()
    {
        await using var context = CreateContext();
        var usuarioId = Guid.NewGuid();
        var outroUsuarioId = Guid.NewGuid();
        var organizacao = NovaOrganizacao("BFA", "bfa");
        var permitida = NovaUnidade(organizacao.Id, "BFA Tietê");
        var naoPermitida = NovaUnidade(organizacao.Id, "BFA Sorocaba");
        context.AddRange(organizacao, permitida, naoPermitida);
        context.VinculosAcesso.AddRange(
            NovoVinculo(usuarioId, permitida),
            NovoVinculo(outroUsuarioId, naoPermitida));
        await context.SaveChangesAsync();
        var consulta = new UnidadesUsuarioConsulta(context);

        var resultadoPermitido = await consulta.ObterAdministradaAsync(
            usuarioId,
            permitida.Id,
            CancellationToken.None);
        var resultadoNaoPermitido = await consulta.ObterAdministradaAsync(
            usuarioId,
            naoPermitida.Id,
            CancellationToken.None);

        Assert.NotNull(resultadoPermitido);
        Assert.Equal(permitida.Id, resultadoPermitido.UnidadeId);
        Assert.Equal(organizacao.Id, resultadoPermitido.OrganizacaoId);
        Assert.Null(resultadoNaoPermitido);
    }

    [Fact]
    public async Task Contexto_retorna_apenas_unidade_e_organizacao_ativas()
    {
        await using var context = CreateContext();
        var organizacaoAtiva = NovaOrganizacao("BFA", "bfa");
        var organizacaoInativa = NovaOrganizacao("Outra", "outra");
        context.Entry(organizacaoInativa).Property(item => item.Ativa).CurrentValue = false;
        var unidadeAtiva = NovaUnidade(organizacaoAtiva.Id, "BFA Tietê");
        var unidadeInativa = NovaUnidade(organizacaoAtiva.Id, "BFA Sorocaba");
        var unidadeOrganizacaoInativa = NovaUnidade(
            organizacaoInativa.Id,
            "Unidade externa");
        unidadeInativa.Desativar(CriadoEmUtc.AddHours(1));
        context.AddRange(
            organizacaoAtiva,
            organizacaoInativa,
            unidadeAtiva,
            unidadeInativa,
            unidadeOrganizacaoInativa);
        await context.SaveChangesAsync();
        var consulta = new UnidadesUsuarioConsulta(context);

        var ativa = await consulta.ObterAtivaAsync(
            unidadeAtiva.Id,
            CancellationToken.None);
        var inativa = await consulta.ObterAtivaAsync(
            unidadeInativa.Id,
            CancellationToken.None);
        var organizacaoDesativada = await consulta.ObterAtivaAsync(
            unidadeOrganizacaoInativa.Id,
            CancellationToken.None);

        Assert.NotNull(ativa);
        Assert.Equal(unidadeAtiva.Id, ativa.UnidadeId);
        Assert.Null(inativa);
        Assert.Null(organizacaoDesativada);
    }

    private static BfaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseInMemoryDatabase($"bfa-unidades-usuario-{Guid.NewGuid():N}")
            .Options;
        return new BfaDbContext(options);
    }

    private static Organizacao NovaOrganizacao(string nome, string slug)
    {
        return new Organizacao(Guid.NewGuid(), nome, slug, CriadoEmUtc);
    }

    private static Unidade NovaUnidade(Guid organizacaoId, string nome)
    {
        return new Unidade(
            Guid.NewGuid(),
            organizacaoId,
            nome,
            $"unidade-{Guid.NewGuid():N}",
            CriadoEmUtc);
    }

    private static VinculoAcesso NovoVinculo(
        Guid usuarioId,
        Unidade unidade,
        bool ativo = true)
    {
        var vinculo = new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            unidade.OrganizacaoId,
            unidade.Id,
            PerfilAcesso.AdministradorUnidade,
            CriadoEmUtc);

        if (!ativo)
        {
            vinculo.Desativar(CriadoEmUtc.AddHours(1));
        }

        return vinculo;
    }
}

using BFA.Application.Franqueadora;
using BFA.Domain.Acessos;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Franqueadora;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.IntegrationTests;

public sealed class PainelFranqueadoraConsultaTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        18,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public async Task Unidades_sao_contadas_somente_na_organizacao_atual_e_por_estado()
    {
        await using var dbContext = CreateContext();
        var usuarioId = Guid.NewGuid();
        var organizacaoAtual = CreateOrganizacao("Organização Atual", "atual");
        var outraOrganizacao = CreateOrganizacao("Outra Organização", "outra");
        dbContext.Organizacoes.AddRange(organizacaoAtual, outraOrganizacao);
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                usuarioId,
                organizacaoAtual.Id,
                null,
                PerfilAcesso.AdministradorRede));
        await AddUnidadeAsync(dbContext, CreateUnidade(organizacaoAtual.Id, "Ativa 1", "ativa-1"));
        await AddUnidadeAsync(dbContext, CreateUnidade(organizacaoAtual.Id, "Ativa 2", "ativa-2"));
        await AddUnidadeAsync(
            dbContext,
            CreateUnidade(organizacaoAtual.Id, "Inativa", "inativa"),
            ativo: false);
        await AddUnidadeAsync(dbContext, CreateUnidade(outraOrganizacao.Id, "Externa", "externa"));
        var consulta = new PainelFranqueadoraConsulta(dbContext);

        var resultado = await consulta.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(EstadoPainelFranqueadora.Disponivel, resultado.Estado);
        var resumo = Assert.IsType<PainelFranqueadoraResumo>(resultado.Resumo);
        Assert.Equal(organizacaoAtual.Id, resumo.OrganizacaoId);
        Assert.Equal("Organização Atual", resumo.NomeOrganizacao);
        Assert.Equal(3, resumo.TotalUnidades);
        Assert.Equal(2, resumo.UnidadesAtivas);
    }

    [Fact]
    public async Task Administradores_ativos_sao_contados_por_perfil_e_organizacao()
    {
        await using var dbContext = CreateContext();
        var usuarioId = Guid.NewGuid();
        var organizacaoAtual = CreateOrganizacao("Organização Atual", "atual");
        var outraOrganizacao = CreateOrganizacao("Outra Organização", "outra");
        var unidadeAtual = CreateUnidade(organizacaoAtual.Id, "Unidade Atual", "unidade-atual");
        var unidadeExterna = CreateUnidade(outraOrganizacao.Id, "Unidade Externa", "unidade-externa");
        dbContext.Organizacoes.AddRange(organizacaoAtual, outraOrganizacao);
        dbContext.Unidades.AddRange(unidadeAtual, unidadeExterna);
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                usuarioId,
                organizacaoAtual.Id,
                null,
                PerfilAcesso.AdministradorRede));
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                Guid.NewGuid(),
                organizacaoAtual.Id,
                null,
                PerfilAcesso.AdministradorRede));
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                Guid.NewGuid(),
                organizacaoAtual.Id,
                null,
                PerfilAcesso.AdministradorRede),
            ativo: false);
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                Guid.NewGuid(),
                organizacaoAtual.Id,
                unidadeAtual.Id,
                PerfilAcesso.AdministradorUnidade));
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                Guid.NewGuid(),
                organizacaoAtual.Id,
                unidadeAtual.Id,
                PerfilAcesso.AdministradorUnidade),
            ativo: false);
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                Guid.NewGuid(),
                outraOrganizacao.Id,
                null,
                PerfilAcesso.AdministradorRede));
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                Guid.NewGuid(),
                outraOrganizacao.Id,
                unidadeExterna.Id,
                PerfilAcesso.AdministradorUnidade));
        var consulta = new PainelFranqueadoraConsulta(dbContext);

        var resultado = await consulta.ObterAsync(usuarioId, CancellationToken.None);

        var resumo = Assert.IsType<PainelFranqueadoraResumo>(resultado.Resumo);
        Assert.Equal(2, resumo.AdministradoresRedeAtivos);
        Assert.Equal(1, resumo.AdministradoresUnidadeAtivos);
    }

    [Fact]
    public async Task Usuario_sem_vinculo_administrador_rede_nao_obtem_painel()
    {
        await using var dbContext = CreateContext();
        var usuarioId = Guid.NewGuid();
        var organizacao = CreateOrganizacao("Organização", "organizacao");
        var unidade = CreateUnidade(organizacao.Id, "Unidade", "unidade");
        dbContext.Organizacoes.Add(organizacao);
        dbContext.Unidades.Add(unidade);
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                usuarioId,
                organizacao.Id,
                unidade.Id,
                PerfilAcesso.AdministradorUnidade));
        var consulta = new PainelFranqueadoraConsulta(dbContext);

        var resultado = await consulta.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(EstadoPainelFranqueadora.SemAcesso, resultado.Estado);
        Assert.Null(resultado.Resumo);
    }

    [Fact]
    public async Task Multiplas_organizacoes_nao_sao_escolhidas_arbitrariamente()
    {
        await using var dbContext = CreateContext();
        var usuarioId = Guid.NewGuid();
        var primeiraOrganizacao = CreateOrganizacao("Primeira", "primeira");
        var segundaOrganizacao = CreateOrganizacao("Segunda", "segunda");
        dbContext.Organizacoes.AddRange(primeiraOrganizacao, segundaOrganizacao);
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                usuarioId,
                primeiraOrganizacao.Id,
                null,
                PerfilAcesso.AdministradorRede));
        await AddVinculoAsync(
            dbContext,
            CreateVinculo(
                usuarioId,
                segundaOrganizacao.Id,
                null,
                PerfilAcesso.AdministradorRede));
        var consulta = new PainelFranqueadoraConsulta(dbContext);

        var resultado = await consulta.ObterAsync(usuarioId, CancellationToken.None);

        Assert.Equal(
            EstadoPainelFranqueadora.SelecaoOrganizacaoNecessaria,
            resultado.Estado);
        Assert.Null(resultado.Resumo);
    }

    private static BfaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseInMemoryDatabase($"bfa-painel-franqueadora-{Guid.NewGuid():N}")
            .Options;

        return new BfaDbContext(options);
    }

    private static async Task AddUnidadeAsync(
        BfaDbContext dbContext,
        Unidade unidade,
        bool ativo = true)
    {
        dbContext.Unidades.Add(unidade);
        dbContext.Entry(unidade).Property(item => item.Ativa).CurrentValue = ativo;
        await dbContext.SaveChangesAsync();
    }

    private static async Task AddVinculoAsync(
        BfaDbContext dbContext,
        VinculoAcesso vinculo,
        bool ativo = true)
    {
        dbContext.VinculosAcesso.Add(vinculo);
        dbContext.Entry(vinculo).Property(item => item.Ativo).CurrentValue = ativo;
        await dbContext.SaveChangesAsync();
    }

    private static Organizacao CreateOrganizacao(string nome, string slug)
    {
        return new Organizacao(Guid.NewGuid(), nome, slug, CriadoEmUtc);
    }

    private static Unidade CreateUnidade(Guid organizacaoId, string nome, string slug)
    {
        return new Unidade(Guid.NewGuid(), organizacaoId, nome, slug, CriadoEmUtc);
    }

    private static VinculoAcesso CreateVinculo(
        Guid usuarioId,
        Guid organizacaoId,
        Guid? unidadeId,
        PerfilAcesso perfil)
    {
        return new VinculoAcesso(
            Guid.NewGuid(),
            usuarioId,
            organizacaoId,
            unidadeId,
            perfil,
            CriadoEmUtc);
    }
}

using BFA.Domain.Acessos;
using BFA.Infrastructure.Acessos;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.IntegrationTests;

public sealed class AcessoUsuarioConsultaTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        18,
        12,
        30,
        0,
        DateTimeKind.Utc);

    [Fact]
    public async Task Administrador_rede_ativo_autoriza_e_inativo_nao_autoriza()
    {
        await using var context = CreateContext();
        var organizacaoId = Guid.NewGuid();
        var usuarioAtivoId = Guid.NewGuid();
        var usuarioInativoId = Guid.NewGuid();
        await AddVinculoAsync(
            context,
            NovoVinculo(usuarioAtivoId, organizacaoId, null, PerfilAcesso.AdministradorRede),
            ativo: true);
        await AddVinculoAsync(
            context,
            NovoVinculo(usuarioInativoId, organizacaoId, null, PerfilAcesso.AdministradorRede),
            ativo: false);
        var consulta = new AcessoUsuarioConsulta(context);

        Assert.True(await consulta.EhAdministradorRedeAsync(usuarioAtivoId, CancellationToken.None));
        Assert.False(await consulta.EhAdministradorRedeAsync(usuarioInativoId, CancellationToken.None));
    }

    [Fact]
    public async Task Administrador_rede_acessa_qualquer_unidade_somente_da_propria_organizacao()
    {
        await using var context = CreateContext();
        var usuarioId = Guid.NewGuid();
        var organizacaoAutorizadaId = Guid.NewGuid();
        await AddVinculoAsync(
            context,
            NovoVinculo(
                usuarioId,
                organizacaoAutorizadaId,
                null,
                PerfilAcesso.AdministradorRede),
            ativo: true);
        var consulta = new AcessoUsuarioConsulta(context);

        Assert.True(await consulta.PossuiAcessoUnidadeAsync(
            usuarioId,
            organizacaoAutorizadaId,
            Guid.NewGuid(),
            CancellationToken.None));
        Assert.False(await consulta.PossuiAcessoUnidadeAsync(
            usuarioId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None));
    }

    [Fact]
    public async Task Administrador_unidade_acessa_somente_organizacao_e_unidade_do_vinculo()
    {
        await using var context = CreateContext();
        var usuarioId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        await AddVinculoAsync(
            context,
            NovoVinculo(
                usuarioId,
                organizacaoId,
                unidadeId,
                PerfilAcesso.AdministradorUnidade),
            ativo: true);
        var consulta = new AcessoUsuarioConsulta(context);

        Assert.True(await consulta.PossuiAcessoUnidadeAsync(
            usuarioId,
            organizacaoId,
            unidadeId,
            CancellationToken.None));
        Assert.False(await consulta.PossuiAcessoUnidadeAsync(
            usuarioId,
            organizacaoId,
            Guid.NewGuid(),
            CancellationToken.None));
        Assert.False(await consulta.PossuiAcessoUnidadeAsync(
            usuarioId,
            Guid.NewGuid(),
            unidadeId,
            CancellationToken.None));
    }

    [Fact]
    public async Task Consultas_de_perfil_consideram_somente_vinculos_ativos()
    {
        await using var context = CreateContext();
        var organizacaoId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();
        var professorAtivoId = Guid.NewGuid();
        var professorInativoId = Guid.NewGuid();
        await AddVinculoAsync(
            context,
            NovoVinculo(
                professorAtivoId,
                organizacaoId,
                unidadeId,
                PerfilAcesso.Professor),
            ativo: true);
        await AddVinculoAsync(
            context,
            NovoVinculo(
                professorInativoId,
                organizacaoId,
                unidadeId,
                PerfilAcesso.Professor),
            ativo: false);
        var consulta = new AcessoUsuarioConsulta(context);

        Assert.True(await consulta.PossuiAlgumPerfilAsync(
            professorAtivoId,
            [PerfilAcesso.Professor],
            CancellationToken.None));
        Assert.True(await consulta.PossuiPerfilNaOrganizacaoAsync(
            professorAtivoId,
            organizacaoId,
            PerfilAcesso.Professor,
            CancellationToken.None));
        Assert.True(await consulta.PossuiPerfilNaUnidadeAsync(
            professorAtivoId,
            organizacaoId,
            unidadeId,
            PerfilAcesso.Professor,
            CancellationToken.None));
        Assert.False(await consulta.PossuiAlgumPerfilAsync(
            professorInativoId,
            [PerfilAcesso.Professor],
            CancellationToken.None));
    }

    [Fact]
    public async Task Mesmo_usuario_pode_ter_perfis_em_multiplas_unidades()
    {
        await using var context = CreateContext();
        var usuarioId = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var unidadeProfessorId = Guid.NewGuid();
        var unidadeAlunoId = Guid.NewGuid();
        await AddVinculoAsync(
            context,
            NovoVinculo(
                usuarioId,
                organizacaoId,
                unidadeProfessorId,
                PerfilAcesso.Professor),
            ativo: true);
        await AddVinculoAsync(
            context,
            NovoVinculo(
                usuarioId,
                organizacaoId,
                unidadeAlunoId,
                PerfilAcesso.Aluno),
            ativo: true);
        var consulta = new AcessoUsuarioConsulta(context);

        Assert.True(await consulta.PossuiAlgumPerfilNaUnidadeAsync(
            usuarioId,
            organizacaoId,
            unidadeProfessorId,
            [PerfilAcesso.Professor],
            CancellationToken.None));
        Assert.True(await consulta.PossuiAlgumPerfilNaUnidadeAsync(
            usuarioId,
            organizacaoId,
            unidadeAlunoId,
            [PerfilAcesso.Aluno],
            CancellationToken.None));
        Assert.False(await consulta.PossuiAlgumPerfilNaUnidadeAsync(
            usuarioId,
            organizacaoId,
            unidadeAlunoId,
            [PerfilAcesso.Professor],
            CancellationToken.None));
    }

    private static BfaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BfaDbContext>()
            .UseInMemoryDatabase($"bfa-autorizacao-{Guid.NewGuid():N}")
            .Options;

        return new BfaDbContext(options);
    }

    private static async Task AddVinculoAsync(
        BfaDbContext context,
        VinculoAcesso vinculo,
        bool ativo)
    {
        context.VinculosAcesso.Add(vinculo);
        context.Entry(vinculo).Property(item => item.Ativo).CurrentValue = ativo;
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static VinculoAcesso NovoVinculo(
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

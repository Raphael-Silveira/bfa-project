using BFA.Domain.DayUses;
using BFA.Domain.Usuarios;
using BFA.Infrastructure.DayUses;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BFA.IntegrationTests;

public sealed class DayUsesPostgreSqlTests(PostgreSqlEfemeroV013Fixture fixture)
    : IClassFixture<PostgreSqlEfemeroV013Fixture>
{
    [Theory]
    [InlineData("Amelia")]
    [InlineData("amelia")]
    [InlineData("AMELIA")]
    [InlineData("aMeLiA")]
    public async Task Filtro_participante_ignora_caixa_em_aluno_e_avulso_e_aplica_trim(string termo)
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        await CriarPerfilUsuarioAsync();
        var alunoId = await fixture.CreateStudentAsync();
        await fixture.ExecuteAsync("UPDATE alunos SET nome_completo = 'Amelia Rocha' WHERE id = @id", ("id", alunoId));
        await using var db = CriarContexto();
        var repositorio = CriarRepositorio(db);
        Assert.True(await repositorio.CriarAsync(CriarDayUse(alunoId: alunoId), CancellationToken.None));
        Assert.True(await repositorio.CriarAsync(CriarDayUse(nomeAvulso: "Amelia Rocha"), CancellationToken.None));

        var resultado = await repositorio.ListarAsync(fixture.OrganizacaoId, fixture.UnidadeUmId,
            new(null, null, $" {termo} ", 9), CancellationToken.None);

        Assert.Equal(2, resultado.TotalItens);
        Assert.Equal(1, resultado.PaginaAtual);
        Assert.Contains(resultado.Itens, item => item.EhAluno && item.Participante == "Amelia Rocha");
        Assert.Contains(resultado.Itens, item => !item.EhAluno && item.Participante == "Amelia Rocha");
        Assert.All(resultado.Itens, item => Assert.Equal("Nome Operador", item.NomeRegistrador));
    }

    [Fact]
    public async Task Papel_runtime_recebe_permissao_delete_em_day_uses_pela_v026()
    {
        await using var conexao = await fixture.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT has_table_privilege('bfa_app_role', 'day_uses', 'DELETE')";

        Assert.True((bool)(await comando.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Filtro_nao_retorna_day_use_de_outra_unidade_ou_organizacao()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        var outraOrganizacaoId = Guid.NewGuid();
        var outraUnidadeId = Guid.NewGuid();
        await fixture.ExecuteAsync(
            "INSERT INTO organizacoes (id,nome,slug,ativa,criado_em_utc,atualizado_em_utc) VALUES (@id,'Outra Org',@slug,true,now(),now()); "
            + "INSERT INTO unidades (id,organizacao_id,nome,slug,ativa,criado_em_utc,atualizado_em_utc) VALUES (@unidade,@id,'Outra Unidade',@unidadeSlug,true,now(),now())",
            ("id", outraOrganizacaoId), ("slug", $"org-{Guid.NewGuid():N}"), ("unidade", outraUnidadeId), ("unidadeSlug", $"un-{Guid.NewGuid():N}"));
        await using var db = CriarContexto();
        var repositorio = CriarRepositorio(db);
        var dentroDoEscopo = CriarDayUse(nomeAvulso: "Amelia Rocha");
        Assert.True(await repositorio.CriarAsync(dentroDoEscopo, CancellationToken.None));
        Assert.True(await repositorio.CriarAsync(CriarDayUse(nomeAvulso: "Amelia Rocha", unidadeId: fixture.UnidadeDoisId), CancellationToken.None));
        Assert.True(await repositorio.CriarAsync(CriarDayUseComEscopo(outraOrganizacaoId, outraUnidadeId, "Amelia Rocha"), CancellationToken.None));

        var resultado = await repositorio.ListarAsync(fixture.OrganizacaoId, fixture.UnidadeUmId,
            new(null, null, "amelia"), CancellationToken.None);

        var unico = Assert.Single(resultado.Itens);
        Assert.Equal(dentroDoEscopo.Id, unico.DayUseId);
        Assert.Equal(1, resultado.TotalItens);
    }

    [Fact]
    public async Task Exclusao_fisica_respeita_unidade_e_criador_e_nao_altera_aluno_ou_matricula()
    {
        await fixture.ResetAsync(frequencia: 1, capacidade: 4);
        await using var db = CriarContexto();
        var alunoId = await db.Matriculas.AsNoTracking()
            .Where(item => item.Id == fixture.MatriculaUnidadeUmId)
            .Select(item => item.AlunoId).SingleAsync();
        var alunosAntes = await db.Alunos.CountAsync();
        var matriculasAntes = await db.Matriculas.CountAsync();
        var registro = new DayUse(Guid.NewGuid(), fixture.OrganizacaoId, fixture.UnidadeUmId,
            alunoId, null, null, null, new(2026, 9, 20), 50m, 30m, false,
            fixture.UsuarioId, new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));
        var repositorio = CriarRepositorio(db);
        Assert.True(await repositorio.CriarAsync(registro, CancellationToken.None));

        Assert.False(await repositorio.ExcluirAsync(fixture.OrganizacaoId, fixture.UnidadeDoisId,
            registro.Id, null, CancellationToken.None));
        Assert.False(await repositorio.ExcluirAsync(fixture.OrganizacaoId, fixture.UnidadeUmId,
            registro.Id, Guid.NewGuid(), CancellationToken.None));
        Assert.True(await db.DayUses.AnyAsync(item => item.Id == registro.Id));
        Assert.True(await repositorio.ExcluirAsync(fixture.OrganizacaoId, fixture.UnidadeUmId,
            registro.Id, fixture.UsuarioId, CancellationToken.None));

        Assert.False(await db.DayUses.AnyAsync(item => item.Id == registro.Id));
        Assert.Equal(alunosAntes, await db.Alunos.CountAsync());
        Assert.Equal(matriculasAntes, await db.Matriculas.CountAsync());
    }

    private async Task CriarPerfilUsuarioAsync()
    {
        var perfil = new PerfilUsuario(Guid.NewGuid(), fixture.UsuarioId, "Nome Operador", null,
            new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CriarContexto();
        db.PerfisUsuario.Add(perfil);
        await db.SaveChangesAsync();
    }

    private DayUse CriarDayUse(Guid? alunoId = null, string? nomeAvulso = null, Guid? unidadeId = null) =>
        CriarDayUseComEscopo(fixture.OrganizacaoId, unidadeId ?? fixture.UnidadeUmId,
            alunoId, nomeAvulso);

    private DayUse CriarDayUseComEscopo(Guid organizacaoId, Guid unidadeId, string nomeAvulso) =>
        CriarDayUseComEscopo(organizacaoId, unidadeId, null, nomeAvulso);

    private DayUse CriarDayUseComEscopo(Guid organizacaoId, Guid unidadeId, Guid? alunoId, string? nomeAvulso) =>
        new(Guid.NewGuid(), organizacaoId, unidadeId, alunoId,
            nomeAvulso, null, null, new(2026, 9, 20), 50m, 30m, false,
            fixture.UsuarioId, new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));

    private BfaDbContext CriarContexto() => new(new DbContextOptionsBuilder<BfaDbContext>()
        .UseNpgsql(fixture.ConnectionString).Options);

    private static DayUsesRepositorio CriarRepositorio(BfaDbContext db) =>
        new(db, NullLogger<DayUsesRepositorio>.Instance);
}

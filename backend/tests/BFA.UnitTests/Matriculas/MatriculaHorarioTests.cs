using BFA.Domain.Matriculas;

namespace BFA.UnitTests.Matriculas;

public sealed class MatriculaHorarioTests
{
    private static readonly DateTime Agora =
        new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criacao_representa_slot_aberto_e_inicializa_auditoria()
    {
        var item = Criar();

        Assert.Null(item.VigenciaFim);
        Assert.Equal(item.CriadoPorUsuarioId, item.AtualizadoPorUsuarioId);
        Assert.Equal(item.CriadoEmUtc, item.AtualizadoEmUtc);
    }

    [Fact]
    public void Encerramento_preenche_vigencia_final_e_auditoria()
    {
        var item = Criar();
        var usuario = Guid.NewGuid();
        var fim = item.VigenciaInicio.AddMonths(1);

        item.Encerrar(fim, usuario, Agora.AddHours(1));

        Assert.Equal(fim, item.VigenciaFim);
        Assert.Equal(usuario, item.AtualizadoPorUsuarioId);
        Assert.Equal(Agora.AddHours(1), item.AtualizadoEmUtc);
    }

    [Fact]
    public void Vigencia_final_anterior_ao_inicio_e_rejeitada()
    {
        var item = Criar();

        Assert.Throws<ArgumentException>(() => item.Encerrar(
            item.VigenciaInicio.AddDays(-1), Guid.NewGuid(), Agora.AddHours(1)));
    }

    [Fact]
    public void Vigencia_final_so_pode_ser_preenchida_uma_vez()
    {
        var item = Criar();
        item.Encerrar(item.VigenciaInicio, Guid.NewGuid(), Agora.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => item.Encerrar(
            item.VigenciaInicio.AddDays(1), Guid.NewGuid(), Agora.AddHours(2)));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("organizacaoId")]
    [InlineData("unidadeId")]
    [InlineData("matriculaId")]
    [InlineData("turmaHorarioId")]
    [InlineData("criadoPorUsuarioId")]
    public void Identificador_obrigatorio_e_validado(string parametro)
    {
        var valores = new Dictionary<string, Guid>
        {
            ["id"] = Guid.NewGuid(),
            ["organizacaoId"] = Guid.NewGuid(),
            ["unidadeId"] = Guid.NewGuid(),
            ["matriculaId"] = Guid.NewGuid(),
            ["turmaHorarioId"] = Guid.NewGuid(),
            ["criadoPorUsuarioId"] = Guid.NewGuid()
        };
        valores[parametro] = Guid.Empty;

        var exception = Assert.Throws<ArgumentException>(() => new MatriculaHorario(
            valores["id"], valores["organizacaoId"], valores["unidadeId"],
            valores["matriculaId"], valores["turmaHorarioId"],
            new DateOnly(2026, 9, 1), valores["criadoPorUsuarioId"], Agora));

        Assert.Equal(parametro, exception.ParamName);
    }

    [Fact]
    public void Data_de_criacao_deve_ser_utc()
    {
        Assert.Throws<ArgumentException>(() => Criar(
            DateTime.SpecifyKind(Agora, DateTimeKind.Local)));
    }

    [Fact]
    public void Identidade_nao_possui_setters_publicos()
    {
        foreach (var nome in new[]
        {
            nameof(MatriculaHorario.Id), nameof(MatriculaHorario.OrganizacaoId),
            nameof(MatriculaHorario.UnidadeId), nameof(MatriculaHorario.MatriculaId),
            nameof(MatriculaHorario.TurmaHorarioId), nameof(MatriculaHorario.VigenciaInicio),
            nameof(MatriculaHorario.CriadoPorUsuarioId), nameof(MatriculaHorario.CriadoEmUtc)
        })
        {
            Assert.False(typeof(MatriculaHorario).GetProperty(nome)!.SetMethod!.IsPublic);
        }
    }

    private static MatriculaHorario Criar(DateTime? criadoEmUtc = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        new DateOnly(2026, 9, 1), Guid.NewGuid(), criadoEmUtc ?? Agora);
}

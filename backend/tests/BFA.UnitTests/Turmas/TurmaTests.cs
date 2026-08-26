using BFA.Domain.Turmas;

namespace BFA.UnitTests.Turmas;

public sealed class TurmaTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026, 8, 24, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criacao_valida_normaliza_e_inicia_ativa_com_auditoria()
    {
        var usuarioId = Guid.NewGuid();

        var turma = Criar(nome: "  Turma Iniciante  ", usuarioId: usuarioId);

        Assert.Equal("Turma Iniciante", turma.Nome);
        Assert.Equal(20, turma.Capacidade);
        Assert.True(turma.Ativo);
        Assert.Equal(usuarioId, turma.CriadoPorUsuarioId);
        Assert.Equal(usuarioId, turma.AtualizadoPorUsuarioId);
        Assert.Equal(CriadoEmUtc, turma.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, turma.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_rejeita_nome_vazio(string nome)
    {
        var exception = Assert.Throws<ArgumentException>(() => Criar(nome: nome));

        Assert.Equal("nome", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criacao_rejeita_capacidade_invalida(int capacidade)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(capacidade: capacidade));

        Assert.Equal("capacidade", exception.ParamName);
    }

    [Fact]
    public void Atualizacao_controlada_permite_trocar_professor_e_preserva_identidade()
    {
        var turma = Criar();
        var id = turma.Id;
        var organizacaoId = turma.OrganizacaoId;
        var unidadeId = turma.UnidadeId;
        var criadoEmUtc = turma.CriadoEmUtc;
        var professorUnidadeId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var atualizadoEmUtc = CriadoEmUtc.AddHours(1);

        turma.Atualizar(
            "Turma Avancada",
            16,
            professorUnidadeId,
            usuarioId,
            atualizadoEmUtc);

        Assert.Equal("Turma Avancada", turma.Nome);
        Assert.Equal(16, turma.Capacidade);
        Assert.Equal(professorUnidadeId, turma.ProfessorUnidadeId);
        Assert.Equal(usuarioId, turma.AtualizadoPorUsuarioId);
        Assert.Equal(atualizadoEmUtc, turma.AtualizadoEmUtc);
        Assert.Equal(id, turma.Id);
        Assert.Equal(organizacaoId, turma.OrganizacaoId);
        Assert.Equal(unidadeId, turma.UnidadeId);
        Assert.Equal(criadoEmUtc, turma.CriadoEmUtc);
    }

    [Fact]
    public void Inativacao_e_reativacao_sao_logicas()
    {
        var turma = Criar();
        var usuarioId = Guid.NewGuid();

        turma.Desativar(usuarioId, CriadoEmUtc.AddHours(1));
        Assert.False(turma.Ativo);

        turma.Ativar(usuarioId, CriadoEmUtc.AddHours(2));
        Assert.True(turma.Ativo);
    }

    [Theory]
    [InlineData(nameof(Turma.Id))]
    [InlineData(nameof(Turma.OrganizacaoId))]
    [InlineData(nameof(Turma.UnidadeId))]
    [InlineData(nameof(Turma.CriadoPorUsuarioId))]
    [InlineData(nameof(Turma.CriadoEmUtc))]
    public void Identidade_e_auditoria_de_criacao_nao_possuem_setter_publico(
        string propriedade)
    {
        var property = typeof(Turma).GetProperty(propriedade);

        Assert.NotNull(property);
        Assert.False(property.SetMethod?.IsPublic ?? false);
    }

    private static Turma Criar(
        string nome = "Turma Iniciante",
        int capacidade = 20,
        Guid? usuarioId = null) => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            nome,
            capacidade,
            usuarioId ?? Guid.NewGuid(),
            CriadoEmUtc);
}

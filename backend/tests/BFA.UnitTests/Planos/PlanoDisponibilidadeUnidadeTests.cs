using BFA.Domain.Planos;

namespace BFA.UnitTests.Planos;

public sealed class PlanoDisponibilidadeUnidadeTests
{
    private static readonly DateTime Agora = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criacao_valida_inicia_ativa_e_com_auditoria_consistente()
    {
        var usuarioId = Guid.NewGuid();
        var disponibilidade = Criar(usuarioId);

        Assert.True(disponibilidade.Ativo);
        Assert.Equal(usuarioId, disponibilidade.CriadoPorUsuarioId);
        Assert.Equal(usuarioId, disponibilidade.AtualizadoPorUsuarioId);
        Assert.Equal(Agora, disponibilidade.CriadoEmUtc);
        Assert.Equal(Agora, disponibilidade.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("organizacao")]
    [InlineData("plano")]
    [InlineData("unidade")]
    [InlineData("usuario")]
    public void Identificador_obrigatorio_e_rejeitado_quando_vazio(string campo)
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        ids[Array.IndexOf(new[] { "id", "organizacao", "plano", "unidade", "usuario" }, campo)] = Guid.Empty;

        Assert.Throws<ArgumentException>(() => new PlanoDisponibilidadeUnidade(
            ids[0], ids[1], ids[2], ids[3], ids[4], Agora));
    }

    [Fact]
    public void Criacao_exige_instante_utc()
    {
        Assert.Throws<ArgumentException>(() => new PlanoDisponibilidadeUnidade(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.SpecifyKind(Agora, DateTimeKind.Local)));
    }

    [Fact]
    public void Pode_desativar_e_reativar_preservando_auditoria_de_criacao()
    {
        var disponibilidade = Criar(Guid.NewGuid());
        var criador = disponibilidade.CriadoPorUsuarioId;

        disponibilidade.Desativar(Guid.NewGuid(), Agora.AddMinutes(1));
        Assert.False(disponibilidade.Ativo);
        disponibilidade.Ativar(Guid.NewGuid(), Agora.AddMinutes(2));

        Assert.True(disponibilidade.Ativo);
        Assert.Equal(criador, disponibilidade.CriadoPorUsuarioId);
        Assert.Equal(Agora, disponibilidade.CriadoEmUtc);
        Assert.Equal(Agora.AddMinutes(2), disponibilidade.AtualizadoEmUtc);
    }

    [Fact]
    public void Identidade_escopo_e_criacao_nao_possuem_setter_publico()
    {
        foreach (var nome in new[]
        {
            nameof(PlanoDisponibilidadeUnidade.Id),
            nameof(PlanoDisponibilidadeUnidade.OrganizacaoId),
            nameof(PlanoDisponibilidadeUnidade.PlanoId),
            nameof(PlanoDisponibilidadeUnidade.UnidadeId),
            nameof(PlanoDisponibilidadeUnidade.CriadoPorUsuarioId),
            nameof(PlanoDisponibilidadeUnidade.CriadoEmUtc)
        })
        {
            Assert.False(typeof(PlanoDisponibilidadeUnidade).GetProperty(nome)!.SetMethod!.IsPublic);
        }
    }

    private static PlanoDisponibilidadeUnidade Criar(Guid usuarioId) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), usuarioId, Agora);
}

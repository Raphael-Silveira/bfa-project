using BFA.Domain.Alunos;

namespace BFA.UnitTests.Alunos;

public sealed class AlunoResponsavelTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        31,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public void Vinculo_valido_inicia_ativo_e_preserva_classificacao()
    {
        var vinculo = CriarVinculo(
            TipoRelacaoResponsavel.Mae,
            principalContato: true,
            responsavelFinanceiro: true);

        Assert.True(vinculo.Ativo);
        Assert.True(vinculo.PrincipalContato);
        Assert.True(vinculo.ResponsavelFinanceiro);
        Assert.Equal(TipoRelacaoResponsavel.Mae, vinculo.TipoRelacao);
        Assert.Null(vinculo.DescricaoRelacao);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Tipo_Outro_exige_descricao_nao_vazia(string? descricao)
    {
        Assert.Throws<ArgumentException>(() => CriarVinculo(
            TipoRelacaoResponsavel.Outro,
            descricao: descricao));
    }

    [Theory]
    [InlineData("Mae")]
    [InlineData("")]
    [InlineData(" ")]
    public void Tipo_diferente_de_Outro_exige_descricao_nula(string descricao)
    {
        Assert.Throws<ArgumentException>(() => CriarVinculo(
            TipoRelacaoResponsavel.Mae,
            descricao: descricao));
    }

    [Fact]
    public void Tipo_Outro_normaliza_descricao()
    {
        var vinculo = CriarVinculo(
            TipoRelacaoResponsavel.Outro,
            descricao: "  Irma mais velha  ");

        Assert.Equal("Irma mais velha", vinculo.DescricaoRelacao);
    }

    [Fact]
    public void Tipo_de_relacao_fora_do_enum_e_rejeitado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CriarVinculo(
            (TipoRelacaoResponsavel)999));
    }

    [Fact]
    public void Vinculo_inativo_pode_ser_reativado_preservando_identidade()
    {
        var vinculo = CriarVinculo(TipoRelacaoResponsavel.Pai);
        var id = vinculo.Id;

        vinculo.Desativar(CriadoEmUtc.AddMinutes(1));
        vinculo.Ativar(CriadoEmUtc.AddMinutes(2));

        Assert.True(vinculo.Ativo);
        Assert.Equal(id, vinculo.Id);
    }

    [Fact]
    public void Classificacao_pode_alterar_sem_trocar_identidade_do_vinculo()
    {
        var vinculo = CriarVinculo(TipoRelacaoResponsavel.Pai);
        var id = vinculo.Id;
        var alunoId = vinculo.AlunoId;
        var responsavelId = vinculo.ResponsavelId;

        vinculo.AtualizarClassificacao(
            TipoRelacaoResponsavel.Outro,
            "Tio",
            principalContato: true,
            responsavelFinanceiro: true,
            CriadoEmUtc.AddMinutes(1));

        Assert.Equal(id, vinculo.Id);
        Assert.Equal(alunoId, vinculo.AlunoId);
        Assert.Equal(responsavelId, vinculo.ResponsavelId);
        Assert.Equal(TipoRelacaoResponsavel.Outro, vinculo.TipoRelacao);
        Assert.Equal("Tio", vinculo.DescricaoRelacao);
    }

    [Fact]
    public void Identidade_historica_nao_possui_setter_publico()
    {
        foreach (var propertyName in new[]
        {
            nameof(AlunoResponsavel.Id),
            nameof(AlunoResponsavel.OrganizacaoId),
            nameof(AlunoResponsavel.AlunoId),
            nameof(AlunoResponsavel.ResponsavelId),
            nameof(AlunoResponsavel.CriadoEmUtc)
        })
        {
            Assert.False(typeof(AlunoResponsavel).GetProperty(propertyName)!.SetMethod!.IsPublic);
        }
    }

    private static AlunoResponsavel CriarVinculo(
        TipoRelacaoResponsavel tipoRelacao,
        string? descricao = null,
        bool principalContato = false,
        bool responsavelFinanceiro = false) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            tipoRelacao,
            principalContato,
            responsavelFinanceiro,
            CriadoEmUtc,
            descricao);
}

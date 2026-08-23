using BFA.Domain.Professores;

namespace BFA.UnitTests.Professores;

public sealed class ProfessorUnidadeTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        22,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public void Criacao_define_vinculo_profissional_e_inicia_ativo()
    {
        var id = Guid.NewGuid();
        var organizacaoId = Guid.NewGuid();
        var professorId = Guid.NewGuid();
        var unidadeId = Guid.NewGuid();

        var vinculo = new ProfessorUnidade(
            id,
            organizacaoId,
            professorId,
            unidadeId,
            CriadoEmUtc);

        Assert.Equal(id, vinculo.Id);
        Assert.Equal(organizacaoId, vinculo.OrganizacaoId);
        Assert.Equal(professorId, vinculo.ProfessorId);
        Assert.Equal(unidadeId, vinculo.UnidadeId);
        Assert.True(vinculo.Ativo);
        Assert.Equal(CriadoEmUtc, vinculo.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, vinculo.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("organizacaoId")]
    [InlineData("professorId")]
    [InlineData("unidadeId")]
    public void Criacao_rejeita_identificador_obrigatorio_vazio(string parametro)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ProfessorUnidade(
            parametro == "id" ? Guid.Empty : Guid.NewGuid(),
            parametro == "organizacaoId" ? Guid.Empty : Guid.NewGuid(),
            parametro == "professorId" ? Guid.Empty : Guid.NewGuid(),
            parametro == "unidadeId" ? Guid.Empty : Guid.NewGuid(),
            CriadoEmUtc));

        Assert.Equal(parametro, exception.ParamName);
    }

    [Theory]
    [InlineData(nameof(ProfessorUnidade.Id))]
    [InlineData(nameof(ProfessorUnidade.OrganizacaoId))]
    [InlineData(nameof(ProfessorUnidade.ProfessorId))]
    [InlineData(nameof(ProfessorUnidade.UnidadeId))]
    [InlineData(nameof(ProfessorUnidade.CriadoEmUtc))]
    public void Identidade_historica_nao_possui_setter_publico(string propriedade)
    {
        var property = typeof(ProfessorUnidade).GetProperty(propriedade);

        Assert.NotNull(property);
        Assert.False(property.SetMethod?.IsPublic ?? false);
    }

    [Fact]
    public void Vinculo_inativo_e_reativado_no_mesmo_registro()
    {
        var vinculo = new ProfessorUnidade(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CriadoEmUtc);
        var idOriginal = vinculo.Id;

        vinculo.Desativar(CriadoEmUtc.AddHours(1));
        vinculo.Ativar(CriadoEmUtc.AddHours(2));

        Assert.True(vinculo.Ativo);
        Assert.Equal(idOriginal, vinculo.Id);
        Assert.Equal(CriadoEmUtc, vinculo.CriadoEmUtc);
    }

    [Fact]
    public void Reativacao_nao_cria_remuneracao_e_preserva_historico_existente()
    {
        var organizacaoId = Guid.NewGuid();
        var vinculo = new ProfessorUnidade(
            Guid.NewGuid(),
            organizacaoId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            CriadoEmUtc);
        var vigenciaFim = new DateOnly(2026, 8, 31);
        var remuneracaoHistorica = new ProfessorRemuneracao(
            Guid.NewGuid(),
            organizacaoId,
            vinculo.Id,
            ModalidadeRemuneracaoProfessor.Mensal,
            2500m,
            new DateOnly(2026, 1, 1),
            vigenciaFim,
            Guid.NewGuid(),
            CriadoEmUtc);
        var remuneracoes = new List<ProfessorRemuneracao> { remuneracaoHistorica };
        var idOriginal = vinculo.Id;
        var organizacaoOriginal = vinculo.OrganizacaoId;
        var professorOriginal = vinculo.ProfessorId;
        var unidadeOriginal = vinculo.UnidadeId;
        var criadoEmOriginal = vinculo.CriadoEmUtc;

        vinculo.Desativar(CriadoEmUtc.AddHours(1));
        vinculo.Ativar(CriadoEmUtc.AddHours(2));

        var preservada = Assert.Single(remuneracoes);
        Assert.Same(remuneracaoHistorica, preservada);
        Assert.Equal(vigenciaFim, preservada.VigenciaFim);
        Assert.Equal(2500m, preservada.Valor);
        Assert.Equal(idOriginal, vinculo.Id);
        Assert.Equal(organizacaoOriginal, vinculo.OrganizacaoId);
        Assert.Equal(professorOriginal, vinculo.ProfessorId);
        Assert.Equal(unidadeOriginal, vinculo.UnidadeId);
        Assert.Equal(criadoEmOriginal, vinculo.CriadoEmUtc);
    }
}

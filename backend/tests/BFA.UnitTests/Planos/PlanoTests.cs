using BFA.Domain.Planos;

namespace BFA.UnitTests.Planos;

public sealed class PlanoTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criacao_sem_unidade_representa_plano_da_rede()
    {
        var plano = Criar(unidadeId: null, nome: "  Plano Oficial BFA  ");

        Assert.True(plano.EhPlanoRede);
        Assert.Null(plano.UnidadeId);
        Assert.Equal("Plano Oficial BFA", plano.Nome);
        Assert.True(plano.Ativo);
        Assert.Equal(plano.CriadoPorUsuarioId, plano.AtualizadoPorUsuarioId);
        Assert.Equal(plano.CriadoEmUtc, plano.AtualizadoEmUtc);
    }

    [Fact]
    public void Criacao_com_unidade_representa_plano_local()
    {
        var unidadeId = Guid.NewGuid();
        var plano = Criar(unidadeId);

        Assert.False(plano.EhPlanoRede);
        Assert.Equal(unidadeId, plano.UnidadeId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_rejeita_nome_vazio(string nome)
    {
        var exception = Assert.Throws<ArgumentException>(() => Criar(nome: nome));

        Assert.Equal("nome", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_nome_acima_do_limite()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Criar(nome: new string('P', Plano.NomeTamanhoMaximo + 1)));

        Assert.Equal("nome", exception.ParamName);
    }

    [Fact]
    public void Inativacao_e_reativacao_preservam_identidade_e_escopo()
    {
        var unidadeId = Guid.NewGuid();
        var plano = Criar(unidadeId);
        var identidade = (plano.Id, plano.OrganizacaoId, plano.UnidadeId, plano.Nome,
            plano.CriadoPorUsuarioId, plano.CriadoEmUtc);
        var atualizador = Guid.NewGuid();

        plano.Desativar(atualizador, CriadoEmUtc.AddHours(1));
        Assert.False(plano.Ativo);
        Assert.Equal(atualizador, plano.AtualizadoPorUsuarioId);
        plano.Ativar(atualizador, CriadoEmUtc.AddHours(2));

        Assert.True(plano.Ativo);
        Assert.Equal(identidade, (plano.Id, plano.OrganizacaoId, plano.UnidadeId, plano.Nome,
            plano.CriadoPorUsuarioId, plano.CriadoEmUtc));
    }

    [Fact]
    public void Inativacao_do_plano_preserva_versao_historica()
    {
        var plano = Criar(Guid.NewGuid());
        var versao = new PlanoVersao(
            Guid.NewGuid(),
            plano.OrganizacaoId,
            plano.Id,
            1,
            12,
            3,
            280m,
            true,
            100m,
            new DateOnly(2026, 9, 1),
            null,
            Guid.NewGuid(),
            CriadoEmUtc);
        var historico = (
            versao.Id,
            versao.OrganizacaoId,
            versao.PlanoId,
            versao.NumeroVersao,
            versao.DuracaoMeses,
            versao.FrequenciaSemanal,
            versao.ValorMensal,
            versao.CobraMatricula,
            versao.ValorMatricula,
            versao.VigenciaInicio,
            versao.VigenciaFim,
            versao.CriadoPorUsuarioId,
            versao.CriadoEmUtc);

        plano.Desativar(Guid.NewGuid(), CriadoEmUtc.AddHours(1));

        Assert.False(plano.Ativo);
        Assert.Equal(
            historico,
            (
                versao.Id,
                versao.OrganizacaoId,
                versao.PlanoId,
                versao.NumeroVersao,
                versao.DuracaoMeses,
                versao.FrequenciaSemanal,
                versao.ValorMensal,
                versao.CobraMatricula,
                versao.ValorMatricula,
                versao.VigenciaInicio,
                versao.VigenciaFim,
                versao.CriadoPorUsuarioId,
                versao.CriadoEmUtc));
    }

    [Theory]
    [InlineData(nameof(Plano.Id))]
    [InlineData(nameof(Plano.OrganizacaoId))]
    [InlineData(nameof(Plano.UnidadeId))]
    [InlineData(nameof(Plano.Nome))]
    [InlineData(nameof(Plano.CriadoPorUsuarioId))]
    [InlineData(nameof(Plano.CriadoEmUtc))]
    public void Identidade_estavel_nao_possui_setter_publico(string propriedade)
    {
        var property = typeof(Plano).GetProperty(propriedade);

        Assert.NotNull(property);
        Assert.False(property.SetMethod?.IsPublic ?? false);
    }

    private static Plano Criar(Guid? unidadeId = null, string nome = "Plano 3x Anual") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            unidadeId,
            nome,
            Guid.NewGuid(),
            CriadoEmUtc);
}

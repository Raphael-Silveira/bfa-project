using BFA.Domain.Professores;

namespace BFA.UnitTests.Professores;

public sealed class ProfessorRemuneracaoTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        22,
        12,
        0,
        0,
        DateTimeKind.Utc);

    private static readonly DateOnly VigenciaInicio = new(2026, 1, 1);

    [Theory]
    [InlineData(ModalidadeRemuneracaoProfessor.Mensal)]
    [InlineData(ModalidadeRemuneracaoProfessor.PorAula)]
    [InlineData(ModalidadeRemuneracaoProfessor.PorHora)]
    public void Criacao_aceita_modalidades_previstas(ModalidadeRemuneracaoProfessor modalidade)
    {
        var remuneracao = Criar(modalidade: modalidade);

        Assert.Equal(modalidade, remuneracao.Modalidade);
        Assert.Equal(2500m, remuneracao.Valor);
    }

    [Fact]
    public void Criacao_rejeita_valor_negativo()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Criar(valor: -0.01m));

        Assert.Equal("valor", exception.ParamName);
    }

    [Fact]
    public void Criacao_rejeita_vigencia_final_anterior_ao_inicio()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Criar(vigenciaFim: VigenciaInicio.AddDays(-1)));

        Assert.Equal("vigenciaFim", exception.ParamName);
    }

    [Fact]
    public void Remuneracao_aberta_pode_receber_vigencia_final()
    {
        var remuneracao = Criar();
        var vigenciaFim = VigenciaInicio.AddMonths(8).AddDays(-1);

        remuneracao.Encerrar(vigenciaFim);

        Assert.Equal(vigenciaFim, remuneracao.VigenciaFim);
    }

    [Fact]
    public void Vigencia_final_encerrada_nao_pode_ser_alterada_novamente()
    {
        var vigenciaFim = VigenciaInicio.AddMonths(8).AddDays(-1);
        var remuneracao = Criar(vigenciaFim: vigenciaFim);

        Assert.Throws<InvalidOperationException>(() =>
            remuneracao.Encerrar(vigenciaFim.AddDays(1)));

        Assert.Equal(vigenciaFim, remuneracao.VigenciaFim);
    }

    [Fact]
    public void Termos_historicos_nao_possuem_setters_publicos()
    {
        var properties = new[]
        {
            nameof(ProfessorRemuneracao.Modalidade),
            nameof(ProfessorRemuneracao.Valor),
            nameof(ProfessorRemuneracao.VigenciaInicio),
            nameof(ProfessorRemuneracao.Observacao),
            nameof(ProfessorRemuneracao.CriadoPorUsuarioId),
            nameof(ProfessorRemuneracao.CriadoEmUtc)
        };

        Assert.All(properties, propertyName =>
        {
            var property = typeof(ProfessorRemuneracao).GetProperty(propertyName);
            Assert.NotNull(property);
            Assert.False(property.SetMethod?.IsPublic ?? false);
        });
    }

    private static ProfessorRemuneracao Criar(
        ModalidadeRemuneracaoProfessor modalidade = ModalidadeRemuneracaoProfessor.Mensal,
        decimal valor = 2500m,
        DateOnly? vigenciaFim = null) => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            modalidade,
            valor,
            VigenciaInicio,
            vigenciaFim,
            Guid.NewGuid(),
            CriadoEmUtc,
            "Remuneracao inicial");
}

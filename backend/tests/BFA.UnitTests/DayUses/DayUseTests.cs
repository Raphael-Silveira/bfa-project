using BFA.Domain.DayUses;

namespace BFA.UnitTests.DayUses;

public sealed class DayUseTests
{
    private static readonly DateTime CriadoEmUtc = new(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Participante_avulso_exige_nome_e_normaliza_dados()
    {
        var dayUse = Criar(alunoId: null, nome: "  Joao da Silva  ", telefone: "(11) 99999-9999", email: " joao@exemplo.com ");

        Assert.Equal("Joao da Silva", dayUse.NomeAvulso);
        Assert.Equal("5511999999999", dayUse.TelefoneAvulso);
        Assert.Equal("joao@exemplo.com", dayUse.EmailAvulso);
    }

    [Fact]
    public void Aluno_cadastrado_nao_copia_dados_avulsos()
    {
        var alunoId = Guid.NewGuid();
        var dayUse = Criar(alunoId, null, null, null);

        Assert.Equal(alunoId, dayUse.AlunoId);
        Assert.Null(dayUse.NomeAvulso);
        Assert.Null(dayUse.TelefoneAvulso);
        Assert.Null(dayUse.EmailAvulso);
    }

    [Fact]
    public void Cortesia_nao_fica_paga()
    {
        var dayUse = Criar(null, "Joao", null, null, valorCobrado: 0, pago: true);

        Assert.Equal(0, dayUse.ValorCobrado);
        Assert.False(dayUse.Pago);
    }

    [Fact]
    public void Pagamento_operacional_pode_ser_marcado()
    {
        var dayUse = Criar(null, "Joao", null, null, valorCobrado: 40);

        dayUse.MarcarComoPago();

        Assert.True(dayUse.Pago);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Avulso_sem_nome_e_rejeitado(string nome)
    {
        Assert.Throws<ArgumentException>(() => Criar(null, nome, null, null));
    }

    [Fact]
    public void Aluno_e_dados_avulsos_ao_mesmo_tempo_sao_rejeitados()
    {
        Assert.Throws<ArgumentException>(() => Criar(Guid.NewGuid(), "Joao", null, null));
    }

    private static DayUse Criar(
        Guid? alunoId,
        string? nome,
        string? telefone,
        string? email,
        decimal valorCobrado = 50,
        bool pago = false) => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), alunoId,
            nome, telefone, email, new DateOnly(2026, 9, 19), 50,
            valorCobrado, pago, Guid.NewGuid(), CriadoEmUtc);
}

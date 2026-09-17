using BFA.Domain.Aulas;

namespace BFA.UnitTests.AlunoArea;

public sealed class ConfirmacaoAulaAlunoTests
{
    [Fact]
    public void Confirmar_e_cancelar_reutilizam_o_mesmo_registro_logico()
    {
        var criadoEm = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);
        var confirmadaEm = criadoEm.AddMinutes(1);
        var reconfirmadaEm = criadoEm.AddMinutes(2);
        var confirmacao = new ConfirmacaoAulaAluno(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), confirmadaEm, criadoEm);

        var id = confirmacao.Id;
        Assert.True(confirmacao.Ativa);
        Assert.Equal(confirmadaEm, confirmacao.ConfirmadaEmUtc);

        confirmacao.Cancelar(reconfirmadaEm);
        Assert.Equal(id, confirmacao.Id);
        Assert.False(confirmacao.Ativa);
        Assert.Equal(confirmadaEm, confirmacao.ConfirmadaEmUtc);

        confirmacao.Confirmar(reconfirmadaEm);
        Assert.Equal(id, confirmacao.Id);
        Assert.True(confirmacao.Ativa);
        Assert.Equal(reconfirmadaEm, confirmacao.ConfirmadaEmUtc);
    }

    [Fact]
    public void Confirmacao_rejeita_data_que_nao_esteja_em_utc()
    {
        var local = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => new ConfirmacaoAulaAluno(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), local, DateTime.UtcNow));
    }
}

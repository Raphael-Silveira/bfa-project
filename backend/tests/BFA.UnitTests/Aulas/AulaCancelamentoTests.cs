using BFA.Domain.Aulas;

namespace BFA.UnitTests.Aulas;

public sealed class AulaCancelamentoTests
{
    private static readonly DateTime Agora =
        new(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);

    private static Aula CriarAula() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        new DateOnly(2026, 9, 18), new TimeOnly(8), new TimeOnly(10), 20,
        Guid.NewGuid(), Agora);

    [Fact]
    public void Cancelar_persiste_motivo_data_e_usuario_autenticado()
    {
        var aula = CriarAula();
        var usuario = Guid.NewGuid();

        aula.Cancelar(usuario, Agora.AddMinutes(1), "Quadra indisponível");

        Assert.Equal(StatusAula.Cancelada, aula.Status);
        Assert.Equal("Quadra indisponível", aula.MotivoCancelamento);
        Assert.Equal(Agora.AddMinutes(1), aula.CanceladaEmUtc);
        Assert.Equal(usuario, aula.CanceladaPorUsuarioId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancelar_rejeita_motivo_vazio(string? motivo)
    {
        var aula = CriarAula();

        Assert.Throws<ArgumentException>(() => aula.Cancelar(
            Guid.NewGuid(), Agora.AddMinutes(1), motivo!));
    }

    [Fact]
    public void Cancelar_rejeita_motivo_acima_do_limite()
    {
        var aula = CriarAula();

        Assert.Throws<ArgumentException>(() => aula.Cancelar(
            Guid.NewGuid(), Agora.AddMinutes(1), new string('x', 501)));
    }

    [Fact]
    public void Cancelar_novamente_preserva_auditoria_original()
    {
        var aula = CriarAula();
        var primeiroUsuario = Guid.NewGuid();
        aula.Cancelar(primeiroUsuario, Agora.AddMinutes(1), "Primeiro motivo");

        Assert.Throws<InvalidOperationException>(() => aula.Cancelar(
            Guid.NewGuid(), Agora.AddMinutes(2), "Segundo motivo"));
        Assert.Equal("Primeiro motivo", aula.MotivoCancelamento);
        Assert.Equal(Agora.AddMinutes(1), aula.CanceladaEmUtc);
        Assert.Equal(primeiroUsuario, aula.CanceladaPorUsuarioId);
    }
}

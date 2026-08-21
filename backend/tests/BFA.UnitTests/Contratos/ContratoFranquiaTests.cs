using BFA.Domain.Contratos;

namespace BFA.UnitTests.Contratos;

public sealed class ContratoFranquiaTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        21,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public void Criacao_define_identidade_vinculo_e_status()
    {
        var id = Guid.NewGuid();
        var franqueadoUnidadeId = Guid.NewGuid();

        var contrato = new ContratoFranquia(
            id,
            franqueadoUnidadeId,
            "  BFA-2026-001  ",
            StatusContratoFranquia.Ativo,
            CriadoEmUtc);

        Assert.Equal(id, contrato.Id);
        Assert.Equal(franqueadoUnidadeId, contrato.FranqueadoUnidadeId);
        Assert.Equal("BFA-2026-001", contrato.Numero);
        Assert.Equal(StatusContratoFranquia.Ativo, contrato.Status);
        Assert.Equal(CriadoEmUtc, contrato.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, contrato.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("franqueadoUnidadeId")]
    public void Criacao_rejeita_identificador_obrigatorio_vazio(string parametro)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ContratoFranquia(
            parametro == "id" ? Guid.Empty : Guid.NewGuid(),
            parametro == "franqueadoUnidadeId" ? Guid.Empty : Guid.NewGuid(),
            null,
            StatusContratoFranquia.Rascunho,
            CriadoEmUtc));

        Assert.Equal(parametro, exception.ParamName);
    }

    [Fact]
    public void Numero_e_opcional()
    {
        var contrato = Criar(numero: null);

        Assert.Null(contrato.Numero);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Numero_vazio_e_rejeitado_quando_informado(string numero)
    {
        var exception = Assert.Throws<ArgumentException>(() => Criar(numero));

        Assert.Equal("numero", exception.ParamName);
    }

    [Fact]
    public void Status_invalido_e_rejeitado()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ContratoFranquia(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            (StatusContratoFranquia)999,
            CriadoEmUtc));

        Assert.Equal("status", exception.ParamName);
    }

    [Fact]
    public void Rascunho_pode_alterar_numero_e_atualizado_em_utc()
    {
        var contrato = Criar("Provisorio");
        var atualizadoEmUtc = CriadoEmUtc.AddHours(1);

        contrato.AtualizarNumeroRascunho("  BFA-2026-001  ", atualizadoEmUtc);

        Assert.Equal("BFA-2026-001", contrato.Numero);
        Assert.Equal(atualizadoEmUtc, contrato.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData(StatusContratoFranquia.Ativo)]
    [InlineData(StatusContratoFranquia.Cancelado)]
    public void Rascunho_pode_ser_ativado_ou_cancelado(StatusContratoFranquia novoStatus)
    {
        var contrato = Criar("BFA-2026-001");
        var atualizadoEmUtc = CriadoEmUtc.AddHours(1);

        contrato.AlterarStatus(novoStatus, atualizadoEmUtc);

        Assert.Equal(novoStatus, contrato.Status);
        Assert.Equal(atualizadoEmUtc, contrato.AtualizadoEmUtc);
    }

    [Fact]
    public void Ativo_nao_pode_alterar_numero()
    {
        var contrato = Criar("BFA-2026-001", StatusContratoFranquia.Ativo);

        Assert.Throws<InvalidOperationException>(() => contrato.AtualizarNumeroRascunho(
            "BFA-2026-002",
            CriadoEmUtc.AddHours(1)));

        Assert.Equal("BFA-2026-001", contrato.Numero);
        Assert.Equal(CriadoEmUtc, contrato.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData(StatusContratoFranquia.Encerrado)]
    [InlineData(StatusContratoFranquia.Cancelado)]
    public void Ativo_pode_ser_encerrado_ou_cancelado(StatusContratoFranquia novoStatus)
    {
        var contrato = Criar("BFA-2026-001", StatusContratoFranquia.Ativo);

        contrato.AlterarStatus(novoStatus, CriadoEmUtc.AddHours(1));

        Assert.Equal(novoStatus, contrato.Status);
    }

    [Theory]
    [InlineData(StatusContratoFranquia.Ativo)]
    [InlineData(StatusContratoFranquia.Rascunho)]
    public void Encerrado_nao_pode_regredir(StatusContratoFranquia novoStatus)
    {
        var contrato = Criar("BFA-2026-001", StatusContratoFranquia.Encerrado);

        Assert.Throws<InvalidOperationException>(() => contrato.AlterarStatus(
            novoStatus,
            CriadoEmUtc.AddHours(1)));

        Assert.Equal(StatusContratoFranquia.Encerrado, contrato.Status);
    }

    [Theory]
    [InlineData(StatusContratoFranquia.Ativo)]
    [InlineData(StatusContratoFranquia.Rascunho)]
    public void Cancelado_nao_pode_regredir(StatusContratoFranquia novoStatus)
    {
        var contrato = Criar("BFA-2026-001", StatusContratoFranquia.Cancelado);

        Assert.Throws<InvalidOperationException>(() => contrato.AlterarStatus(
            novoStatus,
            CriadoEmUtc.AddHours(1)));

        Assert.Equal(StatusContratoFranquia.Cancelado, contrato.Status);
    }

    [Fact]
    public void Operacoes_permitidas_preservam_identidade_vinculo_e_criacao()
    {
        var contrato = Criar("Provisorio");
        var id = contrato.Id;
        var franqueadoUnidadeId = contrato.FranqueadoUnidadeId;
        var criadoEmUtc = contrato.CriadoEmUtc;

        contrato.AtualizarNumeroRascunho("BFA-2026-001", CriadoEmUtc.AddHours(1));
        contrato.AlterarStatus(StatusContratoFranquia.Ativo, CriadoEmUtc.AddHours(2));

        Assert.Equal(id, contrato.Id);
        Assert.Equal(franqueadoUnidadeId, contrato.FranqueadoUnidadeId);
        Assert.Equal(criadoEmUtc, contrato.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc.AddHours(2), contrato.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData(nameof(ContratoFranquia.Id))]
    [InlineData(nameof(ContratoFranquia.FranqueadoUnidadeId))]
    [InlineData(nameof(ContratoFranquia.CriadoEmUtc))]
    public void Campo_de_identidade_possui_setter_privado(string nomePropriedade)
    {
        var propriedade = typeof(ContratoFranquia).GetProperty(nomePropriedade);

        Assert.NotNull(propriedade);
        Assert.NotNull(propriedade.SetMethod);
        Assert.True(propriedade.SetMethod.IsPrivate);
    }

    private static ContratoFranquia Criar(
        string? numero,
        StatusContratoFranquia status = StatusContratoFranquia.Rascunho) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        numero,
        status,
        CriadoEmUtc);
}

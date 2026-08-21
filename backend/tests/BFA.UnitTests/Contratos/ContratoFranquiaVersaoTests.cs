using BFA.Domain.Contratos;

namespace BFA.UnitTests.Contratos;

public sealed class ContratoFranquiaVersaoTests
{
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        21,
        12,
        0,
        0,
        DateTimeKind.Utc);

    private static readonly DateOnly DataInicio = new(2026, 1, 1);

    [Fact]
    public void Royalties_oito_porcento_e_mensalidade_quinhentos_coexistem()
    {
        var versao = Criar(
            percentualRoyalties: 8.00m,
            mensalidadeFixa: 500.00m,
            taxaAdesao: 1000.00m,
            diaVencimento: 10,
            dataFim: new DateOnly(2026, 12, 31));

        Assert.Equal(DataInicio, versao.DataInicio);
        Assert.Equal(new DateOnly(2026, 12, 31), versao.DataFim);
        Assert.Equal(8.00m, versao.PercentualRoyalties);
        Assert.Equal(500.00m, versao.MensalidadeFixa);
        Assert.Equal(1000.00m, versao.TaxaAdesao);
        Assert.Equal(10, versao.DiaVencimento);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("contratoFranquiaId")]
    [InlineData("criadoPorUsuarioId")]
    public void Criacao_rejeita_identificador_obrigatorio_vazio(string parametro)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ContratoFranquiaVersao(
            parametro == "id" ? Guid.Empty : Guid.NewGuid(),
            parametro == "contratoFranquiaId" ? Guid.Empty : Guid.NewGuid(),
            1,
            DataInicio,
            null,
            8m,
            500m,
            null,
            null,
            StatusVersaoContratoFranquia.Rascunho,
            null,
            null,
            CriadoEmUtc,
            parametro == "criadoPorUsuarioId" ? Guid.Empty : Guid.NewGuid()));

        Assert.Equal(parametro, exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Numero_da_versao_deve_ser_positivo(int numeroVersao)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(numeroVersao: numeroVersao));

        Assert.Equal("numeroVersao", exception.ParamName);
    }

    [Fact]
    public void Data_final_anterior_ao_inicio_e_rejeitada()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Criar(dataFim: DataInicio.AddDays(-1)));

        Assert.Equal("dataFim", exception.ParamName);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Royalties_fora_de_zero_a_cem_sao_rejeitados(double percentual)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(percentualRoyalties: (decimal)percentual));

        Assert.Equal("percentualRoyalties", exception.ParamName);
    }

    [Fact]
    public void Mensalidade_negativa_e_rejeitada()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(mensalidadeFixa: -0.01m));

        Assert.Equal("mensalidadeFixa", exception.ParamName);
    }

    [Fact]
    public void Taxa_de_adesao_negativa_e_rejeitada()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(taxaAdesao: -0.01m));

        Assert.Equal("taxaAdesao", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Dia_de_vencimento_fora_de_um_a_trinta_e_um_e_rejeitado(int dia)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(diaVencimento: dia));

        Assert.Equal("diaVencimento", exception.ParamName);
    }

    [Fact]
    public void Status_invalido_e_rejeitado()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Criar(status: (StatusVersaoContratoFranquia)999));

        Assert.Equal("status", exception.ParamName);
    }

    [Fact]
    public void Nova_versao_preserva_a_versao_anterior_como_entidade_distinta()
    {
        var contratoId = Guid.NewGuid();
        var versaoUm = Criar(
            contratoId: contratoId,
            numeroVersao: 1,
            mensalidadeFixa: 500m,
            status: StatusVersaoContratoFranquia.Substituida);
        var versaoDois = Criar(
            contratoId: contratoId,
            numeroVersao: 2,
            mensalidadeFixa: 650m,
            status: StatusVersaoContratoFranquia.Vigente);

        Assert.NotEqual(versaoUm.Id, versaoDois.Id);
        Assert.Equal(500m, versaoUm.MensalidadeFixa);
        Assert.Equal(StatusVersaoContratoFranquia.Substituida, versaoUm.Status);
        Assert.Equal(650m, versaoDois.MensalidadeFixa);
        Assert.Equal(StatusVersaoContratoFranquia.Vigente, versaoDois.Status);
    }

    [Fact]
    public void Rascunho_pode_alterar_royalties_mensalidade_e_vigencia()
    {
        var versao = Criar();
        var novaDataInicio = DataInicio.AddMonths(1);
        var novaDataFim = novaDataInicio.AddYears(1);

        versao.AtualizarTermosRascunho(
            novaDataInicio,
            novaDataFim,
            9.50m,
            650m,
            1200m,
            15,
            "Ajuste antes da formalizacao",
            "Termos revisados");

        Assert.Equal(novaDataInicio, versao.DataInicio);
        Assert.Equal(novaDataFim, versao.DataFim);
        Assert.Equal(9.50m, versao.PercentualRoyalties);
        Assert.Equal(650m, versao.MensalidadeFixa);
        Assert.Equal(1200m, versao.TaxaAdesao);
        Assert.Equal(15, versao.DiaVencimento);
    }

    [Theory]
    [InlineData(StatusVersaoContratoFranquia.Vigente)]
    [InlineData(StatusVersaoContratoFranquia.Cancelada)]
    public void Rascunho_pode_ser_formalizado_ou_cancelado(
        StatusVersaoContratoFranquia novoStatus)
    {
        var versao = Criar();

        versao.AlterarStatus(novoStatus);

        Assert.Equal(novoStatus, versao.Status);
    }

    [Theory]
    [InlineData(StatusVersaoContratoFranquia.Substituida)]
    [InlineData(StatusVersaoContratoFranquia.Cancelada)]
    public void Vigente_pode_ser_substituida_ou_cancelada(
        StatusVersaoContratoFranquia novoStatus)
    {
        var versao = Criar(status: StatusVersaoContratoFranquia.Vigente);

        versao.AlterarStatus(novoStatus);

        Assert.Equal(novoStatus, versao.Status);
    }

    [Fact]
    public void Vigente_nao_pode_alterar_royalties()
    {
        var versao = Criar(status: StatusVersaoContratoFranquia.Vigente);

        Assert.Throws<InvalidOperationException>(() => versao.AtualizarTermosRascunho(
            DataInicio,
            null,
            9m,
            versao.MensalidadeFixa,
            versao.TaxaAdesao,
            versao.DiaVencimento,
            versao.MotivoAlteracao,
            versao.Observacoes));

        Assert.Equal(8m, versao.PercentualRoyalties);
    }

    [Fact]
    public void Vigente_nao_pode_alterar_mensalidade()
    {
        var versao = Criar(status: StatusVersaoContratoFranquia.Vigente);

        Assert.Throws<InvalidOperationException>(() => versao.AtualizarTermosRascunho(
            DataInicio,
            null,
            versao.PercentualRoyalties,
            650m,
            versao.TaxaAdesao,
            versao.DiaVencimento,
            versao.MotivoAlteracao,
            versao.Observacoes));

        Assert.Equal(500m, versao.MensalidadeFixa);
    }

    [Fact]
    public void Vigente_nao_pode_alterar_vigencia()
    {
        var versao = Criar(status: StatusVersaoContratoFranquia.Vigente);

        Assert.Throws<InvalidOperationException>(() => versao.AtualizarTermosRascunho(
            DataInicio.AddDays(1),
            DataInicio.AddYears(1),
            versao.PercentualRoyalties,
            versao.MensalidadeFixa,
            versao.TaxaAdesao,
            versao.DiaVencimento,
            versao.MotivoAlteracao,
            versao.Observacoes));

        Assert.Equal(DataInicio, versao.DataInicio);
        Assert.Null(versao.DataFim);
    }

    [Theory]
    [InlineData(StatusVersaoContratoFranquia.Substituida)]
    [InlineData(StatusVersaoContratoFranquia.Cancelada)]
    public void Versao_historica_nao_pode_alterar_termos(
        StatusVersaoContratoFranquia status)
    {
        var versao = Criar(status: status);

        Assert.Throws<InvalidOperationException>(() => versao.AtualizarTermosRascunho(
            DataInicio,
            null,
            10m,
            700m,
            null,
            null,
            null,
            null));

        Assert.Equal(8m, versao.PercentualRoyalties);
        Assert.Equal(500m, versao.MensalidadeFixa);
    }

    [Theory]
    [InlineData(StatusVersaoContratoFranquia.Rascunho)]
    [InlineData(StatusVersaoContratoFranquia.Vigente)]
    public void Substituida_nao_pode_regredir(StatusVersaoContratoFranquia novoStatus)
    {
        var versao = Criar(status: StatusVersaoContratoFranquia.Substituida);

        Assert.Throws<InvalidOperationException>(() => versao.AlterarStatus(novoStatus));

        Assert.Equal(StatusVersaoContratoFranquia.Substituida, versao.Status);
    }

    [Theory]
    [InlineData(StatusVersaoContratoFranquia.Rascunho)]
    [InlineData(StatusVersaoContratoFranquia.Vigente)]
    public void Cancelada_nao_pode_regredir(StatusVersaoContratoFranquia novoStatus)
    {
        var versao = Criar(status: StatusVersaoContratoFranquia.Cancelada);

        Assert.Throws<InvalidOperationException>(() => versao.AlterarStatus(novoStatus));

        Assert.Equal(StatusVersaoContratoFranquia.Cancelada, versao.Status);
    }

    [Fact]
    public void Edicao_do_rascunho_preserva_identidade_numero_e_auditoria()
    {
        var versao = Criar();
        var id = versao.Id;
        var contratoId = versao.ContratoFranquiaId;
        var numeroVersao = versao.NumeroVersao;
        var criadoEmUtc = versao.CriadoEmUtc;
        var criadoPorUsuarioId = versao.CriadoPorUsuarioId;

        versao.AtualizarTermosRascunho(
            DataInicio.AddDays(1),
            null,
            9m,
            600m,
            null,
            20,
            null,
            null);

        Assert.Equal(id, versao.Id);
        Assert.Equal(contratoId, versao.ContratoFranquiaId);
        Assert.Equal(numeroVersao, versao.NumeroVersao);
        Assert.Equal(criadoEmUtc, versao.CriadoEmUtc);
        Assert.Equal(criadoPorUsuarioId, versao.CriadoPorUsuarioId);
    }

    private static ContratoFranquiaVersao Criar(
        Guid? contratoId = null,
        int numeroVersao = 1,
        DateOnly? dataFim = null,
        decimal percentualRoyalties = 8m,
        decimal mensalidadeFixa = 500m,
        decimal? taxaAdesao = null,
        int? diaVencimento = null,
        StatusVersaoContratoFranquia status = StatusVersaoContratoFranquia.Rascunho) => new(
            Guid.NewGuid(),
            contratoId ?? Guid.NewGuid(),
            numeroVersao,
            DataInicio,
            dataFim,
            percentualRoyalties,
            mensalidadeFixa,
            taxaAdesao,
            diaVencimento,
            status,
            numeroVersao > 1 ? "Reajuste contratual" : null,
            null,
            CriadoEmUtc,
            Guid.NewGuid());
}

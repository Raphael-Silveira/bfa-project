using BFA.Domain.Alunos;

namespace BFA.UnitTests.Alunos;

public sealed class ResponsavelTests
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
    public void Criacao_com_telefone_e_sem_cpf_e_valida()
    {
        var responsavel = CriarResponsavel(cpf: null, telefone: " 15999999999 ", email: null);

        Assert.True(responsavel.Ativo);
        Assert.Null(responsavel.Cpf);
        Assert.Equal("15999999999", responsavel.Telefone);
        Assert.Null(responsavel.Email);
    }

    [Fact]
    public void Criacao_com_email_e_sem_telefone_e_valida()
    {
        var responsavel = CriarResponsavel(telefone: null, email: " responsavel@bfa.test ");

        Assert.Null(responsavel.Telefone);
        Assert.Equal("responsavel@bfa.test", responsavel.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_vazio_e_rejeitado(string nomeCompleto)
    {
        Assert.Throws<ArgumentException>(() => new Responsavel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            nomeCompleto,
            CriadoEmUtc,
            telefone: "15999999999"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", " ")]
    public void Ao_menos_um_canal_de_contato_e_obrigatorio(string? telefone, string? email)
    {
        Assert.Throws<ArgumentException>(() => CriarResponsavel(
            telefone: telefone,
            email: email));
    }

    [Fact]
    public void Atualizacao_invalida_de_contato_nao_altera_dados_existentes()
    {
        var responsavel = CriarResponsavel(telefone: "15999999999", email: null);

        Assert.Throws<ArgumentException>(() => responsavel.AtualizarDados(
            "Outro nome",
            null,
            null,
            null,
            CriadoEmUtc.AddMinutes(1)));

        Assert.Equal("Responsavel BFA", responsavel.NomeCompleto);
        Assert.Equal("15999999999", responsavel.Telefone);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123.456.789-01")]
    [InlineData("1234567890A")]
    public void Cpf_em_formato_invalido_e_rejeitado(string cpf)
    {
        Assert.Throws<ArgumentException>(() => CriarResponsavel(cpf: cpf));
    }

    [Fact]
    public void Usuario_pode_ser_associado_depois_da_criacao()
    {
        var responsavel = CriarResponsavel();
        var usuarioId = Guid.NewGuid();

        responsavel.AlterarUsuario(usuarioId, CriadoEmUtc.AddMinutes(1));

        Assert.Equal(usuarioId, responsavel.UsuarioId);
    }

    [Fact]
    public void Responsavel_pode_ser_desativado_e_reativado_no_dominio()
    {
        var responsavel = CriarResponsavel();

        responsavel.Desativar(CriadoEmUtc.AddMinutes(1));
        Assert.False(responsavel.Ativo);

        responsavel.Ativar(CriadoEmUtc.AddMinutes(2));
        Assert.True(responsavel.Ativo);
    }

    [Fact]
    public void Identidade_tenant_e_criacao_nao_possuem_setter_publico()
    {
        foreach (var propertyName in new[]
        {
            nameof(Responsavel.Id),
            nameof(Responsavel.OrganizacaoId),
            nameof(Responsavel.CriadoEmUtc)
        })
        {
            Assert.False(typeof(Responsavel).GetProperty(propertyName)!.SetMethod!.IsPublic);
        }
    }

    private static Responsavel CriarResponsavel(
        string? cpf = "10987654321",
        string? telefone = "15999999999",
        string? email = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            " Responsavel BFA ",
            CriadoEmUtc,
            cpf: cpf,
            telefone: telefone,
            email: email);
}

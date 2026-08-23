using BFA.Domain.Professores;

namespace BFA.UnitTests.Professores;

public sealed class ProfessorTests
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
    public void Criacao_define_dados_e_inicia_ativo()
    {
        var usuarioId = Guid.NewGuid();
        var professor = new Professor(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  Joao da Silva  ",
            CriadoEmUtc,
            usuarioId,
            "12345678901",
            " 15999999999 ",
            " professor@bfa.test ");

        Assert.Equal(usuarioId, professor.UsuarioId);
        Assert.Equal("Joao da Silva", professor.NomeCompleto);
        Assert.Equal("12345678901", professor.Cpf);
        Assert.Equal("15999999999", professor.Telefone);
        Assert.Equal("professor@bfa.test", professor.Email);
        Assert.True(professor.Ativo);
        Assert.Equal(CriadoEmUtc, professor.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, professor.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_rejeita_nome_vazio(string? nomeCompleto)
    {
        var exception = Assert.Throws<ArgumentException>(() => new Professor(
            Guid.NewGuid(),
            Guid.NewGuid(),
            nomeCompleto!,
            CriadoEmUtc));

        Assert.Equal("nomeCompleto", exception.ParamName);
    }

    [Fact]
    public void Criacao_aceita_cpf_com_onze_digitos()
    {
        var professor = Criar(cpf: "12345678901");

        Assert.Equal("12345678901", professor.Cpf);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("1234567890A")]
    [InlineData("123.456.789-01")]
    public void Criacao_rejeita_cpf_fora_de_onze_digitos(string cpf)
    {
        var exception = Assert.Throws<ArgumentException>(() => Criar(cpf: cpf));

        Assert.Equal("cpf", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_aceita_cpf_ausente(string? cpf)
    {
        var professor = Criar(cpf: cpf);

        Assert.Null(professor.Cpf);
    }

    [Fact]
    public void Criacao_aceita_usuario_ausente()
    {
        var professor = Criar();

        Assert.Null(professor.UsuarioId);
    }

    [Theory]
    [InlineData(nameof(Professor.Id))]
    [InlineData(nameof(Professor.OrganizacaoId))]
    [InlineData(nameof(Professor.CriadoEmUtc))]
    public void Identidade_historica_nao_possui_setter_publico(string propriedade)
    {
        var property = typeof(Professor).GetProperty(propriedade);

        Assert.NotNull(property);
        Assert.False(property.SetMethod?.IsPublic ?? false);
    }

    [Fact]
    public void Usuario_pode_ser_associado_posteriormente()
    {
        var professor = Criar();
        var usuarioId = Guid.NewGuid();
        var atualizadoEmUtc = CriadoEmUtc.AddHours(1);

        professor.AlterarUsuario(usuarioId, atualizadoEmUtc);

        Assert.Equal(usuarioId, professor.UsuarioId);
        Assert.Equal(atualizadoEmUtc, professor.AtualizadoEmUtc);
    }

    [Fact]
    public void Dados_cadastrais_podem_ser_atualizados_sem_alterar_identidade()
    {
        var professor = Criar(cpf: "12345678901");
        var id = professor.Id;
        var organizacaoId = professor.OrganizacaoId;
        var criadoEmUtc = professor.CriadoEmUtc;
        var atualizadoEmUtc = CriadoEmUtc.AddHours(1);

        professor.AtualizarDados(
            "Maria da Silva",
            "10987654321",
            "15988888888",
            "maria@bfa.test",
            atualizadoEmUtc);

        Assert.Equal("Maria da Silva", professor.NomeCompleto);
        Assert.Equal("10987654321", professor.Cpf);
        Assert.Equal("15988888888", professor.Telefone);
        Assert.Equal("maria@bfa.test", professor.Email);
        Assert.Equal(atualizadoEmUtc, professor.AtualizadoEmUtc);
        Assert.Equal(id, professor.Id);
        Assert.Equal(organizacaoId, professor.OrganizacaoId);
        Assert.Equal(criadoEmUtc, professor.CriadoEmUtc);
    }

    [Fact]
    public void Professor_pode_ser_inativado_e_reativado()
    {
        var professor = Criar();
        var inativadoEmUtc = CriadoEmUtc.AddHours(1);
        var reativadoEmUtc = CriadoEmUtc.AddHours(2);

        professor.Desativar(inativadoEmUtc);

        Assert.False(professor.Ativo);
        Assert.Equal(inativadoEmUtc, professor.AtualizadoEmUtc);

        professor.Ativar(reativadoEmUtc);

        Assert.True(professor.Ativo);
        Assert.Equal(reativadoEmUtc, professor.AtualizadoEmUtc);
    }

    private static Professor Criar(string? cpf = null) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Joao da Silva",
        CriadoEmUtc,
        cpf: cpf);
}

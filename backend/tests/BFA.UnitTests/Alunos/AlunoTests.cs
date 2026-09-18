using BFA.Domain.Alunos;

namespace BFA.UnitTests.Alunos;

public sealed class AlunoTests
{
    private static readonly DateOnly DataCivilAtual = new(2026, 8, 31);
    private static readonly DateTime CriadoEmUtc = new(
        2026,
        8,
        31,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public void Criacao_valida_nao_exige_responsavel_e_inicia_ativo()
    {
        var aluno = CriarAluno(cpf: null);

        Assert.True(aluno.Ativo);
        Assert.Null(aluno.Cpf);
        Assert.Null(aluno.UsuarioId);
        Assert.Equal(CriadoEmUtc, aluno.CriadoEmUtc);
        Assert.Equal(CriadoEmUtc, aluno.AtualizadoEmUtc);
    }

    [Fact]
    public void Modelo_nao_possui_UnidadeId()
    {
        Assert.Null(typeof(Aluno).GetProperty("UnidadeId"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_vazio_e_rejeitado(string nome)
    {
        Assert.Throws<ArgumentException>(() => CriarAluno(nomeCompleto: nome));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123.456.789-01")]
    [InlineData("1234567890A")]
    public void Cpf_em_formato_invalido_e_rejeitado(string cpf)
    {
        Assert.Throws<ArgumentException>(() => CriarAluno(cpf: cpf));
    }

    [Fact]
    public void Data_de_nascimento_futura_e_rejeitada()
    {
        Assert.Throws<ArgumentException>(() => CriarAluno(
            dataNascimento: DataCivilAtual.AddDays(1)));
    }

    [Theory]
    [InlineData(2008, 8, 31, false)]
    [InlineData(2008, 9, 1, true)]
    [InlineData(2000, 1, 1, false)]
    public void Menoridade_usa_data_civil_explicita(
        int ano,
        int mes,
        int dia,
        bool esperado)
    {
        var aluno = CriarAluno(dataNascimento: new DateOnly(ano, mes, dia));

        Assert.Equal(esperado, aluno.EhMenorEm(DataCivilAtual));
    }

    [Fact]
    public void Usuario_pode_ser_associado_depois_da_criacao()
    {
        var aluno = CriarAluno();
        var usuarioId = Guid.NewGuid();
        var atualizadoEmUtc = CriadoEmUtc.AddMinutes(1);

        aluno.AlterarUsuario(usuarioId, atualizadoEmUtc);

        Assert.Equal(usuarioId, aluno.UsuarioId);
        Assert.Equal(atualizadoEmUtc, aluno.AtualizadoEmUtc);
    }

    [Fact]
    public void Aluno_pode_ser_desativado_e_reativado_no_dominio()
    {
        var aluno = CriarAluno();

        aluno.Desativar(CriadoEmUtc.AddMinutes(1));
        Assert.False(aluno.Ativo);

        aluno.Ativar(CriadoEmUtc.AddMinutes(2));
        Assert.True(aluno.Ativo);
    }

    [Fact]
    public void Atualizar_contato_preserva_identidade_e_dados_pessoais()
    {
        var aluno = CriarAluno();
        var nome = aluno.NomeCompleto;
        var cpf = aluno.Cpf;
        var nascimento = aluno.DataNascimento;
        var atualizadoEmUtc = CriadoEmUtc.AddMinutes(1);

        aluno.AtualizarContato("(11) 99999-0000", " aluno@exemplo.com ", atualizadoEmUtc);

        Assert.Equal(nome, aluno.NomeCompleto);
        Assert.Equal(cpf, aluno.Cpf);
        Assert.Equal(nascimento, aluno.DataNascimento);
        Assert.Equal("5511999990000", aluno.Telefone);
        Assert.Equal("aluno@exemplo.com", aluno.Email);
        Assert.Equal(atualizadoEmUtc, aluno.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData("+55 (11) 99268-2235", "5511992682235")]
    [InlineData("11992682235", "5511992682235")]
    [InlineData("5511992682235", "5511992682235")]
    [InlineData("+55 (11) 3333-4444", "551133334444")]
    public void Telefone_brasileiro_e_normalizado_para_ddi_55(string entrada, string esperado)
    {
        Assert.Equal(esperado, TelefoneBrasileiro.Normalizar(entrada));
        Assert.Equal(
            entrada.Contains("3333-4444", StringComparison.Ordinal)
                ? "+55 (11) 3333-4444"
                : "+55 (11) 99268-2235",
            TelefoneBrasileiro.Formatar(entrada));
    }

    [Fact]
    public void Telefone_brasileiro_e_formatado_sem_ddi_no_formulario()
    {
        Assert.Equal("(11) 99268-2235", TelefoneBrasileiro.FormatarLocal("5511992682235"));
    }

    [Theory]
    [InlineData("55119926822355")]
    [InlineData("+55 (00) 99268-2235")]
    [InlineData("telefone inválido")]
    public void Telefone_brasileiro_invalido_e_rejeitado(string entrada)
    {
        Assert.Throws<ArgumentException>(() => TelefoneBrasileiro.Normalizar(entrada));
    }

    [Fact]
    public void Identidade_tenant_e_criacao_nao_possuem_setter_publico()
    {
        foreach (var propertyName in new[]
        {
            nameof(Aluno.Id),
            nameof(Aluno.OrganizacaoId),
            nameof(Aluno.CriadoEmUtc)
        })
        {
            Assert.False(typeof(Aluno).GetProperty(propertyName)!.SetMethod!.IsPublic);
        }
    }

    private static Aluno CriarAluno(
        string nomeCompleto = "  Aluna BFA  ",
        DateOnly? dataNascimento = null,
        string? cpf = "12345678901") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            nomeCompleto,
            dataNascimento ?? new DateOnly(2010, 5, 20),
            DataCivilAtual,
            CriadoEmUtc,
            cpf: cpf);
}

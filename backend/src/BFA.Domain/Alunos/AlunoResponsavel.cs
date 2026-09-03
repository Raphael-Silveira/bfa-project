namespace BFA.Domain.Alunos;

public sealed class AlunoResponsavel
{
    public const int TipoRelacaoTamanhoMaximo = 30;
    public const int DescricaoRelacaoTamanhoMaximo = 100;

    private AlunoResponsavel()
    {
    }

    public AlunoResponsavel(
        Guid id,
        Guid organizacaoId,
        Guid alunoId,
        Guid responsavelId,
        TipoRelacaoResponsavel tipoRelacao,
        bool principalContato,
        bool responsavelFinanceiro,
        DateTime criadoEmUtc,
        string? descricaoRelacao = null)
    {
        ValidarIdentificador(id, nameof(id), "O identificador do vinculo deve ser informado.");
        ValidarIdentificador(
            organizacaoId,
            nameof(organizacaoId),
            "O identificador da organizacao deve ser informado.");
        ValidarIdentificador(
            alunoId,
            nameof(alunoId),
            "O identificador do aluno deve ser informado.");
        ValidarIdentificador(
            responsavelId,
            nameof(responsavelId),
            "O identificador do responsavel deve ser informado.");
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        AlunoId = alunoId;
        ResponsavelId = responsavelId;
        TipoRelacao = ValidarTipoRelacao(tipoRelacao);
        DescricaoRelacao = ValidarDescricaoRelacao(tipoRelacao, descricaoRelacao);
        PrincipalContato = principalContato;
        ResponsavelFinanceiro = responsavelFinanceiro;
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid AlunoId { get; private set; }

    public Guid ResponsavelId { get; private set; }

    public TipoRelacaoResponsavel TipoRelacao { get; private set; }

    public string? DescricaoRelacao { get; private set; }

    public bool PrincipalContato { get; private set; }

    public bool ResponsavelFinanceiro { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void AtualizarClassificacao(
        TipoRelacaoResponsavel tipoRelacao,
        string? descricaoRelacao,
        bool principalContato,
        bool responsavelFinanceiro,
        DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
        var tipoValidado = ValidarTipoRelacao(tipoRelacao);
        var descricaoValidada = ValidarDescricaoRelacao(tipoRelacao, descricaoRelacao);

        TipoRelacao = tipoValidado;
        DescricaoRelacao = descricaoValidada;
        PrincipalContato = principalContato;
        ResponsavelFinanceiro = responsavelFinanceiro;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Ativar(DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
        Ativo = true;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Desativar(DateTime atualizadoEmUtc)
    {
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
        Ativo = false;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static void ValidarIdentificador(
        Guid identificador,
        string nomeParametro,
        string mensagem)
    {
        if (identificador == Guid.Empty)
        {
            throw new ArgumentException(mensagem, nomeParametro);
        }
    }

    private static TipoRelacaoResponsavel ValidarTipoRelacao(
        TipoRelacaoResponsavel tipoRelacao)
    {
        if (!Enum.IsDefined(tipoRelacao))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tipoRelacao),
                "O tipo de relacao com o responsavel e invalido.");
        }

        return tipoRelacao;
    }

    private static string? ValidarDescricaoRelacao(
        TipoRelacaoResponsavel tipoRelacao,
        string? descricaoRelacao)
    {
        if (tipoRelacao == TipoRelacaoResponsavel.Outro)
        {
            if (string.IsNullOrWhiteSpace(descricaoRelacao))
            {
                throw new ArgumentException(
                    "A descricao da relacao deve ser informada para o tipo Outro.",
                    nameof(descricaoRelacao));
            }

            var descricaoNormalizada = descricaoRelacao.Trim();

            if (descricaoNormalizada.Length > DescricaoRelacaoTamanhoMaximo)
            {
                throw new ArgumentException(
                    $"A descricao da relacao deve possuir no maximo "
                    + $"{DescricaoRelacaoTamanhoMaximo} caracteres.",
                    nameof(descricaoRelacao));
            }

            return descricaoNormalizada;
        }

        if (descricaoRelacao is not null)
        {
            throw new ArgumentException(
                "A descricao da relacao deve ser nula quando o tipo nao for Outro.",
                nameof(descricaoRelacao));
        }

        return null;
    }

    private static void ValidarDataUtc(DateTime data, string nomeParametro)
    {
        if (data.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data deve estar em UTC.", nomeParametro);
        }
    }
}

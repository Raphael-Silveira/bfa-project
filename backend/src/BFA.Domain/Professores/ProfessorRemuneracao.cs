namespace BFA.Domain.Professores;

public sealed class ProfessorRemuneracao
{
    public const int ModalidadeTamanhoMaximo = 30;
    public const int ObservacaoTamanhoMaximo = 1000;

    private ProfessorRemuneracao()
    {
    }

    public ProfessorRemuneracao(
        Guid id,
        Guid organizacaoId,
        Guid professorUnidadeId,
        ModalidadeRemuneracaoProfessor modalidade,
        decimal valor,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc,
        string? observacao = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da remuneracao deve ser informado.",
                nameof(id));
        }

        if (organizacaoId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da organizacao deve ser informado.",
                nameof(organizacaoId));
        }

        if (professorUnidadeId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do vinculo profissional deve ser informado.",
                nameof(professorUnidadeId));
        }

        if (!Enum.IsDefined(modalidade))
        {
            throw new ArgumentOutOfRangeException(
                nameof(modalidade),
                modalidade,
                "A modalidade de remuneracao e invalida.");
        }

        if (valor < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(valor),
                valor,
                "O valor da remuneracao nao pode ser negativo.");
        }

        ValidarVigencia(vigenciaInicio, vigenciaFim);

        if (criadoPorUsuarioId == Guid.Empty)
        {
            throw new ArgumentException(
                "O usuario responsavel pela criacao deve ser informado.",
                nameof(criadoPorUsuarioId));
        }

        if (criadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        }

        Id = id;
        OrganizacaoId = organizacaoId;
        ProfessorUnidadeId = professorUnidadeId;
        Modalidade = modalidade;
        Valor = valor;
        VigenciaInicio = vigenciaInicio;
        VigenciaFim = vigenciaFim;
        Observacao = NormalizarObservacao(observacao);
        CriadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid ProfessorUnidadeId { get; private set; }

    public ModalidadeRemuneracaoProfessor Modalidade { get; private set; }

    public decimal Valor { get; private set; }

    public DateOnly VigenciaInicio { get; private set; }

    public DateOnly? VigenciaFim { get; private set; }

    public string? Observacao { get; private set; }

    public Guid CriadoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public void Encerrar(DateOnly vigenciaFim)
    {
        if (VigenciaFim.HasValue)
        {
            throw new InvalidOperationException("A remuneracao ja possui vigencia final.");
        }

        ValidarVigencia(VigenciaInicio, vigenciaFim);
        VigenciaFim = vigenciaFim;
    }

    private static void ValidarVigencia(DateOnly vigenciaInicio, DateOnly? vigenciaFim)
    {
        if (vigenciaFim.HasValue && vigenciaFim.Value < vigenciaInicio)
        {
            throw new ArgumentException(
                "A vigencia final nao pode ser anterior a vigencia inicial.",
                nameof(vigenciaFim));
        }
    }

    private static string? NormalizarObservacao(string? observacao)
    {
        if (string.IsNullOrWhiteSpace(observacao))
        {
            return null;
        }

        var observacaoNormalizada = observacao.Trim();

        if (observacaoNormalizada.Length > ObservacaoTamanhoMaximo)
        {
            throw new ArgumentException(
                $"A observacao deve possuir no maximo {ObservacaoTamanhoMaximo} caracteres.",
                nameof(observacao));
        }

        return observacaoNormalizada;
    }
}

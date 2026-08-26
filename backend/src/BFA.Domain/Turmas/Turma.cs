namespace BFA.Domain.Turmas;

public sealed class Turma
{
    public const int NomeTamanhoMaximo = 150;

    private Turma()
    {
    }

    public Turma(
        Guid id,
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorUnidadeId,
        string nome,
        int capacidade,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc)
    {
        ValidarIdentificador(id, nameof(id));
        ValidarIdentificador(organizacaoId, nameof(organizacaoId));
        ValidarIdentificador(unidadeId, nameof(unidadeId));
        ValidarIdentificador(professorUnidadeId, nameof(professorUnidadeId));
        ValidarIdentificador(criadoPorUsuarioId, nameof(criadoPorUsuarioId));
        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        ProfessorUnidadeId = professorUnidadeId;
        Nome = NormalizarNome(nome);
        Capacidade = ValidarCapacidade(capacidade);
        Ativo = true;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        AtualizadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid UnidadeId { get; private set; }

    public Guid ProfessorUnidadeId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public int Capacidade { get; private set; }

    public bool Ativo { get; private set; }

    public Guid CriadoPorUsuarioId { get; private set; }

    public Guid AtualizadoPorUsuarioId { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void Atualizar(
        string nome,
        int capacidade,
        Guid professorUnidadeId,
        Guid atualizadoPorUsuarioId,
        DateTime atualizadoEmUtc)
    {
        ValidarIdentificador(professorUnidadeId, nameof(professorUnidadeId));
        ValidarAtualizacao(atualizadoPorUsuarioId, atualizadoEmUtc);

        Nome = NormalizarNome(nome);
        Capacidade = ValidarCapacidade(capacidade);
        ProfessorUnidadeId = professorUnidadeId;
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Ativar(Guid atualizadoPorUsuarioId, DateTime atualizadoEmUtc)
    {
        ValidarAtualizacao(atualizadoPorUsuarioId, atualizadoEmUtc);
        Ativo = true;
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Desativar(Guid atualizadoPorUsuarioId, DateTime atualizadoEmUtc)
    {
        ValidarAtualizacao(atualizadoPorUsuarioId, atualizadoEmUtc);
        Ativo = false;
        AtualizadoPorUsuarioId = atualizadoPorUsuarioId;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static string NormalizarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome da turma deve ser informado.", nameof(nome));
        }

        var nomeNormalizado = nome.Trim();
        if (nomeNormalizado.Length > NomeTamanhoMaximo)
        {
            throw new ArgumentException(
                $"O nome da turma deve possuir no maximo {NomeTamanhoMaximo} caracteres.",
                nameof(nome));
        }

        return nomeNormalizado;
    }

    private static int ValidarCapacidade(int capacidade)
    {
        if (capacidade <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacidade),
                capacidade,
                "A capacidade da turma deve ser maior que zero.");
        }

        return capacidade;
    }

    private static void ValidarAtualizacao(
        Guid atualizadoPorUsuarioId,
        DateTime atualizadoEmUtc)
    {
        ValidarIdentificador(atualizadoPorUsuarioId, nameof(atualizadoPorUsuarioId));
        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
    }

    private static void ValidarIdentificador(Guid valor, string parametro)
    {
        if (valor == Guid.Empty)
        {
            throw new ArgumentException("O identificador deve ser informado.", parametro);
        }
    }

    private static void ValidarDataUtc(DateTime data, string parametro)
    {
        if (data.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data deve estar em UTC.", parametro);
        }
    }
}

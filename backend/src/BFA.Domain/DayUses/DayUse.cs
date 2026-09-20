using BFA.Domain.Alunos;

namespace BFA.Domain.DayUses;

public sealed class DayUse
{
    public const int NomeAvulsoTamanhoMaximo = 150;
    public const int TelefoneAvulsoTamanhoMaximo = 30;
    public const int EmailAvulsoTamanhoMaximo = 256;

    private DayUse()
    {
    }

    public DayUse(
        Guid id,
        Guid organizacaoId,
        Guid unidadeId,
        Guid? alunoId,
        string? nomeAvulso,
        string? telefoneAvulso,
        string? emailAvulso,
        DateOnly dataUso,
        decimal valorSugerido,
        decimal valorCobrado,
        bool pago,
        Guid criadoPorUsuarioId,
        DateTime criadoEmUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("O identificador do Day Use deve ser informado.", nameof(id));
        if (organizacaoId == Guid.Empty) throw new ArgumentException("A organizacao deve ser informada.", nameof(organizacaoId));
        if (unidadeId == Guid.Empty) throw new ArgumentException("A unidade deve ser informada.", nameof(unidadeId));
        if (criadoPorUsuarioId == Guid.Empty) throw new ArgumentException("O usuario criador deve ser informado.", nameof(criadoPorUsuarioId));
        if (criadoEmUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        if (dataUso == default) throw new ArgumentException("A data de uso deve ser informada.", nameof(dataUso));
        if (valorSugerido < 0) throw new ArgumentOutOfRangeException(nameof(valorSugerido), "O valor sugerido nao pode ser negativo.");
        if (valorCobrado < 0) throw new ArgumentOutOfRangeException(nameof(valorCobrado), "O valor cobrado nao pode ser negativo.");

        var avulso = alunoId is null;
        if (!avulso && (alunoId == Guid.Empty || !string.IsNullOrWhiteSpace(nomeAvulso)
                || !string.IsNullOrWhiteSpace(telefoneAvulso) || !string.IsNullOrWhiteSpace(emailAvulso)))
            throw new ArgumentException("Aluno cadastrado nao pode possuir dados de participante avulso.");

        if (avulso)
        {
            if (string.IsNullOrWhiteSpace(nomeAvulso))
                throw new ArgumentException("O nome do participante avulso deve ser informado.", nameof(nomeAvulso));
            NomeAvulso = NormalizarNome(nomeAvulso);
            TelefoneAvulso = NormalizarTelefone(telefoneAvulso);
            EmailAvulso = NormalizarEmail(emailAvulso);
        }

        Id = id;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        AlunoId = alunoId;
        DataUso = dataUso;
        ValorSugerido = valorSugerido;
        ValorCobrado = valorCobrado;
        Pago = valorCobrado > 0 && pago;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizacaoId { get; private set; }
    public Guid UnidadeId { get; private set; }
    public Guid? AlunoId { get; private set; }
    public string? NomeAvulso { get; private set; }
    public string? TelefoneAvulso { get; private set; }
    public string? EmailAvulso { get; private set; }
    public DateOnly DataUso { get; private set; }
    public decimal ValorSugerido { get; private set; }
    public decimal ValorCobrado { get; private set; }
    public bool Pago { get; private set; }
    public Guid CriadoPorUsuarioId { get; private set; }
    public DateTime CriadoEmUtc { get; private set; }

    public void MarcarComoPago()
    {
        if (ValorCobrado <= 0)
            throw new InvalidOperationException("Uma cortesia nao possui pagamento para registrar.");

        Pago = true;
    }

    private static string NormalizarNome(string nome)
    {
        var resultado = nome.Trim();
        if (resultado.Length > NomeAvulsoTamanhoMaximo)
            throw new ArgumentException($"O nome deve possuir no maximo {NomeAvulsoTamanhoMaximo} caracteres.", nameof(nome));
        return resultado;
    }

    private static string? NormalizarTelefone(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return null;
        return TelefoneBrasileiro.Normalizar(telefone);
    }

    private static string? NormalizarEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var resultado = email.Trim();
        if (resultado.Length > EmailAvulsoTamanhoMaximo || !new System.Net.Mail.MailAddress(resultado).Address.Equals(resultado, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Informe um e-mail valido.", nameof(email));
        return resultado;
    }
}

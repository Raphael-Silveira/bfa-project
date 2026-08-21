namespace BFA.Domain.Contratos;

public sealed class ContratoFranquia
{
    public const int NumeroTamanhoMaximo = 100;
    public const int StatusTamanhoMaximo = 30;

    private ContratoFranquia()
    {
    }

    public ContratoFranquia(
        Guid id,
        Guid franqueadoUnidadeId,
        string? numero,
        StatusContratoFranquia status,
        DateTime criadoEmUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador do contrato deve ser informado.", nameof(id));
        }

        if (franqueadoUnidadeId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do vinculo entre franqueado e unidade deve ser informado.",
                nameof(franqueadoUnidadeId));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "O status do contrato e invalido.");
        }

        ValidarDataUtc(criadoEmUtc, nameof(criadoEmUtc));

        Id = id;
        FranqueadoUnidadeId = franqueadoUnidadeId;
        Numero = NormalizarOpcionalInformado(numero, NumeroTamanhoMaximo, nameof(numero));
        Status = status;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid FranqueadoUnidadeId { get; private set; }

    public string? Numero { get; private set; }

    public StatusContratoFranquia Status { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void AtualizarNumeroRascunho(string? numero, DateTime atualizadoEmUtc)
    {
        if (Status != StatusContratoFranquia.Rascunho)
        {
            throw new InvalidOperationException(
                "Somente um contrato em rascunho pode ter seu numero alterado.");
        }

        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
        Numero = NormalizarOpcionalInformado(numero, NumeroTamanhoMaximo, nameof(numero));
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void AlterarStatus(
        StatusContratoFranquia novoStatus,
        DateTime atualizadoEmUtc)
    {
        if (!Enum.IsDefined(novoStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(novoStatus),
                novoStatus,
                "O novo status do contrato e invalido.");
        }

        var transicaoPermitida = Status switch
        {
            StatusContratoFranquia.Rascunho =>
                novoStatus is StatusContratoFranquia.Rascunho
                    or StatusContratoFranquia.Ativo
                    or StatusContratoFranquia.Cancelado,
            StatusContratoFranquia.Ativo =>
                novoStatus is StatusContratoFranquia.Ativo
                    or StatusContratoFranquia.Encerrado
                    or StatusContratoFranquia.Cancelado,
            StatusContratoFranquia.Encerrado =>
                novoStatus == StatusContratoFranquia.Encerrado,
            StatusContratoFranquia.Cancelado =>
                novoStatus == StatusContratoFranquia.Cancelado,
            _ => false
        };

        if (!transicaoPermitida)
        {
            throw new InvalidOperationException(
                $"A transicao de {Status} para {novoStatus} nao e permitida.");
        }

        ValidarDataUtc(atualizadoEmUtc, nameof(atualizadoEmUtc));
        Status = novoStatus;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static string? NormalizarOpcionalInformado(
        string? valor,
        int tamanhoMaximo,
        string nomeParametro)
    {
        if (valor is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException("O valor informado nao pode ser vazio.", nomeParametro);
        }

        var valorNormalizado = valor.Trim();

        if (valorNormalizado.Length > tamanhoMaximo)
        {
            throw new ArgumentException(
                $"O valor deve possuir no maximo {tamanhoMaximo} caracteres.",
                nomeParametro);
        }

        return valorNormalizado;
    }

    private static void ValidarDataUtc(DateTime data, string nomeParametro)
    {
        if (data.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data deve estar em UTC.", nomeParametro);
        }
    }
}

using BFA.Domain.Contratos;

namespace BFA.Application.Franqueadora.Contratos;

public enum EstadoGerenciamentoContratoFranquia
{
    Sucesso = 1,
    SemAcesso = 2,
    SelecaoOrganizacaoNecessaria = 3,
    NaoEncontrado = 4,
    VinculoInativo = 5,
    DadosInvalidos = 6,
    EstadoInvalido = 7,
    DocumentoObrigatorio = 8,
    ContratoAtivoExistente = 9,
    ConflitoVersao = 10,
    FalhaPersistencia = 11,
    ArquivoInvalido = 12,
    ArquivoMuitoGrande = 13,
    DocumentoIndisponivel = 14
}

public enum EstadoPersistenciaContratoFranquia
{
    Sucesso = 1,
    ContratoAtivoExistente = 2,
    ConflitoVersao = 3,
    Falha = 4
}

public sealed record ResultadoContratoFranquia<T>(
    EstadoGerenciamentoContratoFranquia Estado,
    T? Valor,
    string? Mensagem = null);

public sealed record ResultadoOperacaoContratoFranquia(
    EstadoGerenciamentoContratoFranquia Estado,
    string? Mensagem = null);

public sealed record ContextoContratoFranquia(
    Guid FranqueadoUnidadeId,
    Guid OrganizacaoId,
    Guid FranqueadoId,
    string FranqueadoNome,
    Guid UnidadeId,
    string UnidadeNome,
    bool VinculoAtivo,
    bool UnidadeAtiva);

public sealed record DocumentoContratoFranquiaResumo(
    Guid Id,
    TipoDocumentoContratoFranquia TipoDocumento,
    string NomeOriginal,
    long TamanhoBytes,
    DateTime CriadoEmUtc,
    string EnviadoPor);

public sealed record VersaoContratoFranquiaResumo(
    Guid Id,
    int NumeroVersao,
    DateOnly DataInicio,
    DateOnly? DataFim,
    decimal PercentualRoyalties,
    decimal MensalidadeFixa,
    decimal? TaxaAdesao,
    int? DiaVencimento,
    StatusVersaoContratoFranquia Status,
    string? MotivoAlteracao,
    string? Observacoes,
    DateTime CriadoEmUtc,
    string CriadoPor,
    IReadOnlyList<DocumentoContratoFranquiaResumo> Documentos);

public sealed record ContratoFranquiaPainel(
    ContextoContratoFranquia Contexto,
    Guid? ContratoId,
    string? Numero,
    StatusContratoFranquia? Status,
    IReadOnlyList<VersaoContratoFranquiaResumo> Versoes)
{
    public VersaoContratoFranquiaResumo? VersaoAtual => Versoes
        .FirstOrDefault(versao => versao.Status == StatusVersaoContratoFranquia.Vigente)
        ?? Versoes.FirstOrDefault(versao => versao.Status == StatusVersaoContratoFranquia.Rascunho)
        ?? Versoes.FirstOrDefault();
}

public sealed record TermosContratoFranquiaSolicitacao(
    string? NumeroContrato,
    DateOnly DataInicio,
    DateOnly? DataFim,
    decimal PercentualRoyalties,
    decimal MensalidadeFixa,
    decimal? TaxaAdesao,
    int? DiaVencimento,
    string? MotivoAlteracao,
    string? Observacoes);

public sealed record EnviarDocumentoContratoFranquiaSolicitacao(
    TipoDocumentoContratoFranquia TipoDocumento,
    string NomeOriginal,
    string ContentType,
    Stream Conteudo);

public sealed record DocumentoContratoFranquiaAcesso(
    string ChaveArmazenamento,
    string NomeOriginal,
    string ContentType);

public sealed record DocumentoContratoFranquiaLeitura(
    Stream Conteudo,
    string NomeOriginal,
    string ContentType);

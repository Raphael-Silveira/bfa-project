using BFA.Domain.Acessos;
using BFA.Domain.Aulas;
using BFA.Domain.Matriculas;

namespace BFA.Application.AlunoArea;

public sealed record PerfilAlunoDto(
    Guid AlunoId,
    string NomeCompleto,
    string? Cpf,
    string? Telefone,
    string? Email,
    DateOnly DataNascimento,
    bool Ativo,
    string? Apelido = null,
    string? Cep = null,
    int? EstadoCodigoIbge = null,
    string? EstadoSigla = null,
    string? EstadoNome = null,
    int? MunicipioCodigoIbge = null,
    string? MunicipioNome = null,
    string? Bairro = null,
    string? Logradouro = null,
    string? Numero = null,
    string? Complemento = null,
    string? FotoPerfilChave = null,
    string? FotoPerfilContentType = null);

public enum ResultadoAtualizacaoPerfilAluno
{
    Sucesso,
    NaoEncontrado,
    EmailInvalido,
    TelefoneInvalido,
    ApelidoInvalido,
    CepInvalido,
    EnderecoInvalido,
    FotoInvalida
}

public sealed record MatriculaAlunoDto(
    Guid MatriculaId,
    string PlanoNome,
    int FrequenciaSemanal,
    string Status,
    DateOnly DataInicio,
    DateOnly DataFimPrevista,
    DateOnly? DataFimReal,
    decimal ValorMensal,
    IReadOnlyList<HorarioMatriculaDto> Horarios);

public sealed record MatriculaAlunoConsulta(
    Guid MatriculaId,
    string PlanoNome,
    int FrequenciaSemanal,
    StatusMatricula Status,
    DateOnly DataInicio,
    DateOnly DataFimPrevista,
    DateOnly? DataFimReal,
    decimal ValorMensal,
    IReadOnlyList<HorarioMatriculaDto> Horarios);

public sealed record HorarioMatriculaDto(
    string DiaSemana,
    string HoraInicio,
    string HoraFim,
    string TurmaNome);

public sealed record AulaAlunoDto(
    Guid AulaId,
    DateOnly Data,
    string HoraInicio,
    string HoraFim,
    string TurmaNome,
    string Status,
    string? MotivoCancelamento,
    bool ConfirmacaoAtiva,
    bool PodeAlterarConfirmacao);

public sealed record PresencaAlunoDto(
    DateOnly Data,
    string TurmaNome,
    string HoraInicio,
    string HoraFim,
    string Status,
    string? Observacoes);

public sealed record FrequenciaResumoDto(
    int TotalAulas,
    int Presentes,
    int Ausentes,
    int Justificados,
    decimal PercentualFrequencia,
    IReadOnlyList<PresencaAlunoDto> Presencas);

public sealed record CobrancaAlunoDto(
    Guid CobrancaId,
    string Descricao,
    string Tipo,
    string Valor,
    string ValorPago,
    string SaldoDevedor,
    DateOnly DataVencimento,
    string Status,
    int DiasAtraso);

public sealed record PagamentoAlunoDto(
    DateOnly DataPagamento,
    string Tipo,
    string Valor,
    string FormaPagamento);

public sealed record FinanceiroResumoDto(
    string TotalPendente,
    string TotalPago,
    IReadOnlyList<CobrancaAlunoDto> Cobrancas,
    IReadOnlyList<PagamentoAlunoDto> Pagamentos);

public sealed record DashboardAlunoDto(
    Guid OrganizacaoId,
    PerfilAlunoDto Perfil,
    string NomeUnidade,
    string? ProximaAula,
    string PercentualFrequencia,
    string TotalPendente,
    int TotalAulas,
    MatriculaAlunoDto? MatriculaAtiva,
    Guid? ProximaAulaId = null,
    bool ProximaAulaConfirmada = false,
    bool PodeAlterarConfirmacao = false);

public sealed record AulaConfirmacaoConsulta(
    Guid AulaId,
    Guid OrganizacaoId,
    Guid UnidadeId,
    DateOnly Data,
    TimeOnly HoraInicio,
    StatusAula Status,
    bool ConfirmacaoAtiva,
    bool ConfirmacaoExiste);

public enum ResultadoConfirmacaoAula
{
    Sucesso,
    NaoElegivel,
    JanelaEncerrada,
    NaoEncontrada
}

public interface IAlunoAreaServico
{
    Task<DashboardAlunoDto?> ObterDashboardAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<PerfilAlunoDto?> ObterPerfilAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<ResultadoAtualizacaoPerfilAluno> AtualizarPerfilAsync(
        Guid usuarioId,
        Guid unidadeId,
        string? telefone,
        string? email,
        CancellationToken cancellationToken);

    Task<ResultadoAtualizacaoPerfilAluno> AtualizarPerfilCompletoAsync(
        Guid usuarioId,
        Guid unidadeId,
        string? apelido,
        string? telefone,
        string? email,
        string? cep,
        int? estadoCodigoIbge,
        int? municipioCodigoIbge,
        string? bairro,
        string? logradouro,
        string? numero,
        string? complemento,
        FotoPerfilUpload? foto,
        CancellationToken cancellationToken);

    Task<(Stream Conteudo, string ContentType)?> AbrirFotoPerfilAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MatriculaAlunoDto>> ObterMatriculasAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AulaAlunoDto>> ObterAgendaAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<FrequenciaResumoDto?> ObterFrequenciaAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly dataInicio,
        DateOnly dataFim,
        CancellationToken cancellationToken);

    Task<FinanceiroResumoDto?> ObterFinanceiroAsync(
        Guid usuarioId,
        Guid unidadeId,
        DateOnly? dataInicio,
        DateOnly? dataFim,
        CancellationToken cancellationToken);
}

public interface IConfirmacaoAulaAlunoServico
{
    Task<ResultadoConfirmacaoAula> ConfirmarAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken);

    Task<ResultadoConfirmacaoAula> CancelarAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid aulaId,
        CancellationToken cancellationToken);
}

using BFA.Domain.Professores;

namespace BFA.Application.Unidades.Professores;

public sealed record ProfessorUnidadeResumo(
    Guid ProfessorId,
    string NomeCompleto,
    string? Cpf,
    string? Telefone,
    string? Email,
    bool VinculoAtivo,
    ModalidadeRemuneracaoProfessor? Modalidade,
    decimal? Valor,
    Guid? UsuarioId,
    string? NomeUsuario,
    bool AcessoProfessorAtivo);

public enum FiltroProfessoresUnidade
{
    Ativos,
    Encerrados,
    Todos
}

public sealed record ProfessorUnidadeGerenciamentoResumo(
    Guid ProfessorId,
    string NomeCompleto,
    string? Cpf,
    string? Telefone,
    string? Email,
    bool ProfessorAtivo,
    bool VinculoAtivo,
    ModalidadeRemuneracaoProfessor? ModalidadeAtual,
    decimal? ValorAtual,
    DateOnly? VigenciaInicioAtual);

public sealed record ProfessorRemuneracaoResumo(
    Guid RemuneracaoId,
    ModalidadeRemuneracaoProfessor Modalidade,
    decimal Valor,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string? Observacao);

public sealed record ProfessorRemuneracaoGerenciamentoResumo(
    Guid ProfessorId,
    string NomeCompleto,
    bool VinculoAtivo,
    ProfessorRemuneracaoResumo? RemuneracaoAtual,
    IReadOnlyList<ProfessorRemuneracaoResumo> Historico);

public enum EstadoVinculoProfessorExistente
{
    SemVinculo,
    Ativo,
    Inativo
}

public sealed record ProfessorExistenteResumo(
    Guid ProfessorId,
    string NomeCompleto,
    string? Cpf,
    string? Telefone,
    string? Email,
    bool ProfessorAtivo,
    EstadoVinculoProfessorExistente EstadoVinculo,
    DateOnly? UltimaVigenciaFim);

public enum EstadoPersistenciaProfessorUnidade
{
    Sucesso,
    CpfDuplicado,
    ProfessorNaoEncontrado,
    ProfessorInativo,
    JaVinculado,
    VinculoNaoEncontrado,
    VinculoJaEncerrado,
    DataEncerramentoInvalida,
    VigenciaInicioInvalida,
    RemuneracaoNaoEncontrada,
    Falha
}

public interface IProfessoresUnidadeRepositorio
{
    Task<IReadOnlyList<ProfessorUnidadeResumo>> ListarAsync(
        Guid organizacaoId,
        Guid unidadeId,
        FiltroProfessoresUnidade filtro,
        CancellationToken cancellationToken);

    Task<ProfessorUnidadeGerenciamentoResumo?> ObterGerenciamentoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken);

    Task<ProfessorRemuneracaoGerenciamentoResumo?> ObterRemuneracaoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken);

    Task<bool> ExisteCpfAsync(
        Guid organizacaoId, string cpf, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProfessorExistenteResumo>> BuscarExistentesAsync(
        Guid organizacaoId,
        Guid unidadeId,
        string termo,
        CancellationToken cancellationToken);

    Task<ProfessorExistenteResumo?> ObterExistenteAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaProfessorUnidade> CriarAsync(
        Professor professor,
        ProfessorUnidade vinculo,
        ProfessorRemuneracao remuneracao,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaProfessorUnidade> VincularExistenteAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        ModalidadeRemuneracaoProfessor modalidade,
        decimal valor,
        DateOnly vigenciaInicio,
        string? observacao,
        Guid usuarioId,
        DateTime criadoEmUtc,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaProfessorUnidade> AtualizarCadastroAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        string nomeCompleto,
        string? cpf,
        string? telefone,
        string? email,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaProfessorUnidade> EncerrarVinculoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        DateOnly dataEncerramento,
        DateTime atualizadoEmUtc,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaProfessorUnidade> AlterarRemuneracaoAsync(
        Guid organizacaoId,
        Guid unidadeId,
        Guid professorId,
        ModalidadeRemuneracaoProfessor modalidade,
        decimal valor,
        DateOnly vigenciaInicio,
        string? observacao,
        Guid usuarioId,
        DateTime criadoEmUtc,
        CancellationToken cancellationToken);
}

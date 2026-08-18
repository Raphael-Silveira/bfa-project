namespace BFA.Application.Franqueadora.AcessosUnidade;

public enum EstadoGerenciamentoAcessoUnidade
{
    Sucesso = 1,
    SemAcesso = 2,
    SelecaoOrganizacaoNecessaria = 3,
    UnidadeNaoEncontrada = 4,
    UsuarioNaoEncontrado = 5,
    VinculoJaAtivo = 6,
    VinculoNaoEncontrado = 7
}

public sealed record UnidadeAcessosResumo(
    Guid Id,
    string Nome,
    bool Ativa);

public sealed record AdministradorUnidadeResumo(
    Guid VinculoId,
    Guid UsuarioId,
    string Email,
    bool Ativo,
    DateTime CriadoEmUtc);

public sealed record AcessosUnidadeDetalhe(
    UnidadeAcessosResumo Unidade,
    IReadOnlyList<AdministradorUnidadeResumo> Administradores);

public sealed record AdicionarAdministradorUnidadeSolicitacao(string Email);

public sealed record ResultadoAcessosUnidade<T>(
    EstadoGerenciamentoAcessoUnidade Estado,
    T? Valor)
    where T : class;

public sealed record ResultadoOperacaoAcessoUnidade(
    EstadoGerenciamentoAcessoUnidade Estado);

public enum ResultadoPersistenciaAcessoUnidade
{
    Sucesso = 1,
    VinculoDuplicado = 2
}

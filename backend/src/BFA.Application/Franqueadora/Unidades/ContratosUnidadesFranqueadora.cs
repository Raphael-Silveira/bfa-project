namespace BFA.Application.Franqueadora.Unidades;

public enum EstadoGerenciamentoUnidade
{
    Sucesso = 1,
    SemAcesso = 2,
    SelecaoOrganizacaoNecessaria = 3,
    NaoEncontrada = 4,
    SlugDuplicado = 5
}

public sealed record UnidadeResumo(
    Guid Id,
    string Nome,
    string Slug,
    bool Ativa,
    DateTime CriadoEmUtc);

public sealed record UnidadeDetalhe(
    Guid Id,
    string Nome,
    string Slug,
    bool Ativa,
    DateTime CriadoEmUtc);

public sealed record CriarUnidadeSolicitacao(string Nome, string Slug);

public sealed record AtualizarUnidadeSolicitacao(string Nome, string Slug);

public sealed record ResultadoUnidadesFranqueadora<T>(
    EstadoGerenciamentoUnidade Estado,
    T? Valor)
    where T : class;

public sealed record ResultadoOperacaoUnidade(
    EstadoGerenciamentoUnidade Estado);

public enum ResultadoPersistenciaUnidade
{
    Sucesso = 1,
    SlugDuplicado = 2
}

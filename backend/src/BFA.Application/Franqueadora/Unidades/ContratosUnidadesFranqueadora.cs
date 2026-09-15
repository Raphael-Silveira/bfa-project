namespace BFA.Application.Franqueadora.Unidades;

public enum EstadoGerenciamentoUnidade
{
    Sucesso = 1,
    SemAcesso = 2,
    SelecaoOrganizacaoNecessaria = 3,
    NaoEncontrada = 4,
    SlugDuplicado = 5
}

public enum TipoUnidadeFiltro
{
    Todos = 1,
    Franqueadas = 2,
    Rede = 3
}

public sealed record FiltroUnidadesFranqueadora(
    string? Busca,
    TipoUnidadeFiltro Tipo);

public sealed record UnidadeResumo(
    Guid Id,
    string Nome,
    string Slug,
    bool Ativa,
    DateTime CriadoEmUtc)
{
    public Guid? FranqueadoIdAtivo { get; init; }

    public bool PossuiFranqueadoAtivo => FranqueadoIdAtivo.HasValue;
}

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

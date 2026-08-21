using BFA.Domain.Franqueados;
using BFA.Domain.Contratos;

namespace BFA.Application.Franqueadora.Franqueados;

public enum EstadoGerenciamentoFranqueado
{
    Sucesso = 1,
    SemAcesso = 2,
    SelecaoOrganizacaoNecessaria = 3,
    NaoEncontrado = 4,
    DadosInvalidos = 5,
    DocumentoDuplicado = 6,
    EstadoLocalidadeInvalido = 7,
    MunicipioLocalidadeInvalido = 8,
    UnidadeInvalida = 9,
    UnidadeOcupada = 10,
    UsuarioPrincipalAusente = 11,
    VinculoNaoEncontrado = 12,
    FalhaPersistencia = 13
}

public enum EstadoPersistenciaFranqueado
{
    Sucesso = 1,
    DocumentoDuplicado = 2,
    UnidadeOcupada = 3,
    Falha = 4
}

public sealed record ResultadoFranqueado<T>(
    EstadoGerenciamentoFranqueado Estado,
    T? Valor,
    string? Mensagem = null)
    where T : class;

public sealed record ResultadoOperacaoFranqueado(
    EstadoGerenciamentoFranqueado Estado,
    string? Mensagem = null);

public sealed record FranqueadoResumo(
    Guid Id,
    string NomeRazaoSocial,
    string? NomeFantasia,
    string Documento,
    TipoPessoaFranqueado TipoPessoa,
    int QuantidadeUnidadesAtivas,
    bool Ativo);

public sealed record FranqueadoDados(
    Guid Id,
    Guid OrganizacaoId,
    TipoPessoaFranqueado TipoPessoa,
    string NomeRazaoSocial,
    string? NomeFantasia,
    string Documento,
    string? Telefone,
    string Email,
    string? EmailFinanceiro,
    string? ResponsavelLegal,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Estado,
    string? Cep,
    string? Observacoes,
    bool Ativo);

public sealed record FranqueadoUsuarioResumo(
    Guid UsuarioId,
    string Nome,
    string Email,
    bool Principal,
    bool Ativo);

public sealed record FranqueadoUnidadeResumo(
    Guid UnidadeId,
    string Nome,
    bool VinculoAtivo,
    bool UnidadeAtiva,
    DateTime CriadoEmUtc,
    StatusContratoFranquia? StatusContrato);

public sealed record UnidadeDisponivelFranqueadoResumo(Guid Id, string Nome);

public sealed record FranqueadoDetalhe(
    FranqueadoDados Dados,
    int? EstadoCodigoIbge,
    int? MunicipioCodigoIbge,
    IReadOnlyList<FranqueadoUsuarioResumo> Usuarios,
    IReadOnlyList<FranqueadoUnidadeResumo> Unidades,
    IReadOnlyList<UnidadeDisponivelFranqueadoResumo> UnidadesDisponiveis);

public sealed record AtualizarFranqueadoSolicitacao(
    TipoPessoaFranqueado TipoPessoa,
    string NomeRazaoSocial,
    string? NomeFantasia,
    string Documento,
    string? Telefone,
    string Email,
    string? EmailFinanceiro,
    string? ResponsavelLegal,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    int? EstadoCodigoIbge,
    int? MunicipioCodigoIbge,
    string? Cep,
    string? Observacoes);

public sealed record VincularUnidadeFranqueadoSolicitacao(Guid UnidadeId);

public sealed record FranqueadoVinculoUsuarioResumo(Guid Id, string NomeRazaoSocial);

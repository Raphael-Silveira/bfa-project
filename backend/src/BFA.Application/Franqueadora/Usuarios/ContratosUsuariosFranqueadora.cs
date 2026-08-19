using BFA.Domain.Acessos;
using BFA.Domain.Franqueados;
using BFA.Domain.Usuarios;

namespace BFA.Application.Franqueadora.Usuarios;

public enum TipoCadastroUsuario
{
    AdministradorRede = 1,
    Franqueado = 2
}

public enum EstadoGerenciamentoUsuario
{
    Sucesso = 1,
    SemAcesso = 2,
    SelecaoOrganizacaoNecessaria = 3,
    EmailDuplicado = 4,
    DadosInvalidos = 5,
    UnidadesInvalidas = 6,
    UnidadeComFranqueadoAtivo = 7,
    DocumentoDuplicado = 8,
    FalhaPersistencia = 9,
    UsuarioNaoEncontrado = 10,
    UsuarioComMultiplasOrganizacoes = 11
}

public sealed record UsuarioFranqueadoraResumo(
    Guid Id,
    string Nome,
    string Email,
    IReadOnlyList<string> Funcoes,
    IReadOnlyList<string> Unidades,
    bool Ativo);

public sealed record UnidadeSelecaoUsuarioResumo(
    Guid Id,
    string Nome);

public sealed record FranqueadoCadastroDados(
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
    IReadOnlyCollection<Guid> UnidadesIds);

public sealed record CriarUsuarioFranqueadoraSolicitacao(
    TipoCadastroUsuario TipoCadastro,
    string NomeCompleto,
    string Email,
    string Telefone,
    FranqueadoCadastroDados? Franqueado);

public sealed record ResultadoUsuariosFranqueadora<T>(
    EstadoGerenciamentoUsuario Estado,
    T? Valor,
    string? Mensagem = null)
    where T : class;

public sealed record UsuarioFranqueadoraEdicao(
    Guid UsuarioId,
    string NomeCompleto,
    string Email,
    string? Telefone);

public sealed record UsuarioFranqueadoraEdicaoContexto(
    Guid UsuarioId,
    string? NomeCompleto,
    string Email,
    string? Telefone,
    IReadOnlyList<Guid> OrganizacoesAtivasIds);

public sealed record EditarUsuarioFranqueadoraSolicitacao(
    Guid UsuarioId,
    string NomeCompleto,
    string Email,
    string? Telefone);

public sealed record AtualizarUsuarioFranqueadoraDados(
    Guid UsuarioId,
    Guid OrganizacaoId,
    string NomeCompleto,
    string Email,
    string? Telefone,
    DateTime AtualizadoEmUtc);

public sealed record ResultadoAtualizacaoUsuarioFranqueadora(
    EstadoGerenciamentoUsuario Estado,
    string? Mensagem = null);

public sealed record UsuarioFranqueadoraCriado(
    Guid UsuarioId,
    string NomeCompleto,
    string Email,
    TipoCadastroUsuario TipoCadastro,
    string TokenDefinicaoSenha);

public sealed record ResultadoCriacaoUsuarioFranqueadora(
    EstadoGerenciamentoUsuario Estado,
    UsuarioFranqueadoraCriado? Usuario = null,
    string? Mensagem = null);

public sealed record CadastroUsuarioFranqueadora(
    Guid UsuarioId,
    string Email,
    PerfilUsuario PerfilUsuario,
    Franqueado? Franqueado,
    FranqueadoUsuario? FranqueadoUsuario,
    IReadOnlyList<FranqueadoUnidade> FranqueadosUnidades,
    IReadOnlyList<VinculoAcesso> VinculosAcesso);

public enum EstadoPersistenciaCadastroUsuario
{
    Sucesso = 1,
    EmailDuplicado = 2,
    DocumentoDuplicado = 3,
    UnidadeComFranqueadoAtivo = 4,
    DadosInvalidos = 5,
    Falha = 6
}

public sealed record ResultadoPersistenciaCadastroUsuario(
    EstadoPersistenciaCadastroUsuario Estado,
    string? TokenDefinicaoSenha = null);

public enum EstadoPersistenciaEdicaoUsuario
{
    Sucesso = 1,
    UsuarioNaoEncontrado = 2,
    UsuarioComMultiplasOrganizacoes = 3,
    EmailDuplicado = 4,
    DadosInvalidos = 5,
    Falha = 6
}

public sealed record ResultadoPersistenciaEdicaoUsuario(
    EstadoPersistenciaEdicaoUsuario Estado);

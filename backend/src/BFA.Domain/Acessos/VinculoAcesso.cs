namespace BFA.Domain.Acessos;

public sealed class VinculoAcesso
{
    private VinculoAcesso()
    {
    }

    public VinculoAcesso(
        Guid id,
        Guid usuarioId,
        Guid organizacaoId,
        Guid? unidadeId,
        PerfilAcesso perfil,
        DateTime criadoEmUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador do vinculo deve ser informado.", nameof(id));
        }

        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("O identificador do usuario deve ser informado.", nameof(usuarioId));
        }

        if (organizacaoId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da organizacao deve ser informado.",
                nameof(organizacaoId));
        }

        if (!Enum.IsDefined(perfil))
        {
            throw new ArgumentOutOfRangeException(nameof(perfil), perfil, "O perfil de acesso e invalido.");
        }

        if (perfil == PerfilAcesso.AdministradorRede && unidadeId.HasValue)
        {
            throw new ArgumentException(
                "Administrador de rede nao pode estar vinculado a uma unidade.",
                nameof(unidadeId));
        }

        if (perfil != PerfilAcesso.AdministradorRede
            && (!unidadeId.HasValue || unidadeId.Value == Guid.Empty))
        {
            throw new ArgumentException(
                "O perfil informado deve estar vinculado a uma unidade.",
                nameof(unidadeId));
        }

        if (criadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        }

        Id = id;
        UsuarioId = usuarioId;
        OrganizacaoId = organizacaoId;
        UnidadeId = unidadeId;
        Perfil = perfil;
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public Guid OrganizacaoId { get; private set; }

    public Guid? UnidadeId { get; private set; }

    public PerfilAcesso Perfil { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }

    public void Ativar(DateTime atualizadoEmUtc)
    {
        ValidarDataAtualizacao(atualizadoEmUtc);
        Ativo = true;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    public void Desativar(DateTime atualizadoEmUtc)
    {
        ValidarDataAtualizacao(atualizadoEmUtc);
        Ativo = false;
        AtualizadoEmUtc = atualizadoEmUtc;
    }

    private static void ValidarDataAtualizacao(DateTime atualizadoEmUtc)
    {
        if (atualizadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "A data de atualizacao deve estar em UTC.",
                nameof(atualizadoEmUtc));
        }
    }
}

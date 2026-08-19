namespace BFA.Domain.Franqueados;

public sealed class FranqueadoUsuario
{
    private FranqueadoUsuario()
    {
    }

    public FranqueadoUsuario(
        Guid id,
        Guid franqueadoId,
        Guid usuarioId,
        bool principal,
        DateTime criadoEmUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador do vinculo deve ser informado.", nameof(id));
        }

        if (franqueadoId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do franqueado deve ser informado.",
                nameof(franqueadoId));
        }

        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("O identificador do usuario deve ser informado.", nameof(usuarioId));
        }

        if (criadoEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criacao deve estar em UTC.", nameof(criadoEmUtc));
        }

        Id = id;
        FranqueadoId = franqueadoId;
        UsuarioId = usuarioId;
        Principal = principal;
        Ativo = true;
        CriadoEmUtc = criadoEmUtc;
        AtualizadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; private set; }

    public Guid FranqueadoId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public bool Principal { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime CriadoEmUtc { get; private set; }

    public DateTime AtualizadoEmUtc { get; private set; }
}

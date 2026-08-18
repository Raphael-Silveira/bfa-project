namespace BFA.Application.Acessos;

public interface IUsuarioAtual
{
    bool Autenticado { get; }

    Guid? UsuarioId { get; }
}

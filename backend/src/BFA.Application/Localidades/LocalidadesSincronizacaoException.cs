namespace BFA.Application.Localidades;

public class LocalidadesSincronizacaoException : Exception
{
    public LocalidadesSincronizacaoException(string message)
        : base(message)
    {
    }

    public LocalidadesSincronizacaoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class IbgeLocalidadesException : LocalidadesSincronizacaoException
{
    public IbgeLocalidadesException(string message)
        : base(message)
    {
    }

    public IbgeLocalidadesException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

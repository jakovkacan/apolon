namespace Apolon.Core.Exceptions;

public class OrmException : Exception
{
    public OrmException(string message) : base(message)
    {
    }

    protected OrmException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
using System;

namespace Apolon.Core.Exceptions;

public class DataAccessException : OrmException
{
    public DataAccessException(string message) : base(message)
    {
    }

    public DataAccessException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

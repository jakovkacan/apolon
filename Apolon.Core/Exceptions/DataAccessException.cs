namespace Apolon.Core.Exceptions;

public class DataAccessException(string message) : OrmException(message);
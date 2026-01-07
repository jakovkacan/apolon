using System;

namespace Apolon.Core.Exceptions;

public class OrmException(string message) : Exception(message);
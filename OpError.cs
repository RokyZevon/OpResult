namespace OpResult;

/// <summary>
/// Built-in lightweight error type implementing IOpError.
/// </summary>
public readonly record struct OpError(string Code, string Message) : IOpError
{
    /// <summary>
    /// Creates a new OpError with the specified code and message.
    /// </summary>
    public static OpError New(string code, string message) => new(code, message);

    /// <summary>
    /// Creates a new OpError with the specified message and empty code.
    /// </summary>
    public static OpError New(string message) => new(string.Empty, message);
}

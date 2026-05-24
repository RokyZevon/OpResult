namespace OpResult;

/// <summary>
/// Represents an OpResult error.
/// </summary>
public sealed record class OpError
{
    internal static OpError Empty { get; } = new(string.Empty);

    private OpError(string message) => Message = message;

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    internal static OpError New(string? message) =>
        string.IsNullOrWhiteSpace(message) ? Empty : new(message);
}

namespace OpResult;

using System.Text;

/// <summary>
/// Represents an OpResult error.
/// </summary>
public sealed record class OpError
{
    internal static OpError Empty { get; } = new(string.Empty);

    private OpError(string? message, OpError? innerError = null)
    {
        Message = message ?? string.Empty;
        InnerError = innerError;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the direct inner error that caused this error, if any.
    /// </summary>
    public OpError? InnerError { get; }

    /// <summary>
    /// Returns a display string for the error chain from outermost to innermost.
    /// </summary>
    /// <returns>A display string for the error chain.</returns>
    public override string ToString()
    {
        var current = this;
        var builder = new StringBuilder();

        while (current is not null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                if (builder.Length > 0)
                {
                    builder.Append(" -> ");
                }

                builder.Append(current.Message);
            }

            current = current.InnerError;
        }

        return builder.Length == 0 ? "<error>" : builder.ToString();
    }

    internal static OpError New(string? message) =>
        string.IsNullOrWhiteSpace(message) ? Empty : new(message);

    internal static OpError New(string? message, OpError? innerError) =>
        innerError is null
            ? New(message)
            : new(string.IsNullOrWhiteSpace(message) ? string.Empty : message, innerError);
}
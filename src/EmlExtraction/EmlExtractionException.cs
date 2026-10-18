namespace EmlExtraction;

/// <summary>
/// Thrown when an unexpected (non-content) exception occurs inside detection or extraction, i.e. a library bug.
/// The original exception is kept unchanged in <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class EmlExtractionException : Exception
{
    private static readonly string[] Stages = ["request", "detect", "parse", "metadata", "body", "attachments"];

    /// <summary>Creates the exception.</summary>
    /// <param name="stage">Failing stage: request, detect, parse, metadata, body or attachments.</param>
    /// <param name="inner">The original exception.</param>
    /// <exception cref="ArgumentException"><paramref name="stage"/> is not a known stage name.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public EmlExtractionException(string stage, Exception inner)
        : base(BuildMessage(stage, inner), inner)
    {
        Stage = stage;
    }

    /// <summary>The stage that failed: request, detect, parse, metadata, body or attachments.</summary>
    public string Stage { get; }

    private static string BuildMessage(string stage, Exception inner)
    {
        ArgumentException.ThrowIfNullOrEmpty(stage);
        ArgumentNullException.ThrowIfNull(inner);
        if (Array.IndexOf(Stages, stage) < 0)
            throw new ArgumentException($"Unknown stage '{stage}'; expected one of: {string.Join(", ", Stages)}.", nameof(stage));

        return $"EML extraction failed in stage '{stage}': {inner.GetType().FullName}.";
    }
}

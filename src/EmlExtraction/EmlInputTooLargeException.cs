namespace EmlExtraction;

/// <summary>Thrown by <c>Extract</c> when the input is longer than the extractor's <c>maxInputBytes</c> limit.</summary>
public sealed class EmlInputTooLargeException : ArgumentException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="inputLength">Length of the rejected input in bytes.</param>
    /// <param name="maxInputBytes">The configured limit in bytes.</param>
    /// <param name="paramName">Name of the offending parameter.</param>
    public EmlInputTooLargeException(long inputLength, long maxInputBytes, string paramName)
        : base($"Input is {inputLength} bytes, which exceeds maxInputBytes {maxInputBytes}.", paramName)
    {
        InputLength = inputLength;
        MaxInputBytes = maxInputBytes;
    }

    /// <summary>Length of the rejected input in bytes.</summary>
    public long InputLength { get; }

    /// <summary>The configured limit in bytes; <see cref="InputLength"/> is greater than this value.</summary>
    public long MaxInputBytes { get; }
}

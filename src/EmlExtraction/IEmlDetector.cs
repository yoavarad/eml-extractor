namespace EmlExtraction;

/// <summary>Decides whether a byte buffer is an RFC 822/5322 mail message.</summary>
public interface IEmlDetector
{
    /// <summary>Returns <see langword="true"/> when <paramref name="data"/> looks like a mail message.</summary>
    /// <param name="data">Raw file bytes; only a bounded prefix is inspected.</param>
    /// <exception cref="EmlExtractionException">An unexpected exception (library bug) occurred during detection; stage <c>detect</c>.</exception>
    bool IsEml(ReadOnlySpan<byte> data);
}

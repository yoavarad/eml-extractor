namespace EmlExtraction;

/// <summary>Extracts metadata, main content and attachments from a mail message.</summary>
public interface IEmlExtractor
{
    /// <summary>Parses <paramref name="eml"/> and returns the extraction result. Parse and decode failures are reported in <see cref="EmlExtractionResult.Warnings"/>.</summary>
    /// <param name="eml">Raw message bytes.</param>
    /// <exception cref="EmlInputTooLargeException">The input is longer than the extractor's <c>maxInputBytes</c>.</exception>
    /// <exception cref="EmlExtractionException">An unexpected exception (library bug) occurred in a stage.</exception>
    EmlExtractionResult Extract(ReadOnlyMemory<byte> eml);

    /// <summary>Primary extraction method. Parses <see cref="EmlExtractionRequest.FileBytes"/>; <see cref="EmlExtractionRequest.FileName"/> never changes the result. Parse and decode failures are reported in <see cref="EmlExtractionResult.Warnings"/>.</summary>
    /// <param name="request">Extraction input.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="EmlInputTooLargeException">The input is longer than the extractor's <c>maxInputBytes</c>.</exception>
    /// <exception cref="EmlExtractionException">An unexpected exception (library bug) occurred in a stage.</exception>
    EmlExtractionResult Extract(EmlExtractionRequest request);
}

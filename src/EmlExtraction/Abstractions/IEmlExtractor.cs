using EmlExtraction.Exceptions;
using EmlExtraction.Models;

namespace EmlExtraction.Abstractions;

/// <summary>Extracts metadata, main content and attachments from a mail message.</summary>
public interface IEmlExtractor
{
    /// <summary>Parses <paramref name="eml"/> and returns the extraction result. Parse and decode failures are reported in <see cref="EmlExtractionResult.Warnings"/>.</summary>
    /// <param name="eml">Raw message bytes.</param>
    /// <exception cref="EmlInputTooLargeException">The input is longer than the extractor's <c>maxInputBytes</c>.</exception>
    /// <exception cref="EmlExtractionException">An unexpected exception (library bug) occurred in a stage.</exception>
    EmlExtractionResult Extract(ReadOnlyMemory<byte> eml);

    /// <summary>Reads <paramref name="eml"/> forward to its end and returns the extraction result, without first copying the whole input (ADR-024). The stream need not be seekable and is never disposed.</summary>
    /// <param name="eml">Message stream, read from its current position.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eml"/> is <see langword="null"/>.</exception>
    /// <exception cref="EmlInputTooLargeException">More than the extractor's <c>maxInputBytes</c> was read.</exception>
    /// <exception cref="EmlExtractionException">An unexpected exception (library bug) occurred in a stage.</exception>
    EmlExtractionResult Extract(Stream eml);

    /// <summary>Primary extraction method. Parses <see cref="EmlExtractionRequest.FileBytes"/> or <see cref="EmlExtractionRequest.InputStream"/>; <see cref="EmlExtractionRequest.FileName"/> never changes the result. Parse and decode failures are reported in <see cref="EmlExtractionResult.Warnings"/>.</summary>
    /// <param name="request">Extraction input.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Both or neither of <see cref="EmlExtractionRequest.FileBytes"/> and <see cref="EmlExtractionRequest.InputStream"/> are set.</exception>
    /// <exception cref="EmlInputTooLargeException">The input is longer than the extractor's <c>maxInputBytes</c>.</exception>
    /// <exception cref="EmlExtractionException">An unexpected exception (library bug) occurred in a stage.</exception>
    EmlExtractionResult Extract(EmlExtractionRequest request);
}

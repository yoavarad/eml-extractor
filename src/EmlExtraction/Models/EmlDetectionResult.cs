namespace EmlExtraction.Models;

/// <summary>Outcome of <see cref="Logics.EmlDetector.Detect"/>.</summary>
/// <param name="IsSupported">True when the input looks like a mail message.</param>
/// <param name="MimeType">"message/rfc822" when supported, otherwise empty.</param>
public sealed record EmlDetectionResult(bool IsSupported, string MimeType);

using System.Runtime.InteropServices;
using MimeKit;

namespace EmlExtraction;

public sealed class EmlExtractor : IEmlExtractor
{
    private static readonly ParserOptions Options = CreateOptions();

    public EmlExtractionResult Extract(ReadOnlyMemory<byte> eml)
    {
        var warnings = new List<string>();
        try
        {
            using var stream = Open(eml);
            var message = MimeMessage.Load(Options, stream);
            return Extract(message, warnings);
        }
        catch (Exception ex) when (ex is FormatException or ParseException or IOException or InvalidOperationException)
        {
            warnings.Add($"parse_failed: {ex.GetType().Name}: {ex.Message}");
            return new EmlExtractionResult(
                new Dictionary<string, string>(),
                string.Empty,
                null,
                null,
                [],
                warnings);
        }
    }

    internal static EmlExtractionResult Extract(MimeMessage message, List<string> warnings)
    {
        var metadata = HeaderMapper.Map(message, out var xfields);
        var body = BodySelector.Select(message.Body);

        string? content = null;
        string? contentType = null;
        if (body is not null)
        {
            try
            {
                content = body.Text.TrimEnd('\r', '\n');
                contentType = body.ContentType.MimeType;
            }
            catch (Exception ex)
            {
                warnings.Add($"body_decode_failed: {ex.Message}");
            }
        }

        var attachments = PartCollector.Collect(message.Body, body, warnings);
        return new EmlExtractionResult(metadata, xfields, content, contentType, attachments, warnings);
    }

    private static ParserOptions CreateOptions()
    {
        var options = ParserOptions.Default.Clone();
        options.AllowUnquotedCommasInAddresses = true;
        options.ParameterComplianceMode = RfcComplianceMode.Loose;
        options.Rfc2047ComplianceMode = RfcComplianceMode.Loose;
        return options;
    }

    private static Stream Open(ReadOnlyMemory<byte> eml)
    {
        if (MemoryMarshal.TryGetArray(eml, out var segment)
            && segment.Array is not null
            && segment.Offset == 0
            && segment.Count == eml.Length)
        {
            return new MemoryStream(segment.Array, writable: false);
        }

        return new MemoryStream(eml.ToArray(), writable: false);
    }
}

namespace EmlExtraction.Tests;

public sealed class InternalCorpusTests
{
    private readonly EmlExtractor _extractor = new();

    [SkippableFact]
    public void ZimbraOutlookTnefUnpacksGifAttachments()
    {
        var bytes = Load(["tnef", "zimbra-tnef.msg"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.DoesNotContain(result.Attachments, a => a.Name.Equals("winmail.dat", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Attachments, a => a.Name.Contains("upload", StringComparison.OrdinalIgnoreCase) && a.Size > 0);
    }

    [SkippableFact]
    public void ExchangeMegaHeadersLandInXFields()
    {
        var bytes = Load(["exchange", "leeholmes-o365-megaheaders.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.True(result.Metadata.Count >= 50, $"headers={result.Metadata.Count}");
        Assert.Contains("X-MS-Exchange-Organization-Network-Message-Id:", result.XFields, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-Microsoft-Antispam-Message-Info:", result.XFields, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public void OutlookShapedCidPasteIsExtracted()
    {
        var bytes = Load(["cid", "chatwoot-outlook-cid.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Equal("text/html", result.MainContentMimeType);
        Assert.Contains("cid:image001.jpg@test", result.MainContent);
        Assert.Contains(result.Attachments, a => a.ContentId == "image001.jpg@test" && a.Size > 0);
    }

    [SkippableFact]
    public void ShiftJisBodyDecodes()
    {
        var bytes = Load(["gaps", "shift-jis.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed") || w.StartsWith("body_decode_failed"));
        Assert.Contains("あいうえお", result.MainContent);
    }

    [SkippableFact]
    public void Iso2022JpSubjectAndBodyDecode()
    {
        var bytes = Load(["gaps", "iso2022jp.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
        Assert.False(string.IsNullOrWhiteSpace(result.Metadata.GetValueOrDefault("subject")));
        Assert.False(string.IsNullOrWhiteSpace(result.MainContent));
        Assert.Contains("テスト", result.MainContent);
    }

    [SkippableFact]
    public void Gb2312BodyDecodesWithoutThrowing()
    {
        var bytes = Load(["gaps", "gb2312.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
        Assert.False(string.IsNullOrWhiteSpace(result.MainContent));
        Assert.DoesNotContain('\uFFFD', result.MainContent);
    }

    [SkippableFact]
    public void DsnReportKeepsHumanBodyAndDeliveryStatusPart()
    {
        var bytes = Load(["gaps", "dsn-report.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
        Assert.False(string.IsNullOrWhiteSpace(result.MainContent));
        Assert.Contains(result.Attachments, a =>
            a.Name.Contains("delivery-status", StringComparison.OrdinalIgnoreCase)
            || a.Name.EndsWith(".delivery-status", StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void SmimeSignedYieldsBodyAndJpegAttachment()
    {
        var bytes = Load(["gaps", "smime-signed.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
        Assert.Contains("signed by Thunderbird", result.MainContent);
        Assert.Contains(result.Attachments, a => a.Name.Contains("signed.jpg", StringComparison.OrdinalIgnoreCase) && a.Size > 100);
        Assert.Contains(result.Attachments, a =>
            a.Name.Contains("p7s", StringComparison.OrdinalIgnoreCase)
            || a.Name.Contains("pkcs7-signature", StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void PoiTnefUnpacksRealOleDocAndPdf()
    {
        var bytes = Load(["spec", "tnef", "wrap-poi-quick-winmail.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.DoesNotContain(result.Attachments, a => a.Name.Equals("winmail.dat", StringComparison.OrdinalIgnoreCase));
        var doc = Assert.Single(result.Attachments, a => a.Name.Equals("quick.doc", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(doc.Data, 0xD0, 0xCF, 0x11, 0xE0));
        Assert.True(doc.Size > 10_000);
        var pdf = Assert.Single(result.Attachments, a => a.Name.Equals("quick.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(pdf.Data, "%PDF-"u8));
        Assert.True(pdf.Size > 10_000);
    }

    [SkippableFact]
    public void PoiTnefUnpacksRealPdfAndPng()
    {
        var bytes = Load(["spec", "tnef", "wrap-poi-bug52400-with-attachments.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        var pdf = Assert.Single(result.Attachments, a => a.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(pdf.Data, "%PDF-"u8));
        Assert.True(pdf.Size > 100_000);
        var png = Assert.Single(result.Attachments, a => a.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(png.Data, 0x89, 0x50, 0x4E, 0x47));
        Assert.True(png.Size > 100_000);
    }

    [SkippableFact]
    public void SwiftMailerHasRealOfficeAndOpaqueNestedEml()
    {
        var bytes = Load(["spec", "php-mails", "issue274.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Equal("text/html", result.MainContentMimeType);
        var docx = Assert.Single(result.Attachments, a => a.Name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(docx.Data, 0x50, 0x4B, 0x03, 0x04));
        Assert.True(docx.Size > 8_000);
        var pdf = Assert.Single(result.Attachments, a => a.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(pdf.Data, "%PDF-"u8));
        var nested = Assert.Single(result.Attachments, a => a.Name.EndsWith(".eml", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Attachments, a => a.Name.Contains("inner", StringComparison.OrdinalIgnoreCase));
        var inner = _extractor.Extract(nested.Data);
        Assert.True(inner.Attachments.Count >= 1);
        Assert.Equal(5, result.Attachments.Count);
    }

    [SkippableFact]
    public void ProtonForwardHasDocxXlsxPdfMagic()
    {
        var bytes = Load(["spec", "office", "proton-forwarding-docx.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Contains(result.Attachments, a => a.Name.Equals("test.docx", StringComparison.OrdinalIgnoreCase) && HasMagic(a.Data, 0x50, 0x4B, 0x03, 0x04));
        Assert.Contains(result.Attachments, a => a.Name.Equals("test.xlsx", StringComparison.OrdinalIgnoreCase) && HasMagic(a.Data, 0x50, 0x4B, 0x03, 0x04));
        Assert.Contains(result.Attachments, a => a.Name.Equals("test.pdf", StringComparison.OrdinalIgnoreCase) && HasMagic(a.Data, "%PDF-"u8));
    }

    [SkippableFact]
    public void GmailHebrewIso88598SubjectDecodes()
    {
        var bytes = Load(["spec", "php-mails", "issue149"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        var subject = result.Metadata.GetValueOrDefault("subject") ?? "";
        Assert.Contains("מענה", subject);
        Assert.Contains("Invoice", subject);
    }

    [SkippableFact]
    public void BellcoreHebrewIso88598BodyDecodes()
    {
        var bytes = Load(["spec", "unicode", "jwz-hebrew-01.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Contains("תירבע", result.Metadata.GetValueOrDefault("subject") ?? "");
        Assert.Contains("תירבע", result.MainContent);
    }

    [SkippableFact]
    public void NestedRfc822ForwardStaysOpaque()
    {
        var bytes = Load(["spec", "php-mails", "issue158a"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        var nested = Assert.Single(result.Attachments);
        Assert.EndsWith(".eml", nested.Name, StringComparison.OrdinalIgnoreCase);
        Assert.True(nested.Size > 100);
        Assert.StartsWith("Return-Path:", System.Text.Encoding.ASCII.GetString(nested.Data.AsSpan(0, Math.Min(12, nested.Data.Length))));
    }

    [SkippableFact]
    public void OutlookPasteImage001At01DIsInlinePng()
    {
        var bytes = Load(["spec", "cid", "outlook-paste-image001-01D.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Equal("text/html", result.MainContentMimeType);
        Assert.Contains("cid:image001.png@01D8693E.BE86BE20", result.MainContent);
        var img = Assert.Single(result.Attachments, a => a.Name.Equals("image001.png", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("image001.png@01D8693E.BE86BE20", img.ContentId);
        Assert.Equal("inline", img.Disposition);
        Assert.True(HasMagic(img.Data, 0x89, 0x50, 0x4E, 0x47));
        Assert.True(img.Size > 100);
        Assert.Contains(result.Attachments, a =>
            a.Name.Equals("duckduckgo_small.png", StringComparison.OrdinalIgnoreCase)
            && a.Disposition == "attachment"
            && string.IsNullOrEmpty(a.ContentId));
    }

    [SkippableFact]
    public void HashCidPngPasteKeepsCidInHtml()
    {
        var bytes = Load(["spec", "php-mails", "m0129"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Contains("cid:c024cb5b0c6eaf060a837c53db9af39b5b564d3956b8d4223fbf308b11b4e79e", result.MainContent);
        var img = Assert.Single(result.Attachments);
        Assert.True(HasMagic(img.Data, 0x89, 0x50, 0x4E, 0x47));
        Assert.Equal("inline", img.Disposition);
        Assert.True(img.Size > 1_000);
    }

    private static byte[] Load(string[] parts)
    {
        var path = FindInternal(parts);
        Skip.If(path is null, $"corpus/internal sample missing: {Path.Combine(parts)}");
        return File.ReadAllBytes(path!);
    }

    private static string? FindInternal(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName, "corpus", "internal" }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    private static bool HasMagic(byte[] data, params byte[] magic) =>
        data.Length >= magic.Length && data.AsSpan(0, magic.Length).SequenceEqual(magic);

    private static bool HasMagic(byte[] data, ReadOnlySpan<byte> magic) =>
        data.Length >= magic.Length && data.AsSpan(0, magic.Length).SequenceEqual(magic);
}

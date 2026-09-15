namespace EmlExtraction.Tests;

public sealed class InternalCorpusTests
{
    private readonly EmlExtractor _extractor = new();

    [Fact]
    public void ZimbraOutlookTnefUnpacksGifAttachments()
    {
        if (!TryLoad(["tnef", "zimbra-tnef.msg"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.DoesNotContain(result.Attachments, a => a.Name.Equals("winmail.dat", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Attachments, a => a.Name.Contains("upload", StringComparison.OrdinalIgnoreCase) && a.Size > 0);
    }

    [Fact]
    public void ExchangeMegaHeadersLandInXFields()
    {
        if (!TryLoad(["exchange", "leeholmes-o365-megaheaders.eml"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.True(result.Metadata.Count >= 50, $"headers={result.Metadata.Count}");
        Assert.Contains("X-MS-Exchange-Organization-Network-Message-Id:", result.XFields, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-Microsoft-Antispam-Message-Info:", result.XFields, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OutlookShapedCidPasteIsExtracted()
    {
        if (!TryLoad(["cid", "chatwoot-outlook-cid.eml"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.Equal("text/html", result.MainContentMimeType);
        Assert.Contains("cid:image001.jpg@test", result.MainContent);
        Assert.Contains(result.Attachments, a => a.ContentId == "image001.jpg@test" && a.Size > 0);
    }

    [Fact]
    public void ShiftJisBodyDecodes()
    {
        if (!TryLoad(["gaps", "shift-jis.eml"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed") || w.StartsWith("body_decode_failed"));
        Assert.Contains("あいうえお", result.MainContent);
    }

    [Fact]
    public void Iso2022JpSubjectAndBodyDecode()
    {
        if (!TryLoad(["gaps", "iso2022jp.eml"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
        Assert.False(string.IsNullOrWhiteSpace(result.Metadata.GetValueOrDefault("subject")));
        Assert.False(string.IsNullOrWhiteSpace(result.MainContent));
        Assert.Contains("テスト", result.MainContent);
    }

    [Fact]
    public void Gb2312BodyDecodesWithoutThrowing()
    {
        if (!TryLoad(["gaps", "gb2312.eml"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
        Assert.False(string.IsNullOrWhiteSpace(result.MainContent));
        Assert.DoesNotContain('\uFFFD', result.MainContent);
    }

    [Fact]
    public void DsnReportKeepsHumanBodyAndDeliveryStatusPart()
    {
        if (!TryLoad(["gaps", "dsn-report.eml"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
        Assert.False(string.IsNullOrWhiteSpace(result.MainContent));
        Assert.Contains(result.Attachments, a =>
            a.Name.Contains("delivery-status", StringComparison.OrdinalIgnoreCase)
            || a.Name.EndsWith(".delivery-status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SmimeSignedYieldsBodyAndJpegAttachment()
    {
        if (!TryLoad(["gaps", "smime-signed.eml"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
        Assert.Contains("signed by Thunderbird", result.MainContent);
        Assert.Contains(result.Attachments, a => a.Name.Contains("signed.jpg", StringComparison.OrdinalIgnoreCase) && a.Size > 100);
        Assert.Contains(result.Attachments, a =>
            a.Name.Contains("p7s", StringComparison.OrdinalIgnoreCase)
            || a.Name.Contains("pkcs7-signature", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PoiTnefUnpacksRealOleDocAndPdf()
    {
        if (!TryLoad(["spec", "tnef", "wrap-poi-quick-winmail.eml"], out var bytes))
            return;

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

    [Fact]
    public void PoiTnefUnpacksRealPdfAndPng()
    {
        if (!TryLoad(["spec", "tnef", "wrap-poi-bug52400-with-attachments.eml"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        var pdf = Assert.Single(result.Attachments, a => a.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(pdf.Data, "%PDF-"u8));
        Assert.True(pdf.Size > 100_000);
        var png = Assert.Single(result.Attachments, a => a.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(png.Data, 0x89, 0x50, 0x4E, 0x47));
        Assert.True(png.Size > 100_000);
    }

    [Fact]
    public void SwiftMailerHasRealOfficeAndOpaqueNestedEml()
    {
        if (!TryLoad(["spec", "php-mails", "issue274.eml"], out var bytes))
            return;

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

    [Fact]
    public void ProtonForwardHasDocxXlsxPdfMagic()
    {
        if (!TryLoad(["spec", "office", "proton-forwarding-docx.eml"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Contains(result.Attachments, a => a.Name.Equals("test.docx", StringComparison.OrdinalIgnoreCase) && HasMagic(a.Data, 0x50, 0x4B, 0x03, 0x04));
        Assert.Contains(result.Attachments, a => a.Name.Equals("test.xlsx", StringComparison.OrdinalIgnoreCase) && HasMagic(a.Data, 0x50, 0x4B, 0x03, 0x04));
        Assert.Contains(result.Attachments, a => a.Name.Equals("test.pdf", StringComparison.OrdinalIgnoreCase) && HasMagic(a.Data, "%PDF-"u8));
    }

    [Fact]
    public void GmailHebrewIso88598SubjectDecodes()
    {
        if (!TryLoad(["spec", "php-mails", "issue149"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        var subject = result.Metadata.GetValueOrDefault("subject") ?? "";
        Assert.Contains("מענה", subject);
        Assert.Contains("Invoice", subject);
    }

    [Fact]
    public void BellcoreHebrewIso88598BodyDecodes()
    {
        if (!TryLoad(["spec", "unicode", "jwz-hebrew-01.eml"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Contains("תירבע", result.Metadata.GetValueOrDefault("subject") ?? "");
        Assert.Contains("תירבע", result.MainContent);
    }

    [Fact]
    public void NestedRfc822ForwardStaysOpaque()
    {
        if (!TryLoad(["spec", "php-mails", "issue158a"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        var nested = Assert.Single(result.Attachments);
        Assert.EndsWith(".eml", nested.Name, StringComparison.OrdinalIgnoreCase);
        Assert.True(nested.Size > 100);
        Assert.StartsWith("Return-Path:", System.Text.Encoding.ASCII.GetString(nested.Data.AsSpan(0, Math.Min(12, nested.Data.Length))));
    }

    [Fact]
    public void OutlookPasteImage001At01DIsInlinePng()
    {
        if (!TryLoad(["spec", "cid", "outlook-paste-image001-01D.eml"], out var bytes))
            return;

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

    [Fact]
    public void HashCidPngPasteKeepsCidInHtml()
    {
        if (!TryLoad(["spec", "php-mails", "m0129"], out var bytes))
            return;

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Contains("cid:c024cb5b0c6eaf060a837c53db9af39b5b564d3956b8d4223fbf308b11b4e79e", result.MainContent);
        var img = Assert.Single(result.Attachments);
        Assert.True(HasMagic(img.Data, 0x89, 0x50, 0x4E, 0x47));
        Assert.Equal("inline", img.Disposition);
        Assert.True(img.Size > 1_000);
    }

    private static bool TryLoad(string[] parts, out byte[] bytes)
    {
        var path = FindInternal(parts);
        if (path is null)
        {
            bytes = [];
            return false;
        }

        bytes = File.ReadAllBytes(path);
        return true;
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

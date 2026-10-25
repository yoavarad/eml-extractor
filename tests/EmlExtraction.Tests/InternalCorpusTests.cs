namespace EmlExtraction.Tests;

public sealed class InternalCorpusTests
{
    private readonly CheckedExtractor _extractor = new();

    [SkippableFact]
    public void ZimbraOutlookTnefUnpacksGifAttachments()
    {
        var bytes = Load(["tnef", "zimbra-tnef.msg"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.DoesNotContain(result.Attachments, a => a.FileName.Equals("winmail.dat", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Attachments, a => a.FileName.Contains("upload", StringComparison.OrdinalIgnoreCase) && a.SizeInBytes > 0);
    }

    [SkippableFact]
    public void ExchangeMegaHeadersLandInMetadata()
    {
        var bytes = Load(["exchange", "leeholmes-o365-megaheaders.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.True(result.Metadata.Count >= 50, $"headers={result.Metadata.Count}");
        Assert.True(result.Metadata.ContainsKey("x-ms-exchange-organization-network-message-id"));
        Assert.True(result.Metadata.ContainsKey("x-microsoft-antispam-message-info"));
        Assert.Equal("", result.Metadata["xfields"]);
    }

    [SkippableFact]
    public void OutlookShapedCidPasteIsExtracted()
    {
        var bytes = Load(["cid", "chatwoot-outlook-cid.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Equal("text/html", result.MainContentMimeType);
        Assert.Contains("cid:image001.jpg@test", result.MainContent);
        Assert.Contains(result.Attachments, a => a.ContentId == "image001.jpg@test" && a.SizeInBytes > 0);
    }

    [SkippableFact]
    public void ShiftJisBodyDecodes()
    {
        var bytes = Load(["gaps", "shift-jis.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed", StringComparison.Ordinal) || w.StartsWith("body_decode_failed", StringComparison.Ordinal));
        Assert.Contains("あいうえお", result.MainContent);
    }

    [SkippableFact]
    public void Iso2022JpSubjectAndBodyDecode()
    {
        var bytes = Load(["gaps", "iso2022jp.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(result.Metadata.GetValueOrDefault("subject")));
        Assert.False(string.IsNullOrWhiteSpace(result.MainContent));
        Assert.Contains("テスト", result.MainContent);
    }

    [SkippableFact]
    public void Gb2312BodyDecodesWithoutThrowing()
    {
        var bytes = Load(["gaps", "gb2312.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(result.MainContent));
        Assert.DoesNotContain('\uFFFD', result.MainContent);
    }

    [SkippableFact]
    public void DsnReportKeepsHumanBodyAndDeliveryStatusPart()
    {
        var bytes = Load(["gaps", "dsn-report.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(result.MainContent));
        Assert.Contains(result.Attachments, a =>
            a.FileName.Contains("delivery-status", StringComparison.OrdinalIgnoreCase)
            || a.FileName.EndsWith(".delivery-status", StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void SmimeSignedYieldsBodyAndJpegAttachment()
    {
        var bytes = Load(["gaps", "smime-signed.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed", StringComparison.Ordinal));
        Assert.Contains("signed by Thunderbird", result.MainContent);
        Assert.Contains(result.Attachments, a => a.FileName.Contains("signed.jpg", StringComparison.OrdinalIgnoreCase) && a.SizeInBytes > 100);
        Assert.Contains(result.Attachments, a =>
            a.FileName.Contains("p7s", StringComparison.OrdinalIgnoreCase)
            || a.FileName.Contains("pkcs7-signature", StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void PoiTnefUnpacksRealOleDocAndPdf()
    {
        var bytes = Load(["spec", "tnef", "wrap-poi-quick-winmail.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.DoesNotContain(result.Attachments, a => a.FileName.Equals("winmail.dat", StringComparison.OrdinalIgnoreCase));
        var doc = Assert.Single(result.Attachments, a => a.FileName.Equals("quick.doc", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(doc.BinaryData, 0xD0, 0xCF, 0x11, 0xE0));
        Assert.True(doc.SizeInBytes > 10_000);
        var pdf = Assert.Single(result.Attachments, a => a.FileName.Equals("quick.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(pdf.BinaryData, "%PDF-"u8));
        Assert.True(pdf.SizeInBytes > 10_000);
    }

    [SkippableFact]
    public void PoiTnefUnpacksRealPdfAndPng()
    {
        var bytes = Load(["spec", "tnef", "wrap-poi-bug52400-with-attachments.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        var pdf = Assert.Single(result.Attachments, a => a.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(pdf.BinaryData, "%PDF-"u8));
        Assert.True(pdf.SizeInBytes > 100_000);
        var png = Assert.Single(result.Attachments, a => a.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(png.BinaryData, 0x89, 0x50, 0x4E, 0x47));
        Assert.True(png.SizeInBytes > 100_000);
    }

    [SkippableFact]
    public void SwiftMailerHasRealOfficeAndOpaqueNestedEml()
    {
        var bytes = Load(["spec", "php-mails", "issue274.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Equal("text/html", result.MainContentMimeType);
        var docx = Assert.Single(result.Attachments, a => a.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(docx.BinaryData, 0x50, 0x4B, 0x03, 0x04));
        Assert.True(docx.SizeInBytes > 8_000);
        var pdf = Assert.Single(result.Attachments, a => a.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasMagic(pdf.BinaryData, "%PDF-"u8));
        var nested = Assert.Single(result.Attachments, a => a.FileName.EndsWith(".eml", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Attachments, a => a.FileName.Contains("inner", StringComparison.OrdinalIgnoreCase));
        var inner = _extractor.Extract(nested.BinaryData);
        Assert.True(inner.Attachments.Count >= 1);
        Assert.Equal(5, result.Attachments.Count);
    }

    [SkippableFact]
    public void ProtonForwardHasDocxXlsxPdfMagic()
    {
        var bytes = Load(["spec", "office", "proton-forwarding-docx.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Contains(result.Attachments, a => a.FileName.Equals("test.docx", StringComparison.OrdinalIgnoreCase) && HasMagic(a.BinaryData, 0x50, 0x4B, 0x03, 0x04));
        Assert.Contains(result.Attachments, a => a.FileName.Equals("test.xlsx", StringComparison.OrdinalIgnoreCase) && HasMagic(a.BinaryData, 0x50, 0x4B, 0x03, 0x04));
        Assert.Contains(result.Attachments, a => a.FileName.Equals("test.pdf", StringComparison.OrdinalIgnoreCase) && HasMagic(a.BinaryData, "%PDF-"u8));
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
        Assert.EndsWith(".eml", nested.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(nested.SizeInBytes > 100);
        Assert.StartsWith("Return-Path:", System.Text.Encoding.ASCII.GetString(nested.BinaryData.AsSpan(0, Math.Min(12, nested.BinaryData.Length))));
    }

    [SkippableFact]
    public void OutlookPasteImage001At01DIsInlinePng()
    {
        var bytes = Load(["spec", "cid", "outlook-paste-image001-01D.eml"]);

        var result = _extractor.Extract(bytes);
        Assert.Empty(result.Warnings);
        Assert.Equal("text/html", result.MainContentMimeType);
        Assert.Contains("cid:image001.png@01D8693E.BE86BE20", result.MainContent);
        var img = Assert.Single(result.Attachments, a => a.FileName.Equals("image001.png", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("image001.png@01D8693E.BE86BE20", img.ContentId);
        Assert.Equal("inline", img.Disposition);
        Assert.True(HasMagic(img.BinaryData, 0x89, 0x50, 0x4E, 0x47));
        Assert.True(img.SizeInBytes > 100);
        Assert.Contains(result.Attachments, a =>
            a.FileName.Equals("duckduckgo_small.png", StringComparison.OrdinalIgnoreCase)
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
        Assert.True(HasMagic(img.BinaryData, 0x89, 0x50, 0x4E, 0x47));
        Assert.Equal("inline", img.Disposition);
        Assert.True(img.SizeInBytes > 1_000);
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

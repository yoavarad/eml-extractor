using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MimeKit;

namespace RealisticSamples;

public sealed record ExpectedFile(string FileName, long SizeInBytes, string Sha256, string FullPath);

public static class SampleGenerator
{
    static readonly DateTimeOffset Date = new(2025, 3, 4, 9, 30, 0, TimeSpan.Zero);
    const string Generator = "tools/RealisticSamples";
    static readonly JsonSerializerOptions ManifestJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    const string License = "generated: repo license";

    const string PoiCommit = "62322b97f3f1bca39cdcd0b7705973833c74ad25";
    const string PoiLicenseUrl = "https://github.com/apache/poi/blob/" + PoiCommit + "/legal/LICENSE";

    /// <summary>Public files committed verbatim under corpus/realistic/sources; pinned hashes are of the published bytes.</summary>
    static readonly (string Eml, string Subject, string Dir, string Name, string Sha, string Type, string Sub)[] PublicSources =
    [
        ("attached-doc", "Legacy document", "document", "Bug60942b.doc", "183379e7c855aeed2c496f1170e4e2bec16bfde9fddc49ae0fa14f765947430f", "application", "msword"),
        ("attached-xls", "Legacy sheet", "spreadsheet", "63819.xls", "0defe1c277ac1107498f2326c4376d47effa0051c1e20e5f2cb30caa89c822f3", "application", "vnd.ms-excel"),
        ("attached-msg", "Outlook item", "hsmf", "blank.msg", "b9e5b5d8fac07e7be9604a3f9b039b9f824978d8b59cdeb2768d5182a03825c0", "application", "vnd.ms-outlook"),
    ];

    static string SourcesDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EmlExtraction.slnx"))) d = d.Parent;
        d ??= new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EmlExtraction.slnx"))) d = d.Parent;
        return Path.Combine(d?.FullName ?? throw new InvalidOperationException("repo root not found"), "corpus", "realistic", "sources");
    }

    public static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    static ExpectedFile Exp(string name, byte[] data, string? path = null) => new(name, data.Length, Sha(data), path ?? name);

    static MimeMessage Msg(string subject, string id) => new()
    {
        From = { new MailboxAddress("Alice Example", "alice@example.com") },
        To = { new MailboxAddress("Bob Example", "bob@example.net") },
        Subject = subject,
        Date = Date,
        MessageId = id + "@example.com",
    };

    static MimePart Att(string name, string type, string sub, byte[] data) => new(type, sub)
    {
        Content = new MimeContent(new MemoryStream(data)),
        ContentTransferEncoding = ContentEncoding.Base64,
        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = name },
        FileName = name,
    };

    static Multipart Mixed(string boundary, string body, params MimeEntity[] parts)
    {
        var mp = new Multipart("mixed");
        mp.ContentType.Boundary = boundary;
        mp.Add(new TextPart("plain") { Text = body, ContentTransferEncoding = ContentEncoding.SevenBit });
        foreach (var p in parts) mp.Add(p);
        return mp;
    }

    /// <summary>CRLF regardless of platform so output is identical on every OS.</summary>
    public static readonly FormatOptions Fmt = NewFmt();

    static FormatOptions NewFmt() { var o = FormatOptions.Default.Clone(); o.NewLineFormat = NewLineFormat.Dos; return o; }

    static byte[] Serialize(MimeMessage m)
    {
        using var ms = new MemoryStream();
        m.WriteTo(Fmt, ms);
        return ms.ToArray();
    }

    public static void Generate(string outDir)
    {
        var docx = Binaries.Docx();
        var xlsx = Binaries.Xlsx();
        var pptx = Binaries.Pptx();
        var pdf = Binaries.Pdf();
        var png = Binaries.Png(40);
        var outlookPng = Binaries.Png(90);
        var gmailPng = Binaries.Png(150);

        var samples = new List<(string File, byte[] Bytes, object Expected)>();
        var sourceInfo = new Dictionary<string, object>();

        void Attached(string file, string subject, string att, string t, string st, byte[] data)
        {
            var m = Msg(subject, file);
            m.Body = Mixed("=-bnd-" + file, "See attached file.", Att(att, t, st, data));
            samples.Add((file + ".eml", Serialize(m), new { attachments = new[] { Exp(att, data) } }));
        }

        Attached("attached-docx", "Report", "report.docx", "application", "vnd.openxmlformats-officedocument.wordprocessingml.document", docx);
        Attached("attached-xlsx", "Sheet", "data.xlsx", "application", "vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx);
        Attached("attached-pptx", "Slides", "deck.pptx", "application", "vnd.openxmlformats-officedocument.presentationml.presentation", pptx);
        Attached("attached-pdf", "Invoice", "invoice.pdf", "application", "pdf", pdf);
        Attached("attached-image", "Photo", "photo.png", "image", "png", png);

        foreach (var (eml, subject, dir, name, sha, t, st) in PublicSources)
        {
            var data = File.ReadAllBytes(Path.Combine(SourcesDir(), name));
            if (Sha(data) != sha) throw new InvalidOperationException($"{name} does not match its published SHA-256");
            Attached(eml, subject, name, t, st, data);
            sourceInfo[eml + ".eml"] = new
            {
                url = $"https://github.com/apache/poi/blob/{PoiCommit}/test-data/{dir}/{name}",
                commit = PoiCommit,
                license = "Apache-2.0",
                licenseUrl = PoiLicenseUrl,
                file = "sources/" + name,
                sha256 = sha,
            };
        }

        // Nested message: returned as a single attachment, never recursed (ADR-013).
        var inner = Msg("Inner note", "inner-note");
        inner.Body = new TextPart("plain") { Text = "Nested message body.", ContentTransferEncoding = ContentEncoding.SevenBit };
        var innerBytes = Serialize(inner);
        var outer = Msg("Forwarded", "attached-eml");
        var part = new MessagePart("rfc822", inner)
        {
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = "forwarded.eml" },
        };
        outer.Body = Mixed("=-bnd-attached-eml", "See attached message.", part);
        samples.Add(("attached-eml.eml", Serialize(outer), new { attachments = new[] { Exp("forwarded.eml", innerBytes) } }));

        // TNEF wrapper holding a docx.
        var tnef = Binaries.Tnef(("report.docx", docx));
        var tm = Msg("Outlook wrapper", "tnef-docx");
        tm.Body = Mixed("=-bnd-tnef-docx", "Body.", Att("winmail.dat", "application", "ms-tnef", tnef));
        samples.Add(("tnef-docx.eml", Serialize(tm), new
        {
            attachments = new[] { Exp("report.docx", docx, "winmail.dat/report.docx") },
        }));

        samples.Add(Paste("outlook-paste", "image001.png@01D9A1B2.3C4D5E60", "image001.png", outlookPng,
            "<html><body><p>Screenshot:</p><img src=\"cid:image001.png@01D9A1B2.3C4D5E60\" width=16 height=16></body></html>"));
        samples.Add(Paste("gmail-paste", "ii_lq8x2k1a0", "image.png", gmailPng,
            "<div dir=\"ltr\">Screenshot:<br><img src=\"cid:ii_lq8x2k1a0\" alt=\"image.png\" width=\"16\" height=\"16\"><br></div>"));

        var entries = new List<object>();
        foreach (var (file, bytes, expected) in samples.OrderBy(s => s.File, StringComparer.Ordinal))
        {
            File.WriteAllBytes(Path.Combine(outDir, file), bytes);
            if (sourceInfo.TryGetValue(file, out var src))
                entries.Add(new { file, generator = Generator, license = "Apache-2.0", sha256 = Sha(bytes), source = src, expected });
            else
                entries.Add(new { file, generator = Generator, license = License, sha256 = Sha(bytes), expected });
        }
        var json = JsonSerializer.Serialize(new { samples = entries }, ManifestJson);
        File.WriteAllText(Path.Combine(outDir, "manifest.json"), json.ReplaceLineEndings("\n") + "\n", new UTF8Encoding(false));
    }

    static (string, byte[], object) Paste(string name, string cid, string fileName, byte[] img, string html)
    {
        var m = Msg("Pasted image", name);
        var related = new Multipart("related");
        related.ContentType.Boundary = "=-bnd-" + name;
        related.Add(new TextPart("html") { Text = html, ContentTransferEncoding = ContentEncoding.QuotedPrintable });
        related.Add(new MimePart("image", "png")
        {
            Content = new MimeContent(new MemoryStream(img)),
            ContentTransferEncoding = ContentEncoding.Base64,
            ContentId = cid,
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline) { FileName = fileName },
            FileName = fileName,
        });
        m.Body = related;
        return (name + ".eml", Serialize(m), new
        {
            inlineImages = new[] { new { contentId = cid, file = Exp(fileName, img) } },
        });
    }
}

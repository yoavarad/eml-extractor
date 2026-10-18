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
    const string License = "generated: repo license";

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
            entries.Add(new { file, generator = Generator, license = License, sha256 = Sha(bytes), expected });
        }
        var json = JsonSerializer.Serialize(new { samples = entries }, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
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

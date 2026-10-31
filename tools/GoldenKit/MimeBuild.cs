using System.Security.Cryptography;
using System.Text;
using EmlExtraction.Models;
using MimeKit;

namespace GoldenKit;

internal static class MimeBuild
{
    public static readonly DateTimeOffset Stamp = new(2026, 9, 15, 7, 0, 0, TimeSpan.Zero);

    public static byte[] ToBytes(MimeMessage message)
    {
        using var stream = new MemoryStream();
        var options = FormatOptions.Default.Clone();
        options.International = true;
        message.WriteTo(options, stream);
        return stream.ToArray();
    }

    public static MimeMessage BaseMessage(string id, string subject)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("Alice <alice@example.com>"));
        message.To.Add(MailboxAddress.Parse("Bob <bob@example.com>"));
        message.Subject = subject;
        message.Date = Stamp;
        message.MessageId = $"{id}@golden.example";
        return message;
    }

    public static CaseDef Plain(string id, string[] tags, string subject, string body)
    {
        var message = BaseMessage(id, subject);
        message.Body = new TextPart("plain") { Text = body };
        return FromMessage(id, tags, message, new GoldenExpect
        {
            Metadata = Meta(subject),
            MainContent = Eq(body),
            MainContentMimeType = "text/plain",
            AttachmentCount = 0
        });
    }

    public static CaseDef Html(string id, string[] tags, string subject, string html)
    {
        var message = BaseMessage(id, subject);
        message.Body = new TextPart("html") { Text = html };
        return FromMessage(id, tags, message, new GoldenExpect
        {
            Metadata = Meta(subject),
            MainContent = Eq(html),
            MainContentMimeType = "text/html",
            AttachmentCount = 0
        });
    }

    public static CaseDef Alternative(string id, string[] tags, string subject, string plain, string html)
    {
        var builder = new BodyBuilder { TextBody = plain, HtmlBody = html };
        var message = BaseMessage(id, subject);
        message.Body = builder.ToMessageBody();
        return FromMessage(id, tags, message, new GoldenExpect
        {
            Metadata = Meta(subject),
            MainContent = Eq(html),
            MainContentMimeType = "text/html",
            AttachmentCount = 0
        });
    }

    public static CaseDef WithParts(
        string id,
        string[] tags,
        string subject,
        string textBody,
        bool html,
        IEnumerable<(string Name, string Media, string Sub, byte[] Payload, string Disposition, string? Path)> parts)
    {
        var builder = html
            ? new BodyBuilder { HtmlBody = textBody }
            : new BodyBuilder { TextBody = textBody };
        var expectParts = new List<GoldenAttachment>();
        foreach (var part in parts)
        {
            var fileName = part.Path ?? part.Name;
            MimeEntity added = part.Disposition == "inline"
                ? builder.LinkedResources.Add(fileName, part.Payload, new ContentType(part.Media, part.Sub))
                : builder.Attachments.Add(fileName, part.Payload, new ContentType(part.Media, part.Sub));
            if (added is MimePart mime)
            {
                mime.FileName = fileName;
                mime.ContentDisposition ??= new ContentDisposition(part.Disposition);
                mime.ContentDisposition.Disposition = part.Disposition;
                mime.ContentDisposition.FileName = fileName;
            }

            expectParts.Add(new GoldenAttachment
            {
                Name = part.Name,
                Size = part.Payload.Length,
                Sha256 = Sha(part.Payload),
                Disposition = part.Disposition,
                OriginalPath = part.Path
            });
        }

        var message = BaseMessage(id, subject);
        message.Body = builder.ToMessageBody();
        return FromMessage(id, tags, message, new GoldenExpect
        {
            Metadata = Meta(subject),
            MainContent = html ? Contains("<") : Eq(textBody),
            MainContentMimeType = html ? "text/html" : "text/plain",
            AttachmentCount = expectParts.Count,
            Attachments = expectParts
        });
    }

    public static CaseDef NestedRfc822(string id, string[] tags, string innerSubject, string innerBody, string fileName)
    {
        var inner = BaseMessage(id + "-inner", innerSubject);
        inner.From.Clear();
        inner.From.Add(MailboxAddress.Parse("inner@example.com"));
        inner.Body = new TextPart("plain") { Text = innerBody };

        var rfc822 = new MessagePart("rfc822")
        {
            Message = inner,
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = fileName }
        };
        var mixed = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "outer-body" },
            rfc822
        };
        var outer = BaseMessage(id, "outer");
        outer.Body = mixed;
        return FromMessage(id, tags, outer, new GoldenExpect
        {
            Metadata = Meta("outer"),
            MainContent = Eq("outer-body"),
            MainContentMimeType = "text/plain",
            AttachmentCount = 1,
            Attachments =
            [
                new GoldenAttachment
                {
                    Name = Path.GetFileName(fileName.Replace('\\', '/')),
                    OriginalPath = fileName.Contains('/') || fileName.Contains('\\') ? fileName : null,
                    Reextract = new GoldenExpect
                    {
                        Metadata = Meta(innerSubject),
                        MainContent = Eq(innerBody),
                        AttachmentCount = 0
                    }
                }
            ]
        });
    }

    public static CaseDef RelatedCid(string id, string[] tags, byte[] image, string imageName, string cid)
    {
        var builder = new BodyBuilder { HtmlBody = $"<p>paste</p><img src=\"cid:{cid}\" />" };
        var linked = builder.LinkedResources.Add(imageName, image, new ContentType("image", ExtType(imageName)));
        linked.ContentId = cid;
        linked.ContentDisposition = new ContentDisposition(ContentDisposition.Inline) { FileName = imageName };
        var message = BaseMessage(id, "paste");
        message.Body = builder.ToMessageBody();
        return FromMessage(id, tags, message, new GoldenExpect
        {
            Metadata = Meta("paste"),
            MainContent = new GoldenField { Contains = [$"cid:{cid}"], NotContains = ["iVBORw0KGgo"] },
            MainContentMimeType = "text/html",
            AttachmentCount = 1,
            Attachments =
            [
                new GoldenAttachment
                {
                    Name = imageName,
                    Size = image.Length,
                    Sha256 = Sha(image),
                    Disposition = "inline"
                }
            ]
        });
    }

    public static CaseDef FromMessage(string id, string[] tags, MimeMessage message, GoldenExpect expect)
    {
        expect.Metadata.TryAdd(MetadataKeys.From, new GoldenField { Contains = ["alice@example.com"] });
        expect.Metadata.TryAdd(MetadataKeys.To, new GoldenField { Contains = ["bob@example.com"] });
        return new CaseDef { Id = id, Tags = tags, Eml = ToBytes(message), IsEml = true, Expect = WithFullPaths(expect) };
    }

    public static CaseDef RawEml(string id, string[] tags, string rfc822, GoldenExpect expect)
    {
        var bytes = Encoding.UTF8.GetBytes(rfc822.Replace("\n", "\r\n"));
        return new CaseDef { Id = id, Tags = tags, Eml = bytes, IsEml = true, Expect = WithFullPaths(expect) };
    }

    /// <summary>ADR-013: catalog parts are all top-level (no TNEF wrapper), so FullPath equals the name when it is unique in a fully listed result.</summary>
    private static GoldenExpect WithFullPaths(GoldenExpect expect)
    {
        foreach (var a in expect.Attachments)
        {
            if (a.Name is not null
                && expect.AttachmentCount == expect.Attachments.Count
                && expect.Attachments.Count(b => string.Equals(b.Name, a.Name, StringComparison.OrdinalIgnoreCase)) == 1)
                a.FullPath ??= a.Name;
            if (a.Reextract is not null)
                WithFullPaths(a.Reextract);
        }

        return expect;
    }

    public static CaseDef RawBytes(string id, string[] tags, byte[] data, bool isEml, GoldenExpect? expect = null)
        => new() { Id = id, Tags = tags, Eml = data, IsEml = isEml, Expect = expect ?? new GoldenExpect() };

    public static Dictionary<string, GoldenField> Meta(string subject)
        => new(StringComparer.OrdinalIgnoreCase) { [MetadataKeys.Subject] = Eq(subject)! };

    public static GoldenField Eq(string value) => new() { Eq = value };

    public static GoldenField Contains(params string[] parts) => new() { Contains = [.. parts] };

    public static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static byte[] Stub(string kind, string id)
    {
        var tail = Encoding.UTF8.GetBytes($"{kind}:{id}");
        return kind switch
        {
            "docx" or "xlsx" or "pptx" or "odt" or "ods" or "odp" or "zip" => Concat([(byte)'P', (byte)'K', 3, 4], tail),
            "pdf" => Concat("%PDF-1.4-"u8.ToArray(), tail),
            "jpg" => Concat([0xFF, 0xD8, 0xFF, 0xE0], tail),
            "gif" => Concat("GIF89a"u8.ToArray(), tail),
            "png" => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="),
            "webp" => Concat("RIFF"u8.ToArray(), [16, 0, 0, 0], "WEBP"u8.ToArray(), tail),
            "bmp" => Concat("BM"u8.ToArray(), tail),
            "tif" => Concat("II*"u8.ToArray(), [0], tail),
            "ico" => Concat([0, 0, 1, 0], tail),
            "heic" => Concat([0, 0, 0, 24], "ftypheic"u8.ToArray(), tail),
            "gz" => Concat([0x1F, 0x8B, 0x08], tail),
            "7z" => Concat([0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C], tail),
            "mp3" => Concat("ID3"u8.ToArray(), tail),
            "wav" => Concat("RIFF"u8.ToArray(), [0, 0, 0, 0], "WAVE"u8.ToArray(), tail),
            "mp4" => Concat([0, 0, 0, 24], "ftypmp42"u8.ToArray(), tail),
            "msg" => Concat([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1], tail),
            "exe" => Concat("MZ"u8.ToArray(), tail),
            _ => tail
        };
    }

    public static string ExtType(string name)
    {
        var ext = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "jpg" or "jpeg" => "jpeg",
            "svg" => "svg+xml",
            "tif" or "tiff" => "tiff",
            _ => ext
        };
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var n = parts.Sum(p => p.Length);
        var r = new byte[n];
        var o = 0;
        foreach (var p in parts)
        {
            p.CopyTo(r, o);
            o += p.Length;
        }

        return r;
    }
}

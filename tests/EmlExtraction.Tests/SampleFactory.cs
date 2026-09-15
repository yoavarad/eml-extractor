using System.Security.Cryptography;
using System.Text;
using MimeKit;
using MimeKit.Utils;

namespace EmlExtraction.Tests;

internal static class SampleFactory
{
    public static byte[] ToBytes(MimeMessage message)
    {
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return stream.ToArray();
    }

    public static MimeMessage Plain(
        string subject,
        string text,
        string from = "Alice <alice@example.com>",
        string to = "Bob <bob@example.com>")
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Date = new DateTimeOffset(2026, 9, 15, 7, 0, 0, TimeSpan.Zero);
        message.MessageId = MimeUtils.GenerateMessageId("example.com");
        message.Body = new TextPart("plain") { Text = text };
        return message;
    }

    public static byte[] WithAttachment(string fileName, byte[] payload, string mediaType, string subType)
    {
        var builder = new BodyBuilder { HtmlBody = "<p>see attach</p>", TextBody = "see attach" };
        builder.Attachments.Add(fileName, payload, new ContentType(mediaType, subType));
        var message = Plain("attachments", "unused");
        message.Body = builder.ToMessageBody();
        return ToBytes(message);
    }

    public static byte[] RelatedHtmlWithCid(byte[] image, string contentId)
    {
        var builder = new BodyBuilder
        {
            HtmlBody = $"<p>pasted</p><img src=\"cid:{contentId}\" />"
        };
        var part = builder.LinkedResources.Add("paste.png", image, new ContentType("image", "png"));
        part.ContentId = contentId;
        var message = Plain("paste", "unused");
        message.Body = builder.ToMessageBody();
        return ToBytes(message);
    }

    public static byte[] NestedRfc822()
    {
        var inner = Plain("inner subject", "inner body");
        var rfc822 = new MessagePart("rfc822")
        {
            Message = inner,
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentLocation = null
        };
        rfc822.ContentDisposition.FileName = "forwarded.eml";

        var mixed = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "outer" },
            rfc822
        };
        var outer = Plain("outer", "unused");
        outer.Body = mixed;
        return ToBytes(outer);
    }

    public static byte[] MinimalPng()
    {
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
    }

    public static byte[] FakeOffice(string ext)
    {
        var payload = Encoding.UTF8.GetBytes($"fake-{ext}-" + new string('A', 256));
        var zipLike = new byte[] { (byte)'P', (byte)'K', 0x03, 0x04 }.Concat(payload).ToArray();
        return zipLike;
    }

    public static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
}

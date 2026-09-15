using System.Text;
using MimeKit;

namespace EmlExtraction.Bench;

internal static class Corpus
{
    public static Sample[] Create() =>
    [
        new("tiny", ToBytes(Plain("tiny", "x"))),
        new("small-20kb", HtmlAlternative(20_000)),
        new("headers-heavy", ToBytes(Plain("headers " + new string('s', 200), new string('b', 400)))),
        new("cid-image", WithCid()),
        new("attach-1mb", WithAttachment(1_000_000))
    ];

    public static byte[] ToBytes(MimeMessage message)
    {
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return stream.ToArray();
    }

    public static MimeMessage Plain(string subject, string text)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("alice@example.com"));
        message.To.Add(MailboxAddress.Parse("bob@example.com"));
        message.Subject = subject;
        message.Date = new DateTimeOffset(2026, 9, 15, 7, 0, 0, TimeSpan.Zero);
        message.Body = new TextPart("plain") { Text = text };
        return message;
    }

    private static byte[] HtmlAlternative(int chars)
    {
        var builder = new BodyBuilder
        {
            TextBody = new string('p', chars / 4),
            HtmlBody = "<p>" + new string('h', chars) + "</p>"
        };
        var message = Plain("html", "x");
        message.Body = builder.ToMessageBody();
        return ToBytes(message);
    }

    private static byte[] WithAttachment(int size)
    {
        var builder = new BodyBuilder { TextBody = "see file" };
        builder.Attachments.Add("blob.bin", Encoding.UTF8.GetBytes(new string('A', size)), new ContentType("application", "octet-stream"));
        var message = Plain("att", "x");
        message.Body = builder.ToMessageBody();
        return ToBytes(message);
    }

    private static byte[] WithCid()
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var builder = new BodyBuilder { HtmlBody = "<img src=\"cid:x@x\" />" };
        var part = builder.LinkedResources.Add("x.png", png, new ContentType("image", "png"));
        part.ContentId = "x@x";
        var message = Plain("cid", "x");
        message.Body = builder.ToMessageBody();
        return ToBytes(message);
    }
}

internal sealed record Sample(string Name, byte[] Bytes);

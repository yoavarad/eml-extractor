using System.Text;
using MimeKit;

namespace EmlExtraction.Tests;

public sealed class ExtractionTests
{
    private readonly EmlExtractor _extractor = new();

    [Fact]
    public void ExtractsClassicHeadersAndUnicodeSubject()
    {
        var message = SampleFactory.Plain("שלום 😀 中文 हिंदी", "hello");
        message.Cc.Add(MailboxAddress.Parse("Carol <carol@example.com>"));
        message.ReplyTo.Add(MailboxAddress.Parse("reply@example.com"));
        message.Headers.Add("X-Mailer", "SandboxMail");
        message.Headers.Add("X_Custom_Field", "team-value");
        message.Headers.Add("X-Spam-Score", "1.2");
        message.Headers.Add("DKIM-Signature", "v=1; a=rsa-sha256; dummy");

        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Contains("alice@example.com", result.Metadata["from"]);
        Assert.Contains("bob@example.com", result.Metadata["to"]);
        Assert.Contains("carol@example.com", result.Metadata["cc"]);
        Assert.Equal("שלום 😀 中文 हिंदी", result.Metadata["subject"]);
        Assert.True(result.Metadata.ContainsKey("messageid"));
        Assert.True(result.Metadata.ContainsKey("dkim_signature"));
        Assert.Contains("X-Mailer: SandboxMail", result.XFields);
        Assert.Contains("X_Custom_Field: team-value", result.XFields);
        Assert.Contains("X-Spam-Score: 1.2", result.XFields);
        Assert.Equal("hello", result.MainContent);
        Assert.Equal("text/plain", result.MainContentMimeType);
        Assert.Empty(result.Attachments);
    }

    [Fact]
    public void PrefersHtmlOverPlain()
    {
        var builder = new BodyBuilder
        {
            TextBody = "plain version",
            HtmlBody = "<p>html version</p>"
        };
        var message = SampleFactory.Plain("alt", "unused");
        message.Body = builder.ToMessageBody();
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Equal("<p>html version</p>", result.MainContent);
        Assert.Equal("text/html", result.MainContentMimeType);
        Assert.DoesNotContain(result.Attachments, a => Encoding.UTF8.GetString(a.Data).Contains("plain version"));
    }

    [Fact]
    public void MboxFromPrefixDoesNotPolluteHeaders()
    {
        var inner = SampleFactory.ToBytes(SampleFactory.Plain("mbox-subject", "mbox-body"));
        var raw = Encoding.ASCII.GetBytes("From alice@example.com Tue Sep 15 07:00:00 2026\n")
            .Concat(inner)
            .ToArray();
        var result = _extractor.Extract(raw);
        Assert.Equal("mbox-subject", result.Metadata["subject"]);
        Assert.Equal("mbox-body", result.MainContent);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
        Assert.DoesNotContain(result.Metadata, kv => kv.Value.Contains("Tue Sep 15 07:00:00", StringComparison.Ordinal));
    }

    [Fact]
    public void HeadersOnlyDoesNotThrow()
    {
        var raw = """
            From: alice@example.com
            To: bob@example.com
            Subject: empty
            Date: Tue, 15 Sep 2026 07:00:00 +0000
            Message-ID: <empty@example.com>


            """;
        var result = _extractor.Extract(Encoding.ASCII.GetBytes(raw));
        Assert.Equal("empty", result.Metadata["subject"]);
        Assert.True(string.IsNullOrEmpty(result.MainContent));
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
    }

    [Fact]
    public void FoldedSubjectIsJoined()
    {
        var raw = """
            From: alice@example.com
            To: bob@example.com
            Subject: hello
             world and more
            Date: Tue, 15 Sep 2026 07:00:00 +0000
            Message-ID: <fold@example.com>
            MIME-Version: 1.0
            Content-Type: text/plain; charset=utf-8

            body
            """;
        var result = _extractor.Extract(Encoding.ASCII.GetBytes(raw.Replace("\n", "\r\n")));
        Assert.Contains("hello", result.Metadata["subject"]);
        Assert.Contains("world", result.Metadata["subject"]);
    }

    [Fact]
    public void Windows1255BodyDecodes()
    {
        var hebrew = "שלום";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var win = Encoding.GetEncoding(1255);
        var b64 = Convert.ToBase64String(win.GetBytes(hebrew));
        var raw = $"""
            From: alice@example.com
            To: bob@example.com
            Subject: =?windows-1255?B?{Convert.ToBase64String(win.GetBytes(hebrew))}?=
            Date: Tue, 15 Sep 2026 07:00:00 +0000
            Message-ID: <heb@example.com>
            MIME-Version: 1.0
            Content-Type: text/plain; charset=windows-1255
            Content-Transfer-Encoding: base64

            {b64}
            """;
        var result = _extractor.Extract(Encoding.ASCII.GetBytes(raw.Replace("\n", "\r\n")));
        Assert.Equal(hebrew, result.Metadata["subject"]);
        Assert.Equal(hebrew, result.MainContent);
    }

    [Fact]
    public void Smtputf8Addresses()
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("יוסי", "יוסי@דוגמה.com"));
        message.To.Add(new MailboxAddress("李", "li@example.com"));
        message.Subject = "unicode addr";
        message.Date = new DateTimeOffset(2026, 9, 15, 7, 0, 0, TimeSpan.Zero);
        message.Body = new TextPart("plain") { Text = "x" };
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Contains("יוסי", result.Metadata["from"]);
        Assert.Contains("li@example.com", result.Metadata["to"]);
    }

    [Fact]
    public void DuplicateReceivedAreConcatenated()
    {
        var message = SampleFactory.Plain("r", "b");
        message.Headers.Add("Received", "from mx1.example.com");
        message.Headers.Add("Received", "from mx2.example.com");
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Contains("mx1.example.com", result.Metadata["received"]);
        Assert.Contains("mx2.example.com", result.Metadata["received"]);
    }

    [Fact]
    public void CorruptBase64DoesNotCrash()
    {
        var raw = """
            From: alice@example.com
            To: bob@example.com
            Subject: corrupt
            Date: Tue, 15 Sep 2026 07:00:00 +0000
            Message-ID: <c@example.com>
            MIME-Version: 1.0
            Content-Type: text/plain; charset=utf-8
            Content-Transfer-Encoding: base64

            %%%%not-base64%%%%
            """;
        var result = _extractor.Extract(Encoding.ASCII.GetBytes(raw.Replace("\n", "\r\n")));
        Assert.Equal("corrupt", result.Metadata["subject"]);
    }

    [Fact]
    public void PathInFileNameIsPreservedSeparately()
    {
        var payload = Encoding.UTF8.GetBytes("secret");
        var builder = new BodyBuilder { TextBody = "x" };
        var part = (MimePart)builder.Attachments.Add("../../etc/passwd.txt", payload, new ContentType("text", "plain"));
        part.FileName = "../../etc/passwd.txt";
        var message = SampleFactory.Plain("path", "unused");
        message.Body = builder.ToMessageBody();
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        var att = Assert.Single(result.Attachments);
        Assert.Equal("passwd.txt", att.Name);
        Assert.Equal("../../etc/passwd.txt", att.FullPath);
        Assert.Equal(payload, att.Data);
    }
}

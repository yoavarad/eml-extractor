using System.Reflection;
using System.Text;
using MimeKit;

namespace EmlExtraction.Tests;

public sealed class ExtractionTests
{
    private readonly IEmlExtractor _extractor = new CheckedExtractor();

    [Fact]
    public void FromToCcBccAreAlwaysPresent()
    {
        var raw = """
            From: alice@example.com
            To: bob@example.com
            Subject: no-cc
            Date: Tue, 15 Sep 2026 07:00:00 +0000
            Message-ID: <addr@example.com>
            MIME-Version: 1.0
            Content-Type: text/plain; charset=utf-8

            body
            """;
        var result = _extractor.Extract(Encoding.ASCII.GetBytes(raw.ReplaceLineEndings("\r\n")));
        Assert.Equal("alice@example.com", result.Metadata["from"].Trim());
        Assert.Contains("bob@example.com", result.Metadata["to"]);
        Assert.Equal("", result.Metadata["cc"]);
        Assert.Equal("", result.Metadata["bcc"]);
    }

    [Fact]
    public void ExtractsFromToCcBccIncludingMultiplesAndRtlNames()
    {
        var message = SampleFactory.Plain("recipients", "body");
        message.From.Clear();
        message.From.Add(new MailboxAddress("יוסי", "yossi@example.com"));
        message.From.Add(MailboxAddress.Parse("alice@example.com"));
        message.To.Clear();
        message.To.Add(new MailboxAddress("فاطمة", "fatima@example.com"));
        message.Cc.Add(MailboxAddress.Parse("Carol <carol@example.com>"));
        message.Cc.Add(MailboxAddress.Parse("dave@example.com"));
        message.Bcc.Add(MailboxAddress.Parse("hidden@example.com"));
        message.Bcc.Add(new MailboxAddress("סודי", "secret@example.com"));

        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Contains("yossi@example.com", result.Metadata["from"]);
        Assert.Contains("יוסי", result.Metadata["from"]);
        Assert.Contains("alice@example.com", result.Metadata["from"]);
        Assert.Contains("fatima@example.com", result.Metadata["to"]);
        Assert.Contains("فاطمة", result.Metadata["to"]);
        Assert.Contains("carol@example.com", result.Metadata["cc"]);
        Assert.Contains("dave@example.com", result.Metadata["cc"]);
        Assert.Contains("hidden@example.com", result.Metadata["bcc"]);
        Assert.Contains("secret@example.com", result.Metadata["bcc"]);
        Assert.Contains("סודי", result.Metadata["bcc"]);
    }

    [Fact]
    public void RawBccHeaderIsExtracted()
    {
        var raw = """
            From: Alice <alice@example.com>
            To: Bob <bob@example.com>
            Cc: Carol <carol@example.com>, dave@example.com
            Bcc: hidden@example.com, "סודי" <secret@example.com>
            Subject: bcc-raw
            Date: Tue, 15 Sep 2026 07:00:00 +0000
            Message-ID: <bcc@example.com>
            MIME-Version: 1.0
            Content-Type: text/plain; charset=utf-8

            body
            """;
        var result = _extractor.Extract(Encoding.UTF8.GetBytes(raw.ReplaceLineEndings("\r\n")));
        Assert.Contains("alice@example.com", result.Metadata["from"]);
        Assert.Contains("bob@example.com", result.Metadata["to"]);
        Assert.Contains("carol@example.com", result.Metadata["cc"]);
        Assert.Contains("dave@example.com", result.Metadata["cc"]);
        Assert.Contains("hidden@example.com", result.Metadata["bcc"]);
        Assert.Contains("secret@example.com", result.Metadata["bcc"]);
    }

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
        Assert.True(result.Metadata.ContainsKey("message-id"));
        Assert.True(result.Metadata.ContainsKey("dkim-signature"));
        Assert.Equal("X_Custom_Field: team-value", result.Metadata["xfields"]);
        Assert.Equal("SandboxMail", result.Metadata["x-mailer"]);
        Assert.Equal("1.2", result.Metadata["x-spam-score"]);
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
        var result = _extractor.Extract(Encoding.ASCII.GetBytes(raw.ReplaceLineEndings("\r\n")));
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
        var result = _extractor.Extract(Encoding.ASCII.GetBytes(raw.ReplaceLineEndings("\r\n")));
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
    public void HyphenAndUnderscoreHeadersDoNotCollide()
    {
        var message = SampleFactory.Plain("c", "b");
        message.Headers.Add("X-Foo", "a");
        message.Headers.Add("X_Foo", "b");
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Equal("a", result.Metadata["x-foo"]);
        Assert.Equal("b", result.Metadata["x_foo"]);
    }

    [Fact]
    public void OverlayWritesInPlaceAndOldKeysAbsent()
    {
        var message = SampleFactory.Plain("o", "b");
        message.ReplyTo.Add(MailboxAddress.Parse("reply@example.com"));
        message.Headers.Add("DKIM-Signature", "v=1");
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Contains("reply@example.com", result.Metadata["reply-to"]);
        Assert.True(result.Metadata.ContainsKey("message-id"));
        Assert.DoesNotContain("<", result.Metadata["message-id"]);
        Assert.True(result.Metadata.ContainsKey("dkim-signature"));
        Assert.True(result.Metadata.ContainsKey("content-type"));
        foreach (var gone in new[] { "messageid", "replyto", "dkim_signature", "contenttype", "subject_direction", "content_direction", "mimeversion", "returnpath", "deliveredto", "inreplyto", "reply_to", "message_id" })
            Assert.False(result.Metadata.ContainsKey(gone), gone);
    }

    [Fact]
    public void XFieldsJoinsOnlyLiteralXUnderscoreHeadersInOrder()
    {
        var message = SampleFactory.Plain("x", "b");
        message.Headers.Add("X_A", "1");
        message.Headers.Add("X-B", "2");
        message.Headers.Add("x_c", "3");
        message.Headers.Add("X_A", "4");
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Equal("X_A: 1\nX_A: 4", result.Metadata["xfields"]);
        Assert.Equal("2", result.Metadata["x-b"]);
        Assert.Equal("3", result.Metadata["x_c"]);
    }

    [Fact]
    public void XFieldsEmptyWhenOnlyXDashHeaders()
    {
        var message = SampleFactory.Plain("x", "b");
        message.Headers.Add("X-Mailer", "m");
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Equal("", result.Metadata["xfields"]);
    }

    [Fact]
    public void HeaderNamedXFieldsIsStoredUnderReservedPrefix()
    {
        var message = SampleFactory.Plain("x", "b");
        message.Headers.Add("XFields", "q");
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Equal("q", result.Metadata["header:xfields"]);
        Assert.True(result.Metadata.ContainsKey("xfields"));
    }

    [Fact]
    public void ParseFailedHasExactlyFixedKeys()
    {
        var result = _extractor.Extract(Array.Empty<byte>());
        Assert.Contains(result.Warnings, w => w.StartsWith("parse_failed", StringComparison.Ordinal));
        Assert.Equal(["bcc", "cc", "from", "to", "xfields"], result.Metadata.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.All(result.Metadata.Values, v => Assert.Equal("", v));
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
        var result = _extractor.Extract(Encoding.ASCII.GetBytes(raw.ReplaceLineEndings("\r\n")));
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

    [Fact]
    public void NullRequestThrows()
        => Assert.Throws<ArgumentNullException>(() => _extractor.Extract((EmlExtractionRequest)null!));

    [Fact]
    public void FileNameDoesNotAffectResult()
    {
        var bytes = SampleFactory.ToBytes(SampleFactory.Plain("name", "body"));
        var unnamed = _extractor.Extract(new EmlExtractionRequest { FileBytes = bytes });
        var named = _extractor.Extract(new EmlExtractionRequest { FileBytes = bytes, FileName = "x.eml" });
        Assert.Equal(ResultProjection.Of(unnamed), ResultProjection.Of(named));
    }

    [Fact]
    public void MboxFromPrefixWorksThroughRequest()
    {
        var inner = SampleFactory.ToBytes(SampleFactory.Plain("mbox-subject", "mbox-body"));
        var raw = Encoding.ASCII.GetBytes("From alice@example.com Tue Sep 15 07:00:00 2026\n")
            .Concat(inner)
            .ToArray();
        var result = _extractor.Extract(new EmlExtractionRequest { FileBytes = raw, FileName = "a.mbox" });
        Assert.Equal("mbox-subject", result.Metadata["subject"]);
        Assert.Equal("mbox-body", result.MainContent);
        Assert.DoesNotContain(result.Warnings, w => w.StartsWith("parse_failed"));
    }

    [Fact]
    public void PublicSurfaceExposesNoMimeKitType()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                                   | BindingFlags.DeclaredOnly;
        var leaks = new List<string>();
        foreach (var type in typeof(EmlExtractor).Assembly.GetExportedTypes())
        {
            var exposed = new List<(string Where, Type Type)>();
            if (type.BaseType is not null)
                exposed.Add(("base", type.BaseType));
            exposed.AddRange(type.GetInterfaces().Select(i => ("interface", i)));
            foreach (var member in type.GetMembers(flags))
            {
                switch (member)
                {
                    case MethodInfo m:
                        exposed.Add((m.Name, m.ReturnType));
                        exposed.AddRange(m.GetParameters().Select(p => (m.Name, p.ParameterType)));
                        break;
                    case ConstructorInfo c:
                        exposed.AddRange(c.GetParameters().Select(p => (".ctor", p.ParameterType)));
                        break;
                    case PropertyInfo p:
                        exposed.Add((p.Name, p.PropertyType));
                        break;
                    case FieldInfo f:
                        exposed.Add((f.Name, f.FieldType));
                        break;
                    case EventInfo e when e.EventHandlerType is not null:
                        exposed.Add((e.Name, e.EventHandlerType));
                        break;
                }
            }

            leaks.AddRange(exposed.Where(x => MentionsMimeKit(x.Type)).Select(x => $"{type.Name}.{x.Where}: {x.Type}"));
        }

        Assert.Empty(leaks);
    }

    private static bool MentionsMimeKit(Type type)
    {
        if (type.HasElementType)
            return MentionsMimeKit(type.GetElementType()!);
        if (type.IsGenericType && type.GetGenericArguments().Any(MentionsMimeKit))
            return true;
        return type.Assembly.GetName().Name is "MimeKit"
               || (type.Namespace?.StartsWith("MimeKit", StringComparison.Ordinal) ?? false);
    }
}

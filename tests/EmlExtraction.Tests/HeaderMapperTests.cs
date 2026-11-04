using System.Text;
using EmlExtraction.Logics;
using EmlExtraction.Models;

namespace EmlExtraction.Tests;

public sealed class HeaderMapperTests
{
    [Fact]
    public void EmptyMetadataHasFixedKeysWithEmptyValues()
    {
        var map = HeaderMapper.EmptyMetadata();

        Assert.Equal(
            new[] { MetadataKeys.Bcc, MetadataKeys.Cc, MetadataKeys.From, MetadataKeys.To, MetadataKeys.XFields },
            map.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(map.Values, v => Assert.Equal("", v));
    }

    private readonly CheckedExtractor _extractor = new();

    private IReadOnlyDictionary<string, string> Extract(string headerLine)
    {
        var raw = $"""
            {headerLine}
            Subject: enc
            Date: Tue, 15 Sep 2026 07:00:00 +0000
            Message-ID: <enc@example.com>
            MIME-Version: 1.0
            Content-Type: text/plain; charset=utf-8

            body
            """;
        return _extractor.Extract(Encoding.UTF8.GetBytes(raw.ReplaceLineEndings("\r\n"))).Metadata;
    }

    [Fact]
    public void UnderscoreReplyToFoldsIntoReplyToMetadata()
    {
        var map = Extract("Reply-To: a@x.org\nreply_to: b@y.com");

        Assert.Equal("a@x.org; b@y.com", map[MetadataKeys.ReplyTo]);
        Assert.False(map.ContainsKey("reply_to"));
    }

    [Fact]
    public void UnderscoreReplyToAloneSetsReplyToMetadata()
    {
        var map = Extract("REPLY_TO: b@y.com");

        Assert.Equal("b@y.com", map[MetadataKeys.ReplyTo]);
        Assert.False(map.ContainsKey("reply_to"));
    }

    [Fact]
    public void EnvelopeHeaderMetadataStaysRaw()
    {
        var map = Extract("Rcpt-To: A <a@x.org>\nrcpt_to: b@y.com\nMAILFROM: m@x.org");

        Assert.Equal("A <a@x.org>", map["rcpt-to"]);
        Assert.Equal("b@y.com", map["rcpt_to"]);
        Assert.Equal("m@x.org", map["mailfrom"]);
        Assert.False(map.ContainsKey(MetadataKeys.RcptTo));
    }

    [Fact]
    public void EncodedAddressOnlyDecodes()
        => Assert.Equal("john@x.org", Extract("Cc: =?utf-8?B?am9obkB4Lm9yZw==?=")[MetadataKeys.Cc]);

    [Fact]
    public void EncodedWholeMailboxDecodes()
        => Assert.Equal("Doe, John <john@x.org>", Extract("From: =?utf-8?B?IkRvZSwgSm9obiIgPGpvaG5AeC5vcmc+?=")[MetadataKeys.From]);

    [Fact]
    public void EncodedAddrSpecInsideAnglesDecodes()
        => Assert.Equal("Name <john@x.org>", Extract("To: Name <=?utf-8?B?am9obkB4Lm9yZw==?=>")[MetadataKeys.To]);

    [Fact]
    public void QEncodingDecodes()
        => Assert.Equal("john@x.org", Extract("Bcc: =?utf-8?Q?john=40x.org?=")[MetadataKeys.Bcc]);

    [Fact]
    public void NonUtf8CharsetDecodes()
        => Assert.Equal("José <jose@x.org>", Extract("To: =?iso-8859-1?Q?Jos=E9_=3Cjose=40x.org=3E?=")[MetadataKeys.To]);

    [Fact]
    public void ReplyToDecodes()
        => Assert.Equal("john@x.org", Extract("Reply-To: =?utf-8?B?am9obkB4Lm9yZw==?=")[MetadataKeys.ReplyTo]);

    [Fact]
    public void SenderDecodes()
        => Assert.Equal("john@x.org", Extract("Sender: =?utf-8?B?am9obkB4Lm9yZw==?=")[MetadataKeys.Sender]);

    [Fact]
    public void MalformedEncodedWordDoesNotThrow()
    {
        var value = Extract("To: =?utf-8?B?!!!bad?=")[MetadataKeys.To];
        Assert.False(string.IsNullOrEmpty(value));
    }

    // ADR-026 per-field guards, driven by the EmlExtractor.StageFault field points "metadata.<key>" and
    // "metadata.header.<raw key>": one failing field skips only its derived value and keeps the raw header value (owner decision 2026-11-04).

    private const string AllHeaders = """
        From: Alice <alice@example.com>
        To: Bob <bob@example.com>
        Cc: Carol <carol@example.com>
        Bcc: Dan <dan@example.com>
        Reply-To: Erin <erin@example.com>
        Sender: Frank <frank@example.com>
        Rcpt-To: <rcpt@example.com>
        Mail-From: <mf@example.com>
        Subject: Hello
        Date: Tue, 15 Sep 2026 07:00:00 +0000
        Message-ID: <m1@example.com>
        In-Reply-To: <p1@example.com>
        References: <r1@example.com>
        X_Trace: abc
        X-Custom: kept
        MIME-Version: 1.0
        Content-Type: multipart/mixed; boundary="b"

        --b
        Content-Type: text/plain; charset=utf-8

        body text
        --b
        Content-Type: application/octet-stream
        Content-Disposition: attachment; filename="data.bin"
        Content-Transfer-Encoding: base64

        AQIDBA==
        --b--

        """;

    private const string SinglePart = """
        From: Alice <alice@example.com>
        Subject: Hello
        MIME-Version: 1.0
        Content-Type: text/plain; charset=utf-8

        body text

        """;

    private static readonly string[] FixedKeys =
        [MetadataKeys.From, MetadataKeys.To, MetadataKeys.Cc, MetadataKeys.Bcc, MetadataKeys.XFields];

    public static TheoryData<string> GuardedKeys() =>
    [
        MetadataKeys.From, MetadataKeys.To, MetadataKeys.Cc, MetadataKeys.Bcc, MetadataKeys.ReplyTo,
        MetadataKeys.RcptTo, MetadataKeys.MailFrom, MetadataKeys.Sender, MetadataKeys.Subject, MetadataKeys.Date,
        MetadataKeys.MessageId, MetadataKeys.InReplyTo, MetadataKeys.References, MetadataKeys.XFields
    ];

    [Theory]
    [MemberData(nameof(GuardedKeys))]
    public void ContentFailureInOneFieldLosesOnlyThatField(string key)
    {
        var result = WithFieldFault("metadata." + key, new FormatException("SECRET"), AllHeaders);

        Assert.Equal($"metadata_field_failed: {key}: FormatException", Assert.Single(result.Warnings));
        AssertOnlyDerivedValueLost(key, Clean(AllHeaders), result);
    }

    [Theory]
    [MemberData(nameof(GuardedKeys))]
    public void BugInOneFieldIsInternalErrorAndLosesOnlyThatField(string key)
    {
        var result = WithFieldFault("metadata." + key, new InvalidCastException("SECRET"), AllHeaders);

        Assert.Equal($"internal_error: metadata.{key}: InvalidCastException", Assert.Single(result.Warnings));
        AssertOnlyDerivedValueLost(key, Clean(AllHeaders), result);
    }

    [Theory]
    [InlineData("x-custom", AllHeaders)]
    [InlineData("content-type", SinglePart)]
    public void RawHeaderFailureKeepsTheRawValueAndRecordsTheWarning(string key, string raw)
    {
        var clean = Clean(raw);
        Assert.True(clean.Metadata.ContainsKey(key));

        var content = WithFieldFault("metadata.header." + key, new FormatException("SECRET"), raw);
        var bug = WithFieldFault("metadata.header." + key, new InvalidCastException("SECRET"), raw);

        Assert.Equal($"metadata_field_failed: {key}: FormatException", Assert.Single(content.Warnings));
        Assert.Equal($"internal_error: metadata.{key}: InvalidCastException", Assert.Single(bug.Warnings));
        foreach (var result in new[] { content, bug })
        {
            Assert.Equal(clean.Metadata, result.Metadata);
            Assert.Equal(clean.MainContent, result.MainContent);
        }
    }

    // Owner decision 2026-11-04: a failed key keeps the raw header value already mapped; only the derived value is skipped.
    [Theory]
    [InlineData(MetadataKeys.Date, "Tue, 15 Sep 2026 07:00:00 +0000")]
    [InlineData(MetadataKeys.ReplyTo, "Erin <erin@example.com>")]
    [InlineData(MetadataKeys.From, "Alice <alice@example.com>")]
    [InlineData(MetadataKeys.Subject, "Hello")]
    public void FailedKeyKeepsRawHeaderValue(string key, string raw)
    {
        foreach (Exception ex in new Exception[] { new FormatException("x"), new InvalidCastException("x") })
        {
            var result = WithFieldFault("metadata." + key, ex, AllHeaders);
            Assert.Equal(raw, result.Metadata[key]);
            Assert.Single(result.Warnings);
        }
    }

    [Fact]
    public void FailedXHeaderKeepsItsLineInXFields()
    {
        var result = WithFieldFault("metadata.header.x_trace", new InvalidCastException("x"), AllHeaders);
        Assert.Equal("internal_error: metadata.x_trace: InvalidCastException", Assert.Single(result.Warnings));
        Assert.Equal("X_Trace: abc", result.Metadata[MetadataKeys.XFields]);
    }

    [Fact]
    public void ParticipantsAreEmptyAfterAFailedAddressKeyButRawStringIsKept()
    {
        var result = WithFieldFault("metadata.to", new FormatException("x"), AllHeaders);
        Assert.Equal("Bob <bob@example.com>", result.Metadata[MetadataKeys.To]);
        Assert.Empty(result.Participants[MetadataKeys.To]);
    }

    [Fact]
    public void DateFailureKeepsSubjectAddressesBodyAndAttachments()
    {
        var bytes = SampleFactory.ToBytes(DateMessage());
        var result = WithFieldFault("metadata.date", new FormatException("bad date"), bytes);

        Assert.Equal("metadata_field_failed: date: FormatException", Assert.Single(result.Warnings));
        Assert.False(string.IsNullOrEmpty(result.Metadata[MetadataKeys.Date])); // raw header value kept (owner decision 2026-11-04)
        Assert.Equal("Hello", result.Metadata[MetadataKeys.Subject]);
        Assert.Equal("alice@example.com", Assert.Single(result.Participants[MetadataKeys.From]).Address);
        Assert.Equal("bob@example.com", Assert.Single(result.Participants[MetadataKeys.To]).Address);
        Assert.Equal("body text", result.MainContent?.Trim());
        Assert.Equal("data.bin", Assert.Single(result.Attachments).FileName);
    }

    [Fact]
    public void FieldFailureWarningCarriesNoExceptionMessage()
    {
        var result = WithFieldFault("metadata.subject", new InvalidCastException("SECRET"), AllHeaders);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("SECRET", StringComparison.Ordinal));
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "The test injects a runtime-reserved OutOfMemoryException to prove it propagates.")]
    public void OutOfMemoryInAFieldPropagates()
    {
        var oom = new OutOfMemoryException("oom");
        var thrown = Assert.Throws<OutOfMemoryException>(() => WithFieldFault("metadata.subject", oom, AllHeaders));
        Assert.Same(oom, thrown);
    }

    [Fact]
    public void CleanMessageHasNoWarnings()
        => Assert.Empty(Clean(AllHeaders).Warnings);

    private static List<KeyValuePair<string, string>> Sorted(IReadOnlyDictionary<string, string> map, string except)
        => map.Where(p => p.Key != except).OrderBy(p => p.Key, StringComparer.Ordinal).ToList();

    /// <summary>
    /// The raw header value already mapped for the key stays (a fixed key with no raw value is ""; a key with none is absent);
    /// the participant list is empty; every other key, the body and the attachments are as in a clean run.
    /// </summary>
    private static void AssertOnlyDerivedValueLost(string key, EmlExtractionResult clean, EmlExtractionResult result)
    {
        var raw = RawHeaders(AllHeaders);
        if (key == MetadataKeys.XFields)
            Assert.Equal(clean.Metadata[key], result.Metadata[key]);
        else if (raw.TryGetValue(key, out var value))
            Assert.Equal(value, result.Metadata[key]);
        else if (FixedKeys.Contains(key))
            Assert.Equal("", result.Metadata[key]);
        else
            Assert.False(result.Metadata.ContainsKey(key));
        if (result.Participants.TryGetValue(key, out var list))
            Assert.Empty(list);

        Assert.Equal(Sorted(clean.Metadata, key), Sorted(result.Metadata, key));
        foreach (var (k, participants) in clean.Participants.Where(p => p.Key != key))
            Assert.Equal(participants, result.Participants[k]);
        Assert.Equal(clean.MainContent, result.MainContent);
        Assert.Equal(clean.Attachments.Select(a => a.FileName), result.Attachments.Select(a => a.FileName));
    }

    /// <summary>Single-line top-level headers of <paramref name="raw"/> by lower-cased name (the raw value the mapper stores).</summary>
    private static Dictionary<string, string> RawHeaders(string raw)
        => raw.ReplaceLineEndings("\n").Split('\n').TakeWhile(l => l.Length > 0)
            .Select(l => l.Split(": ", 2)).Where(p => p.Length == 2)
            .ToDictionary(p => p[0].ToLowerInvariant(), p => p[1], StringComparer.Ordinal);

    private static byte[] Bytes(string raw) => Encoding.UTF8.GetBytes(raw.ReplaceLineEndings("\r\n"));

    private static EmlExtractionResult Clean(string raw) => new EmlExtractor().Extract(Bytes(raw));

    private static EmlExtractionResult WithFieldFault(string point, Exception ex, string raw)
        => WithFieldFault(point, ex, Bytes(raw));

    private static EmlExtractionResult WithFieldFault(string point, Exception ex, byte[] bytes)
    {
        EmlExtractor.StageFault = s =>
        {
            if (s == point)
                throw ex;
        };
        try
        {
            return new EmlExtractor().Extract(bytes);
        }
        finally
        {
            EmlExtractor.StageFault = null;
        }
    }

    private static MimeKit.MimeMessage DateMessage()
    {
        var message = SampleFactory.Plain("Hello", "unused");
        var builder = new MimeKit.BodyBuilder { TextBody = "body text" };
        builder.Attachments.Add("data.bin", [1, 2, 3, 4], new MimeKit.ContentType("application", "octet-stream"));
        message.Body = builder.ToMessageBody();
        return message;
    }
}

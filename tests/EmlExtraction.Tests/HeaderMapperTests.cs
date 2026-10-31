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
}

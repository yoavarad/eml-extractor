using System.Text;
using EmlExtraction.Models;

namespace EmlExtraction.Tests;

/// <summary>Structured participants on <see cref="EmlExtractionResult.Participants"/> (ADR-025).</summary>
public sealed class ParticipantsTests
{
    private static readonly string[] ParticipantKeys =
        [MetadataKeys.From, MetadataKeys.To, MetadataKeys.Cc, MetadataKeys.Bcc, MetadataKeys.ReplyTo, MetadataKeys.Sender];

    private readonly CheckedExtractor _extractor = new();

    private EmlExtractionResult Extract(params string[] headerLines)
    {
        var raw = string.Join("\r\n", headerLines) + "\r\n" + """
            Subject: participants
            Date: Tue, 15 Sep 2026 07:00:00 +0000
            Message-ID: <participants@example.com>
            MIME-Version: 1.0
            Content-Type: text/plain; charset=utf-8

            body
            """.ReplaceLineEndings("\r\n");
        return _extractor.Extract(Encoding.UTF8.GetBytes(raw));
    }

    [Fact]
    public void EncodedNicknameDecodes()
    {
        var result = Extract("From: =?utf-8?B?15nXldeQ15E=?= <yoav@example.com>");

        Assert.Equal([new EmailParticipant("יואב", "yoav@example.com", "yoav", "example.com")], result.Participants[MetadataKeys.From]);
    }

    [Fact]
    public void ListKeepsOrderAndEmptyNickname()
    {
        var result = Extract("To: a@x.org, B <b@y.com>");

        Assert.Equal(
            [new EmailParticipant("", "a@x.org", "a", "x.org"), new EmailParticipant("B", "b@y.com", "b", "y.com")],
            result.Participants[MetadataKeys.To]);
    }

    [Fact]
    public void GroupMembersAreFlattened()
    {
        var result = Extract("To: Team: a@x.org, b@y.com;");

        Assert.Equal(
            [new EmailParticipant("", "a@x.org", "a", "x.org"), new EmailParticipant("", "b@y.com", "b", "y.com")],
            result.Participants[MetadataKeys.To]);
    }

    [Fact]
    public void AddressWithoutAtHasEmptyDomain()
    {
        var result = Extract("To: Local <postmaster>");

        Assert.Equal([new EmailParticipant("Local", "postmaster", "postmaster", "")], result.Participants[MetadataKeys.To]);
    }

    [Fact]
    public void SplitsAtLastAt()
    {
        var result = Extract("Cc: \"a@b\"@x.org");

        var participant = Assert.Single(result.Participants[MetadataKeys.Cc]);
        Assert.Equal("x.org", participant.Domain);
        Assert.Equal(participant.Address, participant.User + "@" + participant.Domain);
    }

    [Fact]
    public void EncodedWholeMailboxDecodes()
    {
        var result = Extract("From: =?utf-8?B?IkRvZSwgSm9obiIgPGpvaG5AeC5vcmc+?=");

        Assert.Equal([new EmailParticipant("Doe, John", "john@x.org", "john", "x.org")], result.Participants[MetadataKeys.From]);
    }

    [Fact]
    public void EncodedAddressOnlyDecodes()
    {
        var result = Extract("Cc: =?utf-8?B?am9obkB4Lm9yZw==?=");

        Assert.Equal([new EmailParticipant("", "john@x.org", "john", "x.org")], result.Participants[MetadataKeys.Cc]);
    }

    [Fact]
    public void ReplyToAndSenderAreCovered()
    {
        var result = Extract("From: a@x.org", "Reply-To: R <r@x.org>", "Sender: S <s@x.org>");

        Assert.Equal([new EmailParticipant("R", "r@x.org", "r", "x.org")], result.Participants[MetadataKeys.ReplyTo]);
        Assert.Equal([new EmailParticipant("S", "s@x.org", "s", "x.org")], result.Participants[MetadataKeys.Sender]);
    }

    [Fact]
    public void EncodedSenderDecodes()
    {
        var result = Extract("From: a@x.org", "Sender: =?utf-8?B?am9obkB4Lm9yZw==?=");

        Assert.Equal([new EmailParticipant("", "john@x.org", "john", "x.org")], result.Participants[MetadataKeys.Sender]);
        Assert.Equal("john@x.org", result.Metadata[MetadataKeys.Sender]);
    }

    [Fact]
    public void EncodedGroupMemberIsFlattened()
    {
        var result = Extract("To: Team: =?utf-8?B?am9obkB4Lm9yZw==?=, b@y.com;");

        Assert.Equal(
            [new EmailParticipant("", "john@x.org", "john", "x.org"), new EmailParticipant("", "b@y.com", "b", "y.com")],
            result.Participants[MetadataKeys.To]);
    }

    [Fact]
    public void DecodedTextWithoutMailboxGivesNoEntryButKeepsMetadata()
    {
        var result = Extract("To: =?utf-8?B?VW5kaXNjbG9zZWQgcmVjaXBpZW50czo7?=");

        Assert.Empty(result.Participants[MetadataKeys.To]);
        Assert.Equal("Undisclosed recipients:;", result.Metadata[MetadataKeys.To]);
    }

    [Fact]
    public void AllKeysPresentWithEmptyListsWhenHeadersAbsent()
    {
        var result = Extract("X_Note: none");

        Assert.Equal(ParticipantKeys.Order(StringComparer.Ordinal), result.Participants.Keys.Order(StringComparer.Ordinal));
        Assert.All(result.Participants.Values, Assert.Empty);
    }

    [Fact]
    public void AllKeysPresentWithEmptyListsOnParseFailed()
    {
        var result = _extractor.Extract(Array.Empty<byte>());

        Assert.Contains(result.Warnings, w => w.StartsWith("parse_failed", StringComparison.Ordinal));
        Assert.Equal(ParticipantKeys.Order(StringComparer.Ordinal), result.Participants.Keys.Order(StringComparer.Ordinal));
        Assert.All(result.Participants.Values, Assert.Empty);
    }

    [Fact]
    public void MetadataStringsAreUnchanged()
    {
        var result = Extract("From: =?utf-8?B?15nXldeQ15E=?= <yoav@example.com>", "To: a@x.org, B <b@y.com>");

        Assert.Equal("יואב <yoav@example.com>", result.Metadata[MetadataKeys.From]);
        Assert.Equal("a@x.org; B <b@y.com>", result.Metadata[MetadataKeys.To]);
    }
}

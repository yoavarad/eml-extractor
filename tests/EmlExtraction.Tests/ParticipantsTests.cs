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
    public void UndecodableTokenNextToValidMailboxIsKept()
    {
        var result = Extract("To: a@x.org, =?utf-8?B?!!bad?=");

        Assert.Equal("a@x.org; =?utf-8?B?!!bad?=", result.Metadata[MetadataKeys.To]);
        Assert.Equal(
            [new EmailParticipant("", "a@x.org", "a", "x.org"), Raw("=?utf-8?B?!!bad?=")],
            result.Participants[MetadataKeys.To]);
    }

    private const string Bad = "=?utf-8?B?!!bad?=";

    private static EmailParticipant Raw(string token) => new("", token, "", "");

    [Fact]
    public void UndecodableTokenAloneIsKeptAsRawParticipant()
    {
        var result = Extract($"To: {Bad}");

        Assert.Equal(Bad, result.Metadata[MetadataKeys.To]);
        Assert.Equal([Raw(Bad)], result.Participants[MetadataKeys.To]);
    }

    [Fact]
    public void DuplicateUndecodableTokensAreAllKept()
    {
        var result = Extract($"To: {Bad}, {Bad}");

        Assert.Equal($"{Bad}; {Bad}", result.Metadata[MetadataKeys.To]);
        Assert.Equal([Raw(Bad), Raw(Bad)], result.Participants[MetadataKeys.To]);
    }

    [Fact]
    public void UndecodableTokenKeepsItsPosition()
    {
        var result = Extract($"To: {Bad}, a@x.org, {Bad}, b@y.com");

        Assert.Equal($"{Bad}; a@x.org; {Bad}; b@y.com", result.Metadata[MetadataKeys.To]);
        Assert.Equal(
            [Raw(Bad), new EmailParticipant("", "a@x.org", "a", "x.org"), Raw(Bad), new EmailParticipant("", "b@y.com", "b", "y.com")],
            result.Participants[MetadataKeys.To]);
    }

    [Fact]
    public void UnquotedBadPhraseBeforeValidAddressKeepsTheAddressAndEncodedPhraseStaysIntact()
    {
        var result = Extract($"To: {Bad} <a@x.org>, =?utf-8?Q?Doe=2C_John?= <b@y.com>, {Bad}");

        Assert.Equal(["a@x.org", "b@y.com", Bad], result.Participants[MetadataKeys.To].Select(p => p.Address));
        Assert.Equal("Doe, John", result.Participants[MetadataKeys.To][1].Nickname);
    }

    [Fact]
    public void UndecodableMemberInsideGroupIsKeptBetweenValidMembers()
    {
        var result = Extract($"To: G: a@x.org, {Bad}, b@y.com, {Bad};");

        Assert.Equal(
            [new EmailParticipant("", "a@x.org", "a", "x.org"), Raw(Bad), new EmailParticipant("", "b@y.com", "b", "y.com"), Raw(Bad)],
            result.Participants[MetadataKeys.To]);
        Assert.Equal($"G: a@x.org; {Bad}; b@y.com; {Bad};", result.Metadata[MetadataKeys.To]);
    }

    [Fact]
    public void GroupWithOnlyUndecodableMemberKeepsIt()
    {
        var result = Extract($"To: G: {Bad};");

        Assert.Equal([Raw(Bad)], result.Participants[MetadataKeys.To]);
        Assert.Equal($"G: {Bad};", result.Metadata[MetadataKeys.To]);
    }

    [Fact]
    public void UndecodableTokenInGroupNameIsNotAParticipant()
    {
        var result = Extract($"To: {Bad}: a@x.org, {Bad};");

        Assert.Equal([new EmailParticipant("", "a@x.org", "a", "x.org"), Raw(Bad)], result.Participants[MetadataKeys.To]);
    }

    [Fact]
    public void GroupNextToTopLevelUndecodableItemKeepsOrder()
    {
        var result = Extract($"To: {Bad}, G: a@x.org, {Bad};, c@z.com, {Bad}");

        Assert.Equal(
            [Raw(Bad), new EmailParticipant("", "a@x.org", "a", "x.org"), Raw(Bad), new EmailParticipant("", "c@z.com", "c", "z.com"), Raw(Bad)],
            result.Participants[MetadataKeys.To]);
    }

    [Theory]
    [InlineData("G: a@x.org, {Bad}; (c)")]
    [InlineData("G: a@x.org, {Bad};   (c) ")]
    [InlineData("G: a@x.org, {Bad}; (c (nested))")]
    [InlineData("G: a@x.org,\r\n {Bad}; \r\n (c)")]
    [InlineData("G: a@x.org, {Bad} (c);")]
    [InlineData("G: a@x.org, {Bad} (d) ;")]
    public void GroupWithCommentsKeepsBadMemberAndNoCommentParticipant(string value)
    {
        var result = Extract($"To: {value.Replace("{Bad}", Bad)}");

        Assert.Equal([new EmailParticipant("", "a@x.org", "a", "x.org"), Raw(Bad)], result.Participants[MetadataKeys.To]);
    }

    [Fact]
    public void GroupWithTrailingCommentThenMoreItemsKeepsOrder()
    {
        var result = Extract($"To: G: a@x.org, {Bad}; (c), b@y.com, {Bad}");

        Assert.Equal(
            [new EmailParticipant("", "a@x.org", "a", "x.org"), Raw(Bad), new EmailParticipant("", "b@y.com", "b", "y.com"), Raw(Bad)],
            result.Participants[MetadataKeys.To]);
    }

    [Theory]
    [InlineData("G: a@x.org, {Bad}; (=?utf-8?B?!!bad?=)")]
    [InlineData("G: a@x.org, {Bad}; (=?utf-8?B?!!bad?=), b@y.com")]
    [InlineData("G: a@x.org, {Bad}; (c), (=?utf-8?B?!!bad?=)")]
    public void UndecodableTokenInCommentAfterGroupIsNotAParticipant(string value)
    {
        var result = Extract($"To: {value.Replace("{Bad}", Bad)}");

        Assert.Equal(["a@x.org", Bad], result.Participants[MetadataKeys.To].Take(2).Select(p => p.Address));
        Assert.DoesNotContain(result.Participants[MetadataKeys.To].Skip(2), p => p.Address.Contains("=?"));
        Assert.Equal(value.Contains("b@y.com") ? 3 : 2, result.Participants[MetadataKeys.To].Count);
    }

    [Fact]
    public void GroupWithTrailingCommentKeepsMetadataWithoutComment()
    {
        var result = Extract($"To: G: a@x.org, {Bad}; (c)");

        Assert.Equal($"G: a@x.org; {Bad};", result.Metadata[MetadataKeys.To]);
    }

    [Fact]
    public void EmptyGroupBesideEncodedWordStaysEmpty()
    {
        var result = Extract("To: G:;, =?utf-8?B?am9obkB4Lm9yZw==?=");

        Assert.Equal(["john@x.org"], result.Participants[MetadataKeys.To].Select(p => p.Address));
    }

    [Theory]
    [InlineData("a@x.org (=?utf-8?B?!!bad?=)")]
    [InlineData("\"=?utf-8?B?!!bad?=\" <a@x.org>")]
    public void UndecodableTokenInsideCommentOrQuotedNameIsNotAParticipant(string value)
    {
        var result = Extract($"To: {value}");

        Assert.Single(result.Participants[MetadataKeys.To]);
        Assert.Equal("a@x.org", result.Participants[MetadataKeys.To][0].Address);
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
    public void ParticipantListsCannotBeMutatedByCasting()
    {
        var result = Extract("From: a@x.org", "To: b@y.com, c@z.org", "Sender: S <s@x.org>");
        var intruder = new EmailParticipant("", "evil@x.org", "evil", "x.org");

        Assert.All(result.Participants.Values, list =>
        {
            Assert.IsNotType<List<EmailParticipant>>(list);
            Assert.IsNotType<EmailParticipant[]>(list);
            var mutable = Assert.IsAssignableFrom<IList<EmailParticipant>>(list);
            Assert.True(mutable.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => mutable.Add(intruder));
            Assert.Throws<NotSupportedException>(() => mutable[0] = intruder);
        });
    }

    [Fact]
    public void MetadataStringsAreUnchanged()
    {
        var result = Extract("From: =?utf-8?B?15nXldeQ15E=?= <yoav@example.com>", "To: a@x.org, B <b@y.com>");

        Assert.Equal("יואב <yoav@example.com>", result.Metadata[MetadataKeys.From]);
        Assert.Equal("a@x.org; B <b@y.com>", result.Metadata[MetadataKeys.To]);
    }
}

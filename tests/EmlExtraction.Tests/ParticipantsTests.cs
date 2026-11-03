using System.Text;
using EmlExtraction.Logics;
using EmlExtraction.Models;

namespace EmlExtraction.Tests;

/// <summary>Structured participants on <see cref="EmlExtractionResult.Participants"/> (ADR-025).</summary>
public sealed class ParticipantsTests
{
    private static readonly string[] ParticipantKeys =
        [MetadataKeys.From, MetadataKeys.To, MetadataKeys.Cc, MetadataKeys.Bcc, MetadataKeys.ReplyTo, MetadataKeys.Sender, MetadataKeys.RcptTo, MetadataKeys.MailFrom];

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
    public void EncodedNameBeforeBareAddrSpecIsTheNickname()
    {
        var result = Extract("To: =?utf-8?Q?A?= a@x.com, =?utf-8?Q?B?= b@x.com");

        Assert.Equal(
            [new EmailParticipant("A", "a@x.com", "a", "x.com"), new EmailParticipant("B", "b@x.com", "b", "x.com")],
            result.Participants[MetadataKeys.To]);
    }

    [Theory]
    [InlineData("A <a@x.com>; B <b@x.com>", "=?utf-8?Q?A?= <a@x.com>; =?utf-8?Q?B?= <b@x.com>")]
    [InlineData("a@x.org, B <b@y.com>; c@z.org, d@w.org", "a@x.org, =?utf-8?Q?B?= <b@y.com>; c@z.org, d@w.org")]
    public void StraySemicolonInEncodedListParsesLikeUnencoded(string plain, string encoded)
    {
        var expected = Extract($"To: {plain}");
        var actual = Extract($"To: {encoded}");

        Assert.NotEmpty(expected.Participants[MetadataKeys.To]);
        Assert.Equal(expected.Participants[MetadataKeys.To], actual.Participants[MetadataKeys.To]);
        Assert.Equal(expected.Metadata[MetadataKeys.To], actual.Metadata[MetadataKeys.To]);
    }

    [Theory]
    [InlineData("a@x.org; {Bad}", new[] { "a@x.org" })]
    [InlineData("{Bad}; a@x.org, b@y.com", new[] { Bad, "b@y.com" })]
    public void StraySemicolonNextToUndecodableTokenDoesNotThrow(string value, string[] addresses)
    {
        var result = Extract($"To: {value.Replace("{Bad}", Bad)}");

        Assert.Equal(addresses, result.Participants[MetadataKeys.To].Select(p => p.Address));
    }

    [Fact]
    public void CommaInsideEncodedWordDoesNotSplitTheItem()
    {
        var result = Extract("To: =?utf-8?Q?A=22,?= <a@x.com>, =?utf-8?Q?B?= <b@x.com>");

        Assert.Equal(
            [new EmailParticipant("A\",", "a@x.com", "a", "x.com"), new EmailParticipant("B", "b@x.com", "b", "x.com")],
            result.Participants[MetadataKeys.To]);
    }

    [Theory]
    [InlineData("{Bad} {Bad}")]
    [InlineData("{Bad}<")]
    [InlineData("=?utf-8?Q?A?= <>")]
    public void UnparseableEncodedItemThatIsNoAddrSpecIsKeptRaw(string value)
    {
        var item = value.Replace("{Bad}", Bad);
        var result = Extract($"To: {item}");

        Assert.Equal([Raw(item)], result.Participants[MetadataKeys.To]);
        Assert.Equal(item, result.Metadata[MetadataKeys.To]);
    }

    [Theory]
    [InlineData("rcptto", MetadataKeys.RcptTo)]
    [InlineData("RCPT-TO", MetadataKeys.RcptTo)]
    [InlineData("Rcpt_To", MetadataKeys.RcptTo)]
    [InlineData("mailfrom", MetadataKeys.MailFrom)]
    [InlineData("Mail-From", MetadataKeys.MailFrom)]
    [InlineData("MAIL_FROM", MetadataKeys.MailFrom)]
    public void EnvelopeHeaderVariantsAreParticipants(string name, string key)
    {
        var result = Extract($"{name}: A <a@x.org>, b@y.com");

        Assert.Equal(
            [new EmailParticipant("A", "a@x.org", "a", "x.org"), new EmailParticipant("", "b@y.com", "b", "y.com")],
            result.Participants[key]);
    }

    [Fact]
    public void EnvelopeHeaderOccurrencesAppendInHeaderOrder()
    {
        var result = Extract("rcpt-to: a@x.org", "RcptTo: Team: b@y.com;", "rcpt_to: c@z.org");

        Assert.Equal(["a@x.org", "b@y.com", "c@z.org"], result.Participants[MetadataKeys.RcptTo].Select(p => p.Address));
    }

    [Fact]
    public void EnvelopeHeaderKeepsUndecodableTokenAsRawParticipant()
    {
        var result = Extract($"mailfrom: a@x.org, {Bad}");

        Assert.Equal([new EmailParticipant("", "a@x.org", "a", "x.org"), Raw(Bad)], result.Participants[MetadataKeys.MailFrom]);
    }

    [Fact]
    public void EnvelopeHeaderDecodesEncodedNickname()
    {
        var result = Extract("MailFrom: =?utf-8?B?15nXldeQ15E=?= <yoav@example.com>");

        Assert.Equal([new EmailParticipant("יואב", "yoav@example.com", "yoav", "example.com")], result.Participants[MetadataKeys.MailFrom]);
    }

    [Fact]
    public void UnderscoreReplyToJoinsReplyToInHeaderOrder()
    {
        var result = Extract("reply_to: a@x.org", "Reply-To: B <b@y.com>", "REPLY_TO: c@z.org");

        Assert.Equal(["a@x.org", "b@y.com", "c@z.org"], result.Participants[MetadataKeys.ReplyTo].Select(p => p.Address));
    }

    [Fact]
    public void UnderscoreReplyToAloneIsReplyToParticipant()
    {
        var result = Extract("Reply_To: R <r@x.org>");

        Assert.Equal([new EmailParticipant("R", "r@x.org", "r", "x.org")], result.Participants[MetadataKeys.ReplyTo]);
    }

    public static TheoryData<string, string> SameAsToCases()
    {
        string[] names = ["rcptto", "Mail-From", "reply_to"];
        string[] values =
        [
            "Doe, John <john@x.org>",
            "a@x.org, <>, b@y.com",
            "a@x.org, @@@, b@y.com",
            "a@x.org, \"unterminated",
            "a@x.org, b@",
            "G: =?utf-8?B?am9obkB4Lm9yZw==?=, b@y.com;",
            "G: a@x.org, " + Bad + ", b@y.com;",
            Bad + ", G: =?utf-8?B?am9obkB4Lm9yZw==?=, " + Bad + ";, c@z.com",
            "=?utf-8?Q?A?= a@x.com, =?utf-8?Q?A=22,?= <b@x.com>; =?utf-8?Q?C?= <c@x.com>, d@x.com",
        ];
        var data = new TheoryData<string, string>();
        foreach (var name in names)
            foreach (var value in values)
                data.Add(name, value);
        return data;
    }

    [Theory]
    [MemberData(nameof(SameAsToCases))]
    public void EnvelopeAndUnderscoreReplyToParseLikeTo(string name, string value)
    {
        var key = name switch { "rcptto" => MetadataKeys.RcptTo, "reply_to" => MetadataKeys.ReplyTo, _ => MetadataKeys.MailFrom };
        var expected = Extract($"To: {value}");
        var actual = Extract($"{name}: {value}");

        Assert.NotEmpty(expected.Participants[MetadataKeys.To]);
        Assert.Equal(expected.Participants[MetadataKeys.To], actual.Participants[key]);
        if (key == MetadataKeys.ReplyTo)
            Assert.Equal(expected.Metadata[MetadataKeys.To], actual.Metadata[MetadataKeys.ReplyTo]);
    }

    [Theory]
    [InlineData("Doe, John <john@x.org>")]
    [InlineData("a@x.org, <>, b@y.com")]
    [InlineData("=?utf-8?B?15nXldeQ15E=?= <yoav@example.com>, =?utf-8?B?!!bad?=")]
    public void ReplyToParticipantsUnchangedByUnderscoreReplyTo(string value)
    {
        var alone = Extract($"Reply-To: {value}").Participants[MetadataKeys.ReplyTo];
        var before = Extract("reply_to: c@z.org", $"Reply-To: {value}").Participants[MetadataKeys.ReplyTo];
        var after = Extract($"Reply-To: {value}", "reply_to: c@z.org").Participants[MetadataKeys.ReplyTo];

        Assert.NotEmpty(alone);
        Assert.Equal(alone, after.Take(alone.Count));
        Assert.Equal("c@z.org", after[^1].Address);
        Assert.Equal(alone.Count + 1, after.Count);
        Assert.Equal("c@z.org", before[0].Address);
        Assert.Equal(alone, before.Skip(1));
    }

    [Theory]
    [InlineData("rcptto", MetadataKeys.RcptTo)]
    [InlineData("mail_from", MetadataKeys.MailFrom)]
    [InlineData("reply_to", MetadataKeys.ReplyTo)]
    public void NameMatchedHeadersUseTheExtractorParserOptions(string name, string key)
    {
        // ParserOptions.Default keeps "Doe, John" as one display name; without unquoted commas it splits in two.
        var options = MimeKit.ParserOptions.Default.Clone();
        options.AllowUnquotedCommasInAddresses = false;
        var extractor = new EmlExtractor(EmlExtractor.DefaultMaxInputBytes, options);
        var raw = $"To: Doe, John <john@x.org>\r\n{name}: Doe, John <john@x.org>\r\nSubject: s\r\n\r\nbody\r\n";
        var result = extractor.Extract(Encoding.UTF8.GetBytes(raw));

        EmailParticipant[] expected = [new("", "Doe", "Doe", ""), new("John", "john@x.org", "john", "x.org")];
        Assert.Equal(expected, result.Participants[MetadataKeys.To]);
        Assert.Equal(expected, result.Participants[key]);
    }

    [Fact]
    public void EncodedReplyToAndUnderscoreReplyToMerge()
    {
        string[] lines = ["=?utf-8?B?15nXldeQ15E=?= <yoav@example.com>", $"a@x.org, {Bad}", "G: =?utf-8?B?am9obkB4Lm9yZw==?=;"];
        var expected = Extract(lines.Select(l => $"To: {l}").ToArray());
        var actual = Extract($"Reply-To: {lines[0]}", $"REPLY_TO: {lines[1]}", $"Reply-To: {lines[2]}");

        Assert.Equal(4, expected.Participants[MetadataKeys.To].Count);
        Assert.Equal(expected.Participants[MetadataKeys.To], actual.Participants[MetadataKeys.ReplyTo]);
        Assert.Equal(expected.Metadata[MetadataKeys.To], actual.Metadata[MetadataKeys.ReplyTo]);
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
    public void ParseFailedMetadataHasNoUnderscoreReplyToKey()
    {
        var result = _extractor.Extract(Array.Empty<byte>());

        Assert.Contains(result.Warnings, w => w.StartsWith("parse_failed", StringComparison.Ordinal));
        Assert.False(result.Metadata.ContainsKey("reply_to"));
        Assert.Empty(result.Participants[MetadataKeys.ReplyTo]);
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

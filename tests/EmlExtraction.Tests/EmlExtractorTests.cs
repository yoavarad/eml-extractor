using EmlExtraction.Logics;
using EmlExtraction.Models;

namespace EmlExtraction.Tests;

/// <summary>Result assembly in <see cref="EmlExtractor"/>: participants on success and on parse_failed.</summary>
public sealed class EmlExtractorTests
{
    private static readonly string[] ParticipantKeys =
        [MetadataKeys.From, MetadataKeys.To, MetadataKeys.Cc, MetadataKeys.Bcc, MetadataKeys.ReplyTo, MetadataKeys.Sender];

    [Fact]
    public void ParseFailedResultCarriesAllParticipantKeysEmpty()
    {
        var result = new EmlExtractor().Extract(Array.Empty<byte>());

        Assert.Equal(ParticipantKeys.Order(StringComparer.Ordinal), result.Participants.Keys.Order(StringComparer.Ordinal));
        Assert.All(result.Participants.Values, Assert.Empty);
    }

    [Fact]
    public void ParsedResultCarriesParticipantsFromHeaderMapper()
    {
        var raw = "From: A <a@x.org>\r\nSubject: s\r\n\r\nbody\r\n"u8.ToArray();

        var result = new EmlExtractor().Extract(raw);

        Assert.Equal([new EmailParticipant("A", "a@x.org", "a", "x.org")], result.Participants[MetadataKeys.From]);
    }
}

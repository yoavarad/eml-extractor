using EmlExtraction.Models;

namespace EmlExtraction.Tests;

public sealed class EmlExtractionResultTests
{
    [Fact]
    public void ParticipantsDefaultsToEmptyWhenNotSet()
    {
        var result = new EmlExtractionResult(new Dictionary<string, string>(), null, null, [], []);

        Assert.Empty(result.Participants);
    }

    [Fact]
    public void ParticipantsIsInitOnlyAndKeepsTheGivenValue()
    {
        var participants = new Dictionary<string, IReadOnlyList<EmailParticipant>>
        {
            [MetadataKeys.From] = [new EmailParticipant("", "a@x.org", "a", "x.org")]
        };

        var result = new EmlExtractionResult(new Dictionary<string, string>(), null, null, [], []) { Participants = participants };

        Assert.Same(participants, result.Participants);
    }
}

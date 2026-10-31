using EmlExtraction.Models;

namespace EmlExtraction.Tests;

public sealed class EmailParticipantTests
{
    [Fact]
    public void EqualFieldsMeanEqualParticipants()
        => Assert.Equal(new EmailParticipant("N", "u@d.org", "u", "d.org"), new EmailParticipant("N", "u@d.org", "u", "d.org"));

    [Fact]
    public void DifferentNicknameMeansDifferentParticipant()
        => Assert.NotEqual(new EmailParticipant("A", "u@d.org", "u", "d.org"), new EmailParticipant("B", "u@d.org", "u", "d.org"));
}

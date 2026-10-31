using EmlExtraction.Models;

namespace EmlExtraction.Tests;

/// <summary>Pins the public metadata key constants to their wire values.</summary>
public sealed class MetadataKeysTests
{
    [Theory]
    [InlineData(MetadataKeys.From, "from")]
    [InlineData(MetadataKeys.To, "to")]
    [InlineData(MetadataKeys.Cc, "cc")]
    [InlineData(MetadataKeys.Bcc, "bcc")]
    [InlineData(MetadataKeys.ReplyTo, "reply-to")]
    [InlineData(MetadataKeys.Sender, "sender")]
    [InlineData(MetadataKeys.Subject, "subject")]
    [InlineData(MetadataKeys.Date, "date")]
    [InlineData(MetadataKeys.MessageId, "message-id")]
    [InlineData(MetadataKeys.InReplyTo, "in-reply-to")]
    [InlineData(MetadataKeys.References, "references")]
    [InlineData(MetadataKeys.XFields, "xfields")]
    public void ConstantHasExpectedValue(string actual, string expected) => Assert.Equal(expected, actual);
}

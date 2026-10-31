using EmlExtraction.Models;
using GoldenKit;
using MimeKit;

namespace EmlExtraction.Tests;

public sealed class MimeBuildTests
{
    [Fact]
    public void FromMessageDefaultsFromAndToExpectationsUnderMetadataKeys()
    {
        var message = MimeBuild.BaseMessage("T001", "subj");
        message.Body = new TextPart("plain") { Text = "body" };

        var c = MimeBuild.FromMessage("T001", ["t"], message, new GoldenExpect());

        Assert.Equal(["alice@example.com"], c.Expect.Metadata[MetadataKeys.From].Contains);
        Assert.Equal(["bob@example.com"], c.Expect.Metadata[MetadataKeys.To].Contains);
    }

    [Fact]
    public void FromMessageKeepsExplicitExpectation()
    {
        var message = MimeBuild.BaseMessage("T002", "subj");
        var expect = new GoldenExpect();
        expect.Metadata[MetadataKeys.From] = MimeBuild.Contains("other@example.com");

        var c = MimeBuild.FromMessage("T002", ["t"], message, expect);

        Assert.Equal(["other@example.com"], c.Expect.Metadata[MetadataKeys.From].Contains);
        Assert.Equal(["bob@example.com"], c.Expect.Metadata[MetadataKeys.To].Contains);
    }

    [Fact]
    public void MetaBuildsSubjectExpectationUnderMetadataKey()
    {
        var meta = MimeBuild.Meta("hello");

        Assert.Equal("hello", meta[MetadataKeys.Subject].Eq);
    }
}

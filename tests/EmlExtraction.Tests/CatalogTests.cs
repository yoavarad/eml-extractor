using EmlExtraction.Models;
using GoldenKit;

namespace EmlExtraction.Tests;

public sealed class CatalogTests
{
    private static readonly List<CaseDef> All = Catalog.All();

    [Fact]
    public void ClassicHeaderCaseExpectsHeaderKeys()
    {
        var h030 = All.Single(c => c.Id == "H030").Expect.Metadata;

        Assert.Equal("cc", MetadataKeys.Cc);
        Assert.Equal("reply-to", MetadataKeys.ReplyTo);
        Assert.Equal("sender", MetadataKeys.Sender);
        Assert.Contains(MetadataKeys.Cc, h030.Keys);
        Assert.Contains(MetadataKeys.ReplyTo, h030.Keys);
        Assert.Contains(MetadataKeys.Sender, h030.Keys);
        Assert.DoesNotContain(MetadataKeys.Bcc, h030.Keys);
    }

    [Fact]
    public void CaseIdsAreUniqueAndCountMatchesExpected()
    {
        Assert.Equal(Catalog.ExpectedCount, All.Count);
        Assert.Equal(All.Count, All.Select(c => c.Id).Distinct().Count());
    }
}

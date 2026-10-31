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
}

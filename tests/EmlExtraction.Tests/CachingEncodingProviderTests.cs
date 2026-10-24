using System.Text;
using System.Text.RegularExpressions;
using EmlExtraction.Logics;

namespace EmlExtraction.Tests;

public sealed partial class CachingEncodingProviderTests
{
    private static readonly EncodingProvider Reference = CodePagesEncodingProvider.Instance;

    [Fact]
    public void SameInstanceAsCodePagesProviderForEveryCodePage()
    {
        var provider = new CachingEncodingProvider(Reference);
        foreach (var info in Reference.GetEncodings())
        {
            var expected = Reference.GetEncoding(info.CodePage);
            Assert.NotNull(expected);
            Assert.Same(expected, provider.GetEncoding(info.CodePage));
            Assert.Same(expected, provider.GetEncoding(info.CodePage));
        }
    }

    [Fact]
    public void SameInstanceAsCodePagesProviderForEveryName()
    {
        var provider = new CachingEncodingProvider(Reference);
        foreach (var info in Reference.GetEncodings())
        {
            var expected = Reference.GetEncoding(info.Name);
            Assert.NotNull(expected);
            Assert.Same(expected, provider.GetEncoding(info.Name));
            Assert.Same(expected, provider.GetEncoding(info.Name));
        }
    }

    [Fact]
    public void SameInstanceForEveryCharsetNameInTheGoldenSet()
    {
        var provider = new CachingEncodingProvider(Reference);
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(Path.Combine(FindRepoRoot(), "corpus", "golden"), "input.eml", SearchOption.AllDirectories))
        {
            foreach (Match m in CharsetRegex().Matches(Encoding.Latin1.GetString(File.ReadAllBytes(path))))
                names.Add(m.Groups[1].Value);
        }

        Assert.NotEmpty(names);
        var nonNull = 0;
        foreach (var name in names)
        {
            var expected = Reference.GetEncoding(name);
            if (expected is not null)
                nonNull++;
            Assert.Same(expected, provider.GetEncoding(name));
            Assert.Same(expected, provider.GetEncoding(name));
        }

        Assert.True(nonNull > 0, "no golden charset resolved through the code-pages provider");
    }

    [Fact]
    public void NameLookupIsCaseInsensitiveLikeTheCodePagesProvider()
    {
        var provider = new CachingEncodingProvider(Reference);
        Assert.Same(Reference.GetEncoding("windows-1255"), provider.GetEncoding("WINDOWS-1255"));
        Assert.Same(Reference.GetEncoding("windows-1255"), provider.GetEncoding("Windows-1255"));
    }

    [Fact]
    public void UnknownNameAndCodePageReturnNullAndAreCached()
    {
        var counting = new CountingProvider(null);
        var provider = new CachingEncodingProvider(counting);
        Assert.Null(provider.GetEncoding("no-such-charset"));
        Assert.Null(provider.GetEncoding("no-such-charset"));
        Assert.Null(provider.GetEncoding(-12345));
        Assert.Null(provider.GetEncoding(-12345));
        Assert.Equal(2, counting.Calls);
    }

    [Fact]
    public void UnknownCacheIsBoundedButKnownNamesAreAlwaysCached()
    {
        var counting = new CountingProvider(null);
        var provider = new CachingEncodingProvider(counting);
        const int max = CachingEncodingProvider.MaxCachedUnknown;
        const int junk = max + 500;
        for (var i = 0; i < junk; i++)
        {
            Assert.Null(provider.GetEncoding("bogus-" + i));
            Assert.Null(provider.GetEncoding(-1 - i));
        }

        Assert.Equal(2 * junk, counting.Calls);
        for (var i = 0; i < junk; i++)
        {
            Assert.Null(provider.GetEncoding("bogus-" + i));
            Assert.Null(provider.GetEncoding(-1 - i));
        }

        // Only `max` unknown results (shared by name and code page) were cached; the rest asked the inner provider again.
        Assert.Equal((4 * junk) - max, counting.Calls);

        // Known results are still cached after the unknown bound is hit.
        counting.Result = Encoding.ASCII;
        var before = counting.Calls;
        Assert.Same(Encoding.ASCII, provider.GetEncoding("Windows-1255"));
        Assert.Same(Encoding.ASCII, provider.GetEncoding("windows-1255"));
        Assert.Same(Encoding.ASCII, provider.GetEncoding(1255));
        Assert.Same(Encoding.ASCII, provider.GetEncoding(1255));
        Assert.Equal(before + 2, counting.Calls);
    }

    [Fact]
    public void FallbackOverloadsMatchTheCodePagesProviderAndSkipItsLookups()
    {
        var provider = new CachingEncodingProvider(Reference);
        var byName = provider.GetEncoding("windows-1255", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var byPage = provider.GetEncoding(1255, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        var expected = Reference.GetEncoding("windows-1255", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        Assert.NotNull(byName);
        Assert.NotNull(byPage);
        Assert.Equal(expected!.CodePage, byName!.CodePage);
        Assert.Equal(expected.GetType(), byName.GetType());
        Assert.IsType<EncoderExceptionFallback>(byName.EncoderFallback);
        Assert.IsType<DecoderExceptionFallback>(byName.DecoderFallback);
        Assert.IsType<DecoderReplacementFallback>(byPage!.DecoderFallback);
        Assert.Null(provider.GetEncoding("no-such-charset", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback));
        Assert.Equal(Reference.GetEncodings().Count(), provider.GetEncodings().Count());

        var counting = new CountingProvider(Encoding.ASCII);
        var counted = new CachingEncodingProvider(counting);
        for (var i = 0; i < 5; i++)
        {
            Assert.NotNull(counted.GetEncoding("x", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback));
            Assert.NotNull(counted.GetEncoding(7, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback));
        }

        Assert.Equal(2, counting.Calls);
    }

    [Fact]
    public void ThreadSafeAndConsistentUnderParallelFor()
    {
        var counting = new CountingProvider(Encoding.ASCII);
        var provider = new CachingEncodingProvider(counting);
        const int threads = 16;
        using var barrier = new Barrier(threads);
        var results = new Encoding?[threads][];
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, t =>
        {
            results[t] = new Encoding?[2000];
            barrier.SignalAndWait();
            for (var j = 0; j < 2000; j++)
                results[t][j] = provider.GetEncoding("windows-1255");
        });

        foreach (var row in results)
            foreach (var r in row)
                Assert.Same(Encoding.ASCII, r);

        // Racing first misses may each ask the inner provider, but never more than once per thread.
        Assert.InRange(counting.Calls, 1, threads);
        Assert.Same(Encoding.ASCII, provider.GetEncoding("windows-1255"));
        Assert.InRange(counting.Calls, 1, threads);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "corpus", "golden")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("corpus/golden not found");
    }

    [GeneratedRegex("""charset\s*=\s*"?([A-Za-z0-9_\-:.]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CharsetRegex();

    private sealed class CountingProvider(Encoding? result) : EncodingProvider
    {
        private int calls;

        public Encoding? Result { get; set; } = result;

        public int Calls => Volatile.Read(ref calls);

        public override Encoding? GetEncoding(int codepage)
        {
            Interlocked.Increment(ref calls);
            return Result;
        }

        public override Encoding? GetEncoding(string name)
        {
            Interlocked.Increment(ref calls);
            return Result;
        }
    }
}

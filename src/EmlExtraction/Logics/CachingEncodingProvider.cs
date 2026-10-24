using System.Collections.Concurrent;
using System.Text;

namespace EmlExtraction.Logics;

/// <summary>
/// Wraps <see cref="CodePagesEncodingProvider"/> and caches its lookups (including null for unknown names).
/// Once any provider is registered, every <see cref="Encoding.GetEncoding(string)"/> call asks it first, and the
/// code-pages provider guards its own cache with an upgradeable read lock that only one thread can hold, so at high
/// DOP the lookups serialize. This provider returns the same <see cref="Encoding"/> objects, so decoding is unchanged.
/// </summary>
internal sealed class CachingEncodingProvider : EncodingProvider
{
    /// <summary>Bound on cached unknown (null) results, so attacker-supplied charset names cannot grow the cache without limit. Known encodings are a finite set and are always cached.</summary>
    internal const int MaxCachedUnknown = 1024;

    private readonly EncodingProvider inner;
    private readonly ConcurrentDictionary<int, Encoding?> byCodePage = new();
    private readonly ConcurrentDictionary<string, Encoding?> byName = new(StringComparer.OrdinalIgnoreCase);
    private int unknownCount;

    internal CachingEncodingProvider(EncodingProvider inner) => this.inner = inner;

    public static CachingEncodingProvider Instance { get; } = new(CodePagesEncodingProvider.Instance);

    public override Encoding? GetEncoding(int codepage)
    {
        if (byCodePage.TryGetValue(codepage, out var cached))
            return cached;
        var found = inner.GetEncoding(codepage);
        if (found is not null || Interlocked.Increment(ref unknownCount) <= MaxCachedUnknown)
            byCodePage.TryAdd(codepage, found);
        return found;
    }

    public override Encoding? GetEncoding(string name)
    {
        if (byName.TryGetValue(name, out var cached))
            return cached;
        var found = inner.GetEncoding(name);
        if (found is not null || Interlocked.Increment(ref unknownCount) <= MaxCachedUnknown)
            byName.TryAdd(name, found);
        return found;
    }

    // The fallback overloads are deliberately not overridden: the base implementation clones the encoding returned by
    // the cached overloads above and sets the fallbacks, so MimeKit's fallback lookups skip the code-pages lock too.

    public override IEnumerable<EncodingInfo> GetEncodings() => inner.GetEncodings();
}

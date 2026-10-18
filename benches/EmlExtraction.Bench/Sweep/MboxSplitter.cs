namespace EmlExtraction.Bench.Sweep;

/// <summary>Splits an mbox file into per-message byte arrays. Linked into the test project.</summary>
internal static class MboxSplitter
{
    private static ReadOnlySpan<byte> From => "From "u8;

    /// <summary>True for <c>.mbox</c>/<c>.mbx</c> files or content that starts with an mbox <c>From </c> line.</summary>
    public static bool IsMbox(string path, ReadOnlySpan<byte> data)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".mbox", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mbx", StringComparison.OrdinalIgnoreCase)
            || data.StartsWith(From);
    }

    /// <summary>
    /// A separator is a <c>From </c> line at the start of the file or after a blank line.
    /// The separator line itself is dropped; empty messages are skipped. No separator: the whole input is one message.
    /// </summary>
    public static List<byte[]> Split(byte[] data)
    {
        var starts = new List<int>();
        for (var i = 0; i < data.Length; i++)
        {
            if (IsSeparator(data, i))
                starts.Add(i);
        }

        if (starts.Count == 0)
            return [data];

        var messages = new List<byte[]>(starts.Count);
        for (var k = 0; k < starts.Count; k++)
        {
            var end = k + 1 < starts.Count ? starts[k + 1] : data.Length;
            var newline = Array.IndexOf(data, (byte)'\n', starts[k], end - starts[k]);
            var bodyStart = newline < 0 ? end : newline + 1;
            if (bodyStart < end)
                messages.Add(data[bodyStart..end]);
        }

        return messages;
    }

    private static bool IsSeparator(byte[] data, int i)
    {
        if (!data.AsSpan(i).StartsWith(From))
            return false;
        if (i == 0)
            return true;
        if (i >= 2 && data[i - 1] == '\n' && data[i - 2] == '\n')
            return true;
        return i >= 3 && data[i - 1] == '\n' && data[i - 2] == '\r' && data[i - 3] == '\n';
    }
}

using System.Text;
using EmlExtraction.Logics;

namespace EmlExtraction.Tests;

/// <summary>Parse stage body (<see cref="MimeMessageParser"/>): plain, mbox "From " and BOM-prefixed mbox inputs.</summary>
public sealed class MimeMessageParserTests
{
    private const string Eml = "From: Alice <alice@example.com>\r\nTo: Bob <bob@example.com>\r\nSubject: hello\r\n\r\nbody text\r\n";

    [Fact]
    public void ParsesPlainMessage() => AssertParsed(Encoding.ASCII.GetBytes(Eml));

    [Fact]
    public void ParsesMboxFromLine() => AssertParsed(Encoding.ASCII.GetBytes("From alice@example.com Mon Sep 15 07:00:00 2026\r\n" + Eml));

    [Fact]
    public void ParsesBomPrefixedMboxFromLine()
        => AssertParsed([0xEF, 0xBB, 0xBF, .. Encoding.ASCII.GetBytes("From alice@example.com Mon Sep 15 07:00:00 2026\r\n" + Eml)]);

    [Fact]
    public void MboxParseStopsAtNextFromLine()
    {
        const string from = "From alice@example.com Mon Sep 15 07:00:00 2026\r\n";
        var bytes = Encoding.ASCII.GetBytes(from + Eml + from + Eml.Replace("hello", "second", StringComparison.Ordinal));
        using var input = MimeMessageParser.Open(bytes);

        var message = new MimeMessageParser().Parse(input, bytes);

        Assert.Equal("hello", message.Subject);
        Assert.Equal("body text", message.TextBody?.TrimEnd('\r', '\n'));
    }

    private const string UnquotedComma = "From: a@example.com\r\nTo: Doe, John <j@example.com>\r\nSubject: s\r\n\r\nb\r\n";

    private static MimeKit.ParserOptions StrictOptions()
    {
        var options = MimeKit.ParserOptions.Default.Clone();
        options.AllowUnquotedCommasInAddresses = false;
        return options;
    }

    [Fact]
    public void DefaultOptionsAllowUnquotedCommasInAddresses()
    {
        var bytes = Encoding.ASCII.GetBytes(UnquotedComma);
        using var input = MimeMessageParser.Open(bytes);

        var message = new MimeMessageParser().Parse(input, bytes);

        Assert.Equal("Doe, John", message.To.Mailboxes.Single().Name);
    }

    [Fact]
    public void SuppliedOptionsReplaceDefaultsAndAreCloned()
    {
        var bytes = Encoding.ASCII.GetBytes(UnquotedComma);
        var options = StrictOptions();
        var parser = new MimeMessageParser(options);
        options.AllowUnquotedCommasInAddresses = true; // after construction: must not affect the parser
        using var input = MimeMessageParser.Open(bytes);

        var message = parser.Parse(input, bytes);

        Assert.NotEqual("Doe, John", message.To.Mailboxes.First().Name);
    }

    [Fact]
    public void ExtractorAcceptsParserOptions()
    {
        var bytes = Encoding.ASCII.GetBytes(UnquotedComma);

        var lenient = new EmlExtractor().Extract(bytes);
        var strict = new EmlExtractor(EmlExtractor.DefaultMaxInputBytes, StrictOptions()).Extract(bytes);

        Assert.Contains("Doe, John", lenient.Metadata["to"]);
        Assert.NotEqual(lenient.Metadata["to"], strict.Metadata["to"]);
    }

    [Fact]
    public void OpenWrapsWholeArrayWithoutCopy()
    {
        var bytes = Encoding.ASCII.GetBytes(Eml);
        using var stream = MimeMessageParser.Open(bytes);

        bytes[0] = (byte)'X';

        Assert.False(stream.CanWrite);
        Assert.Equal('X', stream.ReadByte());
    }

    [Fact]
    public void OpenCopiesSlice()
    {
        var bytes = Encoding.ASCII.GetBytes("xx" + Eml);
        using var stream = MimeMessageParser.Open(bytes.AsMemory(2));

        Assert.False(stream.CanWrite);
        Assert.Equal(bytes.Length - 2, stream.Length);
    }

    private static void AssertParsed(byte[] bytes)
    {
        using var input = MimeMessageParser.Open(bytes);
        var message = new MimeMessageParser().Parse(input, bytes);

        Assert.Equal("hello", message.Subject);
        Assert.Equal("alice@example.com", Assert.Single(message.From.Mailboxes).Address);
        Assert.Equal("body text", message.TextBody?.TrimEnd('\r', '\n'));
    }
}

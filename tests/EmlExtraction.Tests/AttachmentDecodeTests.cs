using System.Text;
using EmlExtraction.Abstractions;
using EmlExtraction.Models;

namespace EmlExtraction.Tests;

/// <summary>Pins the decoded attachment bytes and length per transfer encoding (T-opt-attach characterization).</summary>
public sealed class AttachmentDecodeTests
{
    private readonly CheckedExtractor _extractor = new();

    private static byte[] Eml(string encoding, string body, string type = "application/octet-stream") =>
        Encoding.ASCII.GetBytes(
            "From: a@example.com\r\nTo: b@example.com\r\nSubject: s\r\nMIME-Version: 1.0\r\n" +
            "Content-Type: multipart/mixed; boundary=\"b1\"\r\n\r\n" +
            "--b1\r\nContent-Type: text/plain\r\n\r\nhello\r\n" +
            "--b1\r\nContent-Type: " + type + "; name=\"f.bin\"\r\n" +
            "Content-Disposition: attachment; filename=\"f.bin\"\r\n" +
            "Content-Transfer-Encoding: " + encoding + "\r\n\r\n" + body + "\r\n--b1--\r\n");

    private ExtractedAttachment Only(byte[] eml, out IReadOnlyList<string> warnings)
    {
        var result = _extractor.Extract(eml);
        warnings = result.Warnings;
        return Assert.Single(result.Attachments);
    }

    [Fact]
    public void Base64DecodesToExactBytes()
    {
        var payload = Enumerable.Range(0, 5000).Select(i => (byte)(i * 31)).ToArray();
        var b64 = Convert.ToBase64String(payload, Base64FormattingOptions.InsertLineBreaks).Replace("\n", "\r\n");
        var att = Only(Eml("base64", b64), out var warnings);
        Assert.Equal(payload, att.BinaryData);
        Assert.Equal(payload.Length, att.SizeInBytes);
        Assert.Empty(warnings);
    }

    [Fact]
    public void QuotedPrintableDecodesToExactBytes()
    {
        var att = Only(Eml("quoted-printable", "caf=C3=A9 soft=\r\nbreak =41\r\nline2", "text/csv"), out var warnings);
        var expected = Encoding.UTF8.GetBytes("café softbreak A\r\nline2");
        Assert.Equal(expected, att.BinaryData);
        Assert.Equal(expected.Length, att.SizeInBytes);
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("7bit")]
    [InlineData("8bit")]
    [InlineData("binary")]
    public void IdentityEncodingsKeepBytes(string encoding)
    {
        var att = Only(Eml(encoding, "line one\r\nline two"), out var warnings);
        var expected = Encoding.ASCII.GetBytes("line one\r\nline two");
        Assert.Equal(expected, att.BinaryData);
        Assert.Equal(expected.Length, att.SizeInBytes);
        Assert.Empty(warnings);
    }

    [Fact]
    public void EmptyPartYieldsEmptyArray()
    {
        var att = Only(Eml("base64", ""), out var warnings);
        Assert.Empty(att.BinaryData);
        Assert.Equal(0, att.SizeInBytes);
        Assert.Empty(warnings);
    }

    [Fact]
    public void TruncatedBase64DropsPartialQuantum()
    {
        var att = Only(Eml("base64", "SGVsbG8gV29ybGQ"), out var warnings);
        Assert.Equal(Encoding.ASCII.GetBytes("Hello Wor"), att.BinaryData);
        Assert.Equal(9, att.SizeInBytes);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Rfc822PartKeepsCrlfBytes()
    {
        var bytes = SampleFactory.NestedRfc822();
        var first = Assert.Single(_extractor.Extract(bytes).Attachments);
        var again = Assert.Single(_extractor.Extract(bytes).Attachments);
        Assert.Equal(first.BinaryData, again.BinaryData);
        Assert.Equal(first.BinaryData.Length, first.SizeInBytes);
        var text = Encoding.UTF8.GetString(first.BinaryData);
        Assert.Contains("inner body", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
    }

    private static readonly string[] MixedNames = ["e1.txt", "r1.pdf", "e2.bin", "e3.dat", "r2.csv", "e4.txt", "r3.bin"];
    private static readonly string[] NamedTextNames = ["notes.txt", "a.txt"];

    [Fact]
    public void MultipleEmptyAndRealAttachmentsAreAllListed()
    {
        static string Part(string name, string encoding, string body) =>
            "--b1\r\nContent-Type: application/octet-stream; name=\"" + name + "\"\r\n" +
            "Content-Disposition: attachment; filename=\"" + name + "\"\r\n" +
            "Content-Transfer-Encoding: " + encoding + "\r\n\r\n" + body + "\r\n";

        var bytes = Encoding.ASCII.GetBytes(
            "From: a@example.com\r\nTo: b@example.com\r\nSubject: s\r\nMIME-Version: 1.0\r\n" +
            "Content-Type: multipart/mixed; boundary=\"b1\"\r\n\r\n" +
            "--b1\r\nContent-Type: text/plain\r\n\r\nhello\r\n" +
            Part("e1.txt", "base64", "") +
            Part("r1.pdf", "base64", "SGVsbG8=") +
            Part("e2.bin", "quoted-printable", "") +
            Part("e3.dat", "7bit", "") +
            Part("r2.csv", "quoted-printable", "a=3Db") +
            // e4: headers run straight into the next boundary (no blank line); still one empty part.
            "--b1\r\nContent-Type: application/octet-stream; name=\"e4.txt\"\r\n" +
            "Content-Disposition: attachment; filename=\"e4.txt\"\r\n" +
            "Content-Transfer-Encoding: base64\r\n" +
            Part("r3.bin", "7bit", "xyz") +
            "--b1--\r\n");

        var result = _extractor.Extract(bytes);

        Assert.Equal(MixedNames, result.Attachments.Select(a => a.FileName));
        Assert.Equal(new long[] { 0, 5, 0, 0, 3, 0, 3 }, result.Attachments.Select(a => (long)a.SizeInBytes));
        Assert.Equal(Encoding.ASCII.GetBytes("Hello"), result.Attachments[1].BinaryData);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void NamedTextPartsInBodyAlternativeAreListed()
    {
        var bytes = Encoding.ASCII.GetBytes(
            "From: a@example.com\r\nTo: b@example.com\r\nSubject: s\r\nMIME-Version: 1.0\r\n" +
            "Content-Type: multipart/mixed; boundary=\"b1\"\r\n\r\n" +
            "--b1\r\nContent-Type: multipart/alternative; boundary=\"b2\"\r\n\r\n" +
            "--b2\r\nContent-Type: text/plain\r\n\r\nhello\r\n" +
            "--b2\r\nContent-Type: text/html\r\n\r\n<p>x</p>\r\n" +
            "--b2\r\nContent-Type: text/plain; name=\"notes.txt\"\r\n\r\n\r\n" +
            "--b2\r\nContent-Type: text/plain; name=\"..\"\r\n\r\ndots-only name is no name\r\n" +
            "--b2\r\nContent-Type: multipart/related; boundary=\"b3\"\r\n\r\n" +
            "--b3\r\nContent-Type: text/plain\r\nContent-Disposition: inline; filename=\"a.txt\"\r\n\r\nreal\r\n" +
            "--b3--\r\n" +
            "--b2--\r\n" +
            "--b1--\r\n");

        var result = _extractor.Extract(bytes);

        Assert.Contains("<p>x</p>", result.MainContent);
        Assert.Equal(NamedTextNames, result.Attachments.Select(a => a.FileName));
        Assert.Equal(new long[] { 0, 4 }, result.Attachments.Select(a => (long)a.SizeInBytes));
        Assert.Empty(result.Warnings);
    }
}

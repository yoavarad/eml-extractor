using System.Text;
using EmlExtraction.Logics;

namespace EmlExtraction.Tests;

public sealed class DetectorTests
{
    private readonly EmlDetector _detector = new();

    private void Check(ReadOnlySpan<byte> data, bool expected)
    {
        var result = _detector.Detect(data);
        Assert.Equal(expected, _detector.IsEml(data));
        Assert.Equal(_detector.IsEml(data), result.IsSupported);
        Assert.Equal(expected ? "message/rfc822" : "", result.MimeType);
    }

    [Fact]
    public void RejectsOneByteAndLargeNonMail()
    {
        Check(new byte[] { 0x41 }, false);
        Check(new byte[20 * 1024], false);
        Check(Encoding.ASCII.GetBytes(new string('x', 20 * 1024)), false);
    }

    [Fact]
    public void AcceptsGeneratedEml()
    {
        var bytes = SampleFactory.ToBytes(SampleFactory.Plain("hi", "body"));
        Check(bytes, true);
    }

    [Fact]
    public void AcceptsUtf8Bom()
    {
        var eml = SampleFactory.ToBytes(SampleFactory.Plain("hi", "body"));
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        Check(bom.Concat(eml).ToArray(), true);
    }

    [Fact]
    public void AcceptsMboxFromLine()
    {
        var eml = Encoding.ASCII.GetString(SampleFactory.ToBytes(SampleFactory.Plain("hi", "body")));
        var mbox = "From alice@example.com Tue Sep 15 07:00:00 2026\n" + eml;
        Check(Encoding.ASCII.GetBytes(mbox), true);
    }

    [Fact]
    public void RejectsEmpty()
    {
        Check([], false);
        Check("short"u8, false);
    }

    [Fact]
    public void RejectsOleMsgMagic()
    {
        var ole = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }
            .Concat(new byte[64])
            .ToArray();
        Check(ole, false);
    }

    [Fact]
    public void RejectsPdfZipPngJpegJson()
    {
        Check("%PDF-1.4 leftover header looking text From: x"u8.ToArray().Concat(new byte[32]).ToArray(), false);
        Check(new byte[] { (byte)'P', (byte)'K', 0x03, 0x04 }.Concat(new byte[40]).ToArray(), false);
        Check(SampleFactory.MinimalPng().Concat(new byte[20]).ToArray(), false);
        Check(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }.Concat(new byte[40]).ToArray(), false);
        Check("{ \"from\": \"a@b.com\", \"date\": \"now\" }"u8.ToArray(), false);
    }
}

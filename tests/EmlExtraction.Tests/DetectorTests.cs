using System.Text;
using EmlExtraction;

namespace EmlExtraction.Tests;

public sealed class DetectorTests
{
    private readonly EmlDetector _detector = new();

    [Fact]
    public void AcceptsGeneratedEml()
    {
        var bytes = SampleFactory.ToBytes(SampleFactory.Plain("hi", "body"));
        Assert.True(_detector.IsEml(bytes));
    }

    [Fact]
    public void AcceptsUtf8Bom()
    {
        var eml = SampleFactory.ToBytes(SampleFactory.Plain("hi", "body"));
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        Assert.True(_detector.IsEml(bom.Concat(eml).ToArray()));
    }

    [Fact]
    public void AcceptsMboxFromLine()
    {
        var eml = Encoding.ASCII.GetString(SampleFactory.ToBytes(SampleFactory.Plain("hi", "body")));
        var mbox = "From alice@example.com Tue Sep 15 07:00:00 2026\n" + eml;
        Assert.True(_detector.IsEml(Encoding.ASCII.GetBytes(mbox)));
    }

    [Fact]
    public void RejectsEmpty()
    {
        Assert.False(_detector.IsEml([]));
        Assert.False(_detector.IsEml("short"u8));
    }

    [Fact]
    public void RejectsOleMsgMagic()
    {
        var ole = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }
            .Concat(new byte[64])
            .ToArray();
        Assert.False(_detector.IsEml(ole));
    }

    [Fact]
    public void RejectsPdfZipPngJpegJson()
    {
        Assert.False(_detector.IsEml("%PDF-1.4 leftover header looking text From: x"u8.ToArray().Concat(new byte[32]).ToArray()));
        Assert.False(_detector.IsEml(new byte[] { (byte)'P', (byte)'K', 0x03, 0x04 }.Concat(new byte[40]).ToArray()));
        Assert.False(_detector.IsEml(SampleFactory.MinimalPng().Concat(new byte[20]).ToArray()));
        Assert.False(_detector.IsEml(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }.Concat(new byte[40]).ToArray()));
        Assert.False(_detector.IsEml("{ \"from\": \"a@b.com\", \"date\": \"now\" }"u8.ToArray()));
    }
}

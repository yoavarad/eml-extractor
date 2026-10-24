using System.Text;
using EmlExtraction.Exceptions;
using EmlExtraction.Logics;
using EmlExtraction.Models;

namespace EmlExtraction.Tests;

/// <summary>maxInputBytes limit (ydk:req:extraction/max-input-size).</summary>
public sealed class MaxInputBytesTests
{
    private static byte[] Message(int length)
    {
        var head = "From: a@b.com\r\nSubject: hi\r\nContent-Type: text/plain\r\n\r\n";
        var body = new string('x', length - head.Length);
        return Encoding.ASCII.GetBytes(head + body);
    }

    [Fact]
    public void OverLimitThrowsEmlInputTooLargeException()
    {
        var ex = Assert.Throws<EmlInputTooLargeException>(
            () => new EmlExtractor(1024).Extract(new EmlExtractionRequest { FileBytes = Message(1025) }));

        AssertTooLarge(ex);
    }

    [Fact]
    public void BytesOverloadThrowsSameException()
    {
        var ex = Assert.Throws<EmlInputTooLargeException>(() => new EmlExtractor(1024).Extract(Message(1025)));

        AssertTooLarge(ex);
    }

    private static void AssertTooLarge(EmlInputTooLargeException ex)
    {
        Assert.Equal(1025, ex.InputLength);
        Assert.Equal(1024, ex.MaxInputBytes);
        Assert.Equal("request", ex.ParamName);
        Assert.Contains("1025", ex.Message);
        Assert.Contains("1024", ex.Message);
    }

    [Fact]
    public void AtLimitIsParsed()
    {
        var result = new EmlExtractor(1024).Extract(Message(1024));

        Assert.Empty(result.Warnings);
        Assert.Equal("a@b.com", result.Metadata["from"]);
        Assert.NotNull(result.MainContent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveLimitThrows(long limit)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new EmlExtractor(limit));

    [Fact]
    public void DefaultConstantIs500MiB() => Assert.Equal(524_288_000L, EmlExtractor.DefaultMaxInputBytes);
}

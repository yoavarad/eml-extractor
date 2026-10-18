namespace EmlExtraction.Tests;

/// <summary>Error-model types and misuse messages (ADR-023).</summary>
public sealed class ExceptionTypeTests
{
    [Fact]
    public void ExtractionExceptionIsPublicSealedException()
    {
        var type = typeof(EmlExtractionException);

        Assert.True(type.IsPublic);
        Assert.True(type.IsSealed);
        Assert.Equal(typeof(Exception), type.BaseType);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("detect")]
    [InlineData("parse")]
    [InlineData("metadata")]
    [InlineData("body")]
    [InlineData("attachments")]
    public void ExtractionExceptionKeepsStageAndInner(string stage)
    {
        var inner = new InvalidOperationException("secret message content");

        var ex = new EmlExtractionException(stage, inner);

        Assert.Equal(stage, ex.Stage);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal($"EML extraction failed in stage '{stage}': System.InvalidOperationException.", ex.Message);
        Assert.DoesNotContain("secret", ex.Message);
    }

    [Fact]
    public void ExtractionExceptionRejectsNullInner()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new EmlExtractionException("parse", null!));

        Assert.Equal("inner", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bogus")]
    public void ExtractionExceptionRejectsUnknownStage(string? stage)
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new EmlExtractionException(stage!, new Exception()));

        Assert.Equal("stage", ex.ParamName);
        if (stage is { Length: > 0 })
            Assert.Contains(stage, ex.Message);
    }

    [Fact]
    public void InputTooLargeIsPublicSealedArgumentException()
    {
        var type = typeof(EmlInputTooLargeException);

        Assert.True(type.IsPublic);
        Assert.True(type.IsSealed);
        Assert.Equal(typeof(ArgumentException), type.BaseType);
    }

    [Fact]
    public void InputTooLargeExposesLengthsAndMessage()
    {
        var ex = new EmlInputTooLargeException(1025, 1024, "request");

        Assert.Equal(1025, ex.InputLength);
        Assert.Equal(1024, ex.MaxInputBytes);
        Assert.Equal("request", ex.ParamName);
        Assert.StartsWith("Input is 1025 bytes, which exceeds maxInputBytes 1024.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExtractorCtorNamesParameterAndValue(long limit)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new EmlExtractor(limit));

        Assert.Equal("maxInputBytes", ex.ParamName);
        Assert.Contains(limit.ToString(System.Globalization.CultureInfo.InvariantCulture), ex.Message);
    }

    [Fact]
    public void ExtractNullRequestNamesParameter()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new EmlExtractor().Extract((EmlExtractionRequest)null!));

        Assert.Equal("request", ex.ParamName);
    }
}

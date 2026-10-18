using System.Text;
using MimeKit;

namespace EmlExtraction.Tests;

public sealed class AttachmentTests
{
    private readonly EmlExtractor _extractor = new();

    [Theory]
    [InlineData("report.docx", "application", "vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("sheet.xlsx", "application", "vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("deck.pptx", "application", "vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("file.pdf", "application", "pdf")]
    [InlineData("photo.jpg", "image", "jpeg")]
    [InlineData("anim.gif", "image", "gif")]
    [InlineData("archive.zip", "application", "zip")]
    public void OfficeAndBinaryAttachmentsRoundTrip(string name, string type, string sub)
    {
        var payload = SampleFactory.FakeOffice(name);
        var bytes = SampleFactory.WithAttachment(name, payload, type, sub);
        var result = _extractor.Extract(bytes);
        var att = Assert.Single(result.Attachments);
        Assert.Equal(name, att.FileName);
        Assert.Equal(payload.Length, att.SizeInBytes);
        Assert.Equal(SampleFactory.Sha256(payload), SampleFactory.Sha256(att.BinaryData));
        Assert.Null(att.FullPath);
        Assert.Null(att.OriginalPath);
        Assert.Null(att.ContentLocation);
    }

    [Fact]
    public void NestedEmlIsOpaqueBinaryAndNotRecursed()
    {
        var bytes = SampleFactory.NestedRfc822();
        var result = _extractor.Extract(bytes);
        Assert.Equal("outer", result.MainContent);
        var att = Assert.Single(result.Attachments);
        Assert.Equal("forwarded.eml", att.FileName);
        Assert.True(_extractor.Extract(att.BinaryData).MainContent == "inner body"
                    || Encoding.UTF8.GetString(att.BinaryData).Contains("inner body"));
        Assert.DoesNotContain(result.Attachments, a => a.FileName.Contains("inner"));
    }

    [Fact]
    public void CopyPasteCidImageIsExtracted()
    {
        var png = SampleFactory.MinimalPng();
        var bytes = SampleFactory.RelatedHtmlWithCid(png, "paste@local");
        var result = _extractor.Extract(bytes);
        Assert.Contains("cid:paste@local", result.MainContent!);
        var img = Assert.Single(result.Attachments);
        Assert.Equal(png, img.BinaryData);
        Assert.Contains("paste", img.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("inline", img.Disposition);
    }

    [Fact]
    public void AttachedImageIsNotTreatedAsBody()
    {
        var png = SampleFactory.MinimalPng();
        var bytes = SampleFactory.WithAttachment("pic.png", png, "image", "png");
        var result = _extractor.Extract(bytes);
        Assert.Contains("see attach", result.MainContent!);
        var att = Assert.Single(result.Attachments);
        Assert.Equal("pic.png", att.FileName);
        Assert.Equal(png, att.BinaryData);
    }

    [Fact]
    public void CalendarWithFilenameIsAttachment()
    {
        var ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            SUMMARY:Meet
            END:VEVENT
            END:VCALENDAR
            """u8.ToArray();
        var builder = new BodyBuilder { TextBody = "see invite" };
        builder.Attachments.Add("meet.ics", ics, new ContentType("text", "calendar"));
        var message = SampleFactory.Plain("cal", "unused");
        message.Body = builder.ToMessageBody();
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Equal("see invite", result.MainContent);
        Assert.Contains(result.Attachments, a => a.FileName == "meet.ics");
    }

    [Fact]
    public void Pkcs7SignaturePartIsCollected()
    {
        var mixed = new Multipart("signed")
        {
            new TextPart("plain") { Text = "signed-body" },
            new MimePart("application", "pkcs7-signature")
            {
                FileName = "smime.p7s",
                Content = new MimeContent(new MemoryStream("SIG"u8.ToArray()))
            }
        };
        var message = SampleFactory.Plain("signed", "unused");
        message.Body = mixed;
        var result = _extractor.Extract(SampleFactory.ToBytes(message));
        Assert.Equal("signed-body", result.MainContent);
        Assert.Contains(result.Attachments, a => a.FileName == "smime.p7s");
    }
}

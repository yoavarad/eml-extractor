using System.Text;
using EmlExtraction.Abstractions;
using MimeKit;

namespace EmlExtraction.Tests;

public sealed class AttachmentTests
{
    private readonly CheckedExtractor _extractor = new();

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
        Assert.Equal(name, att.FullPath);
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

    // ADR-013: FullPath is the container path.

    [Fact]
    public void DuplicateNamesGetTildeSuffixThatNeverDisplacesALiteralPath()
    {
        var result = _extractor.Extract(Mixed(
            Named("image001.png", "image", "png", "1"),
            Named("image001.png", "image", "png", "2"),
            Named("image001~2.png", "image", "png", "3")));
        Assert.Equal(["image001.png", "image001~3.png", "image001~2.png"], result.Attachments.Select(a => a.FullPath));
        Assert.Equal(["image001.png", "image001.png", "image001~2.png"], result.Attachments.Select(a => a.FileName));
    }

    [Fact]
    public void FullPathUniquenessIsCaseInsensitive()
    {
        var result = _extractor.Extract(Mixed(
            Named("IMAGE.PNG", "image", "png", "1"),
            Named("image.png", "image", "png", "2"),
            Named("Report.PDF", "application", "pdf", "3"),
            Named("report.pdf", "application", "pdf", "4")));
        Assert.Equal(["IMAGE.PNG", "image~2.png", "Report.PDF", "report~2.pdf"], result.Attachments.Select(a => a.FullPath));
        Assert.Equal(["IMAGE.PNG", "image.png", "Report.PDF", "report.pdf"], result.Attachments.Select(a => a.FileName));
    }

    [Theory]
    [InlineData(".hidden", ".hidden~2")]
    [InlineData("noext", "noext~2")]
    [InlineData("a.tar.gz", "a.tar~2.gz")]
    public void TildeGoesBeforeExtensionOfLastSegmentOrIsAppended(string name, string second)
    {
        var result = _extractor.Extract(Mixed(
            Named(name, "application", "octet-stream", "1"),
            Named(name, "application", "octet-stream", "2")));
        Assert.Equal([name, second], result.Attachments.Select(a => a.FullPath));
    }

    [Fact]
    public void TnefFilesArePrefixedWithWrapperName()
    {
        var result = _extractor.Extract(Mixed(Tnef("winmail.dat", ("budget.xlsx", "B"u8.ToArray()))));
        var att = Assert.Single(result.Attachments);
        Assert.Equal("budget.xlsx", att.FileName);
        Assert.Equal("winmail.dat/budget.xlsx", att.FullPath);
        Assert.Equal("B"u8.ToArray(), att.BinaryData);
    }

    [Fact]
    public void TnefWrapperSegmentIsItsFileNameOrWinmailDatWhenNameless()
    {
        var named = _extractor.Extract(Mixed(Tnef("outlook.dat", ("a.txt", "A"u8.ToArray()))));
        Assert.Equal("outlook.dat/a.txt", Assert.Single(named.Attachments).FullPath);

        var nameless = _extractor.Extract(Mixed(
            Tnef(null, ("budget.xlsx", "1"u8.ToArray())),
            Tnef(null, ("budget.xlsx", "2"u8.ToArray()))));
        Assert.Equal(["winmail.dat/budget.xlsx", "winmail.dat/budget~2.xlsx"], nameless.Attachments.Select(a => a.FullPath));
        Assert.All(nameless.Attachments, a => Assert.Equal("budget.xlsx", a.FileName));
    }

    [Fact]
    public void TnefFilesAreProcessedAtWrapperPositionAndCountForPartIndex()
    {
        var result = _extractor.Extract(Mixed(
            Named("a.txt", "text", "plain", "a"),
            Tnef("winmail.dat", ("x.bin", "x"u8.ToArray()), ("y.bin", "y"u8.ToArray())),
            Named("b.txt", "text", "plain", "b"),
            Nameless("image", "png", "z")));
        Assert.Equal(
            ["a.txt", "winmail.dat/x.bin", "winmail.dat/y.bin", "b.txt", "part-4.png"],
            result.Attachments.Select(a => a.FullPath));
    }

    [Fact]
    public void NamelessPartAfterOneRecordIsPart1()
    {
        var result = _extractor.Extract(Mixed(Named("a.txt", "text", "plain", "a"), Nameless("application", "pdf", "p")));
        Assert.Equal(["a.txt", "part-1.pdf"], result.Attachments.Select(a => a.FullPath));
        Assert.Equal("part-1.pdf", result.Attachments[1].FileName);
    }

    [Fact]
    public void FallbackNameFromContentIdIsSanitised()
    {
        var part = Nameless("image", "png", "img");
        part.Headers.Replace(HeaderId.ContentId, "<a/b:c*d@x>");
        var att = Assert.Single(_extractor.Extract(Mixed(part)).Attachments);
        Assert.Equal("a_b_c_d@x.png", att.FileName);
        Assert.Equal("a_b_c_d@x.png", att.FullPath);
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("dir/..")]
    [InlineData("dir/")]
    [InlineData("dir/ ..")]
    public void DotOrEmptyFileNameCountsAsNameless(string name)
    {
        var att = Assert.Single(_extractor.Extract(Mixed(Named(name, "text", "csv", "c"))).Attachments);
        Assert.Equal("part-0.csv", att.FileName);
        Assert.Equal("part-0.csv", att.FullPath);
    }

    [Fact]
    public void NestedMessageStaysOpaqueWithFullPathEqualToFileName()
    {
        var att = Assert.Single(_extractor.Extract(SampleFactory.NestedRfc822()).Attachments);
        Assert.Equal("forwarded.eml", att.FileName);
        Assert.Equal("forwarded.eml", att.FullPath);

        var inner = SampleFactory.Plain("inner", "inner body");
        var result = _extractor.Extract(Mixed(new MessagePart("rfc822") { Message = inner }));
        var nameless = Assert.Single(result.Attachments);
        Assert.Equal("attached.eml", nameless.FileName);
        Assert.Equal("attached.eml", nameless.FullPath);
    }

    private static byte[] Mixed(params MimeEntity[] parts)
    {
        var mixed = new Multipart("mixed") { new TextPart("plain") { Text = "body" } };
        foreach (var part in parts)
            mixed.Add(part);
        var message = SampleFactory.Plain("paths", "unused");
        message.Body = mixed;
        return SampleFactory.ToBytes(message);
    }

    private static MimePart Named(string name, string media, string sub, string payload)
    {
        var part = Nameless(media, sub, payload);
        part.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = name };
        return part;
    }

    private static MimePart Nameless(string media, string sub, string payload) => new(media, sub)
    {
        Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes(payload))),
        ContentTransferEncoding = ContentEncoding.Base64
    };

    /// <summary>Hand-built TNEF stream (attAttachRenddata, attAttachTitle, attAttachData per file) in an application/ms-tnef part.</summary>
    private static MimePart Tnef(string? wrapperName, params (string Name, byte[] Data)[] files)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(0x223E9F78u);
        w.Write((ushort)1);
        foreach (var (name, data) in files)
        {
            TnefAttr(w, 0x00069002, new byte[14]);
            TnefAttr(w, 0x00018010, [.. Encoding.ASCII.GetBytes(name), 0]);
            TnefAttr(w, 0x0006800F, data);
        }

        w.Flush();
        var part = new MimePart("application", "ms-tnef")
        {
            Content = new MimeContent(new MemoryStream(ms.ToArray())),
            ContentTransferEncoding = ContentEncoding.Base64
        };
        if (wrapperName is not null)
            part.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = wrapperName };
        return part;
    }

    private static void TnefAttr(BinaryWriter w, uint id, byte[] data)
    {
        w.Write((byte)2);
        w.Write(id);
        w.Write((uint)data.Length);
        w.Write(data);
        ushort sum = 0;
        foreach (var b in data)
            sum += b;
        w.Write(sum);
    }
}

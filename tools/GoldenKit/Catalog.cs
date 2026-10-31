using System.Text;
using EmlExtraction.Models;
using MimeKit;

namespace GoldenKit;

internal static class Catalog
{
    public const int ExpectedCount = 1200;

    private static readonly string[] Utf8AddressTags = ["rfc6532", "unicode"];

    public static List<CaseDef> All()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var all = new List<CaseDef>();
        all.AddRange(Detect());          // 40
        all.AddRange(Headers());         // 70
        all.AddRange(XFields());         // 50
        all.AddRange(Unicode());         // 100
        all.AddRange(Charsets());        // 50
        all.AddRange(Bodies());          // 70
        all.AddRange(Encodings());       // 40
        all.AddRange(Multiparts());      // 50
        all.AddRange(Disposition());     // 40
        all.AddRange(RelatedCid());      // 60
        all.AddRange(Smtputf8());        // 30
        all.AddRange(Office());          // 100
        all.AddRange(Nested());          // 40
        all.AddRange(Images());          // 70
        all.AddRange(OtherFiles());      // 80
        all.AddRange(Edges());           // 40
        all.AddRange(Combos());          // 70
        all.AddRange(OptionalRfcs());    // 200
        if (all.Count != ExpectedCount)
            throw new InvalidOperationException($"Catalog produced {all.Count} cases, expected {ExpectedCount}");
        if (all.Select(c => c.Id).Distinct().Count() != ExpectedCount)
            throw new InvalidOperationException("Duplicate case ids");
        return all;
    }

    private static List<CaseDef> Detect()
    {
        var list = new List<CaseDef>();
        var ok = new[] { "detect", "rfc5322" };
        var no = new[] { "detect", "not-eml" };
        for (var i = 1; i <= 18; i++)
            list.Add(MimeBuild.Plain($"D{i:D3}", ok, $"detect-ok-{i}", $"body-{i}"));

        var eml = MimeBuild.ToBytes(WithBody("D019", "bom-subject", "bom-body"));
        list.Add(new CaseDef
        {
            Id = "D019",
            Tags = ok,
            Eml = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(eml).ToArray(),
            IsEml = true,
            Expect = PlainExpect("bom-subject", "bom-body")
        });

        var mbox = Encoding.ASCII.GetBytes("From alice@example.com Tue Sep 15 07:00:00 2026\n" + Encoding.ASCII.GetString(MimeBuild.ToBytes(WithBody("D020", "mbox", "mbox-body"))));
        list.Add(new CaseDef { Id = "D020", Tags = [.. ok, "mbox"], Eml = mbox, IsEml = true, Expect = PlainExpect("mbox", "mbox-body") });

        list.Add(MimeBuild.RawBytes("D021", no, [], false));
        list.Add(MimeBuild.RawBytes("D022", no, "short"u8.ToArray(), false));
        list.Add(MimeBuild.RawBytes("D023", no, Concat([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1], new byte[80]), false));
        list.Add(MimeBuild.RawBytes("D024", no, "%PDF-1.7 plus From: x@y.com and Date: now and Subject: no"u8.ToArray(), false));
        list.Add(MimeBuild.RawBytes("D025", no, Concat([(byte)'P', (byte)'K', 3, 4], new byte[60]), false));
        list.Add(MimeBuild.RawBytes("D026", no, Concat([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], new byte[40]), false));
        list.Add(MimeBuild.RawBytes("D027", no, Concat([0xFF, 0xD8, 0xFF, 0xE0], new byte[40]), false));
        list.Add(MimeBuild.RawBytes("D028", no, "{ \"from\": \"a@b.com\", \"date\": \"now\", \"subject\": \"x\" }"u8.ToArray(), false));
        list.Add(MimeBuild.RawBytes("D029", no, "<?xml version=\"1.0\"?><from>a@b.com</from>"u8.ToArray(), false));
        list.Add(MimeBuild.RawBytes("D030", no, "<!DOCTYPE html><html><body>From: a@b.com</body></html>"u8.ToArray(), false));
        list.Add(MimeBuild.RawBytes("D031", no, Concat("MZ"u8.ToArray(), new byte[64]), false));
        list.Add(MimeBuild.RawBytes("D032", no, Concat("GIF89a"u8.ToArray(), new byte[40]), false));
        list.Add(MimeBuild.RawBytes("D033", no, Concat("RIFF"u8.ToArray(), [0, 0, 0, 0], "WAVE"u8.ToArray(), new byte[40]), false));
        list.Add(MimeBuild.RawBytes("D034", no, "\0\0\0\0\0\0\0\0From: a@b.com\nDate: now\nSubject: x\n\n"u8.ToArray(), false));
        list.Add(MimeBuild.RawBytes("D035", no, Encoding.Unicode.GetBytes("From: a@b.com\nDate: now\nSubject: x\n\nbody"), false));
        for (var i = 36; i <= 40; i++)
            list.Add(MimeBuild.RawBytes($"D{i:D3}", no, RandomBytes(i, 64), false));
        return Require(list, 40, "Detect");
    }

    private static List<CaseDef> Headers()
    {
        var tags = new[] { "rfc5322", "rfc822" };
        var list = new List<CaseDef>();
        for (var i = 1; i <= 40; i++)
        {
            var m = MimeBuild.BaseMessage($"H{i:D3}", $"classic-{i}");
            if (i % 2 == 0) m.Cc.Add(MailboxAddress.Parse("Carol <carol@example.com>"));
            if (i % 3 == 0) m.ReplyTo.Add(MailboxAddress.Parse("reply@example.com"));
            if (i % 5 == 0) m.Sender = MailboxAddress.Parse("sender@example.com");
            if (i % 7 == 0) m.Bcc.Add(MailboxAddress.Parse("hidden@example.com"));
            m.Body = new TextPart("plain") { Text = $"hbody-{i}" };
            var expect = PlainExpect($"classic-{i}", $"hbody-{i}");
            if (i % 2 == 0) expect.Metadata[MetadataKeys.Cc] = MimeBuild.Contains("carol@example.com");
            if (i % 3 == 0) expect.Metadata[MetadataKeys.ReplyTo] = MimeBuild.Contains("reply@example.com");
            if (i % 5 == 0) expect.Metadata[MetadataKeys.Sender] = MimeBuild.Contains("sender@example.com");
            if (i % 7 == 0) expect.Metadata[MetadataKeys.Bcc] = MimeBuild.Contains("hidden@example.com");
            list.Add(MimeBuild.FromMessage($"H{i:D3}", tags, m, expect));
        }

        for (var i = 41; i <= 70; i++)
        {
            var m = MimeBuild.BaseMessage($"H{i:D3}", $"received-{i}");
            m.Headers.Add("Received", $"from mx{(i % 4) + 1}.example.net");
            m.Headers.Add("Return-Path", "<bounce@example.com>");
            m.Headers.Add("Delivered-To", "bob@example.com");
            m.InReplyTo = "<prev@example.com>";
            m.Body = new TextPart("plain") { Text = "recv" };
            var expect = PlainExpect($"received-{i}", "recv");
            expect.Metadata["received"] = MimeBuild.Contains("example.net");
            expect.Metadata["return-path"] = MimeBuild.Contains("bounce@example.com");
            expect.Metadata[MetadataKeys.InReplyTo] = MimeBuild.Contains("prev@example.com");
            list.Add(MimeBuild.FromMessage($"H{i:D3}", tags, m, expect));
        }

        return Require(list, 70, "Headers");
    }

    private static List<CaseDef> XFields()
    {
        var tags = new[] { "x-headers", "rfc5322" };
        var list = new List<CaseDef>();
        for (var i = 1; i <= 50; i++)
        {
            var m = MimeBuild.BaseMessage($"X{i:D3}", $"x-{i}");
            m.Headers.Add("X-Mailer", $"Mailer-{i}");
            m.Headers.Add("X_Custom_Field", $"team-{i}");
            m.Headers.Add("X-Spam-Score", $"{i % 9}.{i % 7}");
            if (i % 2 == 0) m.Headers.Add("DKIM-Signature", "v=1; dummy");
            m.Body = new TextPart("plain") { Text = "x" };
            var expect = PlainExpect($"x-{i}", "x");
            expect.XFields = new GoldenField
            {
                Contains = [$"X_Custom_Field: team-{i}"],
                NotContains = ["X-Mailer", "X-Spam-Score", "DKIM-Signature"]
            };
            if (i % 2 == 0)
                expect.Metadata["dkim-signature"] = MimeBuild.Contains("v=1");
            list.Add(MimeBuild.FromMessage($"X{i:D3}", tags, m, expect));
        }

        return Require(list, 50, "XFields");
    }

    private static List<CaseDef> Unicode()
    {
        var tags = new[] { "unicode", "rfc2047" };
        (string Subj, string Body, string[] Extra)[] langs =
        [
            ("שלום עולם", "גוף עברי", ["rtl", "hebrew"]),
            ("السلام عليكم", "نص عربي", ["rtl", "arabic"]),
            ("你好世界", "中文正文", ["cjk"]),
            ("こんにちは", "日本語の本文", ["cjk"]),
            ("안녕하세요", "한국어 본문", ["cjk"]),
            ("नमस्ते दुनिया", "हिन्दी पाठ", []),
            ("สวัสดี", "ข้อความไทย", []),
            ("Привет мир", "Русский текст", []),
            ("Γειά σου", "Ελληνικά", []),
            ("Merhaba dünya", "Türkçe gövde", []),
            ("Witaj świecie", "Polski tekst", []),
            ("Ahoj světe", "Český text", []),
            ("Hello 😀🎉", "emoji body 👨‍👩‍👧‍👦", ["emoji"]),
            ("mix שלום 中文 😀", "mix body", ["emoji"]),
            ("Ñandú pingüino", "Español con tildes", []),
            ("São Paulo", "Português", []),
            ("Smörgåsbord", "Svenska", []),
            ("føljeton", "Norsk", []),
            ("Straße", "Deutsch", []),
            ("Été français", "Français", []),
            ("ខ្ញុំស្រឡាញ់", "Khmer", []),
            ("xin chào", "Tiếng Việt", []),
            ("გამარჯობა", "ქართული", []),
            ("Բարեւ աշխարհ", "Հայերեն", []),
            ("עברית + English", "bidi body", ["rtl"])
        ];
        var list = new List<CaseDef>();
        var n = 0;
        foreach (var lang in langs)
        {
            for (var v = 0; v < 4; v++)
            {
                n++;
                var id = $"U{n:D3}";
                var t = tags.Concat(lang.Extra).ToArray();
                var wrapRtl = lang.Extra.Contains("rtl");
                var htmlBody = wrapRtl
                    ? $"<html dir=\"rtl\"><body dir=\"rtl\"><p>{lang.Body}</p></body></html>"
                    : $"<p>{lang.Body}</p>";
                var subject = v == 3 ? lang.Subj + " #" + v : lang.Subj;
                var plainBody = v == 3 ? lang.Body + " #" + v : lang.Body;
                var c = v switch
                {
                    0 => MimeBuild.Plain(id, t, subject, plainBody),
                    1 => MimeBuild.Html(id, t.Concat(["html-body"]).ToArray(), subject, htmlBody),
                    2 => MimeBuild.Alternative(id, t.Concat(["multipart-alternative"]).ToArray(), subject, "plain-" + lang.Body, htmlBody),
                    _ => MimeBuild.Plain(id, t, subject, plainBody)
                };
                list.Add(c);
            }
        }

        return Require(list, 100, "Unicode");
    }

    private static List<CaseDef> Charsets()
    {
        var tags = new[] { "charset-legacy", "rfc2045", "rfc2047" };
        var list = new List<CaseDef>();
        (string Name, int Cp, string Text)[] rows =
        [
            ("windows-1255", 1255, "שלום"),
            ("windows-1252", 1252, "café"),
            ("iso-8859-1", 28591, "naïve"),
            ("iso-8859-8", 28598, "שלום"),
            ("koi8-r", 20866, "Привет"),
            ("gbk", 936, "你好"),
            ("shift_jis", 932, "こんにちは"),
            ("big5", 950, "繁體"),
            ("windows-1251", 1251, "Привет"),
            ("iso-8859-2", 28592, "Zażółć")
        ];
        var n = 0;
        foreach (var row in rows)
        {
            for (var v = 1; v <= 5; v++)
            {
                n++;
                var enc = Encoding.GetEncoding(row.Cp);
                var payload = enc.GetBytes(row.Text);
                var b64 = Convert.ToBase64String(payload);
                var subjB64 = Convert.ToBase64String(enc.GetBytes(row.Text));
                var raw = $"""
                    From: alice@example.com
                    To: bob@example.com
                    Subject: =?{row.Name}?B?{subjB64}?=
                    Date: Tue, 15 Sep 2026 07:00:00 +0000
                    Message-ID: <C{n:D3}@golden.example>
                    MIME-Version: 1.0
                    Content-Type: text/plain; charset={row.Name}
                    Content-Transfer-Encoding: base64

                    {b64}
                    """;
                list.Add(MimeBuild.RawEml($"C{n:D3}", tags, raw, PlainExpect(row.Text, row.Text)));
            }
        }

        return Require(list, 50, "Charsets");
    }

    private static List<CaseDef> Bodies()
    {
        var list = new List<CaseDef>();
        for (var i = 1; i <= 25; i++)
            list.Add(MimeBuild.Plain($"B{i:D3}", ["plain-body", "rfc2046"], $"plain-{i}", $"plain-body-{i}"));
        for (var i = 26; i <= 50; i++)
            list.Add(MimeBuild.Html($"B{i:D3}", ["html-body", "rfc2046"], $"html-{i}", $"<p>html-body-{i}</p>"));
        for (var i = 51; i <= 65; i++)
            list.Add(MimeBuild.Alternative($"B{i:D3}", ["multipart-alternative", "html-body", "rfc2046"], $"alt-{i}", $"plain-{i}", $"<p>html-{i}</p>"));
        for (var i = 66; i <= 70; i++)
        {
            var raw = $"""
                From: alice@example.com
                To: bob@example.com
                Subject: empty-{i}
                Date: Tue, 15 Sep 2026 07:00:00 +0000
                Message-ID: <B{i:D3}@golden.example>

                """;
            list.Add(MimeBuild.RawEml($"B{i:D3}", ["empty-body", "rfc5322"], raw, new GoldenExpect
            {
                Metadata = MimeBuild.Meta($"empty-{i}"),
                MainContent = MimeBuild.Eq(""),
                AttachmentCount = 0
            }));
        }

        return Require(list, 70, "Bodies");
    }

    private static List<CaseDef> Encodings()
    {
        var list = new List<CaseDef>();
        var texts = Enumerable.Range(1, 10).Select(i => $"qp body {i} = café").ToArray();
        for (var i = 1; i <= 10; i++)
        {
            var m = MimeBuild.BaseMessage($"E{i:D3}", $"qp-{i}");
            m.Body = new TextPart("plain") { Text = texts[i - 1], ContentTransferEncoding = ContentEncoding.QuotedPrintable };
            list.Add(MimeBuild.FromMessage($"E{i:D3}", ["rfc2045"], m, PlainExpect($"qp-{i}", texts[i - 1])));
        }

        for (var i = 11; i <= 20; i++)
        {
            var body = $"b64-body-{i}";
            var m = MimeBuild.BaseMessage($"E{i:D3}", $"b64-{i}");
            m.Body = new TextPart("plain") { Text = body, ContentTransferEncoding = ContentEncoding.Base64 };
            list.Add(MimeBuild.FromMessage($"E{i:D3}", ["rfc2045"], m, PlainExpect($"b64-{i}", body)));
        }

        for (var i = 21; i <= 30; i++)
        {
            var body = $"eight-{i}-שלום";
            var m = MimeBuild.BaseMessage($"E{i:D3}", $"8bit-{i}");
            m.Body = new TextPart("plain") { Text = body, ContentTransferEncoding = ContentEncoding.EightBit };
            list.Add(MimeBuild.FromMessage($"E{i:D3}", ["rfc2045", "unicode"], m, PlainExpect($"8bit-{i}", body)));
        }

        for (var i = 31; i <= 40; i++)
        {
            var raw = $"""
                From: alice@example.com
                To: bob@example.com
                Subject: corrupt-{i}
                Date: Tue, 15 Sep 2026 07:00:00 +0000
                Message-ID: <E{i:D3}@golden.example>
                MIME-Version: 1.0
                Content-Type: text/plain; charset=utf-8
                Content-Transfer-Encoding: base64

                %%%%not-base64-{i}%%%%
                """;
            list.Add(MimeBuild.RawEml($"E{i:D3}", ["rfc2045", "corrupt-transfer-encoding"], raw, new GoldenExpect
            {
                Metadata = MimeBuild.Meta($"corrupt-{i}"),
                AttachmentCount = 0
            }));
        }

        return Require(list, 40, "Encodings");
    }

    private static List<CaseDef> Multiparts()
    {
        var list = new List<CaseDef>();
        for (var i = 1; i <= 20; i++)
        {
            var payload = MimeBuild.Stub("txt", $"M{i:D3}");
            list.Add(MimeBuild.WithParts($"M{i:D3}", ["rfc2046", "attachment"], $"mixed-{i}", "see file", false,
            [
                ($"note-{i}.txt", "text", "plain", payload, "attachment", null)
            ]));
        }

        for (var i = 21; i <= 35; i++)
            list.Add(MimeBuild.Alternative($"M{i:D3}", ["rfc2046", "multipart-alternative"], $"malt-{i}", "p", "<p>h</p>"));

        for (var i = 36; i <= 50; i++)
        {
            var sig = Encoding.UTF8.GetBytes($"SIG-{i}");
            var mixed = new Multipart("signed")
            {
                new TextPart("plain") { Text = $"signed-{i}" },
                new MimePart("application", "pkcs7-signature")
                {
                    FileName = "smime.p7s",
                    Content = new MimeContent(new MemoryStream(sig)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = "smime.p7s" }
                }
            };
            var m = MimeBuild.BaseMessage($"M{i:D3}", $"signed-{i}");
            m.Body = mixed;
            list.Add(MimeBuild.FromMessage($"M{i:D3}", ["rfc2046", "signed-smime"], m, new GoldenExpect
            {
                Metadata = MimeBuild.Meta($"signed-{i}"),
                MainContent = MimeBuild.Eq($"signed-{i}"),
                MainContentMimeType = "text/plain",
                AttachmentCount = 1,
                Attachments = [new GoldenAttachment { Name = "smime.p7s", Size = sig.Length, Sha256 = MimeBuild.Sha(sig) }]
            }));
        }

        return Require(list, 50, "Multiparts");
    }

    private static List<CaseDef> Disposition()
    {
        var list = new List<CaseDef>();
        for (var i = 1; i <= 20; i++)
        {
            var payload = MimeBuild.Stub("bin", $"P{i:D3}");
            list.Add(MimeBuild.WithParts($"P{i:D3}", ["rfc2183", "attachment"], $"disp-a-{i}", "x", false,
            [
                ($"file-{i}.bin", "application", "octet-stream", payload, "attachment", null)
            ]));
        }

        for (var i = 21; i <= 30; i++)
        {
            var payload = MimeBuild.Stub("txt", $"P{i:D3}");
            var path = $"inbox/{i}/file.txt";
            list.Add(MimeBuild.WithParts($"P{i:D3}", ["rfc2183", "path-in-filename"], $"disp-path-{i}", "x", false,
            [
                ("file.txt", "text", "plain", payload, "attachment", path)
            ]));
        }

        for (var i = 31; i <= 40; i++)
        {
            var payload = MimeBuild.Stub("txt", $"P{i:D3}");
            var path = $"C:\\Temp\\file-{i}.txt";
            list.Add(MimeBuild.WithParts($"P{i:D3}", ["rfc2183", "path-in-filename"], $"disp-win-{i}", "x", false,
            [
                ($"file-{i}.txt", "text", "plain", payload, "attachment", path)
            ]));
        }

        return Require(list, 40, "Disposition");
    }

    private static List<CaseDef> RelatedCid()
    {
        var list = new List<CaseDef>();
        var png = MimeBuild.Stub("png", "cid");
        for (var i = 1; i <= 60; i++)
        {
            var name = i % 3 == 0 ? "paste.jpg" : "paste.png";
            var img = i % 3 == 0 ? MimeBuild.Stub("jpg", $"R{i:D3}") : png;
            var cid = $"img{i}@golden.local";
            var tags = new[] { "rfc2387", "rfc2392", "cid", "inline", "image" };
            list.Add(MimeBuild.RelatedCid($"R{i:D3}", tags, img, name, cid));
        }

        return Require(list, 60, "RelatedCid");
    }

    private static List<CaseDef> Smtputf8()
    {
        var list = new List<CaseDef>();
        (string Name, string Addr, string[] Extra)[] people =
        [
            ("יוסי", "yossi@example.com", ["hebrew", "rtl"]),
            ("李", "li@example.com", ["cjk"]),
            ("José", "jose@example.com", []),
            ("Владимир", "vlad@example.com", []),
            ("محمد", "mohammed@example.com", ["arabic", "rtl"])
        ];
        var n = 0;
        foreach (var p in people)
        {
            for (var v = 1; v <= 6; v++)
            {
                n++;
                var m = MimeBuild.BaseMessage($"S{n:D3}", $"utf8-{n}");
                m.From.Clear();
                m.From.Add(new MailboxAddress(p.Name, p.Addr));
                m.Body = new TextPart("plain") { Text = $"hello {p.Name}" };
                var expect = PlainExpect($"utf8-{n}", $"hello {p.Name}");
                expect.Metadata[MetadataKeys.From] = MimeBuild.Contains(p.Addr);
                var tags = Utf8AddressTags.Concat(p.Extra).ToArray();
                list.Add(MimeBuild.FromMessage($"S{n:D3}", tags, m, expect));
            }
        }

        return Require(list, 30, "Smtputf8");
    }

    private static List<CaseDef> Office()
    {
        (string Name, string Media, string Sub, string Kind, string Tag)[] types =
        [
            ("report.docx", "application", "vnd.openxmlformats-officedocument.wordprocessingml.document", "docx", "office"),
            ("sheet.xlsx", "application", "vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx", "office"),
            ("deck.pptx", "application", "vnd.openxmlformats-officedocument.presentationml.presentation", "pptx", "office"),
            ("legacy.doc", "application", "msword", "doc", "office"),
            ("legacy.xls", "application", "vnd.ms-excel", "xls", "office"),
            ("legacy.ppt", "application", "vnd.ms-powerpoint", "ppt", "office"),
            ("text.odt", "application", "vnd.oasis.opendocument.text", "odt", "office"),
            ("calc.ods", "application", "vnd.oasis.opendocument.spreadsheet", "ods", "office"),
            ("show.odp", "application", "vnd.oasis.opendocument.presentation", "odp", "office"),
            ("file.pdf", "application", "pdf", "pdf", "pdf")
        ];
        var list = new List<CaseDef>();
        var n = 0;
        foreach (var t in types)
        {
            for (var v = 1; v <= 10; v++)
            {
                n++;
                var id = $"A{n:D3}";
                var payload = MimeBuild.Stub(t.Kind is "doc" or "xls" or "ppt" ? "bin" : t.Kind, id);
                var html = v % 2 == 0;
                var path = v == 10 ? $"docs/{t.Name}" : null;
                var fileName = path is null ? t.Name : t.Name;
                list.Add(MimeBuild.WithParts(id, ["attachment", t.Tag, "rfc2046"], $"office-{n}", html ? "<p>see</p>" : "see", html,
                [
                    (fileName, t.Media, t.Sub, payload, "attachment", path)
                ]));
            }
        }

        return Require(list, 100, "Office");
    }

    private static List<CaseDef> Nested()
    {
        var list = new List<CaseDef>();
        var tags = new[] { "nested-rfc822", "no-recurse", "rfc822" };
        for (var i = 1; i <= 40; i++)
        {
            var name = i % 5 == 0 ? $"fwd/{i}.eml" : $"forwarded-{i}.eml";
            list.Add(MimeBuild.NestedRfc822($"N{i:D3}", tags, $"inner-{i}", $"inner-body-{i}", name));
        }

        return Require(list, 40, "Nested");
    }

    private static List<CaseDef> Images()
    {
        (string Name, string Kind, string Media, string Sub)[] types =
        [
            ("pic.png", "png", "image", "png"),
            ("pic.jpg", "jpg", "image", "jpeg"),
            ("pic.gif", "gif", "image", "gif"),
            ("pic.webp", "webp", "image", "webp"),
            ("pic.bmp", "bmp", "image", "bmp"),
            ("pic.tif", "tif", "image", "tiff"),
            ("pic.svg", "svg", "image", "svg+xml"),
            ("pic.ico", "ico", "image", "x-icon"),
            ("pic.heic", "heic", "image", "heic"),
            ("pic.jpeg", "jpg", "image", "jpeg")
        ];
        var list = new List<CaseDef>();
        var n = 0;
        foreach (var t in types)
        {
            for (var v = 1; v <= 7; v++)
            {
                n++;
                var id = $"I{n:D3}";
                var payload = t.Kind == "svg"
                    ? Encoding.UTF8.GetBytes($"<svg xmlns='http://www.w3.org/2000/svg' id='{id}'/>")
                    : MimeBuild.Stub(t.Kind, id);
                var tags = new[] { "image", "attachment", "rfc2046" };
                list.Add(MimeBuild.WithParts(id, tags, $"img-{n}", "see pic", false,
                [
                    (t.Name, t.Media, t.Sub, payload, "attachment", v == 7 ? $"photos/{t.Name}" : null)
                ]));
            }
        }

        return Require(list, 70, "Images");
    }

    private static List<CaseDef> OtherFiles()
    {
        (string Name, string Kind, string Media, string Sub, string Tag)[] types =
        [
            ("a.zip", "zip", "application", "zip", "archive"),
            ("a.7z", "7z", "application", "x-7z-compressed", "archive"),
            ("a.tar", "bin", "application", "x-tar", "archive"),
            ("a.gz", "gz", "application", "gzip", "archive"),
            ("a.txt", "txt", "text", "plain", "text"),
            ("a.csv", "txt", "text", "csv", "text"),
            ("a.html", "txt", "text", "html", "text"),
            ("a.xml", "txt", "application", "xml", "text"),
            ("a.json", "txt", "application", "json", "text"),
            ("a.md", "txt", "text", "markdown", "text"),
            ("meet.ics", "txt", "text", "calendar", "calendar"),
            ("me.vcf", "txt", "text", "vcard", "calendar"),
            ("a.mp3", "mp3", "audio", "mpeg", "media"),
            ("a.wav", "wav", "audio", "wav", "media"),
            ("a.mp4", "mp4", "video", "mp4", "media"),
            ("a.bin", "bin", "application", "octet-stream", "binary"),
            ("a.exe", "exe", "application", "octet-stream", "binary"),
            ("mail.msg", "msg", "application", "vnd.ms-outlook", "nested-rfc822"),
            ("a.dat", "bin", "application", "octet-stream", "binary"),
            ("a.rtf", "txt", "application", "rtf", "office")
        ];
        var list = new List<CaseDef>();
        var n = 0;
        foreach (var t in types)
        {
            for (var v = 1; v <= 4; v++)
            {
                n++;
                var id = $"F{n:D3}";
                var payload = t.Kind == "txt" ? Encoding.UTF8.GetBytes($"payload-{id}-{v}") : MimeBuild.Stub(t.Kind, id);
                list.Add(MimeBuild.WithParts(id, ["attachment", t.Tag, "rfc2046"], $"file-{n}", "see", false,
                [
                    (t.Name, t.Media, t.Sub, payload, "attachment", null)
                ]));
            }
        }

        return Require(list, 80, "OtherFiles");
    }

    private static List<CaseDef> Edges()
    {
        var list = new List<CaseDef>();
        for (var i = 1; i <= 15; i++)
        {
            var raw = $"""
                From: alice@example.com
                To: bob@example.com
                Subject: hello
                 folded-{i}
                Date: Tue, 15 Sep 2026 07:00:00 +0000
                Message-ID: <Z{i:D3}@golden.example>
                MIME-Version: 1.0
                Content-Type: text/plain; charset=utf-8

                folded-body-{i}
                """;
            list.Add(MimeBuild.RawEml($"Z{i:D3}", ["folded-header", "rfc5322"], raw, new GoldenExpect
            {
                Metadata = new Dictionary<string, GoldenField> { [MetadataKeys.Subject] = MimeBuild.Contains("hello", $"folded-{i}") },
                MainContent = MimeBuild.Eq($"folded-body-{i}"),
                AttachmentCount = 0
            }));
        }

        for (var i = 16; i <= 25; i++)
        {
            var payload = Encoding.UTF8.GetBytes("secret");
            var name = $"passwd-{i}.txt";
            list.Add(MimeBuild.WithParts($"Z{i:D3}", ["path-in-filename", "rfc2183"], $"trav-{i}", "x", false,
            [
                (name, "text", "plain", payload, "attachment", $"../../etc/{name}")
            ]));
        }

        for (var i = 26; i <= 40; i++)
        {
            var part = new MimePart("application", "octet-stream")
            {
                Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes($"nameless-{i}")))
            };
            var mixed = new Multipart("mixed")
            {
                new TextPart("plain") { Text = "x" },
                part
            };
            var m = MimeBuild.BaseMessage($"Z{i:D3}", $"nameless-{i}");
            m.Body = mixed;
            list.Add(MimeBuild.FromMessage($"Z{i:D3}", ["rfc2046"], m, new GoldenExpect
            {
                Metadata = MimeBuild.Meta($"nameless-{i}"),
                MainContent = MimeBuild.Eq("x"),
                AttachmentCount = 1
            }));
        }

        return Require(list, 40, "Edges");
    }

    private static List<CaseDef> Combos()
    {
        var list = new List<CaseDef>();
        var png = MimeBuild.Stub("png", "combo");
        for (var i = 1; i <= 70; i++)
        {
            var builder = new BodyBuilder
            {
                HtmlBody = $"<p>שלום {i} 😀</p><img src=\"cid:c{i}@x\" />",
                TextBody = $"plain-{i}"
            };
            var linked = builder.LinkedResources.Add("p.png", png, new ContentType("image", "png"));
            linked.ContentId = $"c{i}@x";
            linked.ContentDisposition = new ContentDisposition(ContentDisposition.Inline) { FileName = "p.png" };
            var office = MimeBuild.Stub("docx", $"Q{i:D3}");
            builder.Attachments.Add("r.docx", office, new ContentType("application", "vnd.openxmlformats-officedocument.wordprocessingml.document"));
            var m = MimeBuild.BaseMessage($"Q{i:D3}", $"combo {i} 中文");
            m.Headers.Add("X_Mailer", $"Combo-{i}");
            m.Body = builder.ToMessageBody();
            list.Add(MimeBuild.FromMessage($"Q{i:D3}", ["unicode", "emoji", "cid", "office", "x-headers", "html-body"], m, new GoldenExpect
            {
                Metadata = MimeBuild.Meta($"combo {i} 中文"),
                MainContent = MimeBuild.Contains($"cid:c{i}@x"),
                MainContentMimeType = "text/html",
                XFields = MimeBuild.Contains($"X_Mailer: Combo-{i}"),
                AttachmentCount = 2,
                Attachments =
                [
                    new GoldenAttachment { Name = "p.png", Sha256 = MimeBuild.Sha(png), Disposition = "inline" },
                    new GoldenAttachment { Name = "r.docx", Sha256 = MimeBuild.Sha(office), Size = office.Length }
                ]
            }));
        }

        return Require(list, 70, "Combos");
    }

    private static List<CaseDef> OptionalRfcs()
    {
        var list = new List<CaseDef>();
        // O2047 40
        string[] subjects =
        [
            "=?utf-8?B?4pi6IGVtb2pp?= extra",
            "Hello =?utf-8?Q?=F0=9F=98=80?=",
            "=?utf-8?B?16nXnNeV150=?= =?utf-8?B?INei15XXnNed?=",
            "=?windows-1255?Q?=F9=EC=E5=ED?=",
            "=?utf-8?B?5L2g5aW9?= and ascii"
        ];
        for (var i = 1; i <= 40; i++)
        {
            var subj = subjects[(i - 1) % subjects.Length] + $" #{i}";
            // Use MimeKit for decoded expected: set unicode subject directly, RFC2047 happens on write
            var decoded = new[] { "☺ emoji extra", "Hello 😀", "שלום עולם", "שלום", "你好 and ascii" }[(i - 1) % 5] + (i > 5 ? $" #{i}" : $" #{i}");
            list.Add(MimeBuild.Plain($"O2047-{i:D3}", ["rfc2047", "unicode"], decoded, $"o2047-{i}"));
        }

        // O2183 40 — unicode filenames (RFC 2231 filename*)
        for (var i = 1; i <= 40; i++)
        {
            var payload = Encoding.UTF8.GetBytes($"fn-{i}");
            var name = i % 2 == 0 ? $"קובץ-{i}.txt" : $"файл-{i}.txt";
            list.Add(MimeBuild.WithParts($"O2183-{i:D3}", ["rfc2183", "rfc2047", "unicode"], $"fn-{i}", "x", false,
            [
                (name, "text", "plain", payload, "attachment", null)
            ]));
        }

        // O2387 40
        var png = MimeBuild.Stub("png", "o2387");
        for (var i = 1; i <= 40; i++)
            list.Add(MimeBuild.RelatedCid($"O2387-{i:D3}", ["rfc2387", "rfc2392", "cid"], png, "rel.png", $"start{i}@rel"));

        // O2392 40 — two cid images
        for (var i = 1; i <= 40; i++)
        {
            var img1 = MimeBuild.Stub("png", $"o2392a-{i}");
            var img2 = i % 2 == 0 ? MimeBuild.Stub("jpg", $"o2392b-{i}") : MimeBuild.Stub("gif", $"o2392b-{i}");
            var builder = new BodyBuilder { HtmlBody = $"<img src=\"cid:a{i}@x\" /><img src=\"cid:b{i}@x\" />" };
            var a = builder.LinkedResources.Add("a.png", img1, new ContentType("image", "png"));
            a.ContentId = $"a{i}@x";
            a.ContentLocation = new Uri($"images/a{i}.png", UriKind.Relative);
            a.ContentDisposition = new ContentDisposition(ContentDisposition.Inline) { FileName = "a.png" };
            var bName = i % 2 == 0 ? "b.jpg" : "b.gif";
            var b = builder.LinkedResources.Add(bName, img2, new ContentType("image", i % 2 == 0 ? "jpeg" : "gif"));
            b.ContentId = $"b{i}@x";
            b.ContentDisposition = new ContentDisposition(ContentDisposition.Inline) { FileName = bName };
            var m = MimeBuild.BaseMessage($"O2392-{i:D3}", $"two-cid-{i}");
            m.Body = builder.ToMessageBody();
            list.Add(MimeBuild.FromMessage($"O2392-{i:D3}", ["rfc2392", "cid", "image"], m, new GoldenExpect
            {
                Metadata = MimeBuild.Meta($"two-cid-{i}"),
                MainContent = MimeBuild.Contains($"cid:a{i}@x", $"cid:b{i}@x"),
                MainContentMimeType = "text/html",
                AttachmentCount = 2,
                Attachments =
                [
                    new GoldenAttachment { Name = "a.png", Sha256 = MimeBuild.Sha(img1), Disposition = "inline", ContentLocation = $"images/a{i}.png" },
                    new GoldenAttachment { Name = bName, Sha256 = MimeBuild.Sha(img2), Disposition = "inline" }
                ]
            }));
        }

        // O6532 40 raw utf-8 headers
        for (var i = 1; i <= 40; i++)
        {
            var raw = $"""
                From: "יוסי" <yossi@example.com>
                To: bob@example.com
                Subject: עברית גולמית {i}
                Date: Tue, 15 Sep 2026 07:00:00 +0000
                Message-ID: <O6532-{i:D3}@golden.example>
                MIME-Version: 1.0
                Content-Type: text/plain; charset=utf-8
                Content-Transfer-Encoding: 8bit

                גוף {i}
                """;
            list.Add(MimeBuild.RawEml($"O6532-{i:D3}", ["rfc6532", "unicode", "rtl"], raw, new GoldenExpect
            {
                Metadata =
                {
                    [MetadataKeys.Subject] = MimeBuild.Contains("עברית גולמית"),
                    [MetadataKeys.From] = MimeBuild.Contains("yossi@example.com")
                },
                MainContent = MimeBuild.Contains("גוף"),
                MainContentMimeType = "text/plain",
                AttachmentCount = 0
            }));
        }

        return Require(list, 200, "OptionalRfcs");
    }

    private static MimeMessage WithBody(string id, string subject, string body)
    {
        var m = MimeBuild.BaseMessage(id, subject);
        m.Body = new TextPart("plain") { Text = body };
        return m;
    }

    private static GoldenExpect PlainExpect(string subject, string body) => new()
    {
        Metadata = MimeBuild.Meta(subject),
        MainContent = MimeBuild.Eq(body),
        MainContentMimeType = "text/plain",
        AttachmentCount = 0
    };

    private static bool StartsRtl(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            var v = rune.Value;
            if (v is >= 0x0590 and <= 0x08FF or >= 0xFB1D and <= 0xFDFF)
                return true;
            var cat = System.Text.Rune.GetUnicodeCategory(rune);
            if (cat is System.Globalization.UnicodeCategory.UppercaseLetter
                or System.Globalization.UnicodeCategory.LowercaseLetter
                or System.Globalization.UnicodeCategory.TitlecaseLetter
                or System.Globalization.UnicodeCategory.OtherLetter)
                return false;
        }

        return false;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var n = parts.Sum(p => p.Length);
        var r = new byte[n];
        var o = 0;
        foreach (var p in parts)
        {
            p.CopyTo(r, o);
            o += p.Length;
        }

        return r;
    }

    private static byte[] RandomBytes(int seed, int len)
    {
        var r = new Random(seed);
        var b = new byte[len];
        r.NextBytes(b);
        b[0] = 0x00;
        return b;
    }

    private static List<CaseDef> Require(List<CaseDef> list, int n, string name)
    {
        if (list.Count != n)
            throw new InvalidOperationException($"{name} produced {list.Count}, expected {n}");
        return list;
    }
}

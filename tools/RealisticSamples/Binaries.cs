using System.Globalization;
using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;

namespace RealisticSamples;

/// <summary>Deterministic synthetic attachment payloads (no real data).</summary>
internal static class Binaries
{

    public static byte[] Docx()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new Body(
                new Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("Synthetic quarterly report. Generated sample, no real data.")))));
        }
        return Normalize(ms.ToArray());
    }

    public static byte[] Xlsx()
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var wb = doc.AddWorkbookPart();
            var ws = wb.AddNewPart<WorksheetPart>();
            ws.Worksheet = new Worksheet(new SheetData(
                new Row(new Cell { CellReference = "A1", DataType = CellValues.InlineString, InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text("Item")) },
                        new Cell { CellReference = "B1", CellValue = new CellValue("42") })));
            wb.Workbook = new Workbook(new Sheets(new Sheet { Id = wb.GetIdOfPart(ws), SheetId = 1, Name = "Data" }));
        }
        return Normalize(ms.ToArray());
    }

    public static byte[] Pptx()
    {
        using var ms = new MemoryStream();
        using (var doc = PresentationDocument.Create(ms, PresentationDocumentType.Presentation))
        {
            var pp = doc.AddPresentationPart();
            pp.Presentation = new DocumentFormat.OpenXml.Presentation.Presentation(new SlideIdList());
        }
        return Normalize(ms.ToArray());
    }

    /// <summary>Minimal valid one-page PDF with correct xref offsets.</summary>
    public static byte[] Pdf()
    {
        var sb = new StringBuilder("%PDF-1.4\n");
        var offs = new List<int>();
        void Obj(string body) { offs.Add(sb.Length); sb.Append(CultureInfo.InvariantCulture, $"{offs.Count} 0 obj\n{body}\nendobj\n"); }
        Obj("<< /Type /Catalog /Pages 2 0 R >>");
        Obj("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>");
        const string content = "BT /F1 12 Tf 20 50 Td (Synthetic sample) Tj ET";
        Obj($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        var xref = sb.Length;
        sb.Append(CultureInfo.InvariantCulture, $"xref\n0 {offs.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offs) sb.Append(CultureInfo.InvariantCulture, $"{o:D10} 00000 n \n");
        sb.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {offs.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    /// <summary>16x16 RGB PNG; seed varies the colour so samples differ.</summary>
    public static byte[] Png(byte seed)
    {
        const int w = 16, h = 16;
        var raw = new byte[h * (1 + w * 3)];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * (1 + w * 3) + 1 + x * 3;
                raw[i] = (byte)(seed + x * 8); raw[i + 1] = (byte)(seed * 3 + y * 8); raw[i + 2] = seed;
            }
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        Be(ihdr, 0, w); Be(ihdr, 4, h); ihdr[8] = 8; ihdr[9] = 2;
        Chunk(ms, "IHDR", ihdr);
        using var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.NoCompression, true)) zs.Write(raw);
        Chunk(ms, "IDAT", z.ToArray());
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    /// <summary>Hand-built TNEF stream (winmail.dat) holding the given files.</summary>
    public static byte[] Tnef(params (string Name, byte[] Data)[] files)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(0x223E9F78u); w.Write((ushort)1);
        foreach (var (name, data) in files)
        {
            Attr(w, 0x00069002, new byte[14]);                          // attAttachRenddata
            Attr(w, 0x00018010, [.. Encoding.ASCII.GetBytes(name), 0]); // attAttachTitle
            Attr(w, 0x0006800F, data);                                  // attAttachData
        }
        w.Flush();
        return ms.ToArray();
    }

    static void Attr(BinaryWriter w, uint id, byte[] data)
    {
        w.Write((byte)2); w.Write(id); w.Write((uint)data.Length); w.Write(data);
        ushort sum = 0; foreach (var b in data) sum += b;
        w.Write(sum);
    }

    static void Be(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; Be(len, 0, data.Length); s.Write(len);
        var td = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type).CopyTo(td, 0); data.CopyTo(td, 4);
        s.Write(td);
        var crc = new byte[4]; Be(crc, 0, (int)Crc32(td)); s.Write(crc);
    }

    static uint Crc32(byte[] d)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in d) { c ^= b; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; }
        return ~c;
    }

    /// <summary>Re-zip (stored) with sorted entries, fixed timestamp and sequential relationship ids so bytes are reproducible.</summary>
    static byte[] Normalize(byte[] zip)
    {
        using var src = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var entries = src.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal)
            .Select(e => { using var s = e.Open(); using var m = new MemoryStream(); s.CopyTo(m); return (e.FullName, Data: m.ToArray()); })
            .ToList();
        // SDK assigns random "R<16 hex>" relationship ids; remap them in order of first appearance.
        var rx = new System.Text.RegularExpressions.Regex("R[0-9a-f]{16}");
        var map = new Dictionary<string, string>();
        foreach (var (name, data) in entries.Where(e => e.FullName.EndsWith(".rels", StringComparison.Ordinal)))
            foreach (System.Text.RegularExpressions.Match m in rx.Matches(Encoding.UTF8.GetString(data)))
                map.TryAdd(m.Value, "rId" + (map.Count + 1));
        return WriteStoredZip(entries.Select(e =>
        {
            var data = e.Data;
            if (e.FullName.EndsWith(".xml", StringComparison.Ordinal) || e.FullName.EndsWith(".rels", StringComparison.Ordinal))
                data = Encoding.UTF8.GetBytes(rx.Replace(Encoding.UTF8.GetString(data), m => map.GetValueOrDefault(m.Value, m.Value)));
            return (e.FullName, data);
        }).ToList());
    }

    /// <summary>
    /// Minimal stored (uncompressed) zip writer. System.IO.Compression stamps platform-specific
    /// header fields (version-made-by, unix attributes), so it cannot be byte-identical across OSes.
    /// </summary>
    static byte[] WriteStoredZip(List<(string Name, byte[] Data)> files)
    {
        const ushort dosTime = 0, dosDate = (2020 - 1980) << 9 | 1 << 5 | 1; // 2020-01-01 00:00:00
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var offsets = new List<uint>();
        foreach (var (name, data) in files)
        {
            offsets.Add((uint)ms.Position);
            var n = Encoding.UTF8.GetBytes(name);
            w.Write(0x04034B50u); w.Write((ushort)20); w.Write((ushort)0x0800); w.Write((ushort)0);
            w.Write(dosTime); w.Write(dosDate); w.Write(Crc32(data)); w.Write((uint)data.Length); w.Write((uint)data.Length);
            w.Write((ushort)n.Length); w.Write((ushort)0); w.Write(n); w.Write(data);
        }
        var cdStart = (uint)ms.Position;
        for (var i = 0; i < files.Count; i++)
        {
            var (name, data) = files[i];
            var n = Encoding.UTF8.GetBytes(name);
            w.Write(0x02014B50u); w.Write((ushort)20); w.Write((ushort)20); w.Write((ushort)0x0800); w.Write((ushort)0);
            w.Write(dosTime); w.Write(dosDate); w.Write(Crc32(data)); w.Write((uint)data.Length); w.Write((uint)data.Length);
            w.Write((ushort)n.Length); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0);
            w.Write(0u); w.Write(offsets[i]); w.Write(n);
        }
        var cdSize = (uint)ms.Position - cdStart;
        w.Write(0x06054B50u); w.Write((ushort)0); w.Write((ushort)0);
        w.Write((ushort)files.Count); w.Write((ushort)files.Count); w.Write(cdSize); w.Write(cdStart); w.Write((ushort)0);
        w.Flush();
        return ms.ToArray();
    }
}

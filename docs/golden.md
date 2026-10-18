# Golden set plan — 1,000 core + 200 RFC extras

Authored cases (not parser dumps). Each row has frozen `input.eml` + `expected.json` written from the **same spec** the generator used (subject, body, payload SHA-256). The extractor is never the source of truth.

Layout: `corpus/golden/bulk/<id>/` (1200 cases) and `corpus/golden/seed/` (small CI set).  
Catalog: `corpus/golden/catalog.jsonl`

## Why 1,200

Original ask: detection, metadata (classic + all headers + `xfields`), main body, attachments (name/size/path/bytes, no recurse), correctness for unicode and attachment mix, RFC 5322 + MIME 2045–2049.

+200: RFC 2047, 2183, 2387, 2392, 6532 edges.

Unlabeled TREC volume is not this set.

## File types that must appear (attachments)

| Class | Extensions | MIME (typical) |
| --- | --- | --- |
| Word | doc, docx, odt, rtf | msword / openxml / opendocument / rtf |
| Excel | xls, xlsx, ods, csv | msexcel / openxml / ods / csv |
| PowerPoint | ppt, pptx, odp | mspowerpoint / openxml / odp |
| PDF | pdf | application/pdf |
| Images | png, jpg, gif, webp, bmp, tif, svg, ico, heic | image/* |
| Nested mail | eml (message/rfc822), msg (opaque ole bytes) | rfc822 / vnd.ms-outlook |
| Archives | zip, 7z, tar, gz | zip / x-7z / tar / gzip |
| Calendar / contact | ics, vcf | text/calendar / vcard |
| Text / data | txt, html, xml, json, md | text/* / json |
| Media | mp3, wav, mp4 | audio/* / video/* |
| Generic | bin, dat, exe | octet-stream |

Payloads are **tiny typed stubs** (correct magic / PK header / `%PDF`), not real malware or full OOXML. Quality bar is: decode + name + hash. Inner Office parse is out of scope.

## 1,000 core (original plan)

| Prefix | N | What it represents | RFC |
| --- | ---: | --- | --- |
| D | 40 | Detector: real EML vs OLE/PDF/ZIP/PNG/JPEG/JSON/HTML/empty/mbox/BOM | 5322 sniff |
| H | 70 | Classic From/To/Cc/Bcc/Reply-To/Sender/Subject/Date/Message-ID/Received | 822/2822/5322 |
| X | 50 | All headers + `X-` / `X_` → `xfields` (DKIM stays out of xfields) | 5322 |
| U | 100 | Emoji, RTL, CJK, Indic, Thai, Cyrillic, mixed scripts in subject+body | 2047 + 5322 |
| C | 50 | Charsets: UTF-8, 1255, 1252, ISO-8859-1/8, KOI8-R, GBK, Shift-JIS, Big5 | 2045/2047 |
| B | 70 | Main body: plain, html, alternative (html wins), empty, calendar-as-body | 2046 |
| E | 40 | Transfer encodings: 7bit, 8bit, quoted-printable, base64, corrupt base64 | 2045 |
| M | 50 | multipart/mixed, alternative, parallel, digest, signed (dummy p7s) | 2046 |
| P | 40 | Content-Disposition attachment/inline, filename, path in filename | 2183 |
| R | 60 | multipart/related + `cid:` copy-paste images vs attached images | 2387/2392 |
| S | 30 | SMTPUTF8 addresses / UTF-8 local parts | 6532 |
| A | 100 | Office + PDF attachments (all types above), single and multi-attach | 2046/2183 |
| N | 40 | Nested `message/rfc822` opaque; no recurse into inner parts | 822 as part |
| I | 70 | Image attachments all raster/vector types; inline vs attachment | 2046/2183 |
| F | 80 | Non-office file types: zip/ics/vcf/txt/html/xml/json/media/exe/msg | 2046 |
| Z | 40 | Edges: folded headers, missing body, path traversal, nameless part | 5322/2045 |
| Q | 70 | Combinations: unicode + office + cid + x-headers in one message | mix |
| **Sum** | **1000** | | |

## +200 optional RFC extras

| Prefix | N | Focus |
| --- | ---: | --- |
| O2047 | 40 | Encoded-words: B/Q, split words, language tag, nested, mixed charsets in one subject |
| O2183 | 40 | `filename*`, RFC 2231 continuations, quoted-printable names, `inline` without name |
| O2387 | 40 | `multipart/related` `type=` / `start=` / extra related parts |
| O2392 | 40 | `cid:` with/without brackets, `Content-Location`, multiple inlined images |
| O6532 | 40 | Raw UTF-8 header fields (no encoded-word), unicode Message-ID-ish, UTF-8 From |
| **Sum** | **200** | |

## Tag vocabulary (scorer slices)

`detect`, `not-eml`, `rfc5322`, `rfc2045`, `rfc2046`, `rfc2047`, `rfc2183`, `rfc2387`, `rfc2392`, `rfc6532`, `x-headers`, `unicode`, `emoji`, `rtl`, `charset-legacy`, `html-body`, `plain-body`, `multipart-alternative`, `cid`, `inline`, `attachment`, `office`, `pdf`, `image`, `nested-rfc822`, `no-recurse`, `archive`, `calendar`, `path-in-filename`, `empty-body`, `corrupt-transfer-encoding`

## What we will not gold

- Full `Received` traces
- Warning strings
- Real-world TNEF/winmail.dat is covered by the public harvest (see README: Apache POI HMEF samples), not the committed goldens; ADR-018 adds a committed TNEF sample under `corpus/realistic/`
- Recursive Office content
- Latency

## Tooling

```bash
dotnet run --project tools/GoldenKit -- gen     # write 1200 cases + catalog.jsonl
dotnet run --project tools/GoldenKit -- score   # print % and per-tag report
```

Generator **must not** call `EmlExtractor` when writing `expected.json`.

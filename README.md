# EML extraction

C# / .NET 10 library for RFC 822/2822/5322 mail and RFC 2045–2049 MIME. Parser: [MimeKit](https://github.com/jstedfast/MimeKit) 4.18.0 (MIT).

Repo: https://github.com/yoavarad/eml-extractor

---

## Interface

### 1. Detect

Input: bytes. Output: whether the buffer is an EML (RFC 5322) message.

```csharp
bool ok = new EmlDetector().IsEml(bytes);
```

Sniffs headers (and mbox `From `). Ignores file extension. Rejects OLE `.msg`, PDF, ZIP, PNG, JPEG, and markup/JSON (a first byte of `{`, `[` or `<`).

### 2. Extract

Input: a buffer already identified as EML. Output: metadata, main content, nested files.

```csharp
EmlExtractionResult result = new EmlExtractor().Extract(bytes);
```

Does not throw on malformed MIME; problems go into `result.Warnings`.

#### Metadata

- Classic fields: `from`, `to`, `cc`, `bcc` (always present; empty string if the header is missing), plus `replyto`, `sender`, `subject`, `date`, `messageid`, and the rest of the usual envelope headers
- `subject_direction` and `content_direction`: `ltr` or `rtl` (HTML `dir` / CSS `direction`, otherwise Unicode first-strong letter)
- Every other header as well (keys lowercased, `-` → `_`)
- Headers that start with the literal `X_` (capital X, underscore) are concatenated into `Metadata["xfields"]` (`""` when none); `X-` headers stay under their own metadata keys  
  (`Name: value` per line)

#### Content

`result.MainContent` is the main body of the mail. If both HTML and plain exist (`multipart/alternative`), HTML is used.

Text direction: HTML `dir="rtl"` / `dir="ltr"` (and `direction:` CSS) is kept in the HTML and copied to `result.ContentDirection`. Plain text uses the first strong character (Hebrew/Arabic → `rtl`, Latin/CJK → `ltr`). Unicode bidi marks (`U+200F` RLM, `U+200E` LRM) are not stripped.

#### Nested files (attachments)

For each attachment:

| Field | Meaning |
| --- | --- |
| `Name` | File name (basename) |
| `Size` | Decoded size |
| `FullPath` | Full path if the MIME filename contained `/` or `\` |
| `Data` | Decoded binary |

No further extraction: no file-type detection, no parsing inside Office/PDF/images, no walking a nested `.eml`. A forwarded mail is one opaque `.eml` blob. Copy-paste images (`cid:` / `multipart/related`) are attachments with the image bytes.

---

## Setup and run

Requires the .NET 10 SDK (`global.json` pins `10.0.401`). If `dotnet` is missing:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
```

Clone and restore:

```bash
git clone https://github.com/yoavarad/eml-extractor.git
cd eml-extraction
dotnet restore
```

### Use from code

Reference `src/EmlExtraction/EmlExtraction.csproj` and call `EmlDetector` / `EmlExtractor` as above.

### Tests, score, throughput

```bash
dotnet test                                                    # unit tests + seed goldens (+ public fixtures if present)
dotnet run --project tools/GoldenKit -c Release -- score       # 1200-case quality score
dotnet run --project tools/GoldenKit -c Release -- probe       # harvest local public mail under corpus/internal/
dotnet run --project benches/EmlExtraction.Bench -c Release    # max files/sec on this machine
```

Regenerate the bulk golden files from the authored catalog (does **not** use the extractor to write expected values):

```bash
dotnet run --project tools/GoldenKit -c Release -- gen
```

### Layout

```
src/EmlExtraction/           detect + extract library
tests/EmlExtraction.Tests/   unit tests + seed goldens
benches/EmlExtraction.Bench  files/sec
tools/GoldenKit/             generate and score goldens
corpus/golden/seed/          small set run by `dotnet test`
corpus/golden/bulk/          1200 tagged cases
corpus/golden/catalog.jsonl  one row per bulk case
corpus/internal/             public real mail (gitignored; see Real-world tests)
docs/                        requirements map, golden plan, perf, edge cases
```

---

## Tests (what the original spec asked for)

### 1. Load — max files per second

Measured on Apple M1 Pro, 1 thread, Release, in-memory `byte[]` (no disk). Use this to size cores and RAM.

| Mix | Extract / s |
| --- | ---: |
| Tiny mail | ~48,700 |
| ~20 KB HTML | ~16,900 |
| Mail + 1 MB attachment | ~259 |

Multi-core capacity is measured, not estimated: `dotnet run -c Release --project benches/EmlExtraction.Bench -- sweep` runs the DOP sweep (ADR-015). Attachment-heavy mail is RAM-bound. Full table and sweep options: [docs/perf-results.md](docs/perf-results.md).

### 2. Correctness A — emoji and exotic languages

Subject and body round-trip for Hebrew, Arabic, Chinese, Japanese, Hindi, Thai, Russian, emoji, mixed scripts, RFC 2047 encoded-words, Windows-1255 / other legacy charsets, SMTPUTF8 addresses.

Golden tags: `hebrew` (12/12), `arabic` (10/10), `rtl`, `emoji`, `unicode`, `rfc2047`, `rfc6532`.

### 3. Correctness B — attachment mix

Office-like parts (doc/docx/xls/xlsx/ppt/pptx/odt/ods/odp/rtf/pdf), nested emails (`message/rfc822`, not recursed), attached images, copy-paste CID images, plus zip/ics/vcf/media/exe stubs.

Quality bar in CI: **name + decoded SHA-256**. Payloads there are tiny typed stubs. Real Office/CID/TNEF files are covered by the public harvest below, not by the committed goldens.

---

## Real-world tests

Public mail, not in git (`corpus/internal/` is gitignored; licenses and PII vary). Two layers:

1. **Harvest** — detector + extract over every local public sample (`golden probe`). Last run: **12,853** detected messages, **0** throws, **0** parse failures, **0** warnings, ~3,500 extract/s.
2. **Targeted asserts** — `InternalCorpusTests` (17 cases). Skipped automatically if the files are not on disk; CI does not require them.

`dotnet test` includes those asserts when the files exist. Re-run the harvest with `golden probe` (see commands above). Full map: [docs/edge-cases.md](docs/edge-cases.md).

| What was proven | Source | Result |
| --- | --- | --- |
| Does not crash on mixed real mail | SpamAssassin 2003/2005, Nazario phishing mboxes, [cw-l/email-corpus](https://github.com/cw-l/email-corpus), MimeKit TestData | 12,853 / 12,853 detected; mbox `From ` handled |
| TNEF / `winmail.dat` unpacks to real files | Apache POI HMEF `quick-winmail.dat`, `bug52400-…dat`; Zimbra Outlook 11 `tnef.msg`; MimeKit `ukr.eml` | OLE `.doc` + PDF + PNG; **0** leftover `winmail.dat` (82 TNEF-header mails) |
| Real Office MIME (not stubs) | php-mime-mail-parser `issue274.eml`; ProtonMail parser `forwarding_html_attachment.eml` | OOXML `docx` (PK, 11 KB) + PDF + ODT; Proton `docx`/`xlsx`/`pdf` (PK / `%PDF-` magic; Proton zips are truncated) |
| Nested `message/rfc822` stays opaque | `issue158a`, `issue274.eml`; 154 nested `.eml` in harvest | One `.eml` blob; inner parts not promoted |
| CID paste image | php-mails `m0129`; Chatwoot Outlook 16.0 fixture | Complete PNG + `cid:` left in HTML. Chatwoot JPEG is truncated upstream |
| Outlook `image001.png@01D…` shape | Headers from [Freescout #1162](https://github.com/freescout-help-desk/freescout/issues/1162) | HTML `cid:` matches inline `Content-ID`; PNG is a stand-in (GitHub stripped the original base64) |
| Hebrew from real clients | Gmail `issue149` (`iso-8859-8-i`); 1992 Bellcore ISO-8859-8 in MimeKit `jwz.mbox` | Subject `מענה 'אני לא נמצא': Invoice …`; body/subject `תירבע` |
| Exchange-style headers / `xfields` | [Lee Holmes O365 gist](https://gist.github.com/LeeHolmes/d83dbfd7d4683de2f3e517fdd71e1923); 113 harvest mails | Max 71 headers, 68 xfield lines |
| Legacy charsets, DSN, S/MIME | Jimmy Shift_JIS; GB2312 spam; Thunderbird signed | Decode without throw; `.p7s` kept as attachment |

Not used: **Enron** (attachments already stripped; 422 MB download aborted). **TREC 2007** Waterloo tarball is 404.

Still missing vs live corp mail: Outlook/Gmail Arabic body, Outlook Hebrew body, header block >16 KB, TNEF wrapping modern `.docx`.

---

## Golden set

Expected values are written with the EML (subject, body, hashes). The parser is never used as the source of truth when generating `expected.json`.

| Set | Size | When it runs |
| --- | ---: | --- |
| Seed | ~12 cases | `dotnet test` |
| Bulk | 1000 core + 200 RFC extras = **1200** | `golden score` |

Last score: **1212/1212 cases**, **11333/11333 checks** (seed + bulk). Report: [docs/golden-score.txt](docs/golden-score.txt). Slices: [docs/golden.md](docs/golden.md).

Bulk families include detection, classic headers, `xfields`, unicode/emoji/RTL, charsets, HTML vs plain, transfer encodings, CID paste, SMTPUTF8, Office/PDF, nested mail, images, archives, folded headers, path-in-filename, and RFC 2047 / 2183 / 2387 / 2392 / 6532 extras.

Spec ↔ code: [docs/requirements.md](docs/requirements.md). Edge-case map (including the public harvest): [docs/edge-cases.md](docs/edge-cases.md).

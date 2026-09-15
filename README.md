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

Sniffs headers (and mbox `From `). Ignores file extension. Rejects OLE `.msg`, PDF, ZIP, PNG, JPEG, JSON.

### 2. Extract

Input: a buffer already identified as EML. Output: metadata, main content, nested files.

```csharp
EmlExtractionResult result = new EmlExtractor().Extract(bytes);
```

Does not throw on malformed MIME; problems go into `result.Warnings`.

#### Metadata

- Classic fields: `from`, `to`, `cc`, `bcc`, `replyto`, `sender`, `subject`, `date`, `messageid`, and the rest of the usual envelope headers
- Every other header as well (keys lowercased, `-` → `_`)
- Headers that start with `X_` **or** `X-` (the usual custom-header form) are concatenated into one string: `result.XFields`  
  (`Name: value` per line)

#### Content

`result.MainContent` is the main body of the mail. If both HTML and plain exist (`multipart/alternative`), HTML is used.

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
dotnet test                                                    # unit tests + seed goldens
dotnet run --project tools/GoldenKit -c Release -- score       # 1200-case quality score
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

Rough multi-core: `files/sec ≈ extract/s × cores × 0.7`. Attachment-heavy mail is RAM-bound. Full table and formula: [docs/perf-results.md](docs/perf-results.md).

### 2. Correctness A — emoji and exotic languages

Subject and body round-trip for Hebrew, Arabic, Chinese, Japanese, Hindi, Thai, Russian, emoji, mixed scripts, RFC 2047 encoded-words, Windows-1255 / other legacy charsets, SMTPUTF8 addresses.

Golden tags: `hebrew` (12/12), `arabic` (10/10), `rtl`, `emoji`, `unicode`, `rfc2047`, `rfc6532`.

### 3. Correctness B — attachment mix

Office-like parts (doc/docx/xls/xlsx/ppt/pptx/odt/ods/odp/rtf/pdf), nested emails (`message/rfc822`, not recursed), attached images, copy-paste CID images, plus zip/ics/vcf/media/exe stubs.

Quality bar: **name + decoded SHA-256**. Payloads are tiny typed stubs, not full real OOXML files.

---

## Golden set

Expected values are written with the EML (subject, body, hashes). The parser is never used as the source of truth when generating `expected.json`.

| Set | Size | When it runs |
| --- | ---: | --- |
| Seed | ~12 cases | `dotnet test` |
| Bulk | 1000 core + 200 RFC extras = **1200** | `golden score` |

Last score: **1212/1212 cases**, **11126/11126 checks** (seed + bulk). Report: [docs/golden-score.txt](docs/golden-score.txt). Slices: [docs/golden.md](docs/golden.md).

Bulk families include detection, classic headers, `xfields`, unicode/emoji/RTL, charsets, HTML vs plain, transfer encodings, CID paste, SMTPUTF8, Office/PDF, nested mail, images, archives, folded headers, path-in-filename, and RFC 2047 / 2183 / 2387 / 2392 / 6532 extras.

Still missing from real corp mail (not in this set): Outlook `winmail.dat` (TNEF), real Outlook paste, Exchange mega-headers. How to add: [docs/edge-cases.md](docs/edge-cases.md). Spec ↔ code: [docs/requirements.md](docs/requirements.md).

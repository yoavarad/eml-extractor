# Edge cases catalog

Status: `tested` = covered by generated unit tests. `internal-missing` = need a real sample from the team corpus.

| ID | Case | Status | Notes |
| --- | --- | --- | --- |
| E01 | Subject with emoji + RFC2047 | tested | `ExoticLanguageTests` |
| E02 | Hebrew / Arabic RTL in subject and body | tested | |
| E03 | CJK, Hindi, Thai, Russian | tested | |
| E04 | charset Windows-1255 | tested | `ExtractionTests.Windows1255BodyDecodes` |
| E05 | docx/xlsx/pptx/pdf/zip/jpg/gif payloads | tested | SHA-256 round-trip of dummy bytes; not real OOXML validity |
| E06 | nested `message/rfc822` | tested | outer does not recurse; inner EML is one attachment |
| E07 | attached image | tested | |
| E08 | inline CID paste image | tested | HTML keeps `cid:`; bytes extracted |
| E09 | folded headers | tested | |
| E10 | multiple `X-` / `X_` headers | tested | concatenated into `XFields` |
| E11 | mbox `From ` first line | tested | detector only |
| E12 | UTF-8 BOM | tested | detector |
| E13 | headers-only, empty body | tested | no exception |
| E14 | ~1MB attachment | bench | `attach-1mb` case; not a 50MB soak |
| E15 | corrupt base64 body | tested | parse does not crash |
| E16 | `multipart/signed` + `smime.p7s` | tested | dummy signature bytes, not a real CMS verify |
| E17 | Outlook `winmail.dat` TNEF | **internal-missing** | code path: `TnefPart.ExtractAttachments()`. Need a real TNEF from corp mail. |
| E18 | Outlook extra-long / dotted X-MS-Exchange headers | **internal-missing** | mapper will keep them in metadata + xfields; need size/perf sample |
| E19 | `.msg` OLE disguised as eml | tested | `D0 CF 11 E0` rejected |
| E20 | S/MIME enveloped (`application/pkcs7-mime`) | **internal-missing** | expected: opaque blob attachment, no decrypt without key |
| E21 | filename path traversal | tested | `Name=passwd.txt`, `FullPath=../../etc/passwd.txt`; core never writes disk |
| E22 | SMTPUTF8 unicode addresses | tested | |
| E23 | duplicate `Received` | tested | concatenated with `\n` |
| E24 | Apple Mail uuencode / partial | **internal-missing** | |
| E25 | GBK / Shift-JIS bodies | **internal-missing** | 1255 covered; add if corp mail uses these |
| E26 | `multipart/report` (DSN bounce) | **internal-missing** | |
| E27 | huge header block (Exchange) | **internal-missing** | detector only sniffs 16KB |
| E28 | encrypted PGP `multipart/encrypted` | **internal-missing** | treat payload as attachment |
| E29 | nameless part (no filename, no cid) | tested indirectly | fallback `part-{n}.{subtype}` |
| E30 | 50MB+ single part | **internal-missing** | no size cap in core; factory should bound |

When adding an internal sample, drop it under a local `corpus/internal/` (gitignored) and add a test that only runs if the file exists, so CI stays clean.

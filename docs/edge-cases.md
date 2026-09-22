# Edge cases catalog

Status: `tested` = generated unit tests. `public-sample` = real public mail under gitignored `corpus/internal/` (`InternalCorpusTests` + `golden probe`). `internal-missing` = still need a corp/team sample.

Harvest (2026-09-15, second pass): **12,853** detected messages. **0 throws, 0 parse_failed, 0 warnings** on detected mail. ~3,500 extract/s.

Added this pass (all public, gitignored): Apache POI HMEF TNEF (real `.doc` + `.pdf`), php-mime-mail-parser issue mails (real OOXML/PDF/ODT + nested rfc822 + Gmail Hebrew), ProtonMail parser fixtures, GroupDocs/Chatwoot/Tika CID and attachment samples, Bellcore ISO-8859-8 Hebrew from `jwz.mbox`.

| ID | Case | Status | Notes |
| --- | --- | --- | --- |
| E01 | Subject with emoji + RFC2047 | tested | `ExoticLanguageTests` |
| E02 | Hebrew / Arabic RTL in subject and body | tested + public-sample | Direction reported as `rtl`/`ltr`. HTML `dir` preserved. Gmail Hebrew subject (`issue149`); 1992 Bellcore Hebrew body. |
| E03 | CJK, Hindi, Thai, Russian | tested | plus real ISO-2022-JP (`gaps/iso2022jp.eml`) |
| E04 | charset Windows-1255 | tested | `ExtractionTests.Windows1255BodyDecodes` |
| E05 | docx/xlsx/pptx/pdf/zip/jpg/gif payloads | tested + public-sample | CI goldens are SHA-256 stubs. Public: SwiftMailer `Hello from SwiftMailer.docx` (PK, 11 KB) + PDF + ODT; ProtonMail `test.docx`/`test.xlsx` (PK magic, truncated zip); TNEF `quick.doc` (OLE) + `quick.pdf` |
| E06 | nested `message/rfc822` | tested + public-sample | 154 nested `.eml` in harvest; `issue158a` / `issue274.eml` stay opaque |
| E07 | attached image | tested + public-sample | harvest: hundreds of gif/jpg/png; Tika `testPNG.png` 17 KB |
| E08 | inline CID paste image | tested + public-sample | 493 CID messages / 602 CID parts. Complete PNG paste: php-mails `m0129`. Outlook `image001.png@01D8693E.BE86BE20` fixture uses Freescout #1162 headers; GitHub stripped the original base64 so the PNG bytes are a stand-in |
| E09 | folded headers | tested | |
| E10 | multiple `X-` / `X_` headers | tested | max 68 xfield lines (public phishing/O365-style sample) |
| E11 | mbox `From ` first line | tested | detector **and** extractor (`MimeFormat.Mbox`); SpamAssassin files start this way |
| E12 | UTF-8 BOM | tested | detector |
| E13 | headers-only, empty body | tested | 352 empty-body in harvest, no crash |
| E14 | ~1MB attachment | bench + public-sample | harvest max attach ~3.2 MB (`php-mails/m0006`) |
| E15 | corrupt base64 body | tested | parse does not crash |
| E16 | `multipart/signed` + `smime.p7s` | tested | dummy goldens + real Thunderbird signed (`gaps/smime-signed.eml`) |
| E17 | Outlook `winmail.dat` TNEF | **public-sample** | MimeKit `ukr.eml`, Zimbra `tnef.msg`, Apache POI `quick-winmail.dat` (OLE `.doc` + PDF) and `bug52400` (193 KB PDF + PNG). 82 TNEF-header mails; **0** left as opaque `winmail.dat`. No public TNEF wrapping modern `.docx` |
| E18 | Outlook extra-long / dotted X-MS-Exchange headers | **public-sample** | Lee Holmes O365 dump + 113 harvest mails with `ms_exchange` keys. Max 71 headers |
| E19 | `.msg` OLE disguised as eml | tested | `D0 CF 11 E0` rejected |
| E20 | S/MIME enveloped (`application/pkcs7-mime`) | **public-sample** | 13× `.p7m` + 28× `.x-pkcs7-mime` in harvest as opaque attachments (no decrypt) |
| E21 | filename path traversal | tested | |
| E22 | SMTPUTF8 unicode addresses | tested | |
| E23 | duplicate `Received` | tested | |
| E24 | Apple Mail uuencode / partial | **public-sample** | `jwz.mbox` has `x-uue`; MimeKit `message/partial` (19 parts). Not reconstructed across fragments |
| E25 | GBK / Shift-JIS bodies | **public-sample** | Shift_JIS (`gaps/shift-jis.eml`), GB2312 spam (`gaps/gb2312.eml`), ISO-2022-JP. Exact `GBK` label still rare |
| E26 | `multipart/report` (DSN bounce) | **public-sample** | Jimmy `report_422.eml` + 19 `delivery-status` parts in harvest |
| E27 | huge header block (Exchange) | **public-sample** | Lee Holmes ~12 KB. Detector sniffs 16 KB. Still no public mail **>16 KB** of headers |
| E28 | encrypted PGP `multipart/encrypted` | **internal-missing** | 98 `pgp-signature` parts found; no `multipart/encrypted` payload in this harvest |
| E29 | nameless part (no filename, no cid) | tested indirectly | fallback `part-{n}.{subtype}` |
| E30 | 50MB+ single part | **internal-missing** | probe skips files >40 MB; no such public sample fetched |

When adding a sample, drop it under local `corpus/internal/` (gitignored). `InternalCorpusTests` and `golden probe` run only if the files exist.

## Public sources used

- SpamAssassin 2003/2005 ham+spam (~6k messages): https://spamassassin.apache.org/old/publiccorpus/
- Nazario phishing mboxes: https://monkey.org/~jose/phishing/
- Real-world redacted EML: https://github.com/cw-l/email-corpus (~1.2k)
- MimeKit TestData (MIT): messages, mbox, tnef, smime, partial, openpgp
- Zimbra `tnef.msg`: Outlook 11 winmail.dat
- Jimmy test-data: DSN + Shift_JIS
- Exchange headers: https://gist.github.com/LeeHolmes/d83dbfd7d4683de2f3e517fdd71e1923
- CID fixture: Chatwoot Outlook 16.0 shaped (truncated JPEG) + reconstructed Outlook `image001.png@01D…` from [Freescout #1162](https://github.com/freescout-help-desk/freescout/issues/1162) headers
- Apache POI HMEF: https://github.com/apache/poi/tree/trunk/test-data/hmef (`quick-winmail.dat`, `bug52400-winmail-with-attachments.dat`)
- php-mime-mail-parser `tests/mails`: https://github.com/php-mime-mail-parser/php-mime-mail-parser (issue274 Office mix, issue149 Hebrew, issue158 nested rfc822, m0129 CID PNG)
- ProtonMail proton-bridge parser testdata: `forwarding_html_attachment.eml`
- Apache Tika mail-module test documents; GroupDocs Conversion sample EML

Not used: **Enron** (attachments stripped; 422 MB download was too slow to finish). **TREC 2007** Waterloo tarball now 404.

Still missing from **real corp mail**: live Outlook/Gmail Arabic body, live Outlook Hebrew body, header block >16 KB, TNEF wrapping modern `.docx`, `multipart/encrypted` PGP, original (unstripped) Outlook paste PNG bytes.

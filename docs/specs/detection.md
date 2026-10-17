# Detection

## Why it exists

The downstream pipeline receives files of every kind and needs a cheap, extension-blind answer to one question: is this a mail message? The original specification asks for exactly that, with bytes in and a yes/no out. The detector, [ydk:contract:api/eml-detector], answers it without parsing MIME. On small mail it runs 5 to 15 times faster than extraction ([ydk:nfr:performance/detect-tiny-throughput]).

## How it decides

The detector works only on the leading bytes of the buffer and never throws.

1. **Too short.** Buffers below the minimum length are rejected outright. The threshold is in [ydk:req:detection/detect-eml].
2. **Byte-order mark.** One leading UTF-8 byte-order mark is ignored.
3. **Known non-mail formats.** Before anything looks like headers, the buffer is checked against the leading-byte signatures of Outlook/OLE compound files, PDF, PNG, JPEG, and ZIP. A buffer whose first byte opens JSON or markup (`{`, `[`, or `<`) is also rejected ([ydk:req:detection/reject-non-email-magic]). The tests show why the check comes first: such files can contain header-like text, for example a PDF whose text includes "From:". An Outlook `.msg` file is an OLE container, not RFC 5322 text, so it is rejected even though it holds mail.
4. **Sniff window.** Only the first 16 KB are examined.
5. **mbox separator.** Files exported from mbox archives, such as the SpamAssassin corpus, begin with a "From " separator line. That line is skipped, and the header check runs on the lines after it. The extractor honours the same convention ([ydk:req:extraction/mbox-from-line]).
6. **Header block shape.** The leading lines must look like a mail header block: a few well-formed header lines, at least one of them a well-known envelope header. The block may end at the blank line that separates headers from body or at the first line that is not a header; either way is accepted. A block that does neither before the sniff window runs out must be slightly longer to count. The exact thresholds and the list of envelope headers are in [ydk:req:detection/detect-eml].

## What it does not do

It does not validate MIME structure, decode anything, or guarantee that extraction will succeed. A positive answer means "worth extracting". The extractor can still record [ydk:warning:extraction/parse-failed] for a buffer the detector accepted.

## Evidence

The unit tests in [ydk:test:detection/detector.unit] cover the accept and reject paths. The golden detection slice (40 bulk cases plus the seed OLE case) and the public harvest, which detected all 12,853 sampled messages, are described in [quality.md](quality.md).

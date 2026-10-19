phase1: started, baseline measure launched
phase2: TDD tests (AttachmentDecodeTests, 8) green pre-change; PartCollector ExactBytes (count pass + fixed byte[]) implemented; dotnet test net8+net10 green.
before profile attach-1mb: 4067400 B (4.07) both TFMs. after profile: 1007816 B (1.01) both TFMs.
before sweep (net8, net10) running via /tmp/measure.sh; after measured from copy /c/w129after (separate git init snapshot) to avoid clobbering baseline bin.
TODO: gcdump roots at large peak; large passes before/after; after sweeps x2; compare; perf-results Optimization log; gates; PR.
reviewer round1: added length-mismatch check
gcdump finding (before, 150MiB 100% pass, net10): node1 byte[] 157,286,424 = input held by caller local; node2 byte[] 114,939,840 = ExtractedAttachment.BinaryData (decoded). NOT two input-sized arrays; second array is decoded attachment (75% of input). No MimeKit content copy of input size at peak.
after large+profile done; after sweeps running (/tmp/m-after.log)
phase: measurements done, docs written; next gates+commit

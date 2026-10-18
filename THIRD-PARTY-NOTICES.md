# Third-party notices

This project is licensed under the MIT License (see `LICENSE`). It redistributes or depends on the third-party components below.
`tools/license-audit.py` (run in CI) fails when a package in the restore graph is missing from the NuGet tables or has a license outside the allowlist (MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, MS-PL).
Package licenses were read from each package's `.nuspec`; licenses for packages that ship only a license file (CommandLineParser, Microsoft.DotNet.PlatformAbstractions, both MIT) and xunit.abstractions (license URL, Apache-2.0) were checked by hand. Each package is licensed by its authors under the license shown; the full text ships inside the package.

## Runtime packages (shipped with the library)

| Package | Version | License | Copyright |
| --- | --- | --- | --- |
| MimeKit | 4.18.0 | MIT | .NET Foundation and Contributors |
| BouncyCastle.Cryptography | 2.7.0 | MIT | Copyright (c) Legion of the Bouncy Castle Inc. 2000-2026 |
| System.Security.Cryptography.Pkcs | 10.0.13 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.Bcl.Cryptography | 10.0.13 | MIT | (c) Microsoft Corporation. All rights reserved. |
| System.Formats.Asn1 | 10.0.13 | MIT | (c) Microsoft Corporation. All rights reserved. |

## Test, bench and tooling packages (not shipped)

| Package | Version | License | Copyright |
| --- | --- | --- | --- |
| DocumentFormat.OpenXml | 3.5.1 | MIT | (c) Microsoft Corporation. All rights reserved. |
| DocumentFormat.OpenXml.Framework | 3.5.1 | MIT | (c) Microsoft Corporation. All rights reserved. |
| System.IO.Packaging | 10.0.2 | MIT | (c) Microsoft Corporation. All rights reserved. |
| System.IO.Packaging | 8.0.1 | MIT | (c) Microsoft Corporation. All rights reserved. |
| xunit | 2.9.3 | Apache-2.0 | Copyright (C) .NET Foundation |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | Copyright (C) .NET Foundation |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | (c) Microsoft Corporation. All rights reserved. |
| coverlet.collector | 6.0.4 | MIT | tonerdo and contributors |
| Xunit.SkippableFact | 1.5.23 | MS-PL | Andrew Arnott |
| BenchmarkDotNet | 0.15.8 | MIT | .NET Foundation and contributors |
| BenchmarkDotNet.Annotations | 0.15.8 | MIT | .NET Foundation and contributors |
| CommandLineParser | 2.9.1 | MIT | Copyright (c) 2005 - 2020 Giacomo Stelluti Scala & Contributors |
| Gee.External.Capstone | 2.3.0 | MIT | Copyright (c) Ahmed Garhy |
| Iced | 1.21.0 | MIT | Copyright (C) 2018-present iced project and contributors |
| Microsoft.CodeAnalysis.Analyzers | 3.11.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.CodeAnalysis.CSharp | 4.14.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.CodeAnalysis.Common | 4.14.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.CodeCoverage | 17.14.1 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.Diagnostics.NETCore.Client | 0.2.510501 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.Diagnostics.Runtime | 3.1.512801 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.Diagnostics.Tracing.TraceEvent | 3.1.21 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.DotNet.PlatformAbstractions | 3.1.6 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.DependencyInjection | 6.0.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.DependencyInjection.Abstractions | 6.0.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Logging | 6.0.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Logging.Abstractions | 6.0.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Options | 6.0.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Primitives | 6.0.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.TestPlatform.ObjectModel | 17.14.1 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Microsoft.TestPlatform.TestHost | 17.14.1 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Newtonsoft.Json | 13.0.3 | MIT | Copyright (c) James Newton-King 2008 |
| Perfolizer | 0.6.1 | MIT | Copyright (C) 2020-2025 Andrey Akinshin |
| Pragmastat | 3.2.4 | MIT | Copyright (C) 2025 Andrey Akinshin |
| System.CodeDom | 9.0.5 | MIT | (c) Microsoft Corporation. All rights reserved. |
| System.Collections.Immutable | 10.0.13 | MIT | (c) Microsoft Corporation. All rights reserved. |
| System.Collections.Immutable | 9.0.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| System.Management | 9.0.5 | MIT | (c) Microsoft Corporation. All rights reserved. |
| System.Reflection.Metadata | 10.0.13 | MIT | (c) Microsoft Corporation. All rights reserved. |
| System.Reflection.Metadata | 9.0.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| System.Reflection.MetadataLoadContext | 10.0.13 | MIT | (c) Microsoft Corporation. All rights reserved. |
| System.Reflection.TypeExtensions | 4.7.0 | MIT | (c) Microsoft Corporation. All rights reserved. |
| Validation | 2.5.51 | MS-PL | (c) Andrew Arnott. All rights reserved. |
| xunit.abstractions | 2.0.3 | Apache-2.0 | Copyright (C) .NET Foundation |
| xunit.analyzers | 1.18.0 | Apache-2.0 | Copyright (C) .NET Foundation |
| xunit.assert | 2.9.3 | Apache-2.0 | Copyright (C) .NET Foundation |
| xunit.core | 2.9.3 | Apache-2.0 | Copyright (C) .NET Foundation |
| xunit.extensibility.core | 2.9.3 | Apache-2.0 | Copyright (C) .NET Foundation |
| xunit.extensibility.execution | 2.9.3 | Apache-2.0 | Copyright (C) .NET Foundation |

## Committed samples (ADR-018)

Every committed non-authored sample is listed here. Samples under `corpus/golden/` and the `.eml` files in `corpus/realistic/` are authored or generated by this project (`tools/RealisticSamples`) and are covered by the repo license (`LICENSE`); `corpus/internal/` is gitignored and never redistributed.

## Apache POI test data (Apache-2.0)

Copyright The Apache Software Foundation. Licensed under the Apache License 2.0:
https://github.com/apache/poi/blob/62322b97f3f1bca39cdcd0b7705973833c74ad25/legal/LICENSE

Files redistributed unmodified in `corpus/realistic/sources/` (source URL and SHA-256 in `corpus/realistic/manifest.json`):

- `Bug60942b.doc` (test-data/document)
- `63819.xls` (test-data/spreadsheet)
- `blank.msg` (test-data/hsmf)

The generated `attached-doc.eml`, `attached-xls.eml` and `attached-msg.eml` in `corpus/realistic/` embed these files unmodified as attachments.

Commit: 62322b97f3f1bca39cdcd0b7705973833c74ad25

### Apache POI NOTICE

Reproduced from https://github.com/apache/poi/blob/62322b97f3f1bca39cdcd0b7705973833c74ad25/legal/NOTICE as required by section 4(d) of the Apache License 2.0:

```text
Apache POI
Copyright 2003-2026 The Apache Software Foundation

This product includes software developed at
The Apache Software Foundation (https://www.apache.org/).

This product contains parts that were originally based on software from BEA.
Copyright (c) 2000-2003, BEA Systems, <http://www.bea.com/> (dead link),
which was acquired by Oracle Corporation in 2008.
<http://www.oracle.com/us/corporate/Acquisitions/bea/index.html>
<https://en.wikipedia.org/wiki/BEA_Systems>
Note: The ASF Secretary has on hand a Software Grant Agreement (SGA) from
BEA Systems, Inc. dated 9 Sep 2003 for XMLBeans signed by their EVP/CFO.

This product contains W3C XML Schema documents. Copyright 2001-2003 (c)
World Wide Web Consortium (Massachusetts Institute of Technology, European
Research Consortium for Informatics and Mathematics, Keio University)

This product contains the chunks_parse_cmds.tbl file from the vsdump program.
Copyright (C) 2006-2007 Valek Filippov (frob@df.ru)

This product contains parts of the eID Applet project
<http://eid-applet.googlecode.com> and <https://github.com/e-Contract/eid-applet>.
Copyright (c) 2009-2018
FedICT (federal ICT department of Belgium), e-Contract.be BVBA (https://www.e-contract.be),
Bart Hanssens from FedICT

ExceptionUtils is derived from `scala.util.control.NonFatal` in scala-library
which was released under the Apache 2.0 license.

Copyright (c) 2002-2023 EPFL
Copyright (c) 2011-2023 Lightbend, Inc.

Scala includes software developed at
LAMP/EPFL (https://lamp.epfl.ch/) and
Lightbend, Inc. (https://www.lightbend.com/).
```

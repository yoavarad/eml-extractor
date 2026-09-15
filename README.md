# EML extraction (.NET 10)

MimeKit-based RFC 5322 / MIME extractor. Factory wiring is out of scope.

```csharp
var detector = new EmlDetector();
var extractor = new EmlExtractor();
if (detector.IsEml(bytes))
{
    var result = extractor.Extract(bytes);
    // result.Metadata, result.XFields, result.MainContent, result.Attachments
}
```

## Layout

```
src/EmlExtraction/            library
  Models.cs                   public contract
  EmlDetector.cs
  EmlExtractor.cs             parse + orchestrate
  HeaderMapper.cs
  BodySelector.cs
  PartCollector.cs
tests/EmlExtraction.Tests/    unit tests + seed goldens
benches/EmlExtraction.Bench/  files/sec
tools/GoldenKit/              gen + score the 1200-case set
corpus/golden/seed/           small tagged goldens (CI)
corpus/golden/bulk/           1200 tagged goldens
docs/                         requirements, golden plan, perf, edge cases
```

## Commands

```bash
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
dotnet test
dotnet run --project tools/GoldenKit -c Release -- score
dotnet run --project benches/EmlExtraction.Bench -c Release
```

See [docs/requirements.md](docs/requirements.md) for the original spec mapped to code.

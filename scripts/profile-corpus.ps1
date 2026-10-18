# Corpus-level dotnet-trace / dotnet-counters / dotnet-gcdump run (ADR-020). Output: artifacts/perf/<stamp>/.
# Usage: scripts/profile-corpus.ps1 [--corpus <path>] [--out <dir>] [--window-ms N] [--warmup-ms N] [--min-passes N] [--max-input-bytes N]
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet build benches/EmlExtraction.Bench -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run -c Release --no-build --framework net10.0 --project benches/EmlExtraction.Bench -- profile-trace @args
exit $LASTEXITCODE

#!/usr/bin/env bash
# Corpus-level dotnet-trace / dotnet-counters / dotnet-gcdump run (ADR-020). Output: artifacts/perf/<stamp>/.
# Usage: scripts/profile-corpus.sh [--corpus <path>] [--out <dir>] [--window-ms N] [--warmup-ms N] [--min-passes N] [--max-input-bytes N]
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build benches/EmlExtraction.Bench -c Release --nologo -v q
dotnet run -c Release --no-build --framework net10.0 --project benches/EmlExtraction.Bench -- profile-trace "$@"

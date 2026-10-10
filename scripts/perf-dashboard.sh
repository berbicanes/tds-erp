#!/usr/bin/env bash
# ROADMAP Phase 2: measure GET /api/dashboard with 5.000 production entries (throw-away Testcontainers DB).
# Needs Docker running. Prints p50 / p95 / max. Not part of the normal `dotnet test` run.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
OUT=src/Tds.Api.Tests/bin/Release/net10.0/dashboard-perf.txt
rm -f "$OUT"
dotnet build src/Tds.Api.Tests -c Release -v q -nologo >/dev/null
if ! dotnet test --project src/Tds.Api.Tests -c Release --no-build -- \
     --filter-class "Tds.Api.Tests.Performance.DashboardPerformanceTests" --explicit only > "${TMPDIR:-/tmp}/tds-perf.log" 2>&1; then
  grep -E "failed|Exception|MessageText" "${TMPDIR:-/tmp}/tds-perf.log" | head -20
  echo "Mjerenje nije uspjelo. Cijeli log: ${TMPDIR:-/tmp}/tds-perf.log"
  exit 1
fi
cat "$OUT"

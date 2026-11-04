#!/usr/bin/env bash
set -euo pipefail

results='TestResults/Phase2/CI-Tests'
# Never merge a stale or previously failing run into current coverage evidence.
if [[ -e "$results" ]]; then
  echo "Expected a fresh results directory: $results" >&2
  exit 1
fi
mkdir -p "$results"
dotnet tool restore
dotnet restore CIS-WebAPI.slnx -m:1
dotnet build CIS-WebAPI.slnx --configuration Release --no-restore -m:1

# --no-build is valid here because this job built the entire solution above.
for project in CIS.Tests CIS.IntegrationTests; do
  dotnet test "$project/$project.csproj" --configuration Release --no-build -m:1 \
    --logger 'trx;LogFileName=tests.trx' --collect:'XPlat Code Coverage' \
    --settings coverage.runsettings --results-directory "$results/$project"
done
# Migration-tool tests exercise the real disposable MySQL/MongoDB migration. The
# coverage contract concerns the five productive API assemblies, not this tool.
dotnet test tools/CIS.Migration.Tests/CIS.Migration.Tests.csproj --configuration Release --no-build -m:1 \
  --logger 'trx;LogFileName=migration-tests.trx' --results-directory "$results/migration"

dotnet tool run reportgenerator \
  "-reports:$results/CIS.Tests/**/coverage.cobertura.xml;$results/CIS.IntegrationTests/**/coverage.cobertura.xml" \
  "-targetdir:$results/merged" '-reporttypes:Html;Cobertura;TextSummary'
node scripts/coverage/check-coverage.cjs "$results/merged/Cobertura.xml" "${COVERAGE_THRESHOLD:-80}"
# GitLab extracts the first number from its regex match. The count-bearing gate
# line is unsuitable; emit the report's percentage-only summary for the badge.
grep -E '^  Line coverage:' "$results/merged/Summary.txt"

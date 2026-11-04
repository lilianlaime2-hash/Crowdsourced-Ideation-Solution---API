# Cobertura de los cinco requisitos (Fase 3)

The working requirement is **80% overall line coverage** across productive .NET code. The user accepted this provisional threshold; the teacher's exact rubric has not been supplied. Branch coverage is reported for review and has no invented minimum. Overall coverage means covered lines divided by coverable lines in the merged report, not an average of project percentages.

## Scope

`coverage.runsettings` includes `CIS.Domain`, `CIS.Application`, `CIS.API`, `CIS.DataAccess`, and `CIS.DataAccess.MongoDB`. Tests and third-party assemblies are outside that scope. Production controllers, HTTP clients, authorization, repositories, mapping, configuration, domain policies and entity configurations remain measurable.

Only these generated source artifacts are excluded:

- `**/obj/**/*.cs`: compiler/build outputs, including the ASP.NET OpenAPI generated document implementation. The hand-written OpenAPI transformer remains included.
- `**/Migrations/*.Designer.cs` and `**/Migrations/*ModelSnapshot.cs`: EF-generated schema metadata mirrors. Migration `Up`/`Down` source remains included; real MySQL tests call `Database.MigrateAsync()` and exercise migration application.
- `GeneratedCodeAttribute`: explicitly generated code. `CompilerGeneratedAttribute` is deliberately not excluded, because async services, queries and lambdas contain productive behavior.

The MySQL tests start a disposable `mysql:8.4` Testcontainer using a random host port. Each test recreates only the test container's database and applies the real migrations. They never use an application connection string. MongoDB and HTTP integration tests likewise use disposable test infrastructure. Docker must be available for integration tests; failures are not converted into skipped or passing tests.

## Reproduce

Run from the repository root with .NET 10, Node.js 22 or newer, and Docker available:

```powershell
node --test tests/coverage/check-coverage.test.cjs
dotnet test CIS.Tests/CIS.Tests.csproj -m:1 --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults/Phase2/Final/unit --logger "trx;LogFileName=unit.trx"
dotnet test CIS.IntegrationTests/CIS.IntegrationTests.csproj -m:1 --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults/Phase2/Final/integration --logger "trx;LogFileName=integration.trx"
dotnet test tools/CIS.Migration.Tests/CIS.Migration.Tests.csproj -m:1 --results-directory TestResults/Phase2/Final/migration --logger "trx;LogFileName=migration.trx"
```

Merge **only** reports from that fresh run with ReportGenerator, then gate the merged Cobertura file. Do not mix baseline, stale, or failed test runs. The verified local ReportGenerator version is `5.5.11`; CI should pin the same version.

```powershell
reportgenerator "-reports:TestResults/Phase2/Final/**/coverage.cobertura.xml" "-targetdir:TestResults/Phase2/FinalMerged" "-reporttypes:Html;Cobertura;TextSummary"
node scripts/coverage/check-coverage.cjs TestResults/Phase2/FinalMerged/Cobertura.xml
```

The optional second gate argument is a percentage, for example `85`; omission means `80`. The gate compares unrounded line counts, prints the covered/valid line and branch counts, and exits nonzero for a missing report, unsupported/malformed XML, missing or invalid counters, zero coverable lines, invalid thresholds, or line coverage below the threshold. Branches are informational, including `n/a` for zero branch opportunities.

## Added behavioral coverage

- 18 real MySQL repository cases cover topic/tag persistence, eager loading, updates, soft deletion, scoped pagination, all supported sort fields in both directions, filters, vote uniqueness and deletion, and statistics aggregation/ranking while excluding deleted ideas and deleted parent topics. Migrations and EF entity configurations run against the actual provider.
- 27 domain/statistics unit cases check normalized tag values, invalid names and Unicode handling, tag limits and duplicate rejection, constructor invariants, UTC timestamps, irreversible soft-delete state, invalid votes, statistics author enrichment/fallback, activity scores, pagination metadata and invalid pagination.
- The Node gate suite exercises the acceptance boundary, configurable thresholds, rounding traps, misleading `line-rate`, malformed/missing reports and counters, and branch reporting.

HTTP and MongoDB cases are documented with the remaining Phase 2 evidence. Coverage is evidence of executed statements; the assertions above check observable domain and persistence behavior. It does not replace semantic review or establish the teacher's unstated rubric.

## Verificación vigente

El resultado final se registra en [status.md](status.md) y en TestResults/Phase3/RequiredChecks. Las mediciones anteriores de 313 tests incluían dos regresiones de rendimiento retiradas de esta entrega.


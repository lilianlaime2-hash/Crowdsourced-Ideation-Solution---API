# MongoDB Integration Tests Implementation Plan

**Goal:** Complete the user's 14-case integration plan with real MongoDB and 50 concurrent requests.

**Architecture:** One xUnit collection fixture owns one MongoDB 8.0 Testcontainer per run. Each test owns a unique temporary database with the unique indexes required by `scripts/migration/post-migration-sd3-perf.js`. WebApplicationFactory runs the real JWT middleware, services and MongoDB repositories; only the external Users API name lookup is replaced.

**Tech Stack:** .NET 10, xUnit 2.9, Testcontainers.MongoDb 4.16, MongoDB.Driver 3.13, Microsoft.AspNetCore.Mvc.Testing 10.0.12.

**Spec:** User's table: Sequence 2, Topics 3, Ideas 3, Votes 3, Statistics 1, HTTP 2 = 14.

## Constraints and decisions

- Preserve the existing uncommitted migration on `feature/mongo-migration`; do not move or commit the user's changes.
- Share MongoDB within a run; isolate tests and delete only their generated databases.
- Recreate migration indexes in test setup; do not add a production index lifecycle.
- Release 50 tasks through an asynchronous start gate, without sleeps or timing assertions.
- Expose the API entry point with `public partial class Program` for WebApplicationFactory if required.

## Tasks

- [x] Add shared container, isolated database, data, concurrency and HTTP factory utilities in `CIS.IntegrationTests/Infrastructure/`; add package/project references and remove the empty test.
- [x] Sequence: continue migrated IDs 5 → 6 → 7; generate exactly 1..50 concurrently from a fresh counter and persist 50.
- [x] Topics: persist fields; query/paginate/exclude deleted; persist/rehydrate/reuse tags and filter normalized names.
- [x] Ideas: persist fields; filter by topic/paginate/exclude deleted; persist updates and soft deletion.
- [x] Votes: add/get/exists; reject duplicate pairs including 50 competing inserts while allowing distinct pairs; delete only the selected vote.
- [x] Statistics: verify both aggregation pipelines, totals, ordering and pagination against real data, including deleted and orphan records.
- [x] HTTP: reject missing/invalid/expired/wrong-signature/non-UUID-subject JWTs on protected routes; valid JWT creates 50 topics concurrently, persists the authenticated user, reads resources and receives 404 for absent resources.
- [x] Run integration tests and the full solution; document commands and scope in `CIS.IntegrationTests/README.md`.

## Verification

Run `dotnet test CIS.IntegrationTests --logger "console;verbosity=normal"` with Docker available; expect 14 passed, none skipped. Run `dotnet test CIS-WebAPI.slnx` for regressions. Review isolation, JWT configuration, index preconditions, deterministic ordering and concurrency before reporting results.

## Execution results

- Final verification: `dotnet test CIS-WebAPI.slnx --no-restore --logger "console;verbosity=normal" --logger "trx;LogFilePrefix=verified" --results-directory TestResults/Verified` — 134 unit tests and 14 integration tests passed, zero failures or skipped tests.
- Corrected test connection construction to preserve the container credential's authentication source when selecting the isolated database. The initial HTTP failure and a targeted diagnostic assertion confirmed the cause before the fix.
- Independent read-only review found that statistics fixtures matched insertion order. Reordered lower-ranked records before winners, retaining the expected calculated ranking; the final full solution run passed after this change.
- No production source changes, commits, or modifications to migrated application data were necessary for this task.


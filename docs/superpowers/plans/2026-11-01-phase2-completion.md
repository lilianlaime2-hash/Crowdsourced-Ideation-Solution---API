> HISTORICAL / SUPERSEDED: the user narrowed delivery to five required checks during Phase3. The active plan is [2026-11-01-five-required-checks.md](2026-11-01-five-required-checks.md). Performance work below records the earlier broader scope and is excluded from the current delivery.

# Phase 2 Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans, with superpowers:dispatching-parallel-agents for the independent migration, HTTP and coverage tasks. Steps use checkbox syntax for tracking.

**Goal:** Convert the seven approved stages into reproducible tests, migration checks and CI evidence.

**Architecture:** Preserve the current CIS HTTP contract and its MySQL/MongoDB adapters. Use isolated real databases, canonical cross-database SHA-256 projections, a merged coverage gate, a local Postman collection and k6. Keep external Java authentication simulated explicitly in the isolated harness and allow the collection to target the real service separately.

**Tech Stack:** .NET 10, xUnit, Testcontainers 4.16, MySQL 8, MongoDB 8, Node 22, Postman CLI, k6 2.3, GitLab CI.

**Spec:** `docs/phase2/scope.md` (requirements and provisional decisions accepted in this conversation).

**Local execution status:** all local implementation and verification tasks below are complete. The actual GitLab pipeline still requires publication/runner execution; original carry-over story IDs, the teacher rubric, Java-service evidence and any deployment destination remain external closure items in `docs/phase2/status.md`.

## Global Constraints

- Four users; 500 topics, 200 tags, 1800 topic-tag relations, 2000 ideas, 5000 votes.
- At least three independently reported cases for every one of 13 HTTP operations.
- Provisional coverage gate: 80% productive line coverage; report branch coverage.
- No modifications to real databases, no credentials in reports, no automatic publication or deployment.
- Preserve restored uncommitted migration work and the remaining stash. Work in the existing feature checkout so the pending project separation travels with the tests.

## Review Focus

- Same counts with changed content or a changed embedded relationship must fail checksum verification.
- Mongo Long/date/status representations must preserve SQL meanings without masking invalid fields.
- CLI failures and missing reports must make CI fail; no empty-suite or missing-coverage success.
- Simultaneous writes must retain unique IDs and unique user/idea votes; cleanup cannot claim a reset of logical deletions.
- Both providers must start from equivalent datasets before measuring; generated fixtures are not production performance evidence.

### Task 1: Scope and baseline

- [x] Inspect restored feature branch and preserve stash; enumerate current tests and endpoints.
- [x] Record requirements, accepted provisional 80% target and missing story/rubric/deployment information.
- [x] Run current .NET suite and collect initial coverage: 134 unit +14 integration; merged unfiltered 36.3% including generated code.

### Task 2: Reproducible migration and validation

Files: `tools/CIS.Migration`, `tools/CIS.Migration.Tests`, `docs/phase2/migration.md`.
Interfaces: CLI commands `seed`, `migrate`, `verify --report <path>`; environment `CIS_MYSQL_CONNECTION`, `CIS_MONGO_CONNECTION`.

- [x] Write canonicalization/guard/tamper tests, observe failures, implement minimal tool.
- [x] Run seed/migrate/verify with real temporary MySQL and MongoDB; verify source preservation and intentional mutation rejection.
- [x] Include projects in solution and document safe invocation and projection scope.

### Task 3: Meaningful tests and coverage gate

Files: `CIS.Tests`, `CIS.IntegrationTests/MySql`, `coverage.runsettings`, `scripts/coverage`, `tests/coverage`.
Interface: `node scripts/coverage/check-coverage.cjs <merged Cobertura.xml> [threshold]`.

- [x] Add service/domain/error-path and real MySQL adapter tests where coverage shows missing behavior.
- [x] Test missing/malformed/empty/insufficient coverage reports; implement strict configurable gate.
- [x] Merge fresh test reports; enforce >=80% with explicit generated-file exclusions.

### Task 4: Endpoint matrix

Files: `CIS.IntegrationTests/Http`, `docs/phase2/endpoint-matrix.md`.

- [x] Add and run minimum/maximum/nominal cases for content and pagination, state/ownership/auth cases for other operations.
- [x] Assert real persisted effects and response contents; preserve existing 50-concurrent tests.
- [x] Map every method+route to at least three xUnit rows and report actual count.

### Task 5: Postman CLI and isolated workflow

Files: `tests/postman`, `tests/phase2`, `scripts/phase2`.
Interfaces: local JSON collection with URL/login variables, JUnit result, nonzero failure status; orchestration builds fresh paired databases and API process.

- [x] Write tests for workflow/result guards, observe failures and implement harness.
- [x] Run installed Postman CLI against real CIS/MongoDB and fixture auth; verify CRUD and negative responses.
- [x] Keep collection usable with real Users API through environment variables and document commands.

### Task 6: CI integration

Files: `.gitlab-ci.yml`, CI install helper, tool manifest, `docs/phase2/ci.md`.

- [x] Extend pipeline with actual integration, migration, coverage, CLI checks and retained reports; explicitly configure Docker requirements.
- [x] Run equivalent local commands and validate configuration; create publishable API artifact.
- [x] Record remote runner/pipeline evidence as pending until actually executed on GitLab.

### Task 7: Performance, evidence and review

Files: benchmark runner, `docs/phase2/status.md`, performance documentation.

- [x] Measure all five GET scenarios and eight CRUD writes on equivalent 500/2000/5000 fixture copies; report parameters, rates, latency and errors by provider.
- [x] Run appropriate full suites, whitespace check and fresh independent review; fix important findings with regression tests.
- [x] Update traceability with links to reports, exact counts and honest pending external requirements.

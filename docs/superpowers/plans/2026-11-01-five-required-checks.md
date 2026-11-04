# Five Required Checks — Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans and superpowers:verification-before-completion. This is the current user-approved scope; it supersedes the seven-stage plan.

**Goal:** Complete only CI migration, unit coverage, checksum validation, HTTP integration and Postman CLI during Phase 3.

**Architecture:** Preserve the existing HTTP contract and restored MongoDB adapter. Use real disposable MySQL/MongoDB, SHA-256 business projections, merged coverage, and a local Postman collection. Keep the Java Users API fixture explicitly identified.

**Tech Stack:** .NET 10, xUnit, Testcontainers, MongoDB 8, MySQL 8.4, Node 22, Postman CLI 1.74, GitLab CI.

**Spec:** `docs/phase2/scope.md`; the historical path is retained for compatibility.

## Global Constraints

- Only the five user-listed requirements; performance and endpoint redesign are excluded.
- Four users, 500 topics, 200 tags, 1800 relations, 2000 ideas, 5000 votes in isolated fixtures.
- At least three independently reported cases per each of 13 HTTP operations.
- Text bounds 1/1,150/1000,75/500; page sizes1/100/50 with101 persisted rows.
- Coverage: provisional 80% overall productive lines; report branches without another invented gate.
- Preserve prior work and stash. No real-database changes or publication without authorization.

## Review Focus

- Same-count content/relationship changes fail checksum verification.
- Missing/invalid coverage and Postman reports fail closed.
- Min/max/midpoint pages contain the corresponding actual item counts.
- API process signal termination and cleanup failure do not block remaining teardown.
- No performance dependency remains in the required pipeline or default workflow.

## Tasks and final verification

- [x] Back up the broader implementation; restore only the added SQL optimization and remove its dedicated tests/index additions from this delivery.
- [x] Limit CI to seven jobs with migration before functional checks; validate YAML and Bash syntax.
- [x] Preserve and run unit tests, real repositories, Mongo concurrency and all99 HTTP cases; collect reports separately.
- [x] Keep deterministic migration and six-entity SHA-256 verification; run19 migrator tests and11 post-migration regressions.
- [x] Make full workflow run migration/checksum/Postman only; reject excluded performance options (observed RED then GREEN).
- [x] Use integer midpoint inputs and101 rows; independently run the current131 integration cases.
- [x] Run Postman32 requests/49 assertions on both providers, with fresh databases and cleanup evidence.
- [x] Merge current coverage:2544/2805=90.70%;56 fast Node controls pass; format and artifact build pass.
- [x] Obtain independent read-only review and resolve documentation findings.
- [ ] Execute actual GitLab pipeline after authorized publication and suitable runner availability.

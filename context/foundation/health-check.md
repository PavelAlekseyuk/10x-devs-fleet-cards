---
project: FleetCards
checked_at: 2026-09-19T15:30:00+02:00
health_status: needs-attention
context_type: brownfield
language_family: dotnet
stack_assessment_available: false
checks_run:
  - lockfile
  - dependency_audit
  - outdated_deps
  - test_runner
  - ci_cd
  - configuration
audit_findings:
  critical: 0
  high: 3
  moderate: 13
  low: 0
test_runner_detected: true
ci_provider: null
recommended_fixes: 6
---

## Dependency Health

### Lockfile

Status: missing
Package manager: dotnet

No `packages.lock.json` was detected. NuGet restore is not pinned to a committed dependency graph, which makes builds less reproducible and limits the agent's ability to reason about exact versions.

Fix: enable NuGet lock files with `RestorePackagesWithLockFile` in the project or a shared MSBuild properties file, then run `dotnet restore` and commit each generated `packages.lock.json`.

### Security Audit

Tool: `dotnet list package --vulnerable --include-transitive`
Summary: 0 CRITICAL, 3 HIGH, 13 MODERATE, 0 LOW
Direct vs transitive: mixed; the audit reports both top-level and transitive packages across the solution.

#### HIGH findings

- **Microsoft.OpenApi** 2.0.0 — `GHSA-v5pm-xwqc-g5wc`. Present through `FleetCards.Api` and related test projects. Fix: update the ASP.NET OpenAPI dependency to a non-vulnerable version.
- **MessagePack** 2.5.192 — `GHSA-hv8m-jj95-wg3x` and `GHSA-vh6j-jc39-fggf`. Present through `FleetCards.AppHost`. Fix: update the Aspire/AppHost dependency chain to a non-vulnerable MessagePack version.

Moderate findings include `OpenTelemetry.Api` 1.13.1 (`GHSA-g94r-2vxg-569j`), three advisories for `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.13.1, and nine advisories for `MessagePack` 2.5.192. The same package advisories appear in multiple consuming projects.

### Outdated Dependencies

Packages with major version gaps: 3 direct dependency families.

- **coverlet.collector**: 6.0.4 -> 10.0.1 (4 major versions behind)
- **Microsoft.NET.Test.Sdk**: 17.14.1 -> 18.10.1 (1 major version behind)
- **xunit.runner.visualstudio**: 3.1.4 -> 4.0.0 (1 major version behind)

OpenTelemetry packages are also behind at the minor-version level: 1.13.x -> 1.19.0.

Additional non-major updates are available for `Microsoft.AspNetCore.OpenApi`, `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.ServiceDiscovery`, and `Microsoft.Extensions.Hosting`.

## Test Suite

Test runner: xUnit
Tests found: 3 tests
Test execution: passing

Configuration: `tests/FleetCards.Api.Tests/FleetCards.Api.Tests.csproj`, `tests/FleetCards.Application.Tests/FleetCards.Application.Tests.csproj`, and `tests/FleetCards.Architecture.Tests/FleetCards.Architecture.Tests.csproj`
Framework: xUnit with VSTest adapter 3.1.4 on .NET 10.0

The solution test run completed successfully: 3 passed, 0 failed, 0 skipped.

## CI/CD

Provider: not detected
Configuration: not found

| Stage | Status | Notes |
| --- | --- | --- |
| Lint | ✗ | not configured |
| Test | ✗ | no CI workflow; local xUnit tests pass |
| Build | ✗ | no CI workflow |
| Type check | ✗ | not configured separately; C# compilation provides compile-time checks locally |
| Security | ✗ | no CI vulnerability scan |

No CI/CD configuration detected. You'll set this up in the infrastructure and deployment lesson. For now, the local test runner is sufficient for agent collaboration.

## Configuration

### High severity

None detected.

### Medium severity

None detected.

### Low severity

- **`.editorconfig`** — editor and formatting conventions are not pinned for contributors or agents. Fix: add a repository-level `.editorconfig` covering C# formatting and naming rules.
- **`.env.example` or `.env.template`** — environment variables are not documented in a template. Fix: add a sanitized template for required local and deployment settings, without secrets.

`.gitignore` is present. No `AGENTS.md` or `CLAUDE.md` was found; this is tracked as an upcoming agent-onboarding item rather than a current configuration blocker.

## Stack Assessment Cross-Reference

No `stack-assessment.md` found. Run `/10x-stack-assess` for quality-gate analysis.

## Recommended Fixes

### Fix before agent work (Category A)

### 1. Resolve HIGH dependency advisories

**Impact**: Vulnerable OpenAPI and MessagePack dependencies create security risk and make generated agent changes harder to validate against a trustworthy dependency graph.
**Severity**: high
**Effort**: moderate (15–30 min)
**Fix**:

Update the package references or their Aspire transitive source, then run `dotnet restore` and `dotnet test .\FleetCards.sln`. Re-run `dotnet list package --vulnerable --include-transitive` until the HIGH findings are resolved or explicitly accepted.

### 2. Add committed NuGet lock files

**Impact**: Without a lockfile, agents and CI can resolve different transitive versions from the same project files.
**Severity**: medium
**Effort**: moderate (15–30 min)
**Fix:

Set `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in a shared MSBuild properties file or each project, run `dotnet restore`, and commit the generated `packages.lock.json` files.

### 3. Review major dependency gaps

**Impact**: Large version gaps increase the chance that agent changes encounter stale APIs or unexpected transitive vulnerabilities.
**Severity**: medium
**Effort**: significant (> 1 hour)
**Fix**:

Upgrade the OpenTelemetry, coverlet, test SDK, and xUnit runner families in small groups. Run `dotnet test .\FleetCards.sln` after each group and review breaking changes before merging.

### 4. Add repository formatting conventions

**Impact**: Agents lack a shared formatting and naming contract, so generated C# changes may vary between projects.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**:

Add a repository-level `.editorconfig`, then validate changes with `dotnet format --verify-no-changes` once the rules are established.

### Addressed in upcoming lessons (Category B)

### Add CI/CD automation

**Lesson**: Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)
**What you'll do there**: Add GitHub Actions for restore, build, test, dependency security checks, and deployment gates.

### Add agent instruction files

**Lesson**: Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)
**What you'll do there**: Create the repository's agent context and rules after the project conventions and operational checks are understood.

## Summary

Health status: needs-attention

The solution has a working .NET structure and a passing xUnit suite with 3 tests across API, application, and architecture projects. However, it has 3 HIGH and 13 MODERATE dependency advisories, no committed NuGet lockfiles, and several major dependency gaps. CI/CD and agent instruction files are not present yet, but those are expected upcoming setup work rather than reasons to block local agent collaboration.

Next step: resolve the HIGH advisories and add reproducible NuGet lockfiles, then proceed to agent onboarding while scheduling CI/CD for the infrastructure lesson.

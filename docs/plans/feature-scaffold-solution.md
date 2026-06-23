# Feature: Scaffold .NET Solution

## Goal
Establish the Clean Architecture .NET solution skeleton for TFT Coach: a solution file,
the four source projects (Domain, Application, Infrastructure, API) with correct inward-only
project references, and matching xUnit test projects. This unblocks all later feature work.

## Affected Layers
- Domain, Application, Infrastructure, API (created)
- tests/ (Domain, Application, Infrastructure test projects)

## Project References (inward-only)
- `Application` → `Domain`
- `Infrastructure` → `Application`, `Domain`
- `API` → `Application`, `Infrastructure`, `Domain`
- Test projects reference their corresponding source project.

## Task List
- [x] Verify .NET 10 SDK is available (`dotnet --version`).
- [x] Create solution `TFTCoach.slnx` (new .NET 10 XML solution format).
- [x] Create `src/TFTCoach.Domain` (classlib).
- [x] Create `src/TFTCoach.Application` (classlib) → references Domain.
- [x] Create `src/TFTCoach.Infrastructure` (classlib) → references Application, Domain.
- [x] Create `src/TFTCoach.API` (web/empty) → references Application, Infrastructure, Domain.
- [x] Create `tests/TFTCoach.Domain.Tests` (xUnit) → references Domain.
- [x] Create `tests/TFTCoach.Application.Tests` (xUnit) → references Application.
- [x] Create `tests/TFTCoach.Infrastructure.Tests` (xUnit) → references Infrastructure.
- [x] Add all projects to the solution.
- [x] Run `dotnet build` — succeeds.
- [x] Run `dotnet test` — succeeds (3 placeholder tests pass).

## Notes
- No Infrastructure references from Domain or Application (enforced by project references).
- LM Studio only — no cloud inference packages added during scaffold.

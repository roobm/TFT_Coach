# Feature: Move API endpoints into route module

## Goal
Move REST endpoint mappings (and their Swagger descriptions) out of `Program.cs`
into a dedicated routes file so API routes are easier to maintain.

## Affected Layers
- API layer (`src/TFTCoach.API/Program.cs`)
- API layer (`src/TFTCoach.API/Routes/ApiEndpoints.cs`)

## Task List
- [x] Create this plan file before coding.
- [x] Add a dedicated API route module for endpoint mappings.
- [x] Move existing REST endpoints and Swagger descriptions from `Program.cs`.
- [x] Keep SignalR hub mapping working after refactor.
- [x] Build and test to confirm no behavior changes.

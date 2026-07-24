# Feature: Swagger for localhost API usage

## Goal
Enable Swagger/OpenAPI for the ASP.NET Core backend so local developers can
discover and test API endpoints from localhost via Swagger JSON and Swagger UI.

## Affected Layers
- API layer (`src/TFTCoach.API/Program.cs`)
- API project dependencies (`src/TFTCoach.API/TFTCoach.API.csproj`)
- Repository documentation (`README.md`)

## Task List
- [x] Create branch `feature/swagger-localhost`.
- [x] Create this plan file before coding.
- [x] Add Swagger/OpenAPI service registration in API startup.
- [x] Add Swagger/OpenAPI middleware/endpoints for localhost developer use.
- [x] Add/update API package dependency if required.
- [x] Document local Swagger URLs/usage in `README.md`.
- [x] Run `dotnet build` and `dotnet test` and fix any failures.
- [x] Mark all completed tasks in this plan.

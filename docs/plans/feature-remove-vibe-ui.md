# Feature: Remove standard vibe UI patterns

## Goal
Audit the current frontend UI and remove generic, boilerplate, or "vibe-coded"
patterns so the product looks intentional and specific to TFT Coach.

User ask:
1. Find standard vibe coding things in the UI.
2. Use the Frontend Designer agent to identify and remove them.

## Decisions (from user)
- Scope is frontend presentation and UX cleanup.
- Prioritize deleting/replacing generic visuals and copy over adding new features.
- Keep existing functionality and routes intact.

## Affected layers
- **Frontend:** React pages/components and global CSS theme tokens.
- **Backend/API/Domain/Application/Infrastructure:** no behavior changes expected.
- **Tests:** run existing backend tests per repository workflow; run frontend build.

## Ordered task list
1. [x] Branch `feature/remove-vibe-ui` + create plan file.
2. [x] Frontend Designer audit of existing UI for generic patterns.
3. [x] Remove/replace identified generic patterns in frontend files.
4. [x] Verify frontend build.
5. [x] Run `dotnet build` and `dotnet test`.
6. [x] Update this plan status and summarize by layer.

## Status
- Complete.
- Frontend build passed with `npm run build`.
- Backend validation passed with `dotnet build TFTCoach.slnx` and `dotnet test TFTCoach.slnx` (79/79 passing).

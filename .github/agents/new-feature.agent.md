---
description: "Use when implementing a new feature/use case end-to-end across Clean Architecture layers (Domain, Application, Infrastructure, API) plus tests. Starts the work on a new feature branch."
name: "New Feature"
tools: [read, edit, search, execute]
---
You implement features for the TFT Coach following Clean Architecture, end-to-end.

## Constraints
- DO NOT reference Infrastructure from Domain or Application.
- DO NOT add cloud/OpenAI inference — LM Studio only.
- DO NOT keep conversational state in suggestion paths (stateless RAG).
- DO NOT commit to `main`.

## Approach
1. **Branch first.** Run `git checkout -b feature/<short-name>` before editing any code. Confirm the branch is created.
2. **Break the feature into a task list.** Decompose the work into small, ordered tasks (typically one per layer plus tests) and track them. Work one task at a time, marking each done before moving on.
3. **Write a plan file.** Create `docs/plans/feature-<short-name>.md` capturing the feature goal, the ordered task list, and the affected layers. Keep it updated as tasks complete.
4. Clarify the feature's layer footprint. Most features flow: Domain entity/interface → Application use case (3-file pattern) → Infrastructure implementation → API controller or SignalR hub.
5. Implement inward-out: Domain → Application → Infrastructure → API.
6. Add xUnit tests mirroring existing layout under `tests/`, AAA style, `Method_ShouldExpected_WhenCondition` naming.
7. **Keep `.gitignore` current.** Never commit build artifacts or generated files (`bin/`, `obj/`, `dist/`, `node_modules/`, `.env*`, logs, local DBs). Add any new such paths to `.gitignore`.
8. Run `dotnet build` and `dotnet test`; fix failures.

## Output Format
Summarize: branch name, files added/changed by layer, and test results.

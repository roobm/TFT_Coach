---
description: "Kick off a new TFT Coach feature: create a feature branch, then implement it end-to-end across Clean Architecture layers with tests."
argument-hint: "<feature name and short description>"
---
Start work on a new feature: **${input:feature}**

Follow this flow:

1. **Branch first.** Run `git checkout -b feature/<short-kebab-name>` derived from the feature name. Confirm the branch was created before editing any code.
2. **Break the feature into a task list.** Decompose the work into small, ordered tasks (typically one per layer plus tests) and track them, completing one before starting the next.
3. **Write a plan file** at `docs/plans/feature-<short-kebab-name>.md` capturing the feature goal, the ordered task list, and the affected layers. Keep it updated as tasks complete.
4. Identify the layer footprint. Typical flow: Domain entity/interface → Application use case (3-file pattern `<Name>UseCase.cs`, `<Name>Request.cs`, `<Name>Response.cs`) → Infrastructure implementation → API controller or SignalR hub.
5. Implement inward-out: Domain → Application → Infrastructure → API.
6. Respect the boundaries: never reference Infrastructure from Domain or Application; LM Studio only (no cloud inference); suggestion paths stay stateless.
7. Add xUnit tests under `tests/`, AAA layout, naming `Method_ShouldExpected_WhenCondition`.
8. Update affected documentation as part of the feature. If the README or other docs would become outdated, update them in the same work.
9. Run `dotnet build` and `dotnet test`; fix any failures.

Finish with a summary: branch name, files added/changed by layer, documentation updated, and test results.

# Feature: Request/Result envelope for REST and SignalR APIs

## Goal
Standardize every existing API surface (REST + SignalR) on a single response
contract:

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

Errors use:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "validation.invalid_riot_id",
    "message": "riotId must be in the form gameName#tagLine."
  }
}
```

## Scope
- REST endpoints:
  - `GET /matches`
  - `POST /matches/analysis`
  - `GET /ranks`
  - `POST /teamcode`
- SignalR hub methods:
  - `GetSuggestion`
  - `GetMeta`
  - `GetMetaComps`

## Affected layers
- **Application**: shared result primitives (`RequestResult<T>`, `ApiError`) and
  use-case expected error returns.
- **API**: transport mapping to HTTP status codes and hub envelope responses.
- **Frontend**: REST + SignalR client envelope unwrapping and error propagation.
- **Tests**: updated use-case tests for standardized success/error results.
- **Docs**: README API contract examples.

## Ordered task list
1. [x] Add shared `RequestResult<T>` and `ApiError` models in Application.
2. [x] Refactor expected use-case failures (`GetMatchHistory`, `GetRanks`,
   `DecodeTeamCode`) to return structured errors.
3. [x] Update REST endpoints to always respond with `{ success, data, error }`
   and consistent status-code mapping.
4. [x] Update SignalR hub methods to return envelope responses for success and
   handled failure paths.
5. [x] Update frontend API clients (`api.ts`, `coachHub.ts`) to unwrap
   envelope responses and throw structured error messages.
6. [x] Update/extend Application tests for result envelopes.
7. [x] Update README endpoint/hub contract documentation.

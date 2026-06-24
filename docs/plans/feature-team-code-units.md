# Feature: Team-code board units (paste TFT Team Planner code)

## Goal
Let the player paste a TFT **Team Planner code** (e.g.
`0201d00d02103903b03801305003a000TFTSet17`) on the Live Coach page and have it
decode into the **board units** the coach reasons about — no fragile board
vision, no manual typing.

The code is decoded against Riot's **official** team-planner data
(CommunityDragon `tftchampions-teamplanner.json`), which carries an official
`team_planner_code` per champion. This is authoritative data, not reverse
engineering, and works for any set.

## Code format (verified)
```
0201d00d02103903b03801305003a000TFTSet17
│ └────────────── hex ─────────────┘ └suffix┘
└ version (02 = v2, 3-hex chunks; else v1, 2-hex chunks)
```
- Strip the `TFTSet\d+(_Act\d+)?` suffix → that is the set key.
- First 2 chars = version. `02` → 3-hex chunks, otherwise 2-hex chunks.
- Each chunk → `parseInt(hex,16)` = a `team_planner_code`. Drop zeros (empty slots).
- Look the code up in the set's champion map → display name + cost.

Example decodes to: Aatrox(1), Akali(2), Rhaast(3), Nunu & Willump(4), Shen(5),
Blitzcrank(5), Fiora(5), Graves(5), Vex(5).

## Affected layers
- **Domain**: `TeamPlannerChampion` (Name, Cost, CharacterId).
- **Application**: `ITftTeamPlanner` interface (set → code→champion map);
  `DecodeTeamCode` use case (3-file) — pure parse + lookup.
- **Infrastructure**: `CommunityDragonTeamPlanner` — fetch + cache the
  teamplanner JSON per set; testable `BuildSetMap(JsonElement, setId)`.
- **API**: `POST /teamcode { code }` → `{ set, units:[{name,cost}] }`; DI wiring.
- **Frontend**: `decodeTeamCode(code)` client; a "Paste team code" input on Live
  Coach that fills the Board units textarea.
- **Tests**: use-case decode/invalid/zero-filter; infra JSON parsing.

## Ordered task list
1. [x] Branch `feature/team-code-units` + plan file.
2. [x] Domain: `TeamPlannerChampion`.
3. [x] Application: `ITftTeamPlanner` + `DecodeTeamCode` use case (Request/Response).
4. [x] Infrastructure: `CommunityDragonTeamPlanner` (fetch/cache/parse).
5. [x] API: `POST /teamcode` endpoint + DI registration.
6. [x] Frontend: client + Live Coach paste input → fills units.
7. [x] Tests: xUnit AAA for use case + infra parsing.
8. [x] `dotnet build` + `dotnet test`; `npm run build`; verify in browser.

## Status
- Complete. Backend `dotnet test` 66 pass (Application +4, Infrastructure +4);
  `npm run build` clean. `POST /teamcode` verified: the example code returns 9
  units (Aatrox…Vex) with costs; invalid input → 400. Verified in browser on
  `/live-coach`: pasting the code fills Board units with
  "Aatrox, Akali, Rhaast, Nunu & Willump, Shen, Blitzcrank, Fiora, Graves, Vex"
  and shows "Loaded 9 units from TFTSet17."


## Notes / decisions
- Decoding logic (regex/version/chunking) is pure and lives in the Application
  use case; only the champion data fetch is Infrastructure.
- The teamplanner JSON is cached in memory per set (it is static for a patch).
- External fetch wrapped in try/catch + structured logging; a fetch failure
  surfaces a clean error, never a crash.
- Output is the *planned* comp (intended board), not live mid-game state — it
  populates the units the player presses "Get suggestion" with.

# Project Plan: Real-Time TFT Coach

## 1. Project Overview

A local-first AI coaching application for Teamfight Tactics. Delivers real-time suggestions via a browser overlay during gameplay, and a statistics/recap page for post-game analysis. All inference runs locally via LM Studio. User data is stored in Supabase.

### Core Stack

| Layer | Technology |
|---|---|
| Language Model | LM Studio (OpenAI-compatible local API) |
| Backend | .NET 10 / ASP.NET Core |
| Real-time | SignalR |
| Database + Vectors | Supabase (Postgres + pgvector + Auth) |
| Frontend | React |
| Game State | Riot Live Client Data API |
| Log Watcher | `FileSystemWatcher` (.NET built-in) |

---

## 2. System Architecture

```
Host Machine (macOS)
├── LM Studio                → port 1234 (OpenAI-compatible API)
├── TFT game client          → Live Client Data API port 2999
└── TFT log files            → watched by FileSystemWatcher

Docker (development)
├── backend                  → ASP.NET Core + SignalR
├── seeder                   → run on demand (patch day)
└── supabase local           → postgres + pgvector + auth

React (browser)
└── overlay + recap page     → connects to backend via SignalR
```

```
┌─────────────────────────────────────────────────────────────────┐
│  PROCESS 1 — Seeder (patch day, ~biweekly)                      │
│                                                                  │
│  Riot Data Dragon API ──► .NET Seeder ──► LM Studio             │
│  + Patch Notes                │         (chunk + summarize)     │
│                               ▼                                  │
│                          Supabase pgvector                       │
│                          (tft_patch_data)                        │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  PROCESS 2 — Log Watcher (runs during every game)               │
│                                                                  │
│  TFT Log File ──► FileSystemWatcher ──► Round Parser            │
│                                               │                  │
│                                               ▼                  │
│                                     Supabase Postgres            │
│                              (match_history + pgvector)          │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  PROCESS 3 — Backend + Frontend (always running)                │
│                                                                  │
│  Live Client Data API                                            │
│  (poll every 2-3s)                                               │
│       │                                                          │
│       ▼                                                          │
│  Game State Parser ──► Supabase Query ──► LM Studio             │
│  (board, gold,          (patch data +     (suggestion)          │
│   stage, hp, items)      match history)        │                 │
│                                                ▼                 │
│                                     SignalR ──► React            │
│                                              (overlay)           │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Data Sources

| Source | Process | Frequency | Content |
|---|---|---|---|
| Riot Data Dragon API | Seeder | Per patch | Champion/item/augment/trait stats |
| Patch notes | Seeder | Per patch | Balance changes, meta shifts |
| Live Client Data API | Backend | Every 2–3s | Board, gold, stage, hp, items |
| TFT log files | Log Watcher | Per round | Placements, builds, outcomes |

---

## 4. Supabase Schema

```sql
-- Patch and champion knowledge (vector search)
CREATE TABLE tft_patch_data (
    id          SERIAL PRIMARY KEY,
    patch       VARCHAR(10),
    category    VARCHAR(50),   -- 'champion' | 'item' | 'augment' | 'trait' | 'patch_note'
    content     TEXT,
    embedding   vector(1536),
    created_at  TIMESTAMP DEFAULT NOW()
);

-- Personal match history (structured stats)
CREATE TABLE match_history (
    id          SERIAL PRIMARY KEY,
    user_id     UUID REFERENCES auth.users(id),
    played_at   TIMESTAMP,
    patch       VARCHAR(10),
    placement   INTEGER,
    comp        VARCHAR(100),
    augments    TEXT[],
    gold_left   INTEGER,
    final_hp    INTEGER,
    rounds_lost INTEGER
);

-- Round-level detail (vector search + stats)
CREATE TABLE round_logs (
    id          SERIAL PRIMARY KEY,
    match_id    INTEGER REFERENCES match_history(id),
    round       VARCHAR(10),
    content     TEXT,
    embedding   vector(1536),
    created_at  TIMESTAMP DEFAULT NOW()
);

-- User profiles and payment
CREATE TABLE profiles (
    id          UUID REFERENCES auth.users(id) PRIMARY KEY,
    is_paid     BOOLEAN DEFAULT FALSE,
    created_at  TIMESTAMP DEFAULT NOW()
);
```

---

## 5. Clean Architecture

```
src/
├── TFTCoach.Domain/
│   ├── Entities/
│   │   ├── MatchRound.cs
│   │   ├── GameState.cs
│   │   └── CoachingSuggestion.cs
│   ├── Enums/
│   │   └── SuggestionCategory.cs    # Economy | Board | Items | Comp | Scout
│   └── Interfaces/
│       ├── IVectorRepository.cs
│       ├── IMatchRepository.cs
│       └── IInferenceService.cs
│
├── TFTCoach.Application/
│   ├── UseCases/
│   │   ├── GetSuggestion/
│   │   │   ├── GetSuggestionUseCase.cs
│   │   │   ├── GetSuggestionRequest.cs
│   │   │   └── GetSuggestionResponse.cs
│   │   ├── IngestRoundLog/
│   │   ├── SeedPatchData/
│   │   └── GetRecap/
│   └── Interfaces/
│       └── ILMStudioClient.cs
│
├── TFTCoach.Infrastructure/
│   ├── Persistence/
│   │   ├── SupabaseVectorRepository.cs
│   │   └── SupabaseMatchRepository.cs
│   ├── InferenceService/
│   │   └── LMStudioClient.cs
│   ├── RiotApi/
│   │   ├── LiveClientService.cs
│   │   └── DataDragonService.cs
│   └── LogWatcher/
│       └── TFTFileWatcher.cs
│
└── TFTCoach.API/
    ├── Controllers/
    │   ├── SuggestionController.cs
    │   └── RecapController.cs
    └── Hubs/
        └── CoachingHub.cs           # SignalR hub for overlay
```

---

## 6. Coaching Prompt Design & Output Format

### Core Principle

Suggestions must be readable in one glance and executable in under 5 seconds. The player is between rounds — vague or verbose advice gets ignored.

### Output Format

```
[CATEGORY] Action verb + specific instruction. Reason if needed, one clause max.
```

| Tag | When to fire | Example |
|---|---|---|
| `[ECONOMY]` | Gold, interest, rolling decisions | `[ECONOMY] Stop rolling. Hold at 50g for interest next round.` |
| `[BOARD]` | Unit sells, placement, upgrades | `[BOARD] Sell Warwick — he shares no synergy with your core units.` |
| `[ITEMS]` | Component combinations, holders | `[ITEMS] Finish Rabadon's next — you have two Rods on bench.` |
| `[COMP]` | Pivots, trait completions | `[COMP] You're one Yone from 3-star. Prioritize shop rolls.` |
| `[SCOUT]` | Opponent boards, contested comps | `[SCOUT] Three players running Sorcerers. Consider pivoting.` |

### System Prompt

```
You are a Teamfight Tactics coach giving real-time advice.

RULES:
- Start every response with exactly one of: [ECONOMY] [BOARD] [ITEMS] [COMP] [SCOUT]
- Maximum 2 sentences. First sentence is the action. Second is the reason (optional).
- Always start with an action verb: Sell, Buy, Roll, Hold, Move, Build, Pivot, Scout.
- Never use: "consider", "you might want to", "it could be beneficial".
- If context does not support a clear suggestion, respond: [BOARD] Hold your current board.
- Base your suggestion ONLY on the context provided. Do not invent facts.

LEARNED CONTEXT:
{context}
```

### Trigger Logic

Only fire a suggestion when game state meaningfully changes:
- New round starts
- Unit bought or sold
- Item combined or picked up
- Augment offered
- Player HP drops significantly
- Opponent board state changes (new round)

---

## 7. Development Phases & Checklist

### Phase 1: Environment Setup

- [ ] Install .NET 10 SDK
- [ ] Install Supabase CLI: `brew install supabase/tap/supabase`
- [ ] Run local Supabase: `supabase start`
- [ ] Install LM Studio and load `llama3.1:8b` or similar
- [ ] Benchmark LM Studio response time (target: < 3s)
- [ ] Scaffold solution: `dotnet new sln`
- [ ] Create projects: Domain, Application, Infrastructure, API
- [ ] Install xUnit and test projects

### Phase 2: Supabase Schema

- [x] Run migrations for all tables (see Section 4)
- [x] Enable pgvector extension: `create extension vector`
- [x] Seed one test patch document and verify vector search works
- [x] Set up local `.env` with Supabase URL + anon key

### Phase 3: Seeder

- [x] `DataDragonService` — fetch champion/item/augment/trait data from Riot API
- [x] Patch note fetcher and chunker
- [x] `LMStudioClient` — summarize and prepare chunks for embedding
- [x] `SupabaseVectorRepository` — upsert embeddings into `tft_patch_data`
- [x] CLI flag `--patch 14.x` to re-seed without wiping match history

### Phase 4: Log Watcher

- [ ] Locate TFT log file path on macOS
- [ ] `TFTFileWatcher` using `FileSystemWatcher`
- [ ] Round-end event parser — extract units, items, augments, placement delta
- [ ] `IngestRoundLog` use case — embed and store in `round_logs` + `match_history`

### Phase 5: Backend + SignalR

- [ ] `LiveClientService` — poll Riot Live Client Data API every 2–3s
- [ ] `GetSuggestion` use case — query pgvector, build prompt, call LM Studio
- [ ] `CoachingHub` — SignalR hub pushing suggestions to connected React clients
- [ ] `has_state_changed` logic — only trigger inference on meaningful state changes

### Phase 6: React Frontend

- [ ] Scaffold React app: `npm create vite@latest`
- [ ] SignalR client: `@microsoft/signalr`
- [ ] Overlay component — category-tagged suggestion with color coding
- [ ] Recap/stats page — placement trends, comp performance, economy patterns
- [ ] AI recap component — calls `/recap` endpoint, renders LM Studio summary

---

## 8. Unit Tests (xUnit)

```
tests/
├── TFTCoach.Domain.Tests/
├── TFTCoach.Application.Tests/
│   ├── GetSuggestionTests.cs
│   ├── IngestRoundLogTests.cs
│   └── GetRecapTests.cs
└── TFTCoach.Infrastructure.Tests/
    ├── LMStudioClientTests.cs
    └── TFTFileWatcherTests.cs
```

**Suggestion output format**
```csharp
[Fact]
public void Suggestion_ShouldStartWithValidCategoryTag()
{
    var validTags = new[] { "[ECONOMY]", "[BOARD]", "[ITEMS]", "[COMP]", "[SCOUT]" };
    var suggestion = "[BOARD] Sell Warwick — he shares no synergy with your comp.";
    Assert.Contains(validTags, tag => suggestion.StartsWith(tag));
}

[Fact]
public void Suggestion_ShouldNotExceedTwoSentences()
{
    var suggestion = "[ECONOMY] Hold gold. You are at the 50g interest threshold.";
    var sentences = suggestion.Split('.').Where(s => !string.IsNullOrWhiteSpace(s));
    Assert.True(sentences.Count() <= 2);
}
```

**State change detection**
```csharp
[Fact]
public void HasStateChanged_ShouldReturnFalse_WhenStatesAreIdentical()
{
    var stateA = new GameState { Gold = 34, Round = "3-2", Units = ["Yone", "Ahri"] };
    var stateB = new GameState { Gold = 34, Round = "3-2", Units = ["Yone", "Ahri"] };
    Assert.False(GameStateComparer.HasChanged(stateA, stateB));
}

[Fact]
public void HasStateChanged_ShouldReturnTrue_WhenUnitAdded()
{
    var stateA = new GameState { Gold = 34, Round = "3-2", Units = ["Yone", "Ahri"] };
    var stateB = new GameState { Gold = 34, Round = "3-2", Units = ["Yone", "Ahri", "Lux"] };
    Assert.True(GameStateComparer.HasChanged(stateA, stateB));
}
```

---

## 9. Coding Standards & Constraints

- **Stateless queries**: Every suggestion call is an isolated RAG transaction — no conversational history.
- **Explicit error handling**: All external calls (Riot API, LM Studio, Supabase) wrapped in try/catch with structured logging.
- **Deterministic prompts**: System prompt enforces strict context boundaries. Max 2-sentence output.
- **Local inference only**: All LM Studio calls stay on-device. No OpenAI or cloud inference endpoints.
- **Latency first**: Benchmark LM Studio before building the overlay. If > 4s, reduce vector search results or switch to a smaller model.
- **Clean Architecture boundaries**: No Infrastructure references in Domain or Application layers.
- **Version-safe ingestion**: All patch documents tagged with TFT patch version for selective re-seeding.

---

## 10. Environment Variables

```env
# Supabase
SUPABASE_URL=http://localhost:54321
SUPABASE_ANON_KEY=your-local-anon-key

# LM Studio
LM_STUDIO_URL=http://host.docker.internal:1234/v1

# Riot
RIOT_API_KEY=RGAPI-...
LIVE_CLIENT_URL=http://host.docker.internal:2999
```

---

## 11. Future Ideas

- **Opponent board tracking**: Read all 8 player boards from the Live Client Data API each round. Track which comps are contested in the lobby and warn the player before they over-invest in a contested synergy.

- **Riot ID bootstrap**: On first launch, resolve the player's Riot ID to a PUUID via the Account API and backfill the last 20 matches into `match_history` before the log watcher takes over.

- **Distributable package**: Wrap the entire stack into a single installable app — bundle the backend, React frontend, and model setup into one download with no terminal required. Electron wrapper for the React overlay as an always-on-top window.

- **Licensing & payment system**: Supabase Auth for user accounts, `profiles.is_paid` boolean flipped by a Stripe webhook on successful payment. Online-only license check is acceptable since the game requires internet.

- **Electron overlay**: Wrap the React overlay in Electron for an always-on-top transparent window that sits over the game in windowed mode.

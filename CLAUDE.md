# Alidade — Claude Code Guide

## Project Overview

Alidade is a browser-based OpenStreetMap editor built on Blazor WebAssembly (C#). It targets experienced OSM contributors and aims for feature parity with iD. The UI is rendered via MapLibre GL JS through a JS interop boundary; all application logic lives in C#. Key external data sources are `id-tagging-schema` (presets), `name-suggestion-index`, and `editor-layer-index` (imagery layers), all embedded at build time by `Alidade.OsmGen`.

---

## Build & Run

**Prerequisites**
- .NET SDK (version per `global.json`)
- `dotnet workload install wasm-tools`
- Node.js (npm is invoked automatically by MSBuild)

**Dev server**
```
dotnet run --project Alidade.AppHost/Alidade.AppHost.csproj
```

**Release build**
```
dotnet build -c Release Alidade.sln
```

**Tests**
```
dotnet test
```

MSBuild automatically runs `npm install` and copies MapLibre GL assets to `wwwroot` on build — no manual npm step needed.

---

## Repository Layout

| Project | Purpose |
|---|---|
| `Alidade/` | Blazor WASM app — services, handlers, components, state, JS interop |
| `Alidade.AppHost/` | Aspire dev host — entry point for running locally |
| `Alidade.Core/` | Cross-project shared library — anything needed by more than one project: CQRS base types, pipeline behaviors, settings models and state service, keybinding catalog, undo infrastructure |
| `Alidade.Map/` | MapLibre GL interop service and map-specific handlers |
| `Alidade.Osm/` | OSM domain models, OSM API services, and OSM-specific handlers |
| `Alidade.OsmGen/` | Build-time code generator — embeds tagging schema, NSI, and imagery catalog as compiled C# |
| `Tests/` | xUnit test projects — all suppress CS1591 |

---

## Architecture & Key Patterns

### CQRS via Questy
All state mutations and reads go through command/query handlers. Handlers live in `Handlers/` within their respective project (`Alidade/Handlers/`, `Alidade.Map/Handlers/`, `Alidade.Osm/Handlers/`). OSM element mutation handlers (CreateNode, MoveNode, DeleteWay, etc.) specifically live in `Alidade.Osm/Handlers/Editing/`. Do **not** call services directly from components — always dispatch through the mediator.

**Handler pattern conventions:**
- Three variants: **Command** (`IRequestHandler<T.Command, CommandResult>`), **Query** (`IRequestHandler<T.Query, QueryResult<TData>>`), **Notification** (`INotificationHandler<T.Notification>`)
- The outer holder class must be `sealed` — handlers are never subclassed, and `sealed` enables JIT/AOT devirtualization
- The holder class declaration and its `Handle()` method use `/// <inheritdoc/>` — no separate doc comment
- Only the inner request record (Command, Query, or Notification) carries a full XML doc comment

### State via custom state services
Singleton state services are distributed across projects based on where their models live. Each has a `State` property, a `StateChanged` event, and a `SetState()` method. Components subscribe to `StateChanged` and re-render on change. Handlers are responsible for dispatching state updates.

- `Alidade.Core/Services/` — `SettingsStateService`
- `Alidade.Osm/Services/State/` — `EditBufferStateService`
- `Alidade/Services/State/` — the remaining seven services (`MapStateService`, `SelectionStateService`, `ToolStateService`, `UndoStateService`, `ValidationStateService`, `DraftStateService`, `AuthStateService`)

### DI via Autofac
Each project owns an Autofac module that registers its own services:
- `Alidade.Core/AlidadeCoreModule.cs` — registers `SettingsStateService`
- `Alidade.Map/AlidadeMapModule.cs` — registers `MapInteropService` and map-specific services
- `Alidade.Osm/AlidadeOsmModule.cs` — registers `EditBufferStateService`, `OsmCacheService`, OSM API services, `PresetService`, `NsiService`
- `Alidade/AlidadeModule.cs` — registers app services and state services; loads the above sub-modules; also registers all Questy handlers by scanning `Alidade`, `Alidade.Map`, and `Alidade.Osm` assemblies

Use constructor injection throughout. State services are singletons; application services are instance-per-lifetime-scope.

### JS interop boundary
MapLibre GL and IndexedDB are **only** accessed through `MapInteropService` and `IndexedDbInteropService`. Never call `IJSRuntime` directly from components or services.

### WASM threads
`ValidationService` runs on a background .NET thread. Any shared state it touches must be thread-safe.

### Changeset upload
Always goes through `ChangesetSplitter` (`Alidade.Osm/Services/`), which handles spatial and dependency-aware partitioning before upload.

---

## Key Files

| File | Purpose |
|---|---|
| `Alidade/Program.cs` | Startup and warm-up sequence |
| `Alidade/AlidadeModule.cs` | All DI registrations |
| `Alidade/Services/EditBufferService.cs` | Side-effect coordinator — GeoJSON push to MapLibre, OSM bbox fetch trigger, draft persistence; edit state is held by `EditBufferStateService` |
| `Alidade/Handlers/` | All command/query handlers (organized by feature domain) |
| `Alidade.Map/wwwroot/assets/js/map-interop.js` | MapLibre GL JS interop |
| `ALIDADE-SPEC.md` | Authoritative product specification |
| `CODE_STYLE.md` | C# code style rules |

---

## Code Style

See [CODE_STYLE.md](CODE_STYLE.md) for the full rules. The most commonly violated ones:

**C#**
- **Explicit types over `var`** — use `var` only for anonymous types.
- **Primary constructors** — preferred when the constructor body only assigns parameters to fields.
- **Null checks** — use `is null` / `is not null`; use `==` / `!=` for value comparisons.
- **XML doc comments are required on all public members** — missing docs fail the build (warnings-as-errors). Every doc comment must include:
  - `<summary>` — multi-line, content indented by two spaces
  - `<param>` — for every parameter
  - `<returns>` — for every non-void return
  - `<exception>` — for every exception that can propagate to the caller
- **Attributes on their own line** — each attribute must be on its own line, followed by a newline before the declaration it annotates.
- **Braces on all blocks** — no braceless `if`/`for`, even for single-line bodies.
- **Immutability** — use `init` or `readonly` for members that don't change after construction; use `required` for must-initialize members.
- **Expression-bodied members** — preferred; place `=>` on a new line for methods.
- **Multi-line boolean conditions** — place operators (`&&`, `||`) at the start of the continuation line.
- **No alignment padding** — use a single space after commas and around operators; never add extra spaces to vertically align tokens across lines.
- **One class per file** — filename must match class name.
- **Descriptive names** — prefer full names over abbreviations (e.g. `cancellationToken` not `ct`, `response` not `resp`).
- **Enums are plural.**
- **Prefer returning failure states over throwing exceptions.**
- **Nullable** — annotations are required; don't suppress with `!` without a comment explaining why.

**JavaScript** (interop shims only)
- **Braces on all blocks, body always on its own line** — no braceless `if`/`for`, and never put the body on the same line as the braces: `if (!x) { return; }` is wrong; use a newline after `{`.
- **`const` over `let`; never `var`** — use `let` only when reassignment is required.
- **No `console.log`/`console.debug` in committed code** — these are debugging aids only; application logging belongs on the C# side.

---

## Build Strictness

`Directory.Build.props` applies to all projects:
- Warnings are treated as errors
- Nullable annotations enforced
- XML documentation generation is on (CS1591 is suppressed in all projects under `Tests/`)

---

## Build Details

**Version stamping**
Release builds execute `git describe --long --always --dirty --exclude=* --abbrev=8` via a `SetSourceRevisionId` MSBuild target in `Directory.Build.props` to embed an 8-character commit hash as the version suffix. Debug builds use the suffix `"develop"`.

**MapLibre is copied, not re-bundled**
`Alidade.Map.csproj` copies MapLibre JS, CSS, and license files from node_modules verbatim to `Alidade.Map/wwwroot/assets/lib/maplibre-gl/` via a `CopyMapLibre` MSBuild target. Do not run them through esbuild, webpack, or any other bundler — doing so would strip the `window.maplibregl` UMD global that `map-interop.js` depends on.

**OsmGen generates three datasets**
`Alidade.OsmGen` produces compiled C# for three data sources, not just two:
- `id-tagging-schema` → `Alidade.Osm/AutoGen/TaggingSchemas/`
- `name-suggestion-index` (per-region, ~885 files) → `Alidade.Osm/AutoGen/NameSuggestions/`
- Imagery layer catalog → `Alidade.Osm/AutoGen/ImageryLayers/`

**Aspire C# breakpoints**
C# debugger breakpoints do not attach to Blazor WASM projects launched via Aspire (tracked in dotnet/aspire#5819). Use browser DevTools for debugging instead.

---

## JS Interop Details

**OAuth flow** (`Alidade/wwwroot/assets/js/auth-interop.js`)
Opens a 600×700 popup centered on screen. The primary signal back to the main window is `postMessage` from `oauth-callback.html`. The fallback is a 500 ms localStorage poll for the key `oauth_callback_code`, with a 3-minute timeout — this covers cases where the popup is blocked and the user completes OAuth in the same tab.

**IndexedDB schema** (`Alidade/wwwroot/assets/js/indexeddb-interop.js`)
- DB name: `alidade-db`, version: 1
- Stores: `keyvalue` (no indices) and `tiles` (with a `timestamp` index)
- Tile eviction uses `evictOldTiles(maxAgeMs)` which queries the timestamp index
- No schema migration strategy exists yet — any version bump will require adding `onupgradeneeded` handling

**Bing Maps tile protocol** (`Alidade.Map/wwwroot/assets/js/map-interop.js`)
Registers a custom `bing://` protocol with MapLibre GL that converts slippy tile coordinates to Bing quadkeys at request time. The Bing tile template URL is held in a closure variable `_bingTileTemplate` and set once at startup to avoid repeated string construction.

---

## Deployment

**Target: Cloudflare Pages**

`_redirects` and `_headers` files in `Alidade/wwwroot/` are included in the publish output and picked up by Cloudflare Pages automatically.

| Cloudflare Pages setting | Value |
|---|---|
| Build command | `bash build-cloudflare.sh` |
| Build output directory | `publish/wwwroot` |

**Why `_headers` matters for WASM threads**
The app uses .NET WASM threading, which requires `SharedArrayBuffer`. Browsers gate `SharedArrayBuffer` behind COOP (`Cross-Origin-Opener-Policy: same-origin`) and COEP (`Cross-Origin-Embedder-Policy: require-corp`) as **HTTP response headers**. The `<meta http-equiv>` tags in `index.html` are a Blazor dev-server workaround only — they have no effect on actual COOP/COEP enforcement. The `_headers` file provides the real headers at the CDN layer.

---

## Data Assets

`id-tagging-schema`, `name-suggestion-index`, and `editor-layer-index` are npm packages consumed by `Alidade.OsmGen` to generate compiled C# data classes into `Alidade.Osm/AutoGen/`. If you update their versions, regenerate the output:

```
dotnet run --project Alidade.OsmGen/Alidade.OsmGen.csproj
```

---

## Spec & Docs

`ALIDADE-SPEC.md` is the authoritative product specification — consult it before designing or modifying any feature. It covers goals, data architecture, feature list, performance targets, keyboard shortcuts, and component structure. If changes are required to either ALIDADE-SPEC.md or CLAUDE.md, include them in the plans to update.

# Alidade Product Specification 2026-04-14
## 1. Overview

This document specifies a browser-based OpenStreetMap editor named **Alidade**, built on Blazor WebAssembly. The editor targets experienced OSM contributors first, with a goal of offering iD-equivalent feature coverage at significantly higher responsiveness - leveraging WASM's near-native execution speed to eliminate the jank that affects iD under heavy edits or large changesets.

The initial release targets desktop browsers. A desktop application build via Electron is a planned future target; all architectural decisions should keep this path open. Mobile and simplified "casual mapper" modes are deferred to a later iteration informed by community feedback.

---

## 2. Goals & Non-Goals

### Goals

- Full editing workflow parity with iD for power users
- Significantly faster UI interactions than iD (map panning, selection, tag editing, undo/redo)
- Reuse of established OSM community data assets: `id-tagging-schema` and `name-suggestion-index`
- Standards-compliant OSM API v0.6 integration
- Self-contained package: all OSM community data assets bundled, no external CDN dependencies at runtime
- Offline-capable architecture: full edit session possible without connectivity; sync on reconnect
- Electron desktop app path: architecture must support packaging as a native desktop app with offline tile caching

### Non-Goals (v1)

- Mobile / touch-first UI
- Simplified beginner onboarding wizard
- Custom tile server administration
- Conflation or automated import tooling
- JOSM-style plugin system

---

## 3. Technology Stack

| Layer | Choice | Notes |
|---|---|---|
| Runtime | Blazor WebAssembly (.NET SDK per `global.json`) | AOT compilation enabled for release builds |
| Language | C# | All application logic |
| Map rendering | MapLibre GL JS (JS interop) | Via `IJSRuntime`; renders base map, imagery, and all OSM editing layers as GeoJSON sources |
| OSM API | OSM API v0.6 (REST) | OAuth 2.0 PKCE auth flow |
| CQRS / Mediator | Questy | All commands and queries dispatched through the mediator; handlers in `Alidade/Handlers/` |
| Dependency injection | Autofac | Registrations in `AlidadeModule.cs`; state services are singletons, app services are instance-per-lifetime-scope |
| State management | Custom state services | Singleton state services distributed across projects; components subscribe and re-render on change |
| Geometry | NetTopologySuite | Spatial operations (orthogonalize, circularize, geometry calculations) |
| Preset data | `@openstreetmap/id-tagging-schema` (npm, in `Alidade.OsmGen`) | `Alidade.OsmGen` generates compiled C# data into `Alidade.Osm/AutoGen/TaggingSchemas/` at development time |
| Name suggestions | `name-suggestion-index` (npm, in `Alidade.OsmGen`) | `Alidade.OsmGen` generates compiled C# data into `Alidade.Osm/AutoGen/NameSuggestions/` at development time |
| Imagery layers | `@openstreetmap/editor-layer-index` (npm, in `Alidade.OsmGen`) | `Alidade.OsmGen` generates compiled C# data into `Alidade.Osm/AutoGen/ImageryLayers/` at development time |
| Local persistence | IndexedDB (via JS interop) | Edit buffer, user preferences, cached OSM data, offline tiles |
| Background processing | .NET managed threads (`Task.Run` / `Thread`) | WASM threads feature; requires COOP/COEP headers |
| Desktop packaging | Electron (future) | Blazor WASM runs in Chromium renderer; no architectural changes required |

### Map Rendering Architecture

MapLibre GL JS is used as the single rendering surface for everything visible on the map: base tiles, aerial imagery, OSM data, editing highlights, and selection state. There is no separate SVG, Canvas, or supplementary WebGL overlay.

OSM elements are managed as named GeoJSON sources in MapLibre (`osm-nodes`, `osm-ways`, `osm-relations`, `osm-selected`, `osm-hover`, etc.). After each edit action, C# computes a minimal GeoJSON diff and pushes it to MapLibre in a single JS interop call. MapLibre's style layers handle all visual differentiation (color, width, icon, label) via data-driven expressions keyed on GeoJSON feature properties.

This approach means the map library handles all rendering - including WebGL-accelerated geometry - without requiring C# to manage pixel coordinates or paint loops.

#### Prior Art: iD, RapiD, and the SVG→WebGL Evolution

Understanding why other editors made different choices informs this decision.

**iD** renders its editing overlay as SVG using D3.js, sitting on top of a separate map tile layer. SVG is simple to reason about and sufficient for light editing, but degrades at high data density because every node and way segment is a DOM element.

**RapiD** (Meta's fork) initially inherited iD's SVG/D3 approach but replaced it with a WebGL-backed renderer using PixiJS, which yielded a significant performance improvement for dense datasets. RapiD subsequently upgraded to Pixi v8, which adds WebGPU support alongside WebGL.

**This editor does not need PixiJS** because MapLibre already uses WebGL and already manages a scene graph of geographic features. PixiJS would only add value if we needed a high-performance rendering surface that MapLibre couldn't provide - for example, a game-style overlay with custom shaders or sprite batching. MapLibre's data-driven style expressions and GeoJSON source updates are sufficient for everything an OSM editor requires: coloured way lines, node circles, selection highlights, drag handles, and labels. Adding PixiJS would introduce a second WebGL context competing with MapLibre's, with no benefit.

#### Why Not a Blazor-Native Rendering Approach?

The two candidates evaluated and rejected:

**SkiaSharp (`SkiaSharp.Views.Blazor`)** - Skia is officially supported as a Blazor WASM native dependency and draws to an HTML canvas element. However, it operates on a software-rasterised CPU bitmap that is then uploaded to a 2D canvas context each frame. This makes it unsuitable for a continuously-updating map overlay: performance degrades with canvas size, the WebGL-backed `SKGLView` variant has known stability issues in WASM, and Blazor's JS interop overhead is incurred on every draw call. SkiaSharp is appropriate for self-contained drawing surfaces (charts, diagrams) where render frequency and canvas size are bounded.

**Direct Canvas / HTML5 2D API (via JS interop)** - Viable for simple overlays, but each drawing primitive (line, circle, fill) requires a JS interop call, which accumulates badly at the thousands of elements typical in a dense OSM tile. A batching layer could mitigate this, but the result would be reimplementing a subset of what MapLibre already provides, at lower quality.

The conclusion is that routing all rendering through MapLibre's existing WebGL pipeline is the correct architecture for Alidade. The JS interop boundary is paid once per edit action (one GeoJSON diff push), not once per rendered element.

### JS Interop Boundaries

Blazor WASM's primary performance risk is excessive JS↔WASM marshalling. The architecture minimises crossing this boundary:

- MapLibre owns the rendering surface entirely; C# never polls map state per frame
- C# publishes a GeoJSON diff after each edit action; a single JS interop call applies the entire batch
- Input events (mouse clicks, drags) on the map canvas are captured by a thin JS shim and dispatched to C# as structured event records (coordinates, element IDs, modifiers) - not raw DOM events
- MapLibre camera events (moveend, zoomend) are debounced in JS before notifying C#

---

## 4. Feature Specification

### 4.1 Map Viewport

- Powered by MapLibre GL JS embedded in a Blazor component via JS interop
- Pan, zoom, and rotate behave identically to iD
- Zoom range: 2–22; editing tools activate at zoom ≥ 16 (configurable)
- Coordinate display in the status bar (WGS84, togglable to local grid)
- Minimap overlay (collapsible)

### 4.2 Background Imagery

- Imagery layer list sourced from the **Editor Layer Index** (ELI), fetched at startup
- **Custom tile URL**: users can specify a custom XYZ/TMS tile URL (e.g. `https://tile.example.com/{z}/{x}/{y}.png`) in settings; this is the primary mechanism for offline/local tile server use in the Electron build
- User can add additional custom WMS/TMS/WMTS endpoints
- Opacity slider per layer
- Imagery offset adjustment (drag to align with GPS traces)
- Default aerial layer selected by bounding box, matching iD behavior

### 4.3 Drawing Tools

#### Node
- Click to place a free node
- Click an existing way to insert a node mid-segment

#### Way
- Click sequence to draw; double-click or Enter to finish
- Snap to existing nodes within a configurable pixel radius
- Self-intersection detection with warning prompt
- Split way at selected node (keyboard shortcut)

#### Area / Closed Way
- Same as Way; closing on first node creates a closed way
- Multipolygon outer/inner ring creation via relation editor

#### Drag & reshape
- Drag nodes to reposition
- Drag way segments to create a new intermediate node (iD-style)
- Rectangular building orthogonalization (shortcut: `Q`)
- Circular approximation for roundabouts and amenity=* areas (shortcut: `O`)
- Gridify: split a closed way into a grid of equal rectangular sub-areas (shortcut: `G`). A floating draggable panel lets the user set rows, columns, and rotation angle; a live dashed orange overlay previews the grid before committing. The original way is replaced by the new cell ways in a single undoable action.

### 4.4 Selection & Inspection

- Click to select node, way, or relation
- Shift-click for multi-select
- Rubber-band box select
- Selected elements highlighted; adjacent connected ways dimmed
- Inspector panel shows: element type, ID, version, changeset, user, timestamp
- Direct link to OSM website element page

### 4.5 Tag Editor

The tag editor is the core power-user surface.

- Raw key/value table, always visible (no toggling required)
- Add / remove rows inline
- Key and value fields are free-text with autocomplete
- Autocomplete sources (in priority order):
  1. Keys/values present on the current element
  2. `id-tagging-schema` known keys for the element's primary feature type
  3. Frequency-ranked keys from a bundled taginfo snapshot
- Validation: unknown keys flagged with a warning icon (not blocked); deprecated keys flagged with a migration suggestion drawn from `id-tagging-schema`
- Multi-select tag editing: shows shared tags across selection; edits apply to all selected elements

### 4.6 Preset System

Powered by `id-tagging-schema` (loaded from bundled JSON at startup).

- Preset search panel (keyboard shortcut: `/`)
- Search indexes: preset name, aliases, terms field
- Applying a preset sets the primary tags; existing unrelated tags are preserved
- Preset fields render below the raw tag table as a structured form (e.g. `name`, `opening_hours`, `cuisine`, `maxspeed`)
- Preset fields write back to the raw tag table in real time
- Combo/multi-combo fields match iD conventions
- `name-suggestion-index` integration:
  - NSI suggestions surface when `name` or `brand` keys are present and the feature matches a known category
  - Applying an NSI suggestion populates `brand`, `brand:wikidata`, `brand:wikipedia`, and related tags automatically
  - NSI data is compiled into C# by `Alidade.OsmGen` and split into a global dataset (`NsiGlobalData`) and per-region classes dispatched by `NsiRegionDispatcher`. `NsiService.UpdateForLocation(lat, lon)` uses precomputed bounding boxes (`NsiRegionBounds`) to determine which region classes to activate — no JSON is parsed at runtime

### 4.7 Relation Editor

- Dedicated relation editor panel (mirrors iD's relation panel)
- Create new relation from selected elements
- Add/remove members; reorder by drag
- Role assignment per member (free text with autocomplete from schema)
- Multipolygon relations: visual ring assignment (outer/inner)
- Route relations: ordered member list with gap detection
- Restriction relations: turn restriction diagram (simplified visual, not full JOSM-style)

### 4.8 Undo / Redo

- Full action history tracked in `UndoStateService`
- Each discrete edit (node move, tag change, way split, etc.) is a discrete action
- Undo/redo stack persisted to IndexedDB so edits survive page refresh
- History panel showing action list (similar to JOSM's undo list)
- Keyboard shortcuts: `Ctrl+Z` / `Ctrl+Y`

### 4.9 Validation

Validation runs on a .NET managed background thread (`Task.Run` against the WASM threads-enabled runtime), keeping the UI thread free during evaluation. This requires the app to be served with the following HTTP headers (both browser and Electron):

```
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
```

The validator receives a snapshot of the current edit graph after each action (debounced at ~300ms) and publishes results back to the UI thread via a channel. No JS Web Worker is involved; this is pure .NET threading.

Errors block upload and must be resolved first; warnings are surfaced to the user but do not block upload.

#### Errors (block upload)

| Check | Notes |
|---|---|
| Relation member count exceeds API limit (10,000 elements) | Links to relation editor to remove/merge members |
| Missing role on relation member (where schema requires one) | |

#### Warnings

| Check | Notes |
|---|---|
| Missing required tags (per preset schema) | e.g. `highway` way with no `name` where name is expected |
| Disconnected way endpoint (floating end near but not connected to another way) | |
| Crossing ways without shared node (highway/waterway/railway intersections) | |
| Duplicate nodes at same coordinates | Fixup: merge automatically |
| Very short segment (< 1m) | Likely digitising error |
| Non-square building corners (angle deviation > threshold) | Fixup: run orthogonalise |
| Mutually exclusive tags (e.g. `name` and `noname=yes` together) | |
| Mismatched geometry (e.g. preset expects a node but element is a way) | |
| Tag value exceeds 255 character OSM limit | |
| Impossible oneway (highway with oneway tag forming a routing island - no way in or out) | |
| Deprecated tags | Shows migration suggestion from `id-tagging-schema` |
| Untagged standalone node (locally edited, not a vertex of any way) | |
| Untagged way (locally edited, not a relation member; `area=yes`-only counts as untagged) | |

#### Info

| Check | Notes |
|---|---|
| Suspicious name (name tag contains a URL, phone number, or is all-caps) | |

#### Checks intentionally excluded

The following iD validators are omitted from Alidade as not applicable or out of scope:

- **Untagged node in way** (way-vertex nodes) - nodes that serve only as geometry vertices are perfectly valid and extremely common; flagging them creates noise. Note: untagged *standalone* nodes (not vertices of any way) *are* flagged as a warning.
- **Private data** (`phone`, `email`, `website` on personal features) - paternalistic for a power-user editor
- **Help request** - iD-specific UI affordance, not relevant
- **MapRules** - iD-specific hosted task management integration
- **Incompatible source** - source tag auditing is too noisy without strong community consensus on what's incompatible

---

- Validation panel lists all issues grouped by severity; clicking any issue pans to and selects the relevant element
- Fixup buttons shown where resolution is deterministic (duplicate node merge, tag migration, orthogonalise)
- Oversized-relation error links directly to the relation editor

### 4.10 Notes

- OSM Notes displayed as map markers at zoom ≥ 14
- Click to open note thread; view comments inline
- Create new note (requires login)
- Add comment / close note (requires login)
- Notes are read-only when not logged in

### 4.11 GPX Overlays

- Upload local `.gpx` file; displayed as a styled polyline layer
- Multiple GPX files supported simultaneously
- Per-file color and opacity controls
- Track points shown at high zoom with timestamp tooltip
- GPX data is never uploaded to OSM; local session only

### 4.12 Changeset Upload

Because the edit buffer is backed by IndexedDB rather than localStorage, there is no practical limit on how many elements can be buffered locally. The OSM API hard-caps a single changeset at 10,000 elements, but Alidade handles this transparently rather than blocking the user.

#### Changeset Splitting

When an upload session contains more elements than the configured split threshold (default: 500 elements; user-adjustable, max 9,900), Alidade automatically partitions the edit buffer into multiple changesets. Splitting strategy:

- Group by spatial proximity first (elements in the same area go into the same changeset where possible)
- Never split a relation across changesets - a relation and all its members must upload together; if this would still exceed the 10,000-element limit, the upload is blocked for that relation (the validator will have already flagged this as an error before upload is attempted)
- Dependencies are respected: if way W references node N, N uploads before W in the same or an earlier changeset
- Changeset comments are auto-suffixed with `(1/N)`, `(2/N)`, etc. when splitting; the user can customise the suffix pattern

#### Upload Flow

- Upload dialog shows a summary: N nodes, M ways, K relations; if splitting will occur, shows the projected changeset count
- Changeset comment field (required); source and hashtag fields (optional)
- Conflict detection: before each changeset uploads, re-fetch the current server version of all affected elements; flag version mismatches
- Conflict resolution UI:
  - Side-by-side diff of local vs. server state per conflicting element
  - User chooses: keep mine / keep theirs / merge tags manually
  - Resolved conflicts do not block remaining changesets from uploading
- Progress indicator shows per-changeset status across the full upload session
- On success: list of changeset IDs with links to OSM website
- On partial failure: failed elements are retained in the local buffer with error context; successfully uploaded elements are cleared

---

## 5. Authentication

- OAuth 2.0 PKCE flow against `https://www.openstreetmap.org/oauth2/`
- No server-side component required; token stored in IndexedDB (not localStorage)
- Login state shown in toolbar; avatar and username displayed when authenticated
- Logout clears token; pending edits are preserved

---

## 6. Data Architecture

### Edit Buffer

All edits are accumulated in-memory by `EditBufferService` and mirrored to IndexedDB after each action. This acts as a crash-safe local draft.

```
EditBuffer
  ├── created: OsmElement[]
  ├── modified: Dictionary<OsmId, OsmElement>   // keyed by original ID
  └── deleted: OsmId[]
```

### OSM Data Fetching

- On viewport change (pan/zoom), Alidade issues a bounding box fetch to the OSM API (`/api/0.6/map?bbox=...`)
- Fetched data is cached in IndexedDB keyed by tile (slippy map tile coordinates at zoom 16)
- Cached tiles expire after 5 minutes; a background refresh occurs silently if the user is actively editing in that tile
- Elements modified in the local edit buffer are merged over the fetched data before rendering

### Element ID Scheme

Newly created elements use negative IDs (iD convention). These are replaced with real IDs returned by the OSM API upon upload.

---

## 7. OSM Models

All OSM domain types are hand-written records in `Alidade.Osm/Models/`. No third-party OSM library is used - the model surface needed for an editor is small and well-defined, and owning the types avoids bundle bloat in WASM and dependency drift.

### Core Element Types

```csharp
public enum OsmElementType { Node, Way, Relation }

public record OsmNode(
    long Id,
    int Version,
    int? ChangesetId,
    string? UserName,
    DateTime? Timestamp,
    double Lat,
    double Lon,
    IReadOnlyDictionary<string, string> Tags);

public record OsmWay(
    long Id,
    int Version,
    int? ChangesetId,
    string? UserName,
    DateTime? Timestamp,
    IReadOnlyList<long> NodeIds,
    IReadOnlyDictionary<string, string> Tags);

public record OsmRelation(
    long Id,
    int Version,
    int? ChangesetId,
    string? UserName,
    DateTime? Timestamp,
    IReadOnlyList<OsmMember> Members,
    IReadOnlyDictionary<string, string> Tags);

public record OsmMember(
    OsmElementType Type,
    long Ref,
    string Role);
```

A discriminated union or base type is intentionally avoided - the three element types have meaningfully different shapes and are always handled in type-specific branches. Pattern matching on concrete types is cleaner than a shared base class with downcasting.

### Edit State

Each element in the edit buffer carries an `EditState` so the UI and upload logic can distinguish between untouched fetched data, local modifications, and pending deletions:

```csharp
public enum EditState { Fetched, Created, Modified, Deleted }
```

### Changeset Upload Types

The OSM API's changeset upload endpoint accepts an `OsmChange` XML document containing three sections. These are represented as a simple aggregate rather than a full XML model:

```csharp
public record OsmChange(
    IReadOnlyList<OsmNode> CreatedNodes,
    IReadOnlyList<OsmWay> CreatedWays,
    IReadOnlyList<OsmRelation> CreatedRelations,
    IReadOnlyList<OsmNode> ModifiedNodes,
    IReadOnlyList<OsmWay> ModifiedWays,
    IReadOnlyList<OsmRelation> ModifiedRelations,
    IReadOnlyList<long> DeletedNodeIds,
    IReadOnlyList<long> DeletedWayIds,
    IReadOnlyList<long> DeletedRelationIds);
```

`OsmChange` is serialised to XML by `OsmApiContext` (`Alidade/Services/Api/OsmApiContext.cs`) using `System.Xml.Linq` before upload. The API returns a diffResult XML document mapping old (negative) IDs to the new server-assigned IDs; this is deserialised and applied to the edit buffer to replace temporary IDs.

### Notes

```csharp
public record OsmNote(
    long Id,
    double Lat,
    double Lon,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<OsmNoteComment> Comments);

public record OsmNoteComment(
    string Action,
    DateTime CreatedAt,
    string? UserName,
    string Text);
```

### Serialisation

All API responses are XML. Deserialisation is done with `System.Xml.Linq` (`XDocument.Parse`) directly in `OsmApiService` - no separate serialiser library. The OSM XML schema is stable and small enough that explicit element/attribute mapping is preferable to a reflection-based approach, and avoids any trim/AOT issues in the WASM build.

---

## 8. Performance Targets

| Interaction | Target latency |
|---|---|
| Node drag (single node) | < 16ms (60 fps) |
| Way render after edit | < 32ms |
| Tag autocomplete response | < 50ms |
| Preset search (full index) | < 100ms |
| Viewport pan/zoom | Governed by MapLibre (native GL) |
| Changeset diff generation | < 500ms for up to 1,000 elements |
| Cold startup (WASM load) | < 5s on broadband (AOT build) |

---

## 9. Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `S` | Select tool |
| `A` | Draw node |
| `W` | Draw way |
| `Shift+W` | Draw area |
| `Q` | Orthogonalize selection |
| `O` | Circularize selection |
| `G` | Open gridify panel |
| `X` | Split way at node |
| `C` | Continue drawing from endpoint |
| `Delete` | Delete selected |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `/` | Focus preset search |
| `Escape` | Cancel / deselect |
| `+` / `-` | Zoom in / out |
| `B` | Toggle background imagery panel |
| `V` | Toggle validation panel |
| `U` | Toggle undo history panel |

---

## 10. Data Asset Integration

All OSM community data assets are embedded as compiled C# code rather than JSON files loaded at runtime. `Alidade.OsmGen` is a standalone .NET console app that reads npm packages from its own `node_modules` and generates static C# classes into `Alidade.Osm/AutoGen/`. This keeps the WASM bundle fully self-contained (no runtime CDN fetches or JSON parsing) and allows the C# compiler and linker to trim unused data.

**To update asset versions**, change the version in `Alidade.OsmGen/package.json`, run `npm install` from that directory, then regenerate:

```
dotnet run --project Alidade.OsmGen/Alidade.OsmGen.csproj
```

Commit the regenerated files in `Alidade.Osm/AutoGen/TaggingSchemas/`, `Alidade.Osm/AutoGen/NameSuggestions/`, and `Alidade.Osm/AutoGen/ImageryLayers/` alongside the package.json change.

### id-tagging-schema

- npm package: `@openstreetmap/id-tagging-schema` (pinned to a specific commit in `Alidade.OsmGen/package.json`)
- Generator: `Alidade.OsmGen/Generators/TaggingSchemaGenerator.cs`
- Output: `Alidade.Osm/AutoGen/TaggingSchemas/`
- **Loaded at startup, before the first OSM data fetch.** The preset system, tag autocomplete, and validation all depend on this data being in memory from the moment the user can interact with any element.

### name-suggestion-index

- npm package: `name-suggestion-index` (pinned version in `Alidade.OsmGen/package.json`)
- Generator: `Alidade.OsmGen/Generators/NsiGenerator.cs`
- Output: `Alidade.Osm/AutoGen/NameSuggestions/`
- NSI data is split into a global dataset (`NsiGlobalData`) and per-region classes. Only the region classes for the user's current map area are activated at runtime; all other region data remains as static fields that are never touched and can be trimmed by the AOT linker.

### editor-layer-index

- npm package: `@openstreetmap/editor-layer-index` (pinned to a specific commit in `Alidade.OsmGen/package.json`)
- Generator: `Alidade.OsmGen/Generators/ImageryGenerator.cs`
- Output: `Alidade.Osm/AutoGen/ImageryLayers/`

---

## 11. Project Structure

```
/
├── Alidade.AppHost/               # .NET Aspire AppHost - local dev entry point
│   └── Program.cs
├── Alidade.Core/                  # Cross-project shared library — anything needed by more than one project
│   ├── Models/CQRS/               # CommandResult, QueryResult, NotificationBase, IUndoableCommand, UndoDescriptions
│   ├── Models/Settings/           # SettingsState, KeyBindingsConfig, KeyBindingCatalog, KeyBindingDef, KeyBindingActions
│   ├── Services/                  # SettingsStateService
│   ├── ServiceInterface/          # ISettingsStateService, IOsmApiContext, IOsmEditingService, etc.
│   ├── PipelineBehaviors/         # CommandBehavior, QueryBehavior, NotificationBehavior
│   └── AlidadeCoreModule.cs       # Autofac module for this project
├── Alidade.Map/                   # MapLibre GL interop and map-specific handlers
│   ├── Handlers/                  # Map-specific Questy handlers (imagery, camera, etc.)
│   ├── MapInteropService.cs       # All MapLibre GL JS interop calls
│   ├── AlidadeMapModule.cs        # Autofac module for this project
│   └── wwwroot/
│       └── assets/
│           ├── js/map-interop.js  # MapLibre JS shim (Bing protocol, event forwarding)
│           └── lib/maplibre-gl/   # Copied from node_modules at build time
├── Alidade/                       # Blazor WASM project
│   ├── Components/
│   │   ├── Map/                   # MapViewport and related map components
│   │   ├── Panels/                # TagEditor, PresetPanel, RelationEditor, ValidationPanel, etc.
│   │   └── Dialogs/               # ChangesetUpload, ConflictResolution, etc.
│   ├── Handlers/                  # Questy handlers for app-level concerns (Map, Selection, Auth, Settings, Tool, UndoRedo, Validation)
│   ├── Interop/
│   │   └── IndexedDbInteropService.cs  # All IndexedDB JS interop calls
│   ├── Services/
│   │   ├── State/                 # Seven singleton state services (Map, Selection, Tool, Undo, Validation, Draft, Auth)
│   │   ├── EditBufferService.cs   # Side-effect coordinator — GeoJSON push, bbox fetch trigger, draft persistence
│   │   └── ValidationService.cs   # Runs on background thread; dispatches to validators in Alidade.Osm
│   ├── Models/                    # App-layer models (undo history, draft, selection, etc.)
│   ├── PipelineBehaviors/
│   │   └── UndoPipelineBehavior.cs  # Snapshots EditBufferState before/after each IUndoableCommand
│   ├── AlidadeModule.cs           # Root Autofac module; loads sub-modules; registers all handlers
│   └── wwwroot/
│       └── assets/js/             # indexeddb-interop.js, auth-interop.js
├── Alidade.Osm/                   # OSM domain models, API services, OSM-specific handlers, and validators
│   ├── Handlers/
│   │   ├── Changeset/             # BuildChangesets, CloseChangeset, CreateChangeset, UploadChangeset
│   │   ├── Editing/               # OSM element mutation handlers (CreateNode, MoveNode, DeleteWay, FetchBbox, FetchElement, etc.)
│   │   ├── Parsing/               # BuildOsmChangeXml, ParseDiffResult, ParseNotesJson, ParseOsmXml
│   │   ├── Tagging/               # BulkUpdateTags, MergePresetTags, UpdateTags
│   │   └── Tools/                 # Gridify and Square algorithm handlers
│   ├── Models/
│   │   ├── EditBuffer/            # EditBufferState, ConflictItem
│   │   ├── Validation/            # ValidationIssue, ValidationSeverities
│   │   └── (root)                 # OsmNode, OsmWay, OsmRelation, OsmChange, EditBufferSnapshot, GridifyResult, etc.
│   ├── Services/
│   │   ├── State/                 # EditBufferStateService
│   │   ├── GeometryService.cs     # Orthogonalization, circularization, crossing-way detection
│   │   ├── ChangesetSplitter.cs   # Spatial + dependency-aware changeset splitting
│   │   ├── PresetService.cs       # Loads + indexes id-tagging-schema compiled data
│   │   ├── NsiService.cs          # Region-aware NSI suggestions from compiled data
│   │   ├── OsmEditingService.cs   # OSM API HTTP operations
│   │   ├── OsmNotesService.cs
│   │   └── OsmOAuthClient.cs
│   ├── Validators/                # All validation checks (Error/, Warning/, Info/)
│   ├── AlidadeOsmModule.cs        # Autofac module for this project
│   └── AutoGen/                   # Generated C# — do not edit manually
│       ├── TaggingSchemas/        # Generated from id-tagging-schema
│       ├── NameSuggestions/       # Generated from name-suggestion-index (per-region, ~885 files)
│       └── ImageryLayers/         # Generated from editor-layer-index
├── Alidade.OsmGen/                # Code generator - run manually when updating data asset versions
│   ├── Generators/                # TaggingSchemaGenerator, NsiGenerator, ImageryGenerator
│   └── package.json               # npm deps: id-tagging-schema, name-suggestion-index, editor-layer-index
├── Tests/
│   └── Alidade.Osm.Tests/         # xUnit tests for Alidade.Osm
├── electron/                      # Electron shell (future)
│   ├── main.js                    # Main process; sets COOP/COEP headers
│   └── package.json
└── Alidade.sln
```

### Local Development (Aspire AppHost)

`Alidade.AppHost` is the startup project for local development. It uses .NET Aspire to serve the Blazor WASM project via the dev server, providing a consistent F5 experience in Visual Studio and Rider without needing to configure a web server manually.

`Alidade.AppHost/Program.cs` is minimal - it registers `Alidade` as a resource:

```csharp
builder.AddProject<Projects.Alidade>("alidade")
       .WithExternalHttpEndpoints();
```

The COOP/COEP headers required for WASM threads must be set in `Alidade` for local dev (Aspire does not inject headers). Add a middleware shim in the client's dev server configuration:

```csharp
// Alidade/Program.cs (dev only, guarded by preprocessor or environment check)
app.Use(async (context, next) =>
{
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    await next();
});
```

**Known limitation**: C# breakpoints in Blazor WASM projects do not attach when the app is launched via an Aspire AppHost (tracked in [dotnet/aspire#5819](https://github.com/dotnet/aspire/issues/5819)). The AppHost is the only viable local dev server for a standalone Blazor WASM app, so this is an accepted constraint until the Aspire team resolves it upstream. Browser-side debugging via browser devtools remains fully functional in the meantime.

### Electron Considerations

The Electron main process must set the required COOP/COEP headers on all responses from the local file server so that `SharedArrayBuffer` (needed for WASM threads) is available:

```javascript
// electron/main.js
session.defaultSession.webRequest.onHeadersReceived((details, callback) => {
  callback({
    responseHeaders: {
      ...details.responseHeaders,
      'Cross-Origin-Opener-Policy': ['same-origin'],
      'Cross-Origin-Embedder-Policy': ['require-corp'],
    }
  });
});
```

The Blazor WASM output is served from the local filesystem via a custom protocol handler, requiring no local HTTP server process.
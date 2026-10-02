# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this
repository.

## What this is

Worldhalla is a BepInEx/Harmony framework that lets a mod replace Valheim's world generation
without inventing its own save format, settings storage or multiplayer handshake. It generates
nothing itself: it owns the `.fwl` header, the settings blob, the version check between peers and
the `WorldGenerator` query path, and delegates the actual terrain to whatever `IWorldGenerator` a
mod registers.

Worlds written through Worldhalla must stay loadable forever. The format version in the header
is a public compatibility surface, not an implementation detail — see "World format" below.

World persistency and plugin interface concerns are only valid after worldhalla's public release.
That comes with version 1.0.0 and higher. Until then, versions start with 0. and we're free to change
the interfaces and formats as we like. This clause must be removed with 1.0.0 release.

## Relationship to Riverheim

This repository is consumed by Riverheim as a git submodule mounted at `external/worldhalla`.
Riverheim compiles against **`Worldhalla.API` only** and takes the plugin as a
`[BepInDependency]` at runtime. A change spanning both is a commit here plus a commit in Riverheim
moving the submodule pointer.

## General guidelines

- The basic workflow for this project is to produce a diff and wait for approval.
- Don't bump versions or create any VCS artifacts (branches, commits, tags) unless explicitly prompted.
- Upon encountering a correction or just any unusual insight make suggestions to improve this (CLAUDE.md) file.
- Never ship game data, original or modified: no extracted assets, compiled shaders, or patched
  copies of the game's bundles. Anything derived from game files is produced at runtime on the
  player's machine from their own install. Rewriting the game's files on disk is a last resort.
- Riverheim is a private repository: don't reference its contents (tools, classes, config) in this repository's code,
  comments or docs, even when an idea is ported from there. Describe what Worldhalla does in its own terms.

## Patching

Modding puts compatibility first: Worldhalla shares the game with other mods patching the same
methods, and every patch is a chance to break them. Patch only where it is necessary, and prefer the
least invasive way to get the job done:

- Postfixes and prefixes that let the original run over prefixes that skip it; a prefix returning
  `false` replaces vanilla and silences every mod patching the method, so it needs a reason.
- Don't take over a code path in branches where vanilla would have done the same thing or nothing
  at all; act only in the cases Worldhalla actually changes.
- Narrow transpilers anchored on the specific instructions over rewriting a method.
- Scope a patch to the situation it's for (Worldhalla worlds, client or server) and leave vanilla
  behaviour untouched everywhere else.

## Code style

This is a low-comment codebase — naming carries the intent instead of doc comments. Don't add
comments that just restate what a method/type does; only add one where the WHY is genuinely
non-obvious (e.g. the discovery-timing note below).

Don't let reasoning spill into comments. Working out why an approach is right, what the
alternatives were, or what would break otherwise belongs in the conversation and the commit
message, not next to the code. A comment earns its place by telling the next reader something
they need in order to change this code safely — not by recording how the current shape was
arrived at.

## Solution layout

`Worldhalla.sln` contains:

- **Worldhalla.API** (assembly name **`Worldhalla`**) — the contract a worldgen mod implements: `IWorldGenerator`, `IWorldState`,
  `Biome`, and the `[WorldhallaPlugin]` attribute generators are discovered by, plus the optional
  `ISettingsHandler`/`ISettings` (for generators that take settings) and `IWorldGeography`.
  Deliberately free of game, Unity and BepInEx dependencies so a consumer needs nothing else.
- **Worldhalla.Plugin** (assembly name `Worldhalla.Plugin`, merged with the API into `Worldhalla.dll` for shipping) — the BepInEx plugin. `WorldhallaPlugin`
  wires logging and `PatchAll` in `Awake`, runs generator discovery in `Start`, and unpatches in
  `OnDestroy`. `Core/` holds what the README's Key ideas describe — the registry, the `.fwl` format, the
  query path, the handshake and dedicated-server world creation — with its patches under `Core/Patches/` and
  Harmony and game-type helpers under `Core/Extensions/`. Patches for menus and dialogs live under `UI/Patches/`.
- **Worldhalla.Plugin.Tests** — NUnit suite. Runs the transpilers over the game's real IL without
  launching it; skips rather than fails when no Valheim install is present.
- **GeneratorSample** — a minimal generator mod (moniker `GS`): noise islands, distance-banded biomes and one
  text-field setting. It is the reference for what a consumer needs, so it stays small and references only the API,
  BepInEx, Unity and its noise package, never the game. `ILRepack.targets` merges `DotnetNoise` into the build output
  itself, so `GeneratorSample.dll` is the whole plugin. Without `IWorldGeography` it must keep vanilla's polar boundaries and the water
  gap along them.
- **ChunkWatcher** — a standalone profiling mod, unrelated to the framework at assembly level but
  shipped alongside it as the tool for measuring worldgen cost. See below.

## Build

Requires the .NET 8 SDK and a local Valheim install (see `BuildTools/ValheimLibs.targets`, which
auto-detects common Steam paths on Windows/Linux/macOS; override with `-p:ValheimDataPath=...`).

`NuGet.Config` adds the BepInEx feed. It is load-bearing: `BepInEx.*` and `HarmonyX` are not on
nuget.org, so restore fails outright without it.

### Packaging

Packaging is a publish step, not a build step — an ordinary build produces no zip:

```
dotnet publish Worldhalla.Plugin -c Release
```

`BuildTools/Thunderstore.targets` is project-agnostic: `Worldhalla.Plugin.csproj` describes the
package with `Thunderstore*` properties and items, and the target generates `manifest.json` from
them, adds the icon plus the plugin's own `README.md` and `CHANGELOG.md`, stages the merged assembly
into `BepInEx/plugins/Worldhalla/` and zips it. There is no checked-in manifest; the package version
is the project's `Version`. It must be imported after the project's own properties.

The package ships **one file**, `Worldhalla.dll`: a plugin shipping several assemblies is
exposed to load order problems. `Worldhalla.Plugin/ILRepack.targets` merges on every build, into
`bin/<config>/netstandard2.1/merged/`, with the API as the primary assembly. The merged assembly
must carry the name consumers compiled against, so the API project's assembly is named `Worldhalla`,
the same as the shipped file. The file name and the assembly name can't be separated: the compiler
and ILRepack both name an assembly after its output file (`TargetName` silently overrides
`AssemblyName`). Nothing in the build compares a consumer's reference with the shipped assembly
name, so a mismatch only shows up at runtime — check with `monodis --assembly` /
`monodis --assemblyref` after touching any of this.

A second copy of the API types anywhere — merged into a consumer, or a consumer shipping the
API-only `Worldhalla.dll` a build output may carry — gives that consumer a different
`IWorldGenerator` than the registry it registers into. Consumers must reference the API as
compile-only and never ship it.

The API also ships to NuGet as package `Worldhalla` (`dotnet pack Worldhalla.API -c Release`). It is
reference-only: the assembly goes under `ref/` with no `lib/`, so a consumer referencing the package
can't ship or merge a second copy of the API. Its `Version` must match the plugin's.

Note: a project that references `ILRepack.Lib.MSBuild.Task` without supplying its own
`ILRepack.targets` gets the package's fallback target, which auto-merges **in Release only** with
no `LibraryPath` and fails to resolve Harmony.

## Tests

`RequiresValheimAttribute` turns a missing game install into skips rather than failures, and the
test project compiles its sources out entirely when the game is absent — keep game types out of
fixture fields and method signatures, since a test that never runs can still fail to load its
class.

The suite is organised around what actually breaks:

- **AnchorTests** — one test per instruction pattern the transpilers rely on. A game update breaks
  these first, and a named failure here is the difference between "Worldhalla fails to patch" in
  someone's log and knowing which anchor moved.
- **TranspilerTests** — runs the transpilers over the game's real IL as plain functions and asserts
  structure, not an exact instruction sequence. They carry instruction-count assertions that must
  be updated deliberately when a hook changes.
- **WorldDataTests** — the `.fwl` format round-trips, including the legacy Riverheim migration.
- **RegistryTests** — attribute discovery: labelled types are found, unlabelled `IWorldGenerator`
  implementations are not, and one mod's broken generator doesn't cost everyone else theirs.

## Architecture

### Registering a generator

A mod references `Worldhalla.API`, implements `IWorldGenerator` and labels the class:

```csharp
[BepInDependency("dev.gurebu.worldhalla")]
public class MyPlugin : BaseUnityPlugin { }

[WorldhallaPlugin("dev.you.mygen")]
public class MyGenerator : IWorldGenerator { }
```

The GUID in the attribute is the generator's identity: it is what worlds store and what peers
compare. It lives only there, not on `IWorldGenerator`, so there is a single source for it. Inside
the plugin it travels next to the generator: `Registry.Generators` is keyed by it, and a `World`
carries it in its `WorldgenBinding` (`GetWorldgen().Guid`), even when the generator isn't installed.
The type needs a parameterless constructor.

**Discovery runs in `WorldhallaPlugin.Start`, and must not move to `Awake`.** BepInEx loads a
plugin's assembly when it instantiates that plugin, and every mod depending on Worldhalla is
instantiated after it — during `Awake` the assemblies worth scanning are exactly the ones not
loaded yet. BepInEx 5 has no chainloader-finished event, but it adds every plugin component in one
synchronous pass and Unity runs every `Awake` from that pass before any `Start`. The dedicated
server also runs discovery from `FejdStartup.ParseServerArguments`, which isn't guaranteed to come
after that `Start`; discovery is idempotent, so running it early there is safe.

`Registry` scans only assemblies that reference the API assembly, which skips the game, Unity and
the BCL without enumerating a type, and it isolates failures: a `ReflectionTypeLoadException` or a
throwing constructor costs that one mod its registration and nothing else.

### World format

The header sits where vanilla reads the world version, and always has its sign bit set, so an
unmodded game reads a modded world as an invalid version and refuses it instead of corrupting it.
A modded game reading an unmodded world sees a plain version without the flag and defers to
vanilla. The layout is documented at the top of `Core/Patches/WorldPatch.cs`; keep that comment true.

- `CUSTOM_WORLDGEN_FLAG = 0x80000000` — always set
- low byte — format version, currently `0x00`
- `0xff` is reserved: Riverheim worlds written before the format version existed, handled by `Compat/RiverheimMigration`

Everything Worldhalla adds beyond the header goes into a single length-prefixed blob — itself a
`ZPackage` holding the worldgen id, Worldhalla's own settings and the generator's settings — so a
third-party `.fwl` reader can step over it without parsing its contents. Worldhalla's settings are a
blob of their own, empty for now, so settings can be added to it without changing the layout. Legacy
`0xff` worlds predate all of that and store the id and generator settings inline. Saving one rewrites
it in the current format, so starting a legacy world from the world list asks the player to confirm
first (`FejdStartupPatch.Awake` gates the world list's Start button).

The generator's settings blob is capped at 64 KiB (`Core/SettingsBlob`). The cap is checked
where settings come into being (default settings at discovery, the settings dialog, the command
line) and wherever a blob is read, but never on save: a save that throws mid-game costs more than an
oversized blob.

A world written by a newer format version than the installed one is declined, not misread. **Changing
what an existing format version means breaks worlds that already exist on disk; add a version.**

### Patches

`Core/Patches/WorldPatch` covers save/load of the `.fwl`, `Core/Patches/WorldGeneratorPatch` the per-point
query path (`GetHeight`/`GetBiomeHeight`/`GetBiome`/`GetForestFactor`), and `Core/Patches/ZNetPatch` the
handshake. The transpilers find their insertion points through `Core/Extensions/CodeMatcherExtension`
rather than hardcoded offsets; each helper there is an assumption about vanilla's IL shape, and
each has an anchor test.

`WorldGeneratorPatch` caches one rendered point per thread, because Valheim queries the same point
up to four times per vertex on a biome-border chunk and a generator doing its own blending would
otherwise repeat the work.

`UI/Patches/WorldListPatch` adds a worldgen moniker column to the start menu's world list by cloning
the `name` child of the row template and shrinking `name` to make room, and prepends the generator to
each row's tooltip. It is cosmetic, so players can turn it off (`Compatibility.WorldListPatch` in the
BepInEx config) and `WorldhallaPlugin.Awake` skips the class when they do. A new patch that Worldhalla
works without belongs behind that kind of switch; one it can't work without does not.

`UI/Patches/CreateWorldPanelPatch` replaces the create-world dialog with `UI/WorldCreationDialog` (specified in
`Specs/CREATE_WORLD_DIALOG.md`) and, through a transpiler on `OnNewWorldDone`, assigns the selected generator
right after `new World(name, seed)` — before the same method saves the `.fwl`, so a postfix would be too late.
The dialog is built in `FejdStartup.Awake` inside the vanilla `m_createWorldPanel`, whose own inner panel is hidden;
vanilla's name and seed fields and its Done/Cancel buttons are moved into it rather than cloned, so vanilla's
validation, `OnNewWorldDone` and the panel's show/hide paths keep working unchanged. Moving them must happen before
the panel is first activated: `UIGamePad` caches its parent `UIGroupHandler` in `Awake`.

All new UI must be usable with a controller; nobody on the project tests with one routinely, so it has to be designed in
rather than discovered. Valheim's model, which ours follows:
- Every pop-up gets its own `UIGroupHandler` that outranks the screen beneath it. That turns off the screen's hotkeys
  and focus, and its `m_defaultElement` is what receives focus.
- Hotkeys are `UIGamePad` components with an on-screen hint (B backs out, X confirms, as on vanilla's Done/Cancel).
  `NewButton` strips the template's hotkey unless it is given a gamepad button, since a stray copy of Cancel's B would
  fire alongside the real one.
- Pointer-only input (drag, scroll wheel) needs a stick or button equivalent, as the detailed preview's pan and zoom have.
- A selectable control needs a visible selected state.

The vanilla bindings and groups can be read from the start-menu bundle (see Inspecting game UI).

The dialog's world preview samples vanilla worlds through `UI/Preview/Vanilla/VanillaRiverPlacement`: Harmony
**reverse patches** that copy vanilla's river placement into stub methods, rewritten to draw from `PreviewRandom`
(a bit-exact reimplementation of `UnityEngine.Random`, verified once in game) so they can run off the main thread. They must stay copies. Nothing there may patch `WorldGenerator` itself, which places the rivers of every
real vanilla world. The class carries no Harmony attributes so `PatchAll` never picks it up, and `Prepare` checks the
originals' patch lists before and after copying. HarmonyX 2.7 can't apply detours under .NET 8, so the test host
crashes on any real patch or reverse patch; tests can only run transpilers as plain functions.

The detailed preview's location simulation (`UI/Preview/Locations`) queries a generator that is not
`WorldGenerator.instance`. `WorldGeneratorPatch`'s static hooks (`IsAshlands`, `GetForestFactor`, ...) only know the
running world, so the simulation must reach a Worldhalla world through instance methods alone, which the patch routes
through `__instance.GetWorld()`. A static it needs gets a shared helper both sides call, as `GeneratedWorld.ForestFactor`
is; calling the static would silently answer with vanilla's layout.

Both previews take their colours from `UI/Preview/PreviewPalette`: one sRGB colour per biome and one for water, so a
new biome needs one entry there. The small preview paints them flat; the detailed one (`HdrPreviewStyle`) lights them
in linear space with the knobs in `HdrPreviewSettings`. `Tools/PaletteLab/palette-lab.html` tunes those same values in
a browser and mirrors their defaults, so a change to the style or its defaults goes into both. The lab's world data
lives in its git-ignored `data/` folder (format in the page's header comment) and is never committed.

`Core/Patches/ServerStartupPatch` is the dedicated server's side of world creation, which never passes
through that dialog: `World.GetCreateWorld` (called only from `FejdStartup.ParseServerArguments`)
gets the generator named by `-wh guid <guid>` right after `new World(name, seed)`, for the same
reason, with settings from `ISettingsHandler.BuildSettingsFromCommandLine` over the opaque
`-wh settings <string>` (or `ISettingsHandler.DefaultSettings` without it). A generator that doesn't
implement `ISettingsHandler` takes no settings, so passing `-wh settings` to it stops the server, and
a `SettingsException` from the handler is reported along with its `CommandLineUsage`.
`CommandLine` parses `-wh <command> <value>` pairs, and a new command is one entry in its
table. Anything that should stop the server throws
`ServerStartupException`, which a finalizer on `ParseServerArguments` turns into vanilla's own
failure path (log, `Application.Quit`, return false) — the same one a bad password takes.

### What the game persists (and what it doesn't)

Baseline terrain is never saved. `Heightmap.Regenerate` rebuilds every chunk through
`HeightmapBuilder` → `WorldGenerator.GetBiomeHeight` each time it loads, so the generator is queried
afresh on every load and a world can't contain "old" and "new" terrain side by side — no seams unless
vanilla itself is bugged. What *is* saved is state built on top of the terrain:

- terrain edits — `TerrainComp` stores per-vertex offsets (`m_levelDelta`/`m_smoothDelta`) in its ZDO,
  re-applied over whatever baseline the generator returns now
- zone generation — `ZoneSystem.m_generatedZones` marks zones as generated, and their vegetation and
  locations are spawned once, as ZDOs at heights taken from the terrain at that moment

So a world loaded with a different generator or different settings gets consistent new terrain
everywhere, while saved objects and terrain edits no longer fit it (floating or buried objects, edits
offset from a different baseline).

### Extending game types

`Core/Extensions/WorldExtension` attaches Worldhalla's per-world state to `World` through a
`ConditionalWeakTable`: a single `WorldgenBinding`, absent for a vanilla world. An `InstalledWorldgen` holds the
generator and its settings; a `MissingWorldgen` holds only the GUID and the settings blob as read, so the world can
be saved without its generator. Anything deciding whether a world is a Worldhalla world checks for a binding, not a
generator. That is the intended way to add fields to a game type: treat them
as first-class fields and set them where vanilla sets its own (e.g. next to the name and seed at
construction), rather than carrying values across calls in temporary statics.

### Inspecting game UI

Scene and prefab layout is not in the decompiled code, but it can be read offline. The game's UI
lives in asset bundles under `valheim_Data/StreamingAssets/SoftRef/Bundles`, which
[UnityPy](https://github.com/K0lb3/UnityPy) opens and walks as GameObject/RectTransform trees (the
start menu is in bundles `10ba9da1` and `b8689a71`; search by a known child name to find others).
Scripted components such as `TMP_Text` can't be read that way, because the game ships without the
type metadata needed to decode them — check those at runtime.

Not every game type is in `assembly_valheim.dll`: UI helpers such as `UITooltip` live in
`assembly_guiutils.dll`, and others are spread across the remaining `assembly_*.dll` files in
`valheim_Data/Managed`. Reference such an assembly from `Worldhalla.Plugin.csproj` with
`Private="false"`, as is done for `Unity.TextMeshPro.dll`.

### Multiplayer

`Docs/VALHEIM_NETWORKING.md` maps the game's network stack (sockets, `ZRpc`, the `PeerInfo`
handshake, routed RPCs, ZDO sync) and Worldhalla's extension of it. Read it before touching
`ZNetPatch`, and keep its Worldhalla section true.

Compatibility is decided per world, not per peer. For a vanilla world, Worldhalla sends nothing and
changes nothing, so vanilla and modded peers mix freely in both directions. For a Worldhalla world, the
server requires a client `WorldhallaHandshake` from a Worldhalla with the same **major and minor**
version, and the world's generator at exactly the same `IWorldGenerator.Version`. The client then
requires that generator to accept the settings.

- **Patch releases must not change anything peers have to agree on.** That covers the RPC
  payloads and any `WorldGeneratorPatch` behaviour that alters the result of a query for an existing
  world: the translation layer runs on every client, so two Worldhalla versions that differ there
  generate different terrain for the same world. Any of these needs at least a minor bump.

- Never add fields to vanilla's packages. A vanilla peer reads them positionally. Anything
  Worldhalla needs to send goes in its own RPC, which a vanilla peer drops unread.
- The RPC names `WorldhallaHandshake`/`WorldhallaWorld`/`WorldhallaRefused` and the leading plugin
  version string of their payloads are permanent: they are how two different versions find out that they disagree.
- Anything sent during the handshake must go out before the sender's `PeerInfo`. See the doc for why.
- Every refusal of a Worldhalla client, on either side, leaves a player-facing sentence that
  `FejdStartupPatch` shows in place of vanilla's "Incompatible version". A new refusal needs one too.
- `ZRpc` silently skips argument types it doesn't know, enums included: cast `ConnectionStatus` to
  `int` before invoking `"Error"`.

### Localization

Player-facing text lives in `Worldhalla.Plugin/Localization/<Language>/*.csv`, one folder per language,
named as the game names it (`English`, `German`, ...). The files use the game's own CSV format with a
`Key,<Language>` header, are embedded in the DLL, and are fed to `Localization.LoadCSV` by a postfix on
`Localization.SetupLanguage`: English first, then the selected language over it. So a translation may
leave keys out, but a blank cell blanks the text rather than falling back. Keys start with `wh_` and
take arguments as `$1`, `$2`.

Text set once goes on a label as `$wh_key` with a `Localize` component, which follows language changes;
text set from code goes through `Translations.Get`. Log lines, console errors and generator-supplied
text are not translated. `TranslationTests` checks every language against English with the game's
parser.

`Translations` and the `SetupLanguage` postfix live next to the CSVs, in namespace
`Worldhalla.Plugin.Localization`. Inside `Worldhalla.Plugin` that namespace shadows the game's `Localization`
class, so the game type is written `global::Localization`.

### Logging

`Worldhalla.Plugin.Logger` is an indirection with a `LogLevel` enum and a settable sink, wired to BepInEx in
`WorldhallaPlugin.Awake`. Living in the root namespace, it wins over `UnityEngine.Logger` everywhere in the plugin
without an alias; only inside `WorldhallaPlugin`, where `Logger` is BepInEx's inherited property, it is written out in
full. With no sink installed it discards, which is what keeps the patches
callable from tests that have no BepInEx host. Don't give it a BepInEx type in its signature — the
test project doesn't reference `BepInEx.Core`.

## ChunkWatcher

A separate BepInEx mod (`dev.gurebu.chunkwatcher`) for measuring worldgen cost: average chunk build
time via `HeightmapBuilder.RequestTerrain`, plus an express test on `WorldGenerator.Initialize` that
times `GetBiome` and `GetBiomeHeight` over a million points.

Its express test samples a uniform disc over the whole world. Treat the numbers as
seed-specific — `RenderPoint` cost varies enough between worlds that comparing two different seeds
tells you nothing about a code change. It has no `ProjectReference` to Worldhalla and is not
packaged (its `Thunderstore.targets` is not imported).

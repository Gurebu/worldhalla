# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this
repository.

## What this is

Worldhalla is a BepInEx/Harmony framework that lets a mod replace Valheim's world generation
without inventing its own save format, settings storage or multiplayer handshake. It generates
nothing itself: it owns the `.fwl` header, the settings blob, the version check between peers and
the `WorldGenerator` query path, and delegates the actual terrain to whatever `IWorldGenerator` a
mod registers.

Worlds written through Worldhalla must stay loadable forever. The protocol version in the header
is a public compatibility surface, not an implementation detail — see "World format" below.

## Relationship to Riverheim

This repository is consumed by Riverheim as a git submodule mounted at `external/worldhalla`.
Riverheim compiles against **`Worldhalla.API` only** and takes the plugin as a
`[BepInDependency]` at runtime. A change spanning both is a commit here plus a commit in Riverheim
moving the submodule pointer.

## General guidelines

- The basic workflow for this project is to produce a diff and wait for approval.
- Don't bump versions or create any VCS artifacts (branches, commits, tags) unless explicitly prompted.
- Upon encountering a correction or just any unusual insight make suggestions to improve this (CLAUDE.md) file.

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

- **Worldhalla.API** — the contract a worldgen mod implements: `IWorldGenerator`, `ISettings`,
  `IWorldState`, `Biome`, and the `[WorldhallaPlugin]` attribute generators are discovered by.
  Deliberately free of game, Unity and BepInEx dependencies so a consumer needs nothing else.
- **Worldhalla.Plugin** (assembly name **`Worldhalla`**) — the BepInEx plugin. `WorldhallaPlugin`
  wires logging and `PatchAll` in `Awake`, runs generator discovery in `Start`, and unpatches in
  `OnDestroy`. Patches live under `Patches/`, Harmony helpers under `Extensions/`.
- **Worldhalla.Plugin.Tests** — NUnit suite. Runs the transpilers over the game's real IL without
  launching it; skips rather than fails when no Valheim install is present.
- **ChunkWatcher** — a standalone profiling mod, unrelated to the framework at assembly level but
  shipped alongside it as the tool for measuring worldgen cost. See below.

## Build

Requires the .NET 8 SDK and a local Valheim install (see `BuildTools/ValheimLibs.targets`, which
auto-detects common Steam paths on Windows/Linux/macOS; override with `-p:ValheimDataPath=...`).

`NuGet.Config` adds the BepInEx feed. It is load-bearing: `BepInEx.*` and `HarmonyX` are not on
nuget.org, so restore fails outright without it.

```
dotnet build Worldhalla.sln
dotnet test Worldhalla.sln
```

### Packaging

Packaging is a publish step, not a build step — an ordinary build produces no zip:

```
dotnet publish Worldhalla.Plugin -c Release
```

`Worldhalla.Plugin/Thunderstore.targets` stages `thunderstore/` plus the two assemblies into
`BepInEx/plugins/Worldhalla/` and zips it. It is deliberately self-contained (including its own
copy of the `UpdateVersion` task) so it travels with the project.

The package ships **two assemblies**: `Worldhalla.dll` and `Worldhalla.API.dll`, unmerged. There is
no ILRepack in this repository, and adding one needs care: consumers compile against
`Worldhalla.API`, so a second copy of those types anywhere — merged into the plugin, or shipped by
a consumer — gives that consumer a different `IWorldGenerator` than the registry it registers
into. Consumers must reference the API as compile-only.

Note: a project that references `ILRepack.Lib.MSBuild.Task` without supplying its own
`ILRepack.targets` gets the package's fallback target, which auto-merges **in Release only** with
no `LibraryPath` and fails to resolve Harmony.

## Tests

```
dotnet test Worldhalla.sln
dotnet test Worldhalla.Plugin.Tests --filter "FullyQualifiedName~AnchorTests"
```

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

[WorldhallaPlugin]
public class MyGenerator : IWorldGenerator
{
    public string Guid => "dev.you.mygen";
}
```

Identity comes from `IWorldGenerator.Guid`, not from the attribute, so the two cannot disagree.
The type needs a parameterless constructor.

**Discovery runs in `WorldhallaPlugin.Start`, and must not move to `Awake`.** BepInEx loads a
plugin's assembly when it instantiates that plugin, and every mod depending on Worldhalla is
instantiated after it — during `Awake` the assemblies worth scanning are exactly the ones not
loaded yet. BepInEx 5 has no chainloader-finished event, but it adds every plugin component in one
synchronous pass and Unity runs every `Awake` from that pass before any `Start`.

`Registry` scans only assemblies that reference the API assembly, which skips the game, Unity and
the BCL without enumerating a type, and it isolates failures: a `ReflectionTypeLoadException` or a
throwing constructor costs that one mod its registration and nothing else.

### World format

The header sits where vanilla reads the world version, and always has its sign bit set, so an
unmodded game reads a modded world as an invalid version and refuses it instead of corrupting it.
A modded game reading an unmodded world sees a plain version without the flag and defers to
vanilla. The layout is documented at the top of `Patches/WorldPatch.cs`; keep that comment true.

- `CUSTOM_WORLDGEN_FLAG = 0x80000000` — always set
- low byte — protocol version, currently `0x00`
- `0xff` is reserved: pre-protocol Riverheim worlds, handled by `Compat/RiverheimMigration`

Everything Worldhalla adds beyond the header goes into a single length-prefixed blob — itself a
`ZPackage` holding the worldgen id and the generator's settings — so a third-party `.fwl` reader can
step over it without parsing its contents. Legacy `0xff` worlds predate that and store both inline.

A world written by a newer protocol than the installed one is declined, not misread. **Changing
what an existing protocol version means breaks worlds that already exist on disk; add a version.**

### Patches

`Patches/WorldPatch` covers save/load of the `.fwl`, `Patches/WorldGeneratorPatch` the per-point
query path (`GetHeight`/`GetBiomeHeight`/`GetBiome`/`GetForestFactor`), and `Patches/ZNetPatch` the
handshake. The transpilers find their insertion points through `Extensions/CodeMatcherExtension`
rather than hardcoded offsets; each helper there is an assumption about vanilla's IL shape, and
each has an anchor test.

`WorldGeneratorPatch` caches one rendered point per thread, because Valheim queries the same point
up to four times per vertex on a biome-border chunk and a generator doing its own blending would
otherwise repeat the work.

### Multiplayer

`ZNetPatch` registers an `AssertWorldhallaVersion` RPC on both handshakes. Peers are compatible
when **major and minor match** — patch releases are free, a minor bump refuses connections across
the version boundary.

### Logging

`Internal.Logger` is an indirection with a `LogLevel` enum and a settable sink, wired to BepInEx in
`WorldhallaPlugin.Awake`. With no sink installed it discards, which is what keeps the patches
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

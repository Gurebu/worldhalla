# Worldhalla API

To create a world generator for Worldhalla, you have to roll a BepInEx plugin and have a class somewhere inside
your .dll that implements `Worldhalla.API.IWorldGenerator` and registers with Worldhalla using `[WorldhallaPlugin(GUID)]`.

There's a [Sample project](https://github.com/Gurebu/worldhalla/tree/master/GeneratorSample) that shows a minimal
viable integration you can take a look at. It's a functioning world generator so if you build it and throw into your
BepInEx plugins folder, it will work and enable world generation with its particular (rather simple) algorithm.

## Guidelines

### Stable output

Valheim never saves a world's baseline terrain: every chunk is regenerated from the generator each
time it loads. What it does save — objects placed when a zone was first generated, and terrain edits
stored as offsets — is only valid against the terrain it was created on. A generator must therefore
return the same output for the same seed and settings forever: across its own updates, across
platforms and across runtimes.

**Determinism warning**: Current builds of Valheim on different platforms will use different implementations of
`System.Math` and consequently Unity's `Mathf`. The differences are subtle, but enough to cause divergence in
algorithms with a tendency to avalanche. The game's built-in noise-based and context-free algorithm is mostly
immune to small floating point errors (river placement however is not, though I have not done extensive tests on that),
but take extra care when building your own. **Riverheim** for example rolls its own implementation of floating point
transcendent functions because in development it showed actual cross-platform differences. Basic floating point
operations seem to be safe, but it's always better to run tests than trust.

### Randomness

Don't use `UnityEngine.Random`. Quality of the randomness aside, it will straight up fail with Worldhalla because
it can't be called on a background thread. It's recommended to avoid anything concerning random from `UnityEngine`
overall, especially `PerlinNoise` as it has atrocious quality and will cause you a lot of headache. If you have to have
a passable random API from Unity, use `Unity.Mathematics`. It's not the best choice for a different reason (it's much
faster to work with `double` under Mono than `float`), but Unity is moving towards CoreCLR so maybe it's not a big deal.
`FastNoiseLite` is a good alternative for all of your noise needs, and it can be compiled to work in double precision.

### Version

In multiplayer, every client computes terrain locally. A client may only join a world created with
your generator if it reports exactly the same `Version` string as the server. Change it whenever the
same seed and settings may produce different terrain, deliberately or not. Returning your plugin
version is the safe choice. Keeping the string unchanged across a release lets players on
different versions of your mod play together, so do that only when you're sure the terrain is
identical.

## Implementation

TODO nuget
You can get access to the API by either depending on a nuget package or just including this repository as a git
submodule. Reference `Worldhalla.API` as compile-only and don't ship it with your mod, like this:

```msbuild
<ProjectReference Include="<submodule>/Worldhalla.API/Worldhalla.API.csproj" Private="false" />
```

Then define your core class as a BepInPlugin like you would with any mod and declare it dependent on Worldhalla:

```csharp
[BepInPlugin("dev.you.mygen", "MyGen", "1.0.0")]
[BepInDependency("dev.gurebu.worldhalla")]
public class MyPlugin : BaseUnityPlugin { }
```

The BepInDependency on Worldhalla is necessary to guarantee proper mod load order.

### IWorldGenerator & IWorldState

The only two necessary things to implement are `IWorldGenerator`:

```csharp
[WorldhallaPlugin("dev.you.mygen")]
public class MyGenerator : IWorldGenerator
{
    public string Name => "My Generator";
    public string Moniker => "MG";
    public string Version => "1.0.0";
    
    public IWorldGenerator.Feature[] Features => [];
    
    public IWorldState Generate(int seed, ISettings? settings, IProgress<float> progress, CancellationToken cancellation) { ... }
}
```

and `IWorldState`:

```csharp
public class MyWorldState : IWorldState
{
    public Biome GetBiome(float x, float y) { ... }
    
    public float GetForestation(float x, float y) { ... }
    
    public IWorldState.PointData RenderPoint(float x, float y, Biome biome) { ... }
}
```

The `[WorldhallaPlugin]` attribute is similar in spirit to `[BepInPlugin]` and has to specify your generator's **Globally
Unique Identifier** or **GUID**. This is what gets written in the save file and distinguishes your generator from every
other one. You can't change your **GUID** once you've shipped your mod. It doesn't have to match your BepInPlugin GUID,
but it's not a bad idea to do so and to avoid clashes it's best to use the inverse domain notation for it,
like `"com.example.generator"`.

The `Generate` method is expected to be a pure function (it has to return the exact same world every time it's called
with the same seed and settings) and might be called on a background thread. If it takes a long while, you can report
progress using the `progress` argument and watch out for generation cancellation on the `cancellation` token, but
you're free to ignore these two parameters if your pre-generation process is fast or if you're building a stateless
generator.

All of the `IWorldState` methods are expected to be pure functions that don't change any state and can be called
concurrently on any thread. All units of height are in meters above sea level. All metadata
(other parts of [PointData](./World/IWorldState.cs)) is in the range of `[0 .. 1]`.

One of the gotchas here is the `Biome` parameter of `RenderPoint`. The use case for this in-game is biome blending: when
the game builds a chunk, it will query the corner biomes and gradually transition the heights in the chunk across these
4 biomes, calling the generator multiple times for each point with those different biomes. If your generator does its
own biome blending, declare it as a `Feature`, and you will always receive `Biome.None` here that you can ignore.

### ISettingsHandler

Implementing this interface is optional, but if you choose to do it, it has to be on the same class that implements
`IWorldGenerator`. Implement this if your generator has settings that need to be saved into the world. If your generator
is setting-less, its `Generate` method will receive `null` when called, otherwise it will receive a settings instance.

The interfaces to work with are:
- [ISettingsHandler](./Settings/ISettingsHandler.cs)
- [ISettings](./Settings/ISettings.cs)
- [ISettingsForm](./Settings/ISettingsForm.cs)

#### Binary serialization

This happens in two places: in the save file and over the network. In both cases the final form that gets written and
read is just a binary blob aka `byte[]`, you're free to put anything you want inside of it and are solely responsible
for interpreting its contents. Since this blob can potentially live in the save file for years, take extra care to
maintain deserialization code.

#### Command line deserialization

If your generator can be parametrized, dedicated servers will need to specify what parameters to pass for your world's
initial creation. Server owners will add `-wh settings "<settings>"` to the server's start script, and you will receive
the `<settings>` string as input.

#### Game UI

Finally, when a user creates your world in-game, they also need a way to specify the settings. Worldhalla will ask your
mod for the settings form it presents to the user and then get back to you with the values the user has entered. See
the [Sample Project](https://github.com/Gurebu/worldhalla/tree/master/GeneratorSample) for how it's done.

### IWorldGeography

This optional interface has to be on top of the class that already implements `IWorldState`. World geography are basic
geographic properties of your world that the game doesn't normally expect to differ across worlds. Currently, those are
the world size and the location of the world's polar caps which contain the Ashlands and the Deep North (and the game
will not expect those biomes anywhere else).

Support for these properties in Worldhalla 1.0 is limited, but expect it to change in the future.

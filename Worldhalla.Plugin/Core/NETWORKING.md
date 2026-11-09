# Networking

How Worldhalla extends Valheim's multiplayer handshake, in `Core/Patches/ZNetPatch.cs`. It builds on the
game's own network stack (sockets, `ZRpc`, the `PeerInfo` handshake, keepalive), which this document
doesn't describe.

The goal is that peers only have to agree on Worldhalla when the **shared world** is a Worldhalla
world. For a vanilla world it makes no difference whether either side has Worldhalla, or which
version. Vanilla packages are never modified. Three RPCs of Worldhalla's own carry everything, and a
vanilla peer drops all of them unread:

| RPC | Direction | When | Payload |
|---|---|---|---|
| `WorldhallaClientInfo` | client → server | in reply to `ClientHandshake`, so before the client's `PeerInfo` | `[string plugin version][int n]` then `n` × `[string worldgen id][string worldgen version]` |
| `WorldhallaWorld` | server → client | right before the server's `PeerInfo`, Worldhalla worlds only | `[string plugin version][string worldgen id][byte[] worldhalla settings][byte[] generator settings]` |
| `WorldhallaRefused` | server → client | right before the `Error(3)` that refuses a Worldhalla client | `[string plugin version][int reason][string worldgen id][string worldgen name][string worldgen version]` |

- **Server.** A prefix on `RPC_PeerInfo` lets the vanilla path run untouched when the server's world
  is vanilla. For a Worldhalla world, it looks at the client's recorded `WorldhallaClientInfo` and
  requires three things: the client's Worldhalla has the same major and minor version, the world's
  generator is installed, and its `IWorldGenerator.Version` is exactly the same string. Otherwise it
  logs the reason, sends `Error(3)` (which a vanilla client also understands) and skips the vanilla
  handler. The decision is made on the server so the reason ends up in the server's log.
- **Client.** A transpiler on `RPC_PeerInfo`, right after `m_worldGenVersion` is read and before
  `WorldGenerator.Initialize`, applies the stored `WorldhallaWorld`, if there is one, to the new
  `World`. With no stored message, the world is vanilla. As a backstop, the client still refuses
  (external error plus `Disconnect`) on a Worldhalla version mismatch or a generator it doesn't have. Rejecting
  the settings can only happen here. A second hook, right after `WorldGenerator.Initialize`, refuses the world when
  `Pregenerate` failed to generate it, before vanilla samples it or marks the client connected.
- **Keepalive.** The client generates the world inside its `RPC_PeerInfo`, while its main thread waits,
  and a waiting main thread answers no pings: past 30 s the server closes the connection. While
  `WorldGeneratorPatch.Generate` waits for the generator, it answers a
  ping request of its own making on every peer once a second, through a reverse patch of vanilla's
  `ZRpc.ReceivePing` that leaves out other mods' patches, so the reply is vanilla's own. The server's
  `ReceivePing` resets the timeout on any reply, requested or not, and a Steam socket hands the
  message to Steam at once, without the main thread's help. `KeepaliveTests` pins both halves.
- **Error message.** Whoever refuses leaves a sentence for the player: the server through
  `WorldhallaRefused`, the client directly. A postfix on `FejdStartup.ShowConnectError`, which runs
  when the main menu comes back, puts it into the vanilla "connection failed" panel in place of
  "Incompatible version". A vanilla client only ever sees the vanilla message.

The resulting compatibility matrix (WH = Worldhalla installed):

| World | Server | Client | Result |
|---|---|---|---|
| vanilla | any | any | vanilla behaviour, Worldhalla invisible |
| Worldhalla | WH | vanilla | refused by the server |
| Worldhalla | WH | WH, other major.minor | refused by the server |
| Worldhalla | WH | WH, generator missing or at another version | refused by the server |
| Worldhalla | WH | WH, Worldhalla settings it can't read | refused by the client |
| Worldhalla | WH | WH, settings rejected by the generator | refused by the client |
| Worldhalla | WH | WH, generator fails to generate the world | refused by the client |
| Worldhalla | WH | WH, same major.minor, same generator version | joins |

Two peers compute the same terrain for a world only if they agree on three things:

1. **Worldhalla's wire format.** The payloads above may only change with a minor version bump.
   The RPC names and the leading plugin version string of every payload must never change: they are
   how two different versions find out that they disagree.
2. **Worldhalla's translation layer.** `WorldGeneratorPatch` turns generator output into answers to
   the game's queries (forest factor, Ashlands and Deep North overrides, biome conversion, the vanilla
   fallbacks), and a client runs it locally. Any change there that alters the result for an existing
   world needs a minor version bump too. Patch releases are compatible with each other, so they must
   leave this layer's results alone.
3. **The generator.** `IWorldGenerator.Version` is compared exactly. The generator's author decides
   when it changes, and must change it whenever the same seed and settings may produce different
   terrain.

The plugin version check is independent of the `.fwl` format version in `WorldPatch`. A version
check only keeps multiplayer peers from disagreeing. A terrain change still alters every existing world
on its next load, single-player included, because terrain is never saved.

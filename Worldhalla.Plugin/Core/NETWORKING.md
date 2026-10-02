# Valheim networking

How Valheim's multiplayer layer works, reconstructed from the decompiled game (game version 1.0.16,
network version 40), and how Worldhalla hooks into it. Class and method names are the game's own, so
everything here can be checked against a decompile of `assembly_valheim.dll`. When the game updates,
treat this as a map of where to look, not as proof.

## Layers

```
ZNetView / ZRoutedRpc / ZDOMan      game objects, per-object RPCs, state replication
ZNet                                session: handshake, peer list, player list, time, saving
ZRpc                                named RPCs over one connection, keepalive
ZPackage                            binary serialization (BinaryWriter over a MemoryStream)
ISocket                             ZSteamSocket | ZPlayFabSocket | ZSocket2 (plain TCP)
```

The topology is a star. Every client connects to the server only. Client-to-client traffic (routed
RPCs, ZDOs a client owns) goes through the server. A listen server (a player hosting from the menu)
and a dedicated server run the same code path. `ZNet.IsServer()` is the only difference between the
two roles.

## Transport: `ISocket`

`ZNet.m_onlineBackend` picks one of three implementations. All three deliver whole messages, reliably
and **in order**.

| Socket | Used for | Framing / reliability |
|---|---|---|
| `ZSteamSocket` | Steam (listen and dedicated servers, joining by SteamID or IP) | Steam Networking Sockets. Every message is sent with flag `8` (`k_nSteamNetworkingSend_Reliable`). 30 s connection timeout, send rate fixed at 150 KB/s |
| `ZPlayFabSocket` | Crossplay (Xbox/MS Store/PlayStation, and Steam with crossplay on) | Its own reliability layer over PlayFab Party: sequence ids, acks, retransmits and an out-of-order buffer. Turns zlib compression on in `VersionMatch()` |
| `ZSocket2` | `OnlineBackendType.CustomSocket` (direct TCP via `ZConnector2`) | `TcpClient`. Each message carries a 4-byte length prefix |

`ISocket.VersionMatch()` is called once on each side after the handshake succeeds. It does nothing
on Steam and TCP sockets. On PlayFab it switches the socket to compressed messages. Anything a mod
sends during the handshake has to go out **before** the sender's `PeerInfo`, or it lands on the
wrong side of that switch.

## Serialization: `ZPackage`

A `ZPackage` wraps a `BinaryWriter`/`BinaryReader` over one `MemoryStream`, so everything is
little-endian:

- primitives at their natural size (`bool` = 1 byte)
- `string` = 7-bit-encoded length followed by UTF-8 bytes
- `byte[]` = `int` length followed by the bytes
- nested `ZPackage` = `int` length followed by the bytes
- `Vector3` = 3 floats, `Quaternion` = 4 floats
- `ZDOID` = `long` user id followed by `uint` id

Readers don't check that a package has been fully consumed, so **trailing bytes are ignored**. A
field inserted in the middle of a package, on the other hand, shifts every field after it.

## RPC: `ZRpc`

There is one `ZRpc` per connection (`ZNetPeer.m_rpc`). A message is:

```
[int] method hash = name.GetStableHashCode()   (0 = ping)
[...] arguments, serialized by ZRpc.Serialize
```

- **Registration.** `Register(name, handler)` maps the hash to a delegate. Registering the same name
  again replaces the old handler (the dictionary entry is removed, then added), so the last
  registration wins.
- **Unknown methods are silently dropped.** `HandlePackage` does a `TryGetValue` and nothing else.
  That's why a mod can talk to other copies of itself through its own RPC names without disturbing
  peers that don't have the mod.
- **Argument types.** Supported types are `int, uint, long, float, double, bool, string, ZPackage,
  List<string>, Vector3, Quaternion, ZDOID, HitData, ISerializableParameter`. Anything else,
  **including enums**, is skipped without writing anything. Invoking `"Error"` with a boxed
  `ConnectionStatus` sends an empty argument list. The receiver then throws
  `EndOfStreamException`, and that turns into a version error by accident. Always cast enums to
  `int`.
- **Handler errors.** An `EndOfStreamException` in a handler makes `ZRpc.Update` return
  `ErrorCode.IncompatibleVersion`, and ZNet then sets `ConnectionStatus.ErrorVersion`. Any other
  exception is logged and swallowed.
- **Keepalive.** A ping (hash `0`, `bool` request/reply) goes out every second. The connection is
  closed after 30 s without a reply, or 90 s while `ZRpc.SetLongTimeout(true)` is on during loading.

## The handshake

Sequence, from the socket connecting to both sides being ready:

```
client                                              server
  |  transport connects                               |  hostSocket.Accept() -> new ZNetPeer
  |  OnNewConnection: register base RPCs              |  OnNewConnection: register base RPCs
  |------ ServerHandshake(string inviteSecretKey) --->|
  |                                                   |  RPC_ServerHandshake: ClearPlayerData,
  |<----- ClientHandshake(bool needPassword,          |    needPassword = password set and the
  |                       string passwordSalt) -------|    platform invite key is not valid
  |  RPC_ClientHandshake: password dialog if needed,  |
  |  then SendPeerInfo                                |
  |------ PeerInfo(ZPackage) ------------------------>|
  |                                                   |  RPC_PeerInfo: all admission checks,
  |                                                   |  register session RPCs, SendPeerInfo,
  |<----- PeerInfo(ZPackage) -------------------------|  VersionMatch, send player/admin lists,
  |  RPC_PeerInfo: build World from the package,      |  ZDOMan.AddPeer, ZRoutedRpc.AddPeer
  |  WorldGenerator.Initialize, VersionMatch,         |
  |  status = Connected, ZDOMan/ZRoutedRpc.AddPeer    |
```

Both sides register the same base RPCs in `ZNet.OnNewConnection`: `PeerInfo`, `Disconnect`,
`SavePlayerProfile`, `RPC_RequestValidSimulationDistance` and `RPC_ValidatedSimulationDistance`.
After that, the server adds `ServerHandshake`, and the client adds `Kicked`, `Error` and
`ClientHandshake`. The client then immediately invokes `ServerHandshake`.

### PeerInfo layout

Both directions start with the same fields:

| Field | Type |
|---|---|
| session uid (`ZDOMan.GetSessionID()`) | `long` |
| game version, e.g. `1.0.16` | `string` |
| network version, `40` | `uint`, present only if the game version ≥ 0.214.301 |
| reference position | `Vector3` |
| player name | `string` |
| PlayFab id | `string` |
| desired `SimulationDistance` | serialized struct |

The client's `PeerInfo` then adds:

| Field | Type |
|---|---|
| password hash (`HashPassword(pwd, salt)`, or empty) | `string` |
| invite secret key | `string` |
| Steam session ticket | `byte[]` |

The server's `PeerInfo` then adds:

| Field | Type |
|---|---|
| world name | `string` |
| seed | `int` |
| seed name | `string` |
| world uid | `long` |
| `m_worldGenVersion` | `int` |
| net time | `double` |

The **network version is the only version that is compared**. The game version string is only
parsed to decide whether a network version follows it. A client never reads the `.fwl`: it builds
its `World` from these six fields alone and calls `WorldGenerator.Initialize` straight away.

### Server-side admission, in order (`RPC_PeerInfo`)

| Check | Error sent |
|---|---|
| network version ≠ 40 | `3` ErrorVersion |
| blacklisted or not whitelisted (`IsAllowed`) | `8` ErrorBanned |
| simulation distance | clamped and sent back, no rejection |
| Steam session ticket invalid | `8` |
| PlayFab: blocked user | `8` |
| PlayFab: crossplay not allowed | `10` ErrorPlatformExcluded |
| PlayFab: not authenticated (async) | `5` ErrorConnectFailed |
| 10 or more players | `9` ErrorFull |
| password hash mismatch, unless the invite secret key is valid | `6` ErrorPassword |
| session uid already connected | `7` ErrorAlreadyConnected |

A rejection is **only a message**. The server sends `Error(int)` and returns, leaving the peer
connected but never ready (`m_uid == 0`). The client's `RPC_Error` sets `m_connectionStatus`, and
`Game.FixedUpdate` logs out as soon as the status is neither `Connecting` nor `Connected`. That
teardown is what closes the connection. `FejdStartup.ShowConnectError` maps the status to the
message the player sees (`$error_incompatibleversion` and so on).

When the client is the side that refuses, it uses `ZNet.SetExternalError(status)` and then invokes
`Disconnect`. The server drops the peer. `UpdatePeers` notices the closed socket, and because an
external error was set, it reports that error instead of `ErrorDisconnected`.

### After the handshake

The session RPCs are registered in `RPC_PeerInfo` on success:

- **Both sides:** `ServerSyncedPlayerData`, `PlayerList`, `HistoricalPlayerList`, `AdminList`,
  `RemotePrint`, plus `RoutedRPC` (added by `ZRoutedRpc.AddPeer`) and `ZDOData` (added by
  `ZDOMan.AddPeer`).
- **Server only:** `CharacterID`, `PlayerID`, `RPC_PeerRealtimeMultiplaying`, `Kick`, `Ban`,
  `Unban`, `RPC_RemoteCommand`, `Save`, `PrintBanned`.
- **Client only:** `NetTime`.

`ZNet.SendPeriodicData` runs every 2 s. The server sends `NetTime` and `PlayerList`, plus
`HistoricalPlayerList` when it has changed. A client sends `ServerSyncedPlayerData`: its reference
position, whether that position is public, and a string dictionary.

## Above ZRpc

### `ZRoutedRpc`: addressed messages through the server

A routed message is a single `ZRpc` method, `RoutedRPC(ZPackage)`, carrying:

```
[long] msg id   [long] sender peer   [long] target peer (0 = everybody)
[ZDOID] target ZDO (None = global)   [int] method hash   [ZPackage] arguments
```

A client always sends to the server. The server delivers to the target, or to every ready peer except
the sender when the target is `0`. A message with a target ZDO goes to
`ZNetScene.FindInstance(zdo).HandleRoutedRPC`. That is how `ZNetView.InvokeRPC` reaches the
component on one specific object. Global routed RPCs include `SetGlobalKey`, `GlobalKeys`,
`LocationIcons`, `DestroyZDO` and `RequestZDO`. Unlike `ZRpc`, `ZRoutedRpc.Register` uses
`Dictionary.Add` and **throws** on a duplicate name.

### `ZDOMan`: replicated object state

A ZDO is a networked object's persistent state: prefab, position, rotation, owner and typed key/value
data. Every 50 ms (`c_SendFPS = 20`), `ZDOMan.Update` walks the peer list, one peer per frame. Each
visit builds a sync list of ZDOs near the peer whose data or owner revision is newer than what that
peer last received, and sends them as one `ZDOData` package:

```
[int n] invalidated-sector ZDOIDs x n
{ [ZDOID] [ushort ownerRevision] [uint dataRevision] [long owner] [Vector3 pos] [ZPackage data] }*
[ZDOID.None]
```

A peer isn't sent anything while its socket send queue is over 10 KB. The receiver keeps whichever
copy has the higher data revision. Ownership is an owner id with its own revision. Only the owner
simulates an object and writes to its ZDO. Every 2 s, `ZDOMan.ReleaseZDOS` on the server gives
unowned persistent ZDOs inside a peer's active area to that peer, and releases a peer's ZDOs once
they leave its area. **Clients own and simulate the world around them**: creatures, spawners,
physics. They do that against their own local terrain.

### Zones: who generates what

Zone contents are **server-authoritative**. `ZoneSystem.PokeLocalZone` spawns in `Full` mode, which
places locations, vegetation and the zone controller, only on the server and only for zones not yet
marked generated. The server also creates "ghost" zones around every peer's reference position, so a
remote player's surroundings are generated on the server and reach the client as ZDOs. A client
spawns zones in `Client` mode, which places nothing.

**Terrain is computed locally on every machine.** A client builds each chunk's heightmap through
`HeightmapBuilder` → `WorldGenerator.GetBiomeHeight` from the `World` it built out of `PeerInfo`, and
answers biome queries the same way. Terrain edits (`TerrainComp`) and every placed object arrive as
ZDOs from the server. If a client's generator disagrees with the server's, nothing desyncs at the
protocol level. Instead, the client sees objects floating or buried and terrain edits applied
against the wrong baseline. Anything it owns is simulated against the wrong ground, and the results
are written back into the shared world.

## Server discovery and advertised versions

`ZNet.OpenServer` registers the server with `ZSteamMatchmaking` (Steam game tags or lobby data)
and/or `ZPlayFabMatchmaking` (`number_key13` = network version). The advertised data includes the
game version, the network version (`40`), the world modifiers, the password flag and the seed name.
`ServerListGui` greys out entries whose network version differs, and `FejdStartup` shows
`$error_needslocalupdatetojoin` / `$error_needsserverupdatetojoin`. Nothing mod-specific is
advertised.

## Worldhalla's extension (`Core/Patches/ZNetPatch.cs`)

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

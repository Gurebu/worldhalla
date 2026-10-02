## Dedicated servers

A dedicated server creates the world named by `-world` when it doesn't exist yet. By default that
world is vanilla; to have a generator create it instead, pass its GUID:

```
valheim_server -world MyWorld -wh guid com.example.generatorsample
```

Settings for the new world are passed as a single string, whose format is up to the generator;
without it the generator's defaults are used:

```
valheim_server -world MyWorld -wh guid com.example.generatorsample -wh settings "scale 1.5"
```

These options only apply to worlds being created: an existing world keeps the generator it was made
with, and the server logs a warning if that differs from the one requested. The server refuses to
start, rather than falling back to vanilla, if the GUID belongs to no installed generator, if the generator rejects the settings, or if a
world with that name exists but can't be loaded — vanilla would silently replace such a world with
a new one.

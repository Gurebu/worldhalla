using Worldhalla.API;
using Worldhalla.API.Settings;

namespace Worldhalla.Plugin.Core;

public sealed class RegisteredWorldgen
{
    public RegisteredWorldgen(string guid, IWorldGenerator instance)
    {
        Guid = guid;
        Instance = instance;
        Version = instance.Version ?? throw new InvalidOperationException("Version is null");
        Moniker = instance.Moniker ?? throw new InvalidOperationException("Moniker is null");
        BiomeBlending = instance.Features.Contains(IWorldGenerator.Feature.BiomeBlending);

        if (instance.Name == null)
            throw new InvalidOperationException("Name is null");
    }

    public string Guid { get; }

    public IWorldGenerator Instance { get; }

    public string Version { get; }

    public string Moniker { get; }

    public bool BiomeBlending { get; }

    public string Name => Instance.Name;

    public ISettingsHandler? SettingsHandler => Instance as ISettingsHandler;

    public ISettings? DefaultSettings => SettingsHandler?.DefaultSettings;

    public ISettings? LoadSettings(byte[] blob) => SettingsHandler?.BuildSettingsFromBinaryBlob(blob);
}

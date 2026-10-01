using System.IO;
using SoftReferenceableAssets;
using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview.Locations;

/*
 * Reads prefabs that are only listed in the extended asset manifest, which a release build does not let soft
 * references load. The bundle holding one is opened directly along with its dependencies, and whatever was opened here
 * is closed again before returning, since the game opens the same bundles itself once a world loads. Nothing read may
 * keep a reference into those bundles: plain serialized data survives the closing, engine objects do not.
 */
internal static class GameAssets
{
    // Main thread only
    public static T Read<T>(string path, Func<GameObject, T> read)
    {
        var database = new AssetBundleDatabase([
            Path.Combine(Shared.GetBuildDirectory(), Shared.c_ManifestFileName),
            Path.Combine(Shared.GetBuildDirectory(), Shared.c_ManifestExtendedFileName),
        ]);

        var location = FindLocation(database, path);
        var bundlePaths = database.GetAllBundleNamesAndPaths();
        var opened = new List<AssetBundle>();

        try
        {
            foreach (var dependency in database.GetAllDependencies(location.m_bundleName))
                Open(dependency, bundlePaths[dependency], opened);

            var bundle = Open(location.m_bundleName, bundlePaths[location.m_bundleName], opened)
                ?? throw new InvalidOperationException($"Bundle '{location.m_bundleName}' could not be opened");

            var prefab = bundle.LoadAsset<GameObject>(location.m_path)
                ?? throw new InvalidOperationException($"'{location.m_path}' is not in bundle '{location.m_bundleName}'");

            return read(prefab);
        }
        finally
        {
            foreach (var bundle in opened)
                bundle.Unload(true);
        }
    }

    // Copies through the GPU, since the sprite's texture is usually neither readable nor its own
    public static Texture2D Copy(Sprite sprite)
    {
        var rect = sprite.textureRect;
        var source = sprite.texture;
        var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var active = RenderTexture.active;

        try
        {
            Graphics.Blit(source, target);
            RenderTexture.active = target;

            var copy = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(rect, 0, 0);
            copy.Apply(false);
            return copy;
        }
        finally
        {
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static AssetLocation FindLocation(AssetBundleDatabase database, string path)
    {
        foreach (var id in database.GetAllAssetIDs())
        {
            var location = database.GetLocation(id);
            if (location.m_path == path)
                return location;
        }

        throw new InvalidOperationException($"'{path}' is not in the asset manifests");
    }

    private static AssetBundle? Open(string name, string path, List<AssetBundle> opened)
    {
        if (FindOpen(name) is { } open)
            return open;

        var bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null)
            return FindOpen(name);

        opened.Add(bundle);
        return bundle;
    }

    private static AssetBundle? FindOpen(string name)
    {
        return AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(bundle => bundle.name == name);
    }
}

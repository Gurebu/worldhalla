using System.Reflection;
using Worldhalla.API;
using Worldhalla.API.Attributes;

namespace Worldhalla.Plugin.Internal;

internal static class Registry
{
    private static readonly Dictionary<string, IWorldGenerator> WorldGenerators = new();
    private static readonly HashSet<Assembly> ScannedAssemblies = [];

    private static readonly string ApiAssemblyName = typeof(WorldhallaPluginAttribute).Assembly.GetName().Name;

    public static IEnumerable<IWorldGenerator> Generators => WorldGenerators.Values;

    public static void DiscoverGenerators()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!ScannedAssemblies.Add(assembly) || !ReferencesApi(assembly))
                continue;

            foreach (var type in LoadableTypes(assembly))
                RegisterIfLabelled(type);
        }
    }

    internal static bool RegisterGenerator(string guid, IWorldGenerator generator)
    {
        if (WorldGenerators.TryGetValue(guid, out _))
            return false;
        
        WorldGenerators.Add(guid, generator);

        return true;
    }

    public static bool FindByGuid(string guid, out IWorldGenerator generator)
    {
        return WorldGenerators.TryGetValue(guid, out generator);
    }

    // An assembly that cannot see the attribute cannot carry it, which skips the game and Unity wholesale.
    private static bool ReferencesApi(Assembly assembly)
    {
        return assembly.GetName().Name == ApiAssemblyName
               || assembly.GetReferencedAssemblies().Any(reference => reference.Name == ApiAssemblyName);
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        // A mod referencing types from an absent optional dependency still yields the types that did load.
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(type => type != null).Select(type => type!);
        }
        catch (Exception e)
        {
            Logger.LogError($"Could not read types from '{assembly.GetName().Name}': {e.Message}");

            return Array.Empty<Type>();
        }
    }

    private static void RegisterIfLabelled(Type type)
    {
        if (type.GetCustomAttribute<WorldhallaPluginAttribute>() == null)
            return;

        if (type.IsAbstract || type.ContainsGenericParameters || !typeof(IWorldGenerator).IsAssignableFrom(type))
        {
            Logger.LogError($"'{type.FullName}' is labelled [WorldGenerator] but is not a concrete IWorldGenerator, skipping");
            return;
        }

        IWorldGenerator generator;

        try
        {
            generator = (IWorldGenerator)Activator.CreateInstance(type)!;
        }
        catch (Exception e)
        {
            Logger.LogError($"Could not instantiate worldgen '{type.FullName}': {e.Message}");

            return;
        }

        if (RegisterGenerator(generator.Guid, generator))
            Logger.LogInfo($"Discovered worldgen '{generator.Guid}' in '{type.Assembly.GetName().Name}'");
        else
            Logger.LogWarning($"Worldgen '{generator.Guid}' from '{type.FullName}' is already registered, skipping");
    }
    
    public static void PurgeAll()
    {
        WorldGenerators.Clear();
        ScannedAssemblies.Clear();
    }
}

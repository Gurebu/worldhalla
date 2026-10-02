using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview.Vanilla;

/*
 * Private copies of vanilla's river placement for the preview. Vanilla draws rivers from the engine's global
 * UnityEngine.Random, which only the main thread may use; these copies draw from PreviewRandom instead, so they can
 * run on a preview thread.
 *
 * The copies are Harmony reverse patches: the original IL is copied into the stubs below and rewritten there.
 * Nothing here patches WorldGenerator itself, and Prepare checks that afterwards, since the real methods place the
 * rivers of every vanilla world that gets played.
 */
public static class VanillaRiverPlacement
{
    private static readonly string[] CopiedMethods =
    [
        nameof(WorldGenerator.Pregenerate),
        nameof(WorldGenerator.PlaceRivers),
        nameof(WorldGenerator.PlaceStreams),
        nameof(WorldGenerator.FindStreamStartPoint),
        nameof(WorldGenerator.FindStreamEndPoint),
        nameof(WorldGenerator.FindRandomRiverEnd),
        nameof(WorldGenerator.RenderRivers),
    ];

    private static bool prepared;
    private static bool available;

    private static IEnumerable<MethodInfo> Originals => CopiedMethods.Select(name => AccessTools.Method(typeof(WorldGenerator), name));

    private const string COPY_SUFFIX = "Copy";

    private static MethodInfo CopyOf(MethodInfo original) => AccessTools.Method(typeof(VanillaRiverPlacement), original.Name + COPY_SUFFIX);

    public static bool Prepare()
    {
        if (prepared)
            return available;

        prepared = true;

        var before = Originals.Select(PatchState).ToList();
        try
        {
            var transpiler = AccessTools.Method(typeof(VanillaRiverPlacement), nameof(Rewrite));
            foreach (var original in Originals)
                Harmony.ReversePatch(original,
                    new HarmonyMethod(CopyOf(original)) { reversePatchType = HarmonyReversePatchType.Original },
                    transpiler, null);
        }
        catch (Exception e)
        {
            Logger.LogWarning($"Vanilla world previews will have no rivers, copying river placement failed: {e}");
            return false;
        }

        if (!Originals.Select(PatchState).SequenceEqual(before))
        {
            Logger.LogError("Copying vanilla river placement changed the patches on WorldGenerator; vanilla world previews will have no rivers");
            return false;
        }

        available = true;
        return true;
    }

    public static void Pregenerate(WorldGenerator generator)
    {
        if (!available)
            throw new InvalidOperationException("River placement copies are not prepared");

        using (PreviewRandom.Use(new UnityRandom()))
            PregenerateCopy(generator);
    }

    private static string PatchState(MethodInfo original)
    {
        var info = Harmony.GetPatchInfo(original);
        if (info == null)
            return "";

        return string.Join(";", info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers).Concat(info.ILManipulators)
            .Select(patch => $"{patch.owner}:{patch.PatchMethod.DeclaringType?.FullName}.{patch.PatchMethod.Name}"));
    }

    private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
    {
        var copies = Originals.ToDictionary(original => (MethodBase)original, CopyOf);

        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo method)
            {
                if (method.DeclaringType == typeof(UnityEngine.Random))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = RandomReplacement(method);
                }
                else if (copies.TryGetValue(method, out var copy))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = copy;
                }
            }

            yield return instruction;
        }
    }

    private static MethodInfo RandomReplacement(MethodInfo method)
    {
        var name = method.Name switch
        {
            "get_state" => nameof(PreviewRandom.GetState),
            "set_state" => nameof(PreviewRandom.SetState),
            _ => method.Name,
        };
        var parameters = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        return AccessTools.Method(typeof(PreviewRandom), name, parameters) is { } replacement && replacement.ReturnType == method.ReturnType
            ? replacement
            : throw new InvalidOperationException($"River placement uses UnityEngine.Random.{method.Name}, which the preview can't reproduce");
    }

    // Stubs: Harmony replaces each body with a rewritten copy of the WorldGenerator method named without the suffix

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PregenerateCopy(WorldGenerator instance) => throw Stub();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<WorldGenerator.River> PlaceRiversCopy(WorldGenerator instance) => throw Stub();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<WorldGenerator.River> PlaceStreamsCopy(WorldGenerator instance, bool isDN) => throw Stub();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FindStreamStartPointCopy(WorldGenerator instance, int iterations, float minHeight, float maxHeight,
        out Vector2 p, out float starth, bool riverPreGen) => throw Stub();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FindStreamEndPointCopy(WorldGenerator instance, int iterations, float minHeight, float maxHeight,
        Vector2 start, float minLength, float maxLength, out Vector2 end, bool riverPreGen) => throw Stub();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int FindRandomRiverEndCopy(WorldGenerator instance, List<WorldGenerator.River> rivers, List<Vector2> points,
        Vector2 p, float maxDistance, float heightLimit, float checkStep) => throw Stub();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderRiversCopy(WorldGenerator instance, List<WorldGenerator.River> rivers, WorldGenerator.RiverAdd addRule) => throw Stub();

    private static Exception Stub() => new InvalidOperationException("River placement copy was called before it was made");
}

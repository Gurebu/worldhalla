using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview.Vanilla;

/*
 * Private copies of vanilla's river placement for the preview. Vanilla draws rivers from the engine's global
 * UnityEngine.Random, which only the main thread may use; these copies draw from a generator of their own instead,
 * so they can run on a preview thread. Where vanilla seeds the engine a copy makes a Xorshift128, and it hands that
 * to the copies it calls as their last argument.
 *
 * The copies are Harmony reverse patches: the original IL is copied into the stubs below and rewritten there.
 * Nothing here may patch WorldGenerator itself, since the real methods place the rivers of every vanilla world that
 * gets played.
 */
public static class VanillaRiverPlacement
{
    internal static readonly Dictionary<MethodInfo, MethodInfo> Copies = typeof(Patches)
        .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .ToDictionary(copy => AccessTools.Method(typeof(WorldGenerator), copy.Name));

    public static void Initialize()
    {
        var transpiler = AccessTools.Method(typeof(VanillaRiverPlacement), nameof(Rewrite));
        foreach (var (original, copy) in Copies)
            Harmony.ReversePatch(
                original,
                new HarmonyMethod(copy) { reversePatchType = HarmonyReversePatchType.Original },
                transpiler,
                null);
    }

    public static void Apply(WorldGenerator worldGenerator)
    {
        Patches.Pregenerate(worldGenerator);
    }

    internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase copy)
    {
        (OpCode opcode, object operand)? random = RandomArgument(copy) is { } argument ? (OpCodes.Ldarg_S, (byte)argument) : null;
        var savedStateStoreFollows = false;

        foreach (var instruction in instructions)
        {
            if (savedStateStoreFollows)
            {
                if (!instruction.IsStloc())
                    throw Unsupported(copy, "doesn't keep the saved random state in a local");

                savedStateStoreFollows = false;
                yield return Become(instruction, OpCodes.Nop);
            }
            else if (instruction.operand is MethodInfo { DeclaringType: var type } method && type == typeof(UnityEngine.Random))
            {
                switch (method.Name)
                {
                    case "get_state": // getter for UnityEngine.Random.State
                        savedStateStoreFollows = true;
                        yield return Become(instruction, OpCodes.Nop);
                        break;
                    case "set_state": // setter for UnityEngine.Random.State
                        yield return Become(instruction, OpCodes.Pop);
                        break;
                    case nameof(UnityEngine.Random.InitState):
                        if (random != null)
                            throw Unsupported(copy, "seeds a random generator while another is in use");

                        var local = generator.DeclareLocal(typeof(Xorshift128));
                        random = (OpCodes.Ldloc, local);
                        yield return Become(instruction, OpCodes.Newobj, AccessTools.Constructor(typeof(Xorshift128), [typeof(int)]));
                        yield return new CodeInstruction(OpCodes.Stloc, local);
                        break;
                    case nameof(UnityEngine.Random.Range):
                        yield return LoadRandom(instruction);
                        yield return Become(instruction, OpCodes.Call, method.ReturnType == typeof(int) ? IntRange : FloatRange);
                        break;
                    default:
                        throw Unsupported(copy, $"uses UnityEngine.Random.{method.Name}");
                }
            }
            else if (instruction.operand is MethodInfo called && Copies.TryGetValue(called, out var calledCopy))
            {
                if (RandomArgument(calledCopy) != null)
                    yield return LoadRandom(instruction);
                yield return Become(instruction, OpCodes.Call, calledCopy);
            }
            else
            {
                yield return instruction;
            }
        }

        // A branch may land on the call itself, with its arguments already pushed
        CodeInstruction LoadRandom(CodeInstruction call)
        {
            if (random is not var (opcode, operand))
                throw Unsupported(copy, "draws a random number without seeding first");

            return new CodeInstruction(opcode, operand).MoveLabelsFrom(call);
        }
    }

    private static CodeInstruction Become(CodeInstruction instruction, OpCode opcode, object? operand = null)
    {
        instruction.opcode = opcode;
        instruction.operand = operand;
        return instruction;
    }

    private static int? RandomArgument(MethodBase method)
    {
        var index = Array.FindIndex(method.GetParameters(), parameter => parameter.ParameterType == typeof(Xorshift128));
        return index < 0 ? null : index;
    }

    private static Exception Unsupported(MethodBase copy, string what)
    {
        return new InvalidOperationException($"{copy.Name} {what}, which the preview can't reproduce");
    }

    private static readonly MethodInfo FloatRange = ((Func<float, float, Xorshift128, float>)Range).Method;
    private static readonly MethodInfo IntRange = ((Func<int, int, Xorshift128, int>)Range).Method;

    private static float Range(float min, float max, Xorshift128 random) => random.Range(min, max);

    private static int Range(int min, int max, Xorshift128 random) => random.Range(min, max);

    private static class Patches
    {
        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Pregenerate(
            WorldGenerator instance) => throw new InvalidOperationException();

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static List<WorldGenerator.River> PlaceRivers(
            WorldGenerator instance) => throw new InvalidOperationException();

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static List<WorldGenerator.River> PlaceStreams(
            WorldGenerator instance,
            bool isDN) => throw new InvalidOperationException();

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool FindStreamStartPoint(
            WorldGenerator instance,
            int iterations,
            float minHeight,
            float maxHeight,
            out Vector2 p,
            out float starth,
            bool riverPreGen,
            Xorshift128 random) => throw new InvalidOperationException();

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool FindStreamEndPoint(
            WorldGenerator instance,
            int iterations,
            float minHeight,
            float maxHeight,
            Vector2 start,
            float minLength,
            float maxLength,
            out Vector2 end,
            bool riverPreGen,
            Xorshift128 random) => throw new InvalidOperationException();

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int FindRandomRiverEnd(
            WorldGenerator instance,
            List<WorldGenerator.River> rivers,
            List<Vector2> points,
            Vector2 p,
            float maxDistance,
            float heightLimit,
            float checkStep,
            Xorshift128 random) => throw new InvalidOperationException();

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RenderRivers(
            WorldGenerator instance,
            List<WorldGenerator.River> rivers,
            WorldGenerator.RiverAdd addRule,
            Xorshift128 random) => throw new InvalidOperationException();
    }
}

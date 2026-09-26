using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NUnit.Framework;
using Worldhalla.Plugin.Patches;

namespace Worldhalla.Plugin.Tests;

/*
 * These read the game's own IL and run the transpilers over it as plain functions - no Harmony
 * patching, no game process. What they are really guarding is the anchors: every CodeMatcher
 * extension encodes an assumption about vanilla's instruction shape, and a game update that breaks
 * one otherwise surfaces as a patch failure at startup.
 *
 * They assert structure rather than an exact instruction sequence on purpose - a golden IL dump
 * would fail on every unrelated game update and teach us to regenerate it without reading it.
 */
public class TranspilerTests
{
    private static readonly Type[] SaveWorldFWLDataArguments = { typeof(DateTime), typeof(FileWriter).MakeByRefType() };

    private static ILGenerator Generator() => new DynamicMethod("test", typeof(void), Type.EmptyTypes).GetILGenerator();

    private static List<CodeInstruction> Original(Type type, string name, Type[] parameters = null, ILGenerator generator = null)
        => PatchProcessor.GetOriginalInstructions(AccessTools.Method(type, name, parameters), generator);

    private static bool IsCallTo(CodeInstruction instruction, Type declaringType, string name)
        => instruction.operand is MethodInfo method && method.DeclaringType == declaringType && method.Name == name;

    private static int IndexOfCall(List<CodeInstruction> instructions, Type declaringType, string name)
    {
        var index = instructions.FindIndex(instruction => IsCallTo(instruction, declaringType, name));
        Assert.That(index, Is.GreaterThanOrEqualTo(0), $"no call to {declaringType.Name}.{name} was inserted");

        return index;
    }

    private static int CallCount(List<CodeInstruction> instructions, Type declaringType, string name)
        => instructions.Count(instruction => IsCallTo(instruction, declaringType, name));

    private static Type WorldPatchType => typeof(WorldPatch);
    private static Type ZNetPatchType => typeof(ZNetPatch);
    private static Type CreateWorldPanelPatchType => typeof(CreateWorldPanelPatch);
    private static Type ServerStartupPatchType => typeof(ServerStartupPatch);

    [Test, RequiresValheim]
    public void WorldSaveInsertsBothHooksInOrder()
    {
        var original = Original(typeof(World), nameof(World.SaveWorldFWLData), SaveWorldFWLDataArguments);
        var patched = WorldPatch.TranspileWorldSave(original).ToList();

        Assert.That(CallCount(patched, WorldPatchType, "WriteHeader"), Is.EqualTo(1));
        Assert.That(CallCount(patched, WorldPatchType, "WriteWorldData"), Is.EqualTo(1));
        Assert.That(IndexOfCall(patched, WorldPatchType, "WriteHeader"),
            Is.LessThan(IndexOfCall(patched, WorldPatchType, "WriteWorldData")));
        Assert.That(patched, Has.Count.EqualTo(original.Count + 6), "vanilla instructions were consumed, not inserted beside");
    }

    [Test, RequiresValheim]
    public void WorldSaveWritesTheHeaderBeforeTheWorldVersion()
    {
        var original = Original(typeof(World), nameof(World.SaveWorldFWLData), SaveWorldFWLDataArguments);
        var patched = WorldPatch.TranspileWorldSave(original).ToList();

        var versionWrite = patched.FindIndex(instruction =>
            instruction.operand is MethodInfo method
            && method.DeclaringType == typeof(ZPackage)
            && method.Name == nameof(ZPackage.Write)
            && method.GetParameters()[0].ParameterType == typeof(int));

        Assert.That(IndexOfCall(patched, WorldPatchType, "WriteHeader"), Is.LessThan(versionWrite));
        Assert.That(IndexOfCall(patched, WorldPatchType, "WriteWorldData"), Is.GreaterThan(versionWrite));
    }

    [Test, RequiresValheim]
    public void WorldLoadInsertsBothHooksInOrder()
    {
        var generator = Generator();
        var original = Original(typeof(World), nameof(World.LoadWorld), generator: generator);
        var patched = WorldPatch.TranspileWorldLoad(original, generator).ToList();

        Assert.That(CallCount(patched, WorldPatchType, "ReadHeader"), Is.EqualTo(1));
        Assert.That(CallCount(patched, WorldPatchType, "ReadWorldData"), Is.EqualTo(1));
        Assert.That(IndexOfCall(patched, WorldPatchType, "ReadHeader"),
            Is.LessThan(IndexOfCall(patched, WorldPatchType, "ReadWorldData")));
        Assert.That(patched, Has.Count.EqualTo(original.Count + 14), "vanilla instructions were consumed, not inserted beside");
    }

    /*
     * Both hooks have to bail into the same place vanilla uses for a world it cannot read, or a
     * world we decline to load carries on into the vanilla path with half a package consumed.
     */
    [Test, RequiresValheim]
    public void WorldLoadBranchesToTheBadVersionConstructor()
    {
        var generator = Generator();
        var original = Original(typeof(World), nameof(World.LoadWorld), generator: generator);
        var patched = WorldPatch.TranspileWorldLoad(original, generator).ToList();

        var afterHeader = patched[IndexOfCall(patched, WorldPatchType, "ReadHeader") + 1];
        var afterWorldData = patched[IndexOfCall(patched, WorldPatchType, "ReadWorldData") + 1];

        Assert.That(afterHeader.opcode, Is.EqualTo(OpCodes.Brfalse_S));
        Assert.That(afterWorldData.opcode, Is.EqualTo(OpCodes.Brfalse_S));
        Assert.That(afterHeader.operand, Is.EqualTo(afterWorldData.operand));

        var target = patched.FindIndex(instruction => instruction.labels.Contains((Label)afterHeader.operand));
        Assert.That(target, Is.GreaterThanOrEqualTo(0), "the branch target label is attached to nothing");

        var badVersionWorld = patched.FindIndex(target, instruction =>
            instruction.opcode == OpCodes.Newobj
            && instruction.operand is ConstructorInfo info
            && info.DeclaringType == typeof(World)
            && info.GetParameters().Length == 2);

        Assert.That(badVersionWorld - target, Is.InRange(0, 3), "the branch does not land on the bad version World constructor");
    }

    [Test, RequiresValheim]
    public void WorldLoadCarriesTheProtocolInfoInALocal()
    {
        var generator = Generator();
        var original = Original(typeof(World), nameof(World.LoadWorld), generator: generator);
        var patched = WorldPatch.TranspileWorldLoad(original, generator).ToList();

        var written = patched[IndexOfCall(patched, WorldPatchType, "ReadHeader") - 1];
        var read = patched[IndexOfCall(patched, WorldPatchType, "ReadWorldData") - 1];

        Assert.That(written.opcode, Is.EqualTo(OpCodes.Ldloca));
        Assert.That(read.opcode, Is.EqualTo(OpCodes.Ldloc));
        Assert.That(written.operand, Is.SameAs(read.operand), "the two hooks are not talking through the same local");
        Assert.That(((LocalBuilder)written.operand).LocalType, Is.EqualTo(typeof(WorldPatch.ProtocolInfo)));
    }

    /*
     * The name is lifted off the SaveWithBackups into a local so the hooks take a plain string and
     * stay callable without a save file. Unlike the ldarg it replaced, a local is only valid once
     * something has stored to it - so the store has to come before every read of it.
     */
    [Test, RequiresValheim]
    public void WorldLoadCarriesTheWorldNameInAStringLocal()
    {
        var generator = Generator();
        var original = Original(typeof(World), nameof(World.LoadWorld), generator: generator);
        var patched = WorldPatch.TranspileWorldLoad(original, generator).ToList();

        var store = patched.FindIndex(instruction => instruction.opcode == OpCodes.Stloc
                                                     && instruction.operand is LocalBuilder local
                                                     && local.LocalType == typeof(string));
        Assert.That(store, Is.GreaterThanOrEqualTo(0), "the world name was not stored into a string local");

        var name = patched[store].operand;
        var reads = patched.Select((instruction, index) => (instruction, index))
            .Where(entry => entry.instruction.opcode == OpCodes.Ldloc && ReferenceEquals(entry.instruction.operand, name))
            .Select(entry => entry.index)
            .ToList();

        Assert.That(patched[store - 1].operand,
            Is.EqualTo(AccessTools.PropertyGetter(typeof(SaveWithBackups), nameof(SaveWithBackups.Name))),
            "the local is not being filled from the save file's name");
        Assert.That(reads, Has.Count.EqualTo(2), "both hooks should read the world name through the same local");
        Assert.That(reads, Is.All.GreaterThan(store), "a hook reads the world name before it is stored");
    }

    /*
     * The new world is saved a few instructions later, so the worldgen has to be on it before
     * anything else in OnNewWorldDone gets to touch it.
     */
    [Test, RequiresValheim]
    public void NewWorldDoneAssignsTheWorldgenRightAfterTheWorldIsCreated()
    {
        var original = Original(typeof(FejdStartup), nameof(FejdStartup.OnNewWorldDone));
        var patched = CreateWorldPanelPatch.TranspileNewWorldDone(original).ToList();

        Assert.That(CallCount(patched, CreateWorldPanelPatchType, "AssignSelectedWorldgen"), Is.EqualTo(1));
        Assert.That(patched, Has.Count.EqualTo(original.Count + 2));

        var hook = IndexOfCall(patched, CreateWorldPanelPatchType, "AssignSelectedWorldgen");
        Assert.That(patched[hook - 1].opcode, Is.EqualTo(OpCodes.Ldarg_0));
        Assert.That(patched[hook - 2].operand is FieldInfo { Name: nameof(FejdStartup.m_world) } && patched[hook - 2].opcode == OpCodes.Stfld,
            Is.True, "the hook does not directly follow the m_world assignment");
    }

    /*
     * GetCreateWorld saves the new world's metadata before returning it, so the worldgen has to be
     * on it by then or the first .fwl on disk is a vanilla one.
     */
    [Test, RequiresValheim]
    public void GetCreateWorldPreparesTheWorldBeforeSavingIt()
    {
        var original = Original(typeof(World), nameof(World.GetCreateWorld));
        var patched = ServerStartupPatch.TranspileGetCreateWorld(original).ToList();

        Assert.That(CallCount(patched, ServerStartupPatchType, "PrepareCreatedWorld"), Is.EqualTo(1));
        Assert.That(patched, Has.Count.EqualTo(original.Count + 2));

        var hook = IndexOfCall(patched, ServerStartupPatchType, "PrepareCreatedWorld");
        Assert.That(patched[hook - 1].IsLdloc(), Is.True);
        Assert.That(patched[hook - 2].IsStloc(), Is.True, "the hook does not directly follow the new World store");
        Assert.That(hook, Is.LessThan(IndexOfCall(patched, typeof(World), nameof(World.SaveWorldFWLData))));
    }

    [Test, RequiresValheim]
    public void SendPeerInfoInsertsTheWorldDataHook()
    {
        var original = Original(typeof(ZNet), nameof(ZNet.SendPeerInfo));
        var patched = ZNetPatch.TranspileSendPeerInfo(original).ToList();

        Assert.That(CallCount(patched, ZNetPatchType, "WriteWorldData"), Is.EqualTo(1));
        Assert.That(patched, Has.Count.EqualTo(original.Count + 2));
    }

    [Test, RequiresValheim]
    public void RpcPeerInfoInsertsTheWorldDataHook()
    {
        var generator = Generator();
        var original = Original(typeof(ZNet), nameof(ZNet.RPC_PeerInfo), generator: generator);
        var patched = ZNetPatch.TranspileRpcPeerInfo(original, generator).ToList();

        Assert.That(CallCount(patched, ZNetPatchType, "ReadWorldData"), Is.EqualTo(1));
        Assert.That(patched, Has.Count.EqualTo(original.Count + 5));
    }
}

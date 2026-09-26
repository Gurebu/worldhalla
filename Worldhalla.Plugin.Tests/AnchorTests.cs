using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NUnit.Framework;
using Worldhalla.Plugin.Extensions;

namespace Worldhalla.Plugin.Tests;

/*
 * One test per anchor the patches rely on. These are the first things a game update breaks, and a
 * named failure here is the difference between "Riverheim fails to patch" in someone's log and
 * knowing which instruction pattern moved.
 */
public class AnchorTests
{
    private static readonly Type[] SaveWorldFWLDataArguments = { typeof(DateTime), typeof(FileWriter).MakeByRefType() };

    private static CodeMatcher Matcher(Type type, string name, Type[] parameters = null)
    {
        // CreateLabel needs one, so the matcher has to be built the way the transpilers build it
        var generator = new DynamicMethod("test", typeof(void), Type.EmptyTypes).GetILGenerator();

        return new CodeMatcher(PatchProcessor.GetOriginalInstructions(AccessTools.Method(type, name, parameters), generator), generator);
    }

    private static void AssertPreviousIs(CodeMatcher matcher, Predicate<CodeInstruction> expected, string what)
        => Assert.That(expected(matcher.InstructionAt(-1)), Is.True, $"matcher did not stop after {what}");

    private static bool IsZPackageConstructor(CodeInstruction instruction)
        => instruction.opcode == OpCodes.Newobj && ((ConstructorInfo)instruction.operand).DeclaringType == typeof(ZPackage);

    [TestCase(nameof(World.LoadWorld))]
    [RequiresValheim]
    public void ZPackageConstructorIsFoundInWorldLoad(string method)
    {
        var matcher = Matcher(typeof(World), method);

        Assert.DoesNotThrow(() => matcher.AdvanceToAfterZPackageConstructor(out _));
        AssertPreviousIs(matcher, instruction => instruction.IsStloc(), "the ZPackage local store");
    }

    [Test, RequiresValheim]
    public void ZPackageConstructorIsFoundInWorldSave()
    {
        var matcher = Matcher(typeof(World), nameof(World.SaveWorldFWLData), SaveWorldFWLDataArguments);

        Assert.DoesNotThrow(() => matcher.AdvanceToAfterZPackageConstructor(out _));
        AssertPreviousIs(matcher, instruction => instruction.IsStloc(), "the ZPackage local store");
    }

    [Test, RequiresValheim]
    public void ZPackageConstructorIsFoundInSendPeerInfo()
    {
        var matcher = Matcher(typeof(ZNet), nameof(ZNet.SendPeerInfo));

        Assert.DoesNotThrow(() => matcher.AdvanceToAfterZPackageConstructor(out _));
        AssertPreviousIs(matcher, instruction => instruction.IsStloc(), "the ZPackage local store");
    }

    /*
     * This helper does not check IsInvalid, so the assertion below is the only thing standing
     * between a moved anchor and settings written at an arbitrary offset.
     */
    [Test, RequiresValheim]
    public void WorldVersionWriteIsFoundInWorldSave()
    {
        var matcher = Matcher(typeof(World), nameof(World.SaveWorldFWLData), SaveWorldFWLDataArguments);

        matcher.AdvanceToAfterZPackageConstructor(out _);
        matcher.AdvanceToAfterWorldVersionWrite();

        AssertPreviousIs(matcher, instruction =>
            instruction.operand is MethodInfo method
            && method.DeclaringType == typeof(ZPackage)
            && method.Name == nameof(ZPackage.Write)
            && method.GetParameters()[0].ParameterType == typeof(int), "the world version write");
    }

    [Test, RequiresValheim]
    public void WorldGenVersionWriteIsFoundInSendPeerInfo()
    {
        var matcher = Matcher(typeof(ZNet), nameof(ZNet.SendPeerInfo));

        Assert.DoesNotThrow(() => matcher.AdvanceToAfterWorldGenVersionWrite());
    }

    [Test, RequiresValheim]
    public void WorldGenVersionReadIsFoundInRpcPeerInfo()
    {
        var matcher = Matcher(typeof(ZNet), nameof(ZNet.RPC_PeerInfo));

        Assert.DoesNotThrow(() => matcher.AdvanceToAfterWorldGenVersionRead());
    }

    [Test, RequiresValheim]
    public void BadVersionWorldConstructorIsFoundInWorldLoad()
    {
        var matcher = Matcher(typeof(World), nameof(World.LoadWorld));

        Assert.DoesNotThrow(() => matcher.MatchBadVersionWorldConstructor(out _));
    }

    [Test, RequiresValheim]
    public void NewWorldAssignmentIsFoundInNewWorldDone()
    {
        var matcher = Matcher(typeof(FejdStartup), nameof(FejdStartup.OnNewWorldDone));

        Assert.DoesNotThrow(() => matcher.AdvanceToAfterNewWorldAssignment());
        AssertPreviousIs(matcher, instruction =>
            instruction.opcode == OpCodes.Stfld
            && instruction.operand is FieldInfo field
            && field.DeclaringType == typeof(FejdStartup)
            && field.Name == nameof(FejdStartup.m_world), "the m_world store");
    }

    /*
     * Also unguarded: a failed match here reaches MatchStoreWithLoad, which reports whatever
     * instruction it happened to land on rather than the anchor that moved.
     */
    [Test, RequiresValheim]
    public void DefaultWorldConstructorIsFoundInWorldLoad()
    {
        var matcher = Matcher(typeof(World), nameof(World.LoadWorld));

        matcher.AdvanceToAfterZPackageConstructor(out _);
        matcher.AdvanceToAfterDefaultWorldConstructor(out _);

        AssertPreviousIs(matcher, instruction => instruction.IsStloc(), "the World local store");
    }
}

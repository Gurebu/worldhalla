using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Worldhalla.Plugin.Core.Extensions;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class CodeMatcherExtension
{
    private static CodeInstruction MatchStoreWithLoad(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Stloc_0)
            return new CodeInstruction(OpCodes.Ldloc_0);
        if (instruction.opcode == OpCodes.Stloc_1)
            return new CodeInstruction(OpCodes.Ldloc_1);
        if (instruction.opcode == OpCodes.Stloc_2)
            return new CodeInstruction(OpCodes.Ldloc_2);
        if (instruction.opcode == OpCodes.Stloc_3)
            return new CodeInstruction(OpCodes.Ldloc_3);
        if (instruction.opcode == OpCodes.Stloc_S)
            return new CodeInstruction(OpCodes.Ldloc_S, instruction.operand);
        if (instruction.opcode == OpCodes.Stloc)
            return new CodeInstruction(OpCodes.Ldloc, instruction.operand);
        
        throw new InvalidOperationException($"Not a store instruction: {instruction.opcode}");
    }

    public static CodeMatcher AdvanceToAfterZPackageConstructor(this CodeMatcher matcher, out CodeInstruction ld_ZPackage)
    {
        matcher.MatchForward(true,
            new CodeMatch(instruction =>
            {
                if (instruction.opcode != OpCodes.Newobj)
                    return false;

                return ((ConstructorInfo)instruction.operand).DeclaringType == typeof(ZPackage);
            }),
            new CodeMatch(instruction => instruction.IsStloc()));

        if (matcher.IsInvalid)
            throw new InvalidOperationException("Error patching, unable to find zPackage instancing");

        ld_ZPackage = MatchStoreWithLoad(matcher.Instruction);

        return matcher.Advance(1);
    }

    public static CodeMatcher AdvanceToAfterWorldVersionWrite(this CodeMatcher matcher)
    {
        matcher.MatchForward(true,
            new CodeMatch(instruction =>
            {
                if (instruction.opcode != OpCodes.Callvirt)
                    return false;

                return ((MethodInfo)instruction.operand).DeclaringType == typeof(ZPackage) &&
                       ((MethodInfo)instruction.operand).Name == nameof(ZPackage.Write) &&
                       ((MethodInfo)instruction.operand).GetParameters()[0].ParameterType == typeof(int);
            }));

        if (matcher.IsInvalid)
            throw new InvalidOperationException("Error patching, unable to find world version write");

        return matcher.Advance(1);
    }

    private static bool IsZPackageCall(CodeInstruction instruction, string name, Type argument = null)
    {
        if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
            return false;

        if (instruction.operand is not MethodInfo method)
            return false;

        if (method.DeclaringType != typeof(ZPackage) || method.Name != name)
            return false;

        var parameters = method.GetParameters();

        return argument == null
            ? parameters.Length == 0
            : parameters.Length == 1 && parameters[0].ParameterType == argument;
    }

    private static bool IsWorldGenVersionAccess(CodeInstruction instruction, OpCode opcode)
    {
        return instruction.opcode == opcode
               && instruction.operand is FieldInfo field
               && field.DeclaringType == typeof(World)
               && field.Name == nameof(World.m_worldGenVersion);
    }

    public static CodeMatcher AdvanceToAfterWorldGenVersionRead(this CodeMatcher matcher)
    {
        matcher.MatchForward(true,
            new CodeMatch(instruction => IsZPackageCall(instruction, nameof(ZPackage.ReadInt))),
            new CodeMatch(instruction => IsWorldGenVersionAccess(instruction, OpCodes.Stfld)));

        if (matcher.IsInvalid)
            throw new InvalidOperationException("Error patching, unable to find world gen version read");

        return matcher.Advance(1);
    }

    public static CodeMatcher MatchBadVersionWorldConstructor(this CodeMatcher matcher, out Label label)
    {
        matcher.MatchForward(false,
            new CodeMatch(instruction => instruction.IsLdarg()),
            new CodeMatch(instruction => instruction.opcode == OpCodes.Ldc_I4_1
                                         || instruction.Is(OpCodes.Ldc_I4, 1)
                                         || instruction.Is(OpCodes.Ldc_I4_S, 1)),
            new CodeMatch(instruction =>
            {
                if (instruction.opcode != OpCodes.Newobj)
                    return false;

                var info = (ConstructorInfo)instruction.operand;
                if (info.DeclaringType != typeof(World))
                    return false;

                var parameters = info.GetParameters();
                if (parameters.Length != 2)
                    return false;

                return parameters[0].ParameterType == typeof(SaveWithBackups) && parameters[1].ParameterType == typeof(World.SaveDataError);
            }));

        if (matcher.IsInvalid)
            throw new InvalidOperationException("Error patching, unable to find World instancing");

        matcher.CreateLabel(out label);

        return matcher;
    }

    private static bool IsNewWorldConstructor(CodeInstruction instruction)
    {
        if (instruction.opcode != OpCodes.Newobj)
            return false;

        var info = (ConstructorInfo)instruction.operand;
        if (info.DeclaringType != typeof(World))
            return false;

        var parameters = info.GetParameters();

        return parameters.Length == 2 && parameters.All(parameter => parameter.ParameterType == typeof(string));
    }

    public static CodeMatcher AdvanceToAfterNewWorldAssignment(this CodeMatcher matcher)
    {
        matcher.MatchForward(true,
            new CodeMatch(IsNewWorldConstructor),
            new CodeMatch(instruction => instruction.opcode == OpCodes.Stfld
                                         && instruction.operand is FieldInfo field
                                         && field.DeclaringType == typeof(FejdStartup)
                                         && field.Name == nameof(FejdStartup.m_world)));

        if (matcher.IsInvalid)
            throw new InvalidOperationException("Error patching, unable to find new World assignment");

        return matcher.Advance(1);
    }

    public static CodeMatcher AdvanceToAfterNewWorldStore(this CodeMatcher matcher, out CodeInstruction ld_World)
    {
        matcher.MatchForward(true,
            new CodeMatch(IsNewWorldConstructor),
            new CodeMatch(instruction => instruction.IsStloc()));

        if (matcher.IsInvalid)
            throw new InvalidOperationException("Error patching, unable to find new World store");

        ld_World = MatchStoreWithLoad(matcher.Instruction);

        return matcher.Advance(1);
    }

    public static CodeMatcher AdvanceToAfterDefaultWorldConstructor(this CodeMatcher matcher, out CodeInstruction ld_World)
    {
        matcher.MatchForward(true,
            new CodeMatch(instruction =>
            {
                if (instruction.opcode != OpCodes.Newobj)
                    return false;

                var info = (ConstructorInfo)instruction.operand;
                if (info.DeclaringType != typeof(World))
                    return false;
                
                return info.GetParameters().Length == 0;
            }),
            new CodeMatch(instruction => instruction.IsStloc()));

        if (matcher.IsInvalid)
            throw new InvalidOperationException("Error patching, unable to find default World instancing");

        ld_World = MatchStoreWithLoad(matcher.Instruction);

        return matcher.Advance(1);
    }
}

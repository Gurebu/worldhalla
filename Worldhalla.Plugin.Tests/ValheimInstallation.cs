using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Worldhalla.Plugin.Tests;

/*
 * Tests carrying this are ignored rather than failed when the assembly they patch isn't there, so a
 * run on a machine without the game reports skips. Keep game types out of fixture fields and method
 * signatures - a test that is never started can still fail to load its own class.
 */
public sealed class RequiresValheimAttribute : NUnitAttribute, IApplyToTest
{
    public void ApplyToTest(Test test)
    {
        if (test.RunState == RunState.NotRunnable || ValheimInstallation.Available)
            return;

        test.RunState = RunState.Ignored;
        test.Properties.Set(PropertyNames.SkipReason, "no Valheim installation found");
    }
}

internal static class ValheimInstallation
{
    private static bool? available;

    public static bool Available => available ??= CanLoad("assembly_valheim") && CanLoad("UnityEngine.CoreModule");

    private static bool CanLoad(string assembly)
    {
        try
        {
            Assembly.Load(assembly);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Worldhalla.Plugin.Patches;

[HarmonyPatch(typeof(ZRpc))]
public static class ZRpcPatch
{
    // Vanilla's own ReceivePing, without other mods' patches on it: handed a ping request, it sends the reply
    [HarmonyReversePatch, HarmonyPatch(nameof(ZRpc.ReceivePing))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AnswerPing(ZRpc rpc, ZPackage request)
    {
        throw new NotImplementedException("Replaced by Harmony");
    }

    internal static ZPackage PingRequest()
    {
        var request = new ZPackage();
        request.Write(true);
        request.SetPos(0);
        return request;
    }
}

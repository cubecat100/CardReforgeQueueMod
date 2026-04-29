#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using System;

namespace CardReforgeQueueMod;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    private static Harmony? _harmony;

    public static void Initialize()
    {
        Log.Warn("[CardReforgeQueueMod] Initialize");

        try
        {
            _harmony ??= new Harmony("cardreforgequeuemod.mod");
            _harmony.PatchAll();
            Log.Warn("[CardReforgeQueueMod] Harmony patches applied");
        }
        catch (Exception ex)
        {
            Log.Error($"[CardReforgeQueueMod] Harmony patch initialization failed: {ex}");
            throw;
        }
    }
}

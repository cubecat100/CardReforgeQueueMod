#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.TopBar;

namespace CardReforgeQueueMod.Patches;

[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
public static class TopBarPatch
{
    public static void Postfix(NTopBar __instance)
    {
        TopBarReforgeQueueUi.EnsureInstalled(__instance);
    }
}

[HarmonyPatch(typeof(NTopBarPauseButton), "OnRelease")]
public static class TopBarPauseButtonPatch
{
    public static void Prefix(NTopBarPauseButton __instance)
    {
        TopBarReforgeQueueUi.ClosePopupFrom(__instance);
    }
}

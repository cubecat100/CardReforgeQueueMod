#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using System.Threading.Tasks;

namespace CardReforgeQueueMod.Patches;

[HarmonyPatch(typeof(NRestSiteRoom), "UpdateRestSiteOptions")]
public static class RestSitePatch
{
    public static void Postfix(NRestSiteRoom __instance)
    {
        RestSiteReforgeQueueUi.EnsureInstalled(__instance);
    }
}

[HarmonyPatch(typeof(SmithRestSiteOption), "OnSelect")]
public static class SmithRestSiteOptionPatch
{
    public static bool Prefix(SmithRestSiteOption __instance, ref Task<bool> __result)
    {
        if (RestSiteReforgeQueueUi.TryCreateAutoUpgradeTask(__instance, out var task) == false)
        {
            return true;
        }

        __result = task;
        return false;
    }
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Upgrade), new[] { typeof(CardModel), typeof(CardPreviewStyle) })]
public static class CardUpgradeQueuePatch
{
    public static void Postfix(CardModel card)
    {
        var queuePath = TopBarReforgeQueueUi.GetQueuePath(card.Owner);
        ReforgeQueueStorage.RemoveFirst(queuePath, ReforgeQueueCardRow.GetCardKey(card));
    }
}

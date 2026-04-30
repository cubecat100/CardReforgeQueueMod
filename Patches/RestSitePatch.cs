#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using System.Collections.Generic;

namespace CardReforgeQueueMod.Patches;

[HarmonyPatch(typeof(NRestSiteRoom), "UpdateRestSiteOptions")]
public static class RestSitePatch
{
    public static void Postfix(NRestSiteRoom __instance)
    {
        RestSiteReforgeQueueUi.EnsureInstalled(__instance);
    }
}

[HarmonyPatch(typeof(NDeckUpgradeSelectScreen), "ShowScreen")]
public static class DeckUpgradeSelectScreenPatch
{
    public static void Postfix(NDeckUpgradeSelectScreen __result, IReadOnlyList<CardModel> cards)
    {
        RestSiteReforgeQueueUi.TryInstallAutoSelector(__result, cards);
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

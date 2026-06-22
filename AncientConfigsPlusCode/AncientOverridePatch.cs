using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace AncientConfigsPlus.AncientConfigsPlusCode;

[HarmonyPatch(typeof (ActModel), "GenerateRooms")]
public class AncientOverridePatch
{
    private static readonly List<AncientEventModel> _rolledAncients = [];
    
    [HarmonyPostfix]
    private static void AddToModelPool(
        ActModel __instance,
        Rng rng)
    {
        if (__instance.ActNumber() <= 3)
        {
            var ancient = __instance._rooms.Ancient = AncientConfigsPlusConfig.GetWeightedAncient(__instance.ActNumber(), rng, _rolledAncients);
            if (AncientConfigsPlusConfig.IsMultiact(ancient))
                _rolledAncients.Add(ancient);
            if(__instance.ActNumber() == 3) _rolledAncients.Clear();
        }
    }
}



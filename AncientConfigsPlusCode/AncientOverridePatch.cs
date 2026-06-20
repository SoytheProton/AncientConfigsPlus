using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace AncientConfigsPlus.AncientConfigsPlusCode;

[HarmonyPatch(typeof (ActModel), "GenerateRooms")]
public class AncientOverridePatch
{
    [HarmonyPostfix]
    private static void AddToModelPool(
        ActModel __instance, 
        Rng rng)
    { 
        if(__instance.ActNumber() <= 3)
            __instance._rooms.Ancient = AncientConfigsPlusConfig.GetWeightedAncient(__instance.ActNumber(), rng);
    }
}



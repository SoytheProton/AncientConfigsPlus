using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace AncientConfigsPlus.AncientConfigsPlusCode;

[HarmonyPatch(typeof (ActModel), "GenerateRooms")]
public class AncientOverridePatch
{
    private static readonly List<AncientEventModel> RolledAncients = [];
    
    [HarmonyPostfix]
    private static void AddToModelPool(
        ActModel __instance,
        Rng rng)
    {
        if (__instance.ActNumber() > 3 || ExceptionAncientsExtension.CompleteExceptionAncients.Contains(__instance.Ancient.Id)) 
            return;

        var ancient = __instance._rooms.Ancient = AncientConfigsPlusConfig.AncientModelLogic(__instance, rng, RolledAncients, __instance.Ancient);
        if (AncientConfigsPlusConfig.IsMultiact(ancient))
            RolledAncients.Add(ancient);
        
        if(__instance.ActNumber() >= 3) RolledAncients.Clear();
    }
}



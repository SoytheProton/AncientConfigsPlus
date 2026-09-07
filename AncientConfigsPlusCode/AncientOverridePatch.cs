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

        AncientEventModel ancient;
        if (ExceptionAncientsExtension.ActSpecificAncients.Values.Any(v => v.Contains(__instance.Id)))
        {
            MainFile.Logger.Info("Exception rolled.");
            ancient = __instance._rooms.Ancient = AncientConfigsPlusConfig.GetWeightedAncient(__instance, rng, RolledAncients, true);
        }
        else 
            ancient = __instance._rooms.Ancient = AncientConfigsPlusConfig.GetWeightedAncient(__instance, rng, RolledAncients, false);
        
        if (AncientConfigsPlusConfig.IsMultiact(ancient))
            RolledAncients.Add(ancient);
        if(__instance.ActNumber() == 3) RolledAncients.Clear();
    }
}



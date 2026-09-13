using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace AncientConfigsPlus.AncientConfigsPlusCode;

[HarmonyPatch(typeof (ActModel), "GenerateRooms")]
public class AncientOverridePatch
{
    private static readonly List<ModelId> RolledAncients = [];
    
    [HarmonyPostfix]
    private static void AddToModelPool(
        ActModel __instance,
        Rng rng)
    {
        if (__instance.ActNumber() > 3 || ExceptionAncientsExtension.CompleteExceptionAncients.Contains(__instance.Ancient.Id)) 
            return;
        
        var runState = Traverse.Create(RunManager.Instance)
            .Property("State")
            .GetValue<RunState>();
        // var orderedPlayers = runState.Players.OrderBy(runState.GetPlayerSlotIndex).ToList();

        var ancient = __instance._rooms.Ancient = AncientConfigsPlusConfig.AncientModelLogic(__instance, rng, RolledAncients, __instance.Ancient);
        if (AncientConfigsPlusConfig.IsMultiact(ancient))
            RolledAncients.Add(ancient.Id);
        
        if(__instance.ActNumber() >= 3) RolledAncients.Clear();
    }
    
    /*private static async Task<int> GetEffectiveAncientCountAsync(IReadOnlyList<Player> orderedPlayers)
    {
        ChooseTheAncientConfig.RefreshFromModConfig();

        if (RunManager.Instance.NetService.Type == NetGameType.Singleplayer)
        {
            return ChooseTheAncientConfig.AncientCount;
        }

        var hostPlayer = GetHostPlayer(orderedPlayers);
        var choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(hostPlayer);

        if (LocalContext.IsMe(hostPlayer))
        {
            int hostAncientCount = ChooseTheAncientConfig.AncientCount;

            RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
                hostPlayer,
                choiceId,
                PlayerChoiceResult.FromIndex(hostAncientCount));

            return hostAncientCount;
        }

        int syncedCount = (await RunManager.Instance.PlayerChoiceSynchronizer
                .WaitForRemoteChoice(hostPlayer, choiceId))
            .AsIndex();

        syncedCount = Math.Clamp(syncedCount, 2, 8);

        return syncedCount;
    }
    
    private static Player GetHostPlayer(IReadOnlyList<Player> orderedPlayers)
    {
        switch (RunManager.Instance.NetService.Type)
        {
            case NetGameType.Singleplayer:
            case NetGameType.Replay:
            case NetGameType.Host:
                return LocalContext.GetMe(orderedPlayers) ?? orderedPlayers[0];

            case NetGameType.Client:
                if (RunManager.Instance.NetService is INetClientGameService clientService &&
                    clientService.NetClient != null)
                {
                    var hostNetId = clientService.NetClient.HostNetId;
                    var hostPlayer = orderedPlayers.FirstOrDefault(p => p.NetId == hostNetId);
                    if (hostPlayer != null)
                        return hostPlayer;
                }

                break;
        }

        return orderedPlayers[0];
    }*/
}



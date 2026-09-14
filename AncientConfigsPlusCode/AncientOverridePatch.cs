using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace AncientConfigsPlus.AncientConfigsPlusCode;

[HarmonyPatch(typeof (ActModel), "GenerateRooms")]
public class AncientOverridePatch
{
    private static readonly List<ModelId> RolledAncients = [];

    private static List<AncientEventModel>? _canonicalAncientPool;
    
    [HarmonyPostfix]
    private static void AddToModelPool(
        ActModel __instance,
        Rng rng)
    {
        if (__instance.ActNumber() > 3 || ExceptionAncientsExtension.CompleteExceptionAncients.Contains(__instance.Ancient.Id)) 
            return;
        
        var ancient = AncientConfigsPlusConfig.AncientModelLogic(__instance, rng, RolledAncients, __instance.Ancient);
        
        if (AncientConfigsPlusConfig.IsMultiact(ancient))
            RolledAncients.Add(ancient.Id);
        if(__instance.ActNumber() >= 3) 
            RolledAncients.Clear();
        
        // TaskHelper.RunSafely(AncientCoordination(ancient, __instance));
    }

    /*private static List<AncientEventModel> GetCanonicalPool()
    {
        _canonicalAncientPool ??= ModelDb.AllAncients
            .DistinctBy(ancient => ancient.Id)
            .OrderBy(ancient => ancient.Id.Entry, StringComparer.Ordinal)
            .ToList();

        return _canonicalAncientPool;
    }

    private static async Task AncientCoordination(AncientEventModel ancient, ActModel act)
    {
        var index = GetCanonicalPool().FirstIndex(a => a.Id == ancient.Id);
        
        var runState = Traverse.Create(RunManager.Instance)
            .Property("State")
            .GetValue<RunState>();
        var orderedPlayers = runState.Players.OrderBy(runState.GetPlayerSlotIndex).ToList();

        var syncedIndex = await SyncAncientChoice(orderedPlayers, index);
        
        var synced = act._rooms.Ancient = _canonicalAncientPool?[syncedIndex] ?? ancient;
                
        if (AncientConfigsPlusConfig.IsMultiact(synced))
            RolledAncients.Add(synced.Id);
        
        if(act.ActNumber() >= 3) RolledAncients.Clear();
    }
    
    private static async Task<int> SyncAncientChoice(IReadOnlyList<Player> orderedPlayers, int index)
    {
        if (RunManager.Instance.NetService.Type == NetGameType.Singleplayer)
        {
            return index;
        }

        var hostPlayer = GetHostPlayer(orderedPlayers);
        var choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(hostPlayer);

        if (LocalContext.IsMe(hostPlayer))
        {
            RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
                hostPlayer,
                choiceId,
                PlayerChoiceResult.FromIndex(index));

            return index;
        }

        var syncedCount = (await RunManager.Instance.PlayerChoiceSynchronizer
                .WaitForRemoteChoice(hostPlayer, choiceId))
            .AsIndex();

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



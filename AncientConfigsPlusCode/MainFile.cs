using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace AncientConfigsPlus.AncientConfigsPlusCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    private const string ModId = "AncientConfigsPlus";

    public static Logger Logger { get; } =
        new(ModId, LogType.Generic);

    public static void Initialize()
    {
        Harmony harmony = new(ModId);
        ModConfigRegistry.Register(ModId, new AncientConfigsPlusConfig());
        harmony.PatchAll();
    }
}
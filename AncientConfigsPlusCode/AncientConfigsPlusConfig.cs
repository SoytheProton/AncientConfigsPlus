using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Config;
using BaseLib.Config.UI;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Random;

namespace AncientConfigsPlus.AncientConfigsPlusCode;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
[ConfigHoverTipsByDefault]
public class AncientConfigsPlusConfig : SimpleModConfig
{
    public static string EnabledAct1 { get; set; } = "";
    public static string EnabledAct2 { get; set; } = "";
    public static string EnabledAct3 { get; set; } = "";

    private static readonly Dictionary<int, PropertyInfo> SlotProps = new()
    {
        { 1, typeof(AncientConfigsPlusConfig).GetProperty(nameof(EnabledAct1))! },
        { 2, typeof(AncientConfigsPlusConfig).GetProperty(nameof(EnabledAct2))! },
        { 3, typeof(AncientConfigsPlusConfig).GetProperty(nameof(EnabledAct3))! },
    };
    
    private Dictionary<string, decimal> GetDefaultWeights(int slot)
    {
        var ancients = GetAncientsForSlot(slot);
        var count = ancients.Count;
        if (count == 0) return new Dictionary<string, decimal>();

        var equalWeight = 100m / count;
        return ancients.ToDictionary(a => a.GetType().Name, _ => equalWeight);
    }

    private static Dictionary<string, decimal> ParseWeights(int slot)
    {
        var raw = (string)SlotProps[slot].GetValue(null)!;
        var result = new Dictionary<string, decimal>();
        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':');
            if (parts.Length == 2 && decimal.TryParse(parts[1], out var weight))
                result[parts[0]] = Math.Clamp(weight, 0, 100);
            else if (parts.Length == 1)
                result[parts[0]] = 1;
        }
        return result;
    }

    private static Dictionary<string, decimal> ManFuckTheseWeights(Dictionary<string, decimal> weights)
    {
        var total = weights.Values.Sum();

        var normalized = weights.ToDictionary(
            kv => kv.Key,
            kv => Math.Round(kv.Value / total * 100m, 1)
        );
        
        var drift = 100m - normalized.Values.Sum();
        
        if (Math.Abs(drift) > 0.001m)
        {
            var largest = normalized.MaxBy(kv => kv.Value).Key;
            normalized[largest] += drift;
        }
        return normalized;
    }

    private static void SaveWeights(int slot, Dictionary<string, decimal> weights)
    {
        var entries = weights.Select(kv => $"{kv.Key}:{kv.Value}");
        SlotProps[slot].SetValue(null, string.Join(",", entries));
    }

    private static List<AncientEventModel> GetAncientsForSlot(int slot)
    {
        var retVal  = new List<AncientEventModel>();
        ActModel act;
        switch (slot)
        {
            case 2:
                act = ModelDb.Act<Hive>();
                retVal.Add(ModelDb.AncientEvent<Tezcatara>());
                retVal.Add(ModelDb.AncientEvent<Orobas>());
                retVal.Add(ModelDb.AncientEvent<Pael>());
                retVal.Add(ModelDb.AncientEvent<Darv>());
                break;
            case 3:
                act = ModelDb.Act<Glory>();
                retVal.Add(ModelDb.AncientEvent<Vakuu>());
                retVal.Add(ModelDb.AncientEvent<Nonupeipe>());
                retVal.Add(ModelDb.AncientEvent<Tanx>());
                retVal.Add(ModelDb.AncientEvent<Darv>());
                break;
            default:
                act = ModelDb.Act<Overgrowth>();
                retVal.Add(ModelDb.AncientEvent<Neow>());
                break;
        }

        retVal.AddRange(ModelDb.AllAncients.Where(a => a is CustomAncientModel && ((CustomAncientModel)a).IsValidForAct(act)).ToList());
        return retVal;
    }

    private static bool IsMultiact(AncientEventModel ancient) => (GetAncientsForSlot(1).Contains(ancient) ? 1 : 0) + (GetAncientsForSlot(2).Contains(ancient) ? 1 : 0) + (GetAncientsForSlot(3).Contains(ancient) ? 1 : 0) >= 2;

    public static AncientEventModel GetWeightedAncient(int slot, Rng rng)
    {
        var all = GetAncientsForSlot(slot);
        var weights = ParseWeights(slot);

        var weighted = all
            .Select(a => (act: a, weight: weights.GetValueOrDefault(a.GetType().Name, 0)))
            .Where(x => x.weight > 0)
            .ToList();

        if (weighted.Count == 0)
            return all[rng.NextInt(all.Count)];

        var totalWeight = weighted.Sum(x => x.weight);
        var roll = (decimal) rng.NextFloat((float)totalWeight);
        var cumulative = 0m;
        foreach (var (act, weight) in weighted)
        {
            cumulative += weight;
            if (roll < cumulative)
                return act;
        }
        return weighted.Last().act;
    }

    private static string GetActDisplayName(AncientEventModel ancient, int slot)
    {
        var title = ancient.Title.GetFormattedText();
        if (IsMultiact(ancient) && GetAncientsForSlot(slot).Contains(ancient))
            title += " [" + new LocString("settings_ui", "ANCIENTCONFIGSPLUS-ACT"+slot+"_HEADER.title").GetFormattedText() +"]";
        if (ancient is CustomAncientModel)
        {
            var actAssembly = ancient.GetType().Assembly;
            var mod = ModManager.GetLoadedMods()
                .FirstOrDefault(m => m.assembly == actAssembly);
            if (mod?.manifest?.name != null)
                return $"{title} ({mod.manifest.name})";
        }
        return title;
    }

    public override void SetupConfigUI(Control optionContainer)
    {
        var tabScene = ResourceLoader.Load<PackedScene>("uid://cfcqxx8wkmljw");
        var tickboxScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/tickbox.tscn");

        // Tab bar
        var tabBar = new HBoxContainer();
        tabBar.AddThemeConstantOverride("separation", 8);
        tabBar.CustomMinimumSize = new Vector2(0f, 90f);
        tabBar.Alignment = BoxContainer.AlignmentMode.Center;
        optionContainer.AddChild(tabBar);

        var basicTab = tabScene.Instantiate<NSettingsTab>();
        tabBar.AddChild(basicTab);
        basicTab.SetLabel(new LocString("settings_ui", "ANCIENTCONFIGSPLUS-TAB_BASIC.title").GetFormattedText());

        var advancedTab = tabScene.Instantiate<NSettingsTab>();
        tabBar.AddChild(advancedTab);
        advancedTab.SetLabel(new LocString("settings_ui", "ANCIENTCONFIGSPLUS-TAB_ADVANCED.title").GetFormattedText());

        var basicContent = new VBoxContainer();
        var advancedContent = new VBoxContainer();
        advancedContent.Visible = false;
        optionContainer.AddChild(basicContent);
        optionContainer.AddChild(advancedContent);

        basicTab.Select();

        // Per-slot refresh actions
        var refreshBasicActions = new List<Action>();
        var refreshAdvancedActions = new List<Action>();

        for (int slot = 1; slot <= 3; slot++)
        {
            var currentSlot = slot;
            var acts = GetAncientsForSlot(slot);
            if (acts.Count == 0) continue;

            var weights = ParseWeights(slot);
            var actNames = acts.Select(a => a.GetType().Name).ToHashSet();

            foreach (var key in weights.Keys.Where(k => !actNames.Contains(k)).ToList())
                weights.Remove(key);

            if (!weights.Values.Any(w => w > 0))
                weights = GetDefaultWeights(slot);
            
            SaveWeights(slot, ManFuckTheseWeights(weights));

            // ── Basic tab ──
            basicContent.AddChild(CreateSectionHeader($"Act{slot}Header", slot == 1));

            var basicControls = new List<(string actName, NTickbox tickbox)>();
            bool suppressBasic = false;

            foreach (var act in acts)
            {
                var actName = act.GetType().Name;
                var displayName = GetActDisplayName(act, slot);

                var label = CreateRawLabelControl(displayName, 28);
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

                var tickbox = new NTickbox();
                tickbox.CustomMinimumSize = new Vector2(64f, 64f);
                tickbox.MouseFilter = Control.MouseFilterEnum.Stop;
                tickbox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;

                var tickboxVisuals = tickboxScene.Instantiate<Control>();
                tickboxVisuals.Name = "TickboxVisuals";
                tickboxVisuals.UniqueNameInOwner = true;
                tickbox.AddChild(tickboxVisuals);
                tickboxVisuals.Owner = tickbox;

                basicControls.Add((actName, tickbox));

                var hbox = new HBoxContainer();
                hbox.AddThemeConstantOverride("separation", 10);
                hbox.CustomMinimumSize = new Vector2(0f, 64f);
                hbox.AddChild(tickbox);
                hbox.AddChild(label);
                basicContent.AddChild(hbox);
            }

            // Basic refresh
            void RefreshBasic()
            {
                suppressBasic = true;
                var current = ParseWeights(currentSlot);
                foreach (var (actName, tickbox) in basicControls)
                    tickbox.IsTicked = current.GetValueOrDefault(actName, 0) > 0;
                suppressBasic = false;
            }
            refreshBasicActions.Add(RefreshBasic);
            Callable.From(RefreshBasic).CallDeferred();

            // Basic toggle handlers
            foreach (var (actName, tickbox) in basicControls)
            {
                var capturedName = actName;
                tickbox.Toggled += (NTickbox tb) =>
                {
                    if (suppressBasic) return;
                    var current = ParseWeights(currentSlot);

                    if (tb.IsTicked)
                    {
                        current[capturedName] = 1;
                    }
                    else
                    {
                        var enabledCount = current.Count(kv =>
                            actNames.Contains(kv.Key) && kv.Value > 0);
                        if (enabledCount > 1)
                        {
                            current[capturedName] = 0;
                        }
                        else
                        {
                            suppressBasic = true;
                            tb.IsTicked = true;
                            suppressBasic = false;
                            return;
                        }
                    }

                    // Equalize weights for enabled acts
                    var enabled = current.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
                    var equalWeight = 100m / enabled.Count;
                    foreach (var key in current.Keys.ToList())
                        current[key] = enabled.Contains(key) ? equalWeight : 0;
                    var total = current.Values.Sum();
                    if (total != 100 && enabled.Count > 0)
                        current[enabled[0]] += 100 - total;

                    SaveWeights(currentSlot, ManFuckTheseWeights(current));
                    Changed();
                    SaveDebounced();
                };
            }

            basicContent.AddChild(CreateDividerControl());

            // ── Advanced tab ──
            advancedContent.AddChild(CreateSectionHeader($"Act{slot}Header", slot == 1));

            var advancedControls = new List<(string actName, HSlider slider, MegaRichTextLabel percentLabel)>();
            bool suppressAdvanced = false;

            foreach (var act in acts)
            {
                var actName = act.GetType().Name;
                var displayName = GetActDisplayName(act, slot);

                var nameLabel = CreateRawLabelControl(displayName, 28);
                nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

                var percentLabel = CreateRawLabelControl("", 28);
                percentLabel.CustomMinimumSize = new Vector2(96f, 0f);
                percentLabel.HorizontalAlignment = HorizontalAlignment.Right;

                var slider = new HSlider();
                slider.MinValue = 0;
                slider.MaxValue = 100;
                slider.Step = 0.1f;
                slider.CustomMinimumSize = new Vector2(200f, 32f);
                slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                slider.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

                advancedControls.Add((actName, slider, percentLabel));

                var hbox = new HBoxContainer();
                hbox.AddThemeConstantOverride("separation", 10);
                hbox.CustomMinimumSize = new Vector2(0f, 64f);
                hbox.AddChild(nameLabel);
                hbox.AddChild(slider);
                hbox.AddChild(percentLabel);
                advancedContent.AddChild(hbox);
            }

            void RefreshAdvancedLabels()
            {
                var total = advancedControls.Sum(c => c.slider.Value);
                foreach (var (_, s, label) in advancedControls)
                    label.Text = total > 0 ? $"{s.Value * 100f / total:F1}%" : "0%";
            }

            // Advanced refresh from saved weights
            void RefreshAdvanced()
            {
                suppressAdvanced = true;
                var current = ParseWeights(currentSlot);
                var totalW = current.Values.Sum();
                foreach (var (actName, slider, _) in advancedControls)
                {
                    var w = current.GetValueOrDefault(actName, 0);
                    slider.Value = decimal.ToDouble(totalW > 0m ? w * 100m / totalW : 0m);
                }
                RefreshAdvancedLabels();
                suppressAdvanced = false;
            }
            refreshAdvancedActions.Add(RefreshAdvanced);
            Callable.From(RefreshAdvanced).CallDeferred();

            // Advanced slider handlers
            foreach (var (actName, slider, _) in advancedControls)
            {
                var capturedName = actName;
                slider.ValueChanged += (double val) =>
                {
                    if (suppressAdvanced) return;
                    suppressAdvanced = true;

                    var newVal = val;
                    var others = advancedControls.Where(c => c.actName != capturedName).ToList();
                    var otherTotal = others.Sum(c => c.slider.Value);
                    var remaining = 100 - newVal;

                    foreach (var (_, s, _) in others)
                    {
                        if (otherTotal > 0)
                            s.Value = s.Value / otherTotal * remaining;
                        else if (others.Count > 0)
                            s.Value = remaining / others.Count;
                    }

                    var currentTotal = advancedControls.Sum(c => c.slider.Value);
                    if (currentTotal != 100 && others.Count > 0)
                    {
                        var drift = 100 - advancedControls.Sum(c => c.slider.Value);
                        if (Math.Abs(drift) > 0.001)
                        {
                            var tempCurrent = new Dictionary<string, decimal>();
                            foreach (var (name, s, _) in advancedControls)
                                tempCurrent[name] = (decimal) s.Value;
                            tempCurrent = ManFuckTheseWeights(tempCurrent);
                            foreach (var (name, s, _) in others)
                            {
                                s.Value = decimal.ToDouble(tempCurrent[name]);
                            }
                        }
                    }

                    // Save as weights
                    var current = new Dictionary<string, decimal>();
                    foreach (var (name, s, _) in advancedControls)
                        current[name] = (decimal) s.Value;
                    SaveWeights(currentSlot, ManFuckTheseWeights(current));

                    RefreshAdvancedLabels();
                    suppressAdvanced = false;
                    Changed();
                    SaveDebounced();
                };
            }

            advancedContent.AddChild(CreateDividerControl());
        }

        // Tab switching with sync
        basicTab.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ =>
        {
            basicTab.Select();
            advancedTab.Deselect();
            basicContent.Visible = true;
            advancedContent.Visible = false;
            foreach (var refresh in refreshBasicActions) refresh();
        }));

        advancedTab.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ =>
        {
            advancedTab.Select();
            basicTab.Deselect();
            advancedContent.Visible = true;
            basicContent.Visible = false;
            foreach (var refresh in refreshAdvancedActions) refresh();
        }));

        // making a god damn custom restore defaults because the other one doesnt work
        NConfigButton rawButtonControl = CreateRawButtonControl(GetBaseLibLabelText("RestoreDefaultsButton"), async () =>
        {
            try
            {
                await ConfirmRestoreDefaultsSpecial(refreshBasicActions, refreshAdvancedActions);
            }
            catch (Exception ex)
            {
                ModConfigLogger.Error("Unable to show restore confirmation dialog: " + ex.Message);
            }
        });
        rawButtonControl.Name = "ResetDefaultsButton";
        rawButtonControl.CustomMinimumSize = new Vector2(360f, rawButtonControl.CustomMinimumSize.Y);
        rawButtonControl.SetColor(Color.FromHtml("#b03f3f".AsSpan()));
        CenterContainer centerContainer = new CenterContainer();
        centerContainer.Name =  "ResetDefaultsButtonContainer";
        centerContainer.CustomMinimumSize = new Vector2(0.0f, 128f);
        centerContainer.AddChild(rawButtonControl);
        optionContainer.AddChild(centerContainer);
    }

    private async Task ConfirmRestoreDefaultsSpecial(List<Action>  refreshBasicActions, List<Action> refreshAdvancedActions)
    {
        NGenericPopup modalToCreate = NGenericPopup.Create();
        if (modalToCreate == null || NModalContainer.Instance == null)
            return;
        NModalContainer.Instance.Add(modalToCreate);
        if (!await modalToCreate.WaitForConfirmation(new LocString("settings_ui", "BASELIB-RESTORE_MODCONFIG_CONFIRMATION.body"), new LocString("settings_ui", "BASELIB-RESTORE_MODCONFIG_CONFIRMATION.header"), new LocString("main_menu_ui", "GENERIC_POPUP.cancel"), new LocString("main_menu_ui", "GENERIC_POPUP.confirm")))
            return;
        RestoreDefaultsNoConfirmSpecial(refreshBasicActions, refreshAdvancedActions);
    }

    private void RestoreDefaultsNoConfirmSpecial(List<Action>  refreshBasicActions, List<Action> refreshAdvancedActions)
    {
        for (int slot = 1; slot <= 3; slot++)
            SaveWeights(slot, ManFuckTheseWeights(GetDefaultWeights(slot)));
        foreach (var refresh in refreshBasicActions) refresh();
        foreach (var refresh in refreshAdvancedActions) refresh();
        Save();
        ConfigReloaded();
    }
}
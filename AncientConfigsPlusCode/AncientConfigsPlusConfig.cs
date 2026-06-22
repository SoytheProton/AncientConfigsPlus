using System.Reflection;
using System.Text.RegularExpressions;
using BaseLib.Abstracts;
using BaseLib.Config;
using BaseLib.Config.UI;
using BaseLib.Extensions;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
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

    // nightmare code trying to determine some bs...
    private int IsOnlyAncient(Dictionary<string, int> current, HashSet<String> ancientNames, int slot, String trackedAncient)
    {
        int enabledCount = 0;
        List<String> multiact = [];
        foreach (var kv in current.Where(kv => ancientNames.Contains(kv.Key)))
        {
            if (kv.Value <= 0) continue;
            if (kv.Key != trackedAncient && IsMultiact(GetAncientFromName(kv.Key)!))
                multiact.Add(kv.Key);
            enabledCount++;
        }

        if (multiact.Count > 0)
        {
            MainFile.Logger.Info(multiact.Count + " multiact ancients detected");
            for (int i = 1; i <= 3; i++)
            {
                if (i == slot) continue;
                var dict = ParseWeights(i);
                if (dict.Count(kv => kv.Value > 0) > 1) continue;
                var ancient = dict.FirstOrDefault(kv => kv.Value > 0).Key;
                if (multiact.Contains(ancient))
                { 
                    enabledCount--;
                    multiact.Remove(ancient);
                }
            }
        }
        MainFile.Logger.Info(enabledCount + " ancients enabled");
        return enabledCount;
    }
    
    private AncientEventModel? GetAncientFromName(string name) => ModelDb.AllAncients.FirstOrDefault(a => a.GetType().Name == name);
    
    private Dictionary<string, int> GetDefaultWeights(int slot) => GetAncientsForSlot(slot).ToDictionary(a => a.GetType().Name, _ => 1);

    private static Dictionary<string, int> ParseWeights(int slot)
    {
        var raw = (string)SlotProps[slot].GetValue(null)!;
        var result = new Dictionary<string, int>();
        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var weight))
                result[parts[0]] = Math.Clamp(weight, 0, 100);
            else if (parts.Length == 1)
                result[parts[0]] = 1;
        }
        return result;
    }

    private static void SaveWeights(int slot, Dictionary<string, int> weights)
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

    internal static bool IsMultiact(AncientEventModel ancient) => (GetAncientsForSlot(1).Contains(ancient) ? 1 : 0) + (GetAncientsForSlot(2).Contains(ancient) ? 1 : 0) + (GetAncientsForSlot(3).Contains(ancient) ? 1 : 0) >= 2;

    public static AncientEventModel GetWeightedAncient(int slot, Rng rng, List<AncientEventModel> rolledAncients)
    {
        var all = GetAncientsForSlot(slot);
        var weights = ParseWeights(slot);

        var weighted = all
            .Select(a => (ancient: a, weight: weights.GetValueOrDefault(a.GetType().Name, 0)))
            .Where(x => x.weight > 0 && !rolledAncients.Contains(x.ancient))
            .ToList();

        if (weighted.Count == 0)
            return all[rng.NextInt(all.Count)];

        var totalWeight = weighted.Sum(x => x.weight);
        var roll = rng.NextInt(totalWeight);
        var cumulative = 0;
        foreach (var (act, weight) in weighted)
        {
            cumulative += weight;
            if (roll < cumulative)
                return act;
        }
        return weighted.Last().ancient;
    }

    private static string GetAncientDisplayName(AncientEventModel ancient, int slot)
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
            var ancients = GetAncientsForSlot(slot);
            if (ancients.Count == 0) continue;

            var weights = ParseWeights(slot);
            var ancientNames = ancients.Select(a => a.GetType().Name).ToHashSet();

            foreach (var key in weights.Keys.Where(k => !ancientNames.Contains(k)).ToList())
                weights.Remove(key);

            if (!weights.Values.Any(w => w > 0))
                weights = GetDefaultWeights(slot);
            
            SaveWeights(slot, weights);

            // ── Basic tab ──
            basicContent.AddChild(CreateSectionHeader($"Act{slot}Header", slot == 1));

            var basicControls = new List<(string ancientName, NTickbox tickbox)>();
            bool suppressBasic = false;

            foreach (var ancient in ancients)
            {
                var ancientName = ancient.GetType().Name;
                var displayName = GetAncientDisplayName(ancient, slot);

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

                basicControls.Add((ancientName, tickbox));

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
                foreach (var (ancientName, tickbox) in basicControls)
                    tickbox.IsTicked = current.GetValueOrDefault(ancientName, 0) > 0;
                suppressBasic = false;
            }
            refreshBasicActions.Add(RefreshBasic);
            Callable.From(RefreshBasic).CallDeferred();

            // Basic toggle handlers
            foreach (var (ancientName, tickbox) in basicControls)
            {
                var capturedName = ancientName;
                var slot1 = slot;
                tickbox.Toggled += tb =>
                {
                    if (suppressBasic) return;
                    var current = ParseWeights(currentSlot);

                    if (tb.IsTicked)
                    {
                        current[capturedName] = 1;
                    }
                    else
                    {

                        if (IsOnlyAncient(current, ancientNames, slot1, capturedName) > 1)
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
                    
                    SaveWeights(currentSlot, current);
                    Changed();
                    SaveDebounced();
                };
            }

            basicContent.AddChild(CreateDividerControl());

            // ── Advanced tab ──
            advancedContent.AddChild(CreateSectionHeader($"Act{slot}Header", slot == 1));

            var advancedControls = new List<(string ancientName, HSlider slider, LineEdit lineEdit)>();
            bool suppressAdvanced = false;

            foreach (var ancient in ancients)
            {
                var ancientName = ancient.GetType().Name;
                var displayName = GetAncientDisplayName(ancient, slot);

                var nameLabel = CreateRawLabelControl(displayName, 28);
                nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

                var weightLabel = new LineEdit();
                weightLabel.CustomMinimumSize = new Vector2(32f, 0f);
                weightLabel.Alignment = HorizontalAlignment.Right;
                weightLabel.AddThemeFontOverride(ThemeConstants.LineEdit.Font, GD.Load<FontFile>("res://fonts/kreon_regular.ttf"));
                weightLabel.AddThemeFontSizeOverrideAll(28);

                var slider = new HSlider();
                slider.MinValue = 0;
                slider.MaxValue = 50;
                slider.Step = 1;
                slider.CustomMinimumSize = new Vector2(200f, 32f);
                slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                slider.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

                advancedControls.Add((ancientName, slider, weightLabel));

                var hbox = new HBoxContainer();
                hbox.AddThemeConstantOverride("separation", 10);
                hbox.CustomMinimumSize = new Vector2(0f, 64f);
                hbox.AddChild(nameLabel);
                hbox.AddChild(slider);
                hbox.AddChild(weightLabel);
                advancedContent.AddChild(hbox);
            }

            void RefreshAdvancedLabels()
            {
                foreach (var (_, s, label) in advancedControls)
                    label.Text = $"{(int) s.Value}";
            }

            // Advanced refresh from saved weights
            void RefreshAdvanced()
            {
                suppressAdvanced = true;
                var current = ParseWeights(currentSlot);
                foreach (var (ancientName, slider, _) in advancedControls)
                {
                    slider.Value = current.GetValueOrDefault(ancientName, 0);
                }
                RefreshAdvancedLabels();
                suppressAdvanced = false;
            }
            refreshAdvancedActions.Add(RefreshAdvanced);
            Callable.From(RefreshAdvanced).CallDeferred();
            
            foreach (var (name, slider, lineEdit) in advancedControls)
            {
                var slot1 = slot;
                slider.ValueChanged += dub =>
                {
                    if (suppressAdvanced) return;
                    suppressAdvanced = true;
                    
                    var current = new Dictionary<string, int>();
                    foreach (var (n, s, _) in advancedControls)
                        current[n] = (int) s.Value;
                    
                    if(dub == 0)
                    {
                        if (IsOnlyAncient(current, ancientNames, slot1, name) == 0)
                        {
                            suppressAdvanced = true;
                            slider.Value = current[name] = 1;
                            lineEdit.Text = slider.Value.ToString();
                            suppressAdvanced = false;
                        }
                    }
                    
                    SaveWeights(currentSlot, current);

                    RefreshAdvancedLabels();
                    suppressAdvanced = false;
                    Changed();
                    SaveDebounced();
                };

                Regex regex = new Regex("[^0-9]");
                
                lineEdit.TextChanged += newText =>
                {
                    if (suppressAdvanced) return;
                    suppressAdvanced = true;
                    
                    if (regex.IsMatch(newText))
                    {
                        int caretPos = lineEdit.CaretColumn;
                        lineEdit.Text = regex.Replace(lineEdit.Text, "");
                        lineEdit.CaretColumn = caretPos - (newText.Length - lineEdit.Text.Length);
                        suppressAdvanced = false;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(lineEdit.Text) || string.IsNullOrWhiteSpace(newText))
                        {
                            int caretPos = lineEdit.CaretColumn;
                            lineEdit.Text = "0";
                            lineEdit.CaretColumn = caretPos - (newText.Length - lineEdit.Text.Length);
                        }

                        if (!int.TryParse(lineEdit.Text, out var parsedInt))
                        {
                            suppressAdvanced = false;
                            return;
                        }

                        var current = new Dictionary<string, int>();
                        var sliderValue = slider.Value != 0 ? slider.Value : 1;
                        foreach (var (n, s, _) in advancedControls)
                            current[n] = (int) s.Value;
                        slider.Value = current[name] = parsedInt;
                        
                        if(parsedInt == 0)
                        {
                            if (IsOnlyAncient(current, ancientNames, slot1, name) == 0)
                            {
                                suppressAdvanced = true;
                                lineEdit.Text = sliderValue.ToString();
                                slider.Value = current[name] = (int) sliderValue;
                                suppressAdvanced = false;
                            }
                        }
                        
                        SaveWeights(currentSlot, current);

                        RefreshAdvancedLabels();
                        suppressAdvanced = false;
                        Changed();
                        SaveDebounced();
                    }
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
            SaveWeights(slot, GetDefaultWeights(slot));
        foreach (var refresh in refreshBasicActions) refresh();
        foreach (var refresh in refreshAdvancedActions) refresh();
        Save();
        ConfigReloaded();
    }
}
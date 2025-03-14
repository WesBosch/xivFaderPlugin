using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FaderPlugin.Data;
using faderPlugin.Resources;
using ImGuiNET;
using System.Linq;

namespace FaderPlugin.Windows.Config;

public partial class ConfigWindow
{
    private List<ConfigEntry> SelectedConfig = [];
    private readonly List<Element> SelectedElements = [];

    private Constants.OverrideKeys CurrentOverrideKey => (Constants.OverrideKeys) Configuration.OverrideKey;

    private void Settings()
    {
        using var tabItem = ImRaii.TabItem(Language.TabSettings);
        if (!tabItem.Success)
            return;

        if (ImGui.CollapsingHeader(Language.SettingsGeneralHeader, ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextUnformatted(Language.SettingsFocusKey);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(195.0f * ImGuiHelpers.GlobalScale);
            using (var combo = ImRaii.Combo("##UserFocusCombo", CurrentOverrideKey.ToString()))
            {
                if (combo.Success)
                {
                    foreach (var option in Enum.GetValues<Constants.OverrideKeys>())
                    {
                        if (ImGui.Selectable(option.ToString(), option.Equals(CurrentOverrideKey)))
                        {
                            Configuration.OverrideKey = (int)option;
                            Configuration.Save();
                        }
                    }
                }
            }

            ImGuiComponents.HelpMarker(Language.SettingsFocusKeyTooltip);

            var focusOnHotbarsUnlock = Configuration.FocusOnHotbarsUnlock;
            if (ImGui.Checkbox("##focus_on_unlocked_bars", ref focusOnHotbarsUnlock))
            {
                Configuration.FocusOnHotbarsUnlock = focusOnHotbarsUnlock;
                Configuration.Save();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(Language.SettingsFocusHotbarUnlock);
            ImGuiComponents.HelpMarker(Language.SettingsFocusHotbarUnlockTooltip);

            var emoteChat = Configuration.EmoteActivity;
            if (ImGui.Checkbox(Language.SettingsEmoteActivity, ref emoteChat))
            {
                Configuration.EmoteActivity = emoteChat;
                Configuration.Save();
            }

            var importChat = Configuration.ImportantActivity;
            if (ImGui.Checkbox(Language.SettingsSystemTrigger, ref importChat))
            {
                Configuration.ImportantActivity = importChat;
                Configuration.Save();
            }

            var idleDelay = (float)TimeSpan.FromMilliseconds(Configuration.DefaultDelay).TotalSeconds;
            ImGui.TextUnformatted(Language.SettingsDelay);
            ImGui.SameLine();
            var defaultDelayEnabled = Configuration.DefaultDelayEnabled;
            if (ImGui.Checkbox("##default_delay_enabled", ref defaultDelayEnabled))
            {
                Configuration.DefaultDelayEnabled = defaultDelayEnabled;
                Configuration.Save();
            }

            if (defaultDelayEnabled)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(170.0f * ImGuiHelpers.GlobalScale);
                if (ImGui.SliderFloat("##default_delay", ref idleDelay, 0.1f, 15f, $"%.1f {Language.Seconds}"))
                {
                    Configuration.DefaultDelay = (int)TimeSpan.FromSeconds(Math.Round(idleDelay, 1)).TotalMilliseconds;
                    Configuration.Save();
                }
            }

            ImGuiComponents.HelpMarker(Language.SettingsDelayTooltip);

            ImGui.TextUnformatted(Language.SettingsChatActivityTimeout);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(170 * ImGuiHelpers.GlobalScale);
            var chatActivityTimeout = (int)TimeSpan.FromMilliseconds(Configuration.ChatActivityTimeout).TotalSeconds;
            if (ImGui.SliderInt("##chat_activity_timeout", ref chatActivityTimeout, 1, 20, $"%d {Language.Seconds}"))
            {
                Configuration.ChatActivityTimeout = (int)TimeSpan.FromSeconds(chatActivityTimeout).TotalMilliseconds;
                Configuration.Save();
            }

            ImGui.TextUnformatted("Transition time:"); // TODO: Localize
            ImGui.SameLine();
            ImGui.SetNextItemWidth(170.0f * ImGuiHelpers.GlobalScale);

            // Convert internal speed to a transition time in milliseconds.
            float transitionTimeMs = Configuration.TransitionSpeed > 0.0001f
                ? (1.0f / Configuration.TransitionSpeed) * 1000.0f
                : 100.0f; // fallback value if speed is too low ( shouldn't happen )

            if (ImGui.SliderFloat("##transition_time_ms", ref transitionTimeMs, 10.0f, 1000.0f, $"%.0f {Language.Milliseconds}"))
            {
                // Round to the nearest 10ms increment. could remove but round numbers are satisfying.
                transitionTimeMs = (float)Math.Round(transitionTimeMs / 10.0f) * 10.0f;
                Configuration.TransitionSpeed = 1000.0f / transitionTimeMs;
                Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Time in milliseconds for an addon to fully transition in opacity."); // TODO: Localize

        }

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        Helper.WrappedText(Language.SettingsMultiSelectionHint);
        ImGuiHelpers.ScaledDummy(5);

        var startPos = ImGui.GetCursorPos();

        var style = ImGui.GetStyle();
        var buttonWidth = ImGui.CalcTextSize("Context Action Hotbar   ?").X + style.FramePadding.X * 2 + style.ScrollbarSize;
        var childSize = buttonWidth + style.WindowPadding.X * 2;
        using (var child = ImRaii.Child("ElementList", new Vector2(childSize , 0), true))
        {
            if (child.Success)
            {
                foreach (var element in Enum.GetValues<Element>())
                {
                    if (element.ShouldIgnoreElement())
                        continue;

                    var buttonText = ElementUtil.GetElementName(element);
                    var tooltipText = element.TooltipForElement();
                    if (tooltipText != string.Empty)
                        buttonText += "   ?";

                    using var pushedStyle = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));

                    var desiredButtonColor = ImGui.GetColorU32(ImGuiCol.Button);
                    if (SelectedElements.Contains(element))
                        desiredButtonColor = ImGui.GetColorU32(ImGuiColors.HealerGreen);

                    var hasScrollbar = ImGui.GetScrollMaxY() > 0.0f;
                    using var pushedColor = ImRaii.PushColor(ImGuiCol.Button, desiredButtonColor);
                    if (ImGui.Button(buttonText, new Vector2(buttonWidth - (hasScrollbar ? style.ScrollbarSize : 0.0f), 0)))
                    {
                        if (!ImGui.IsKeyDown(ImGuiKey.ModCtrl))
                            SelectedElements.Clear();

                        if (SelectedElements.Count == 0)
                            SelectedConfig = Configuration.GetElementConfig(element);

                        if (!SelectedElements.Remove(element))
                            SelectedElements.Add(element);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        if (tooltipText != string.Empty)
                            Helper.Tooltip(tooltipText);

                        var addonNames = ElementUtil.GetAddonName(element);
                        if(addonNames.Length == 0)
                            continue;

                        var color = ImGui.GetColorU32(ImGuiColors.HealerGreen);
                        var drawlist = ImGui.GetBackgroundDrawList();
                        foreach (var addonName in addonNames)
                        {
                            var addonPosition = Addon.GetAddonPosition(addonName);
                            if (!addonPosition.IsPresent)
                                continue;

                            drawlist.AddRect(addonPosition.Start, addonPosition.End, color, 0, ImDrawFlags.None, 5.0f * ImGuiHelpers.GlobalScale);
                        }
                    }
                }
            }
        }

        ImGui.SetCursorPos(startPos with {X = startPos.X + childSize});
        using (var contentChild = ImRaii.Child("ConfigPage", Vector2.Zero, true))
        {
            if (contentChild.Success)
            {
                // Config for the selected elements.
                if (SelectedElements.Count == 0)
                    return;

                var selectedElement = SelectedElements[0];
                var elementName = ElementUtil.GetElementName(selectedElement);
                if(SelectedElements.Count > 1)
                    elementName += $" & {Language.SettingsOthers}";

                ImGui.TextUnformatted(Language.SettingsElementConfiguration.Format(elementName));
                if(SelectedElements.Count > 1)
                    if(ImGui.Button(Language.SettingsSyncToElement.Format(selectedElement)))
                        SaveSelectedElementsConfig();

                // Config for each condition.
                for(var i = 0; i < SelectedConfig.Count; i++)
                {

                    var elementState = SelectedConfig[i].state;
                    var elementSetting = SelectedConfig[i].setting;


                    // State
                    var itemWidth = 200.0f * ImGuiHelpers.GlobalScale;
                    ImGui.SetNextItemWidth(itemWidth);

                    var stateName = StateUtil.GetStateName(elementState);
                    if(elementState == State.Default)
                    {
                        var pos = ImGui.GetCursorPos();
                        ImGui.TextUnformatted(stateName);
                        ImGui.SetCursorPos(pos with {X = pos.X + itemWidth + ImGui.GetStyle().ItemSpacing.X});
                    }
                    else
                    {
                        using (var combo = ImRaii.Combo($"##{elementName}-{i}-state", stateName))
                        {
                            if (combo.Success)
                            {
                                foreach(var state in StateUtil.OrderedStates)
                                {
                                    if(state is State.None or State.Default)
                                        continue;

                                    if(ImGui.Selectable(StateUtil.GetStateName(state)))
                                    {
                                        SelectedConfig[i].state = state;
                                        SaveSelectedElementsConfig();
                                    }
                                }
                            }
                        }

                        ImGui.SameLine();
                    }

                    // Opacity
                    {
                        float opacity = SelectedConfig[i].Opacity;
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(itemWidth);
                        if (ImGui.SliderFloat($"##{elementName}-{i}-opacity", ref opacity, 0.0f, 1.0f, "Opacity: %.2f"))
                        {
                            SelectedConfig[i].Opacity = opacity;
                            // If the opacity is increased above 0.05 while the element is disabled,
                            // force the setting back to Show.
                            if (opacity > 0.05f && SelectedConfig[i].setting == Setting.Hide)
                            {
                                SelectedConfig[i].setting = Setting.Show;
                            }
                            SaveSelectedElementsConfig();
                        }
                    }
                    ImGui.SameLine();
                    // Only show the "Disable Element" checkbox if the configuration is the default state and the opacity is <= 0.05.
                    if (SelectedConfig[i].state == State.Default && SelectedConfig[i].Opacity <= 0.05f)
                    {
                        bool hide = SelectedConfig[i].setting == Setting.Hide;
                        if (ImGui.Checkbox($"##{elementName}-{i}-hide", ref hide))
                        {
                            SelectedConfig[i].setting = hide ? Setting.Hide : Setting.Show;
                            SaveSelectedElementsConfig();
                        }
                        ImGui.SameLine();
                        ImGui.TextUnformatted("Disable Element");
                        ImGuiComponents.HelpMarker("Check this box to disable the element when its opacity is at or below 0.05. Leave unchecked to force the element to remain clickable/hoverable even at low opacity.");
                    }

                    if (elementState == State.Default)
                        continue;

                    // Up
                    ImGui.SameLine();
                    using var innerFont = ImRaii.PushFont(UiBuilder.IconFont);
                    if(ImGui.Button($"{FontAwesomeIcon.ArrowUp.ToIconString()}##{elementName}-{i}-up"))
                    {
                        if(i > 0)
                        {
                            var swap1 = SelectedConfig[i - 1];
                            var swap2 = SelectedConfig[i];

                            if(swap1.state != State.Default && swap2.state != State.Default)
                            {
                                SelectedConfig[i] = swap1;
                                SelectedConfig[i - 1] = swap2;

                                SaveSelectedElementsConfig();
                            }
                        }
                    }

                    // Down
                    ImGui.SameLine();
                    if(ImGui.Button($"{FontAwesomeIcon.ArrowDown.ToIconString()}##{elementName}-{i}-down"))
                    {
                        if(i < SelectedConfig.Count - 1)
                        {
                            var swap1 = SelectedConfig[i + 1];
                            var swap2 = SelectedConfig[i];

                            if(swap1.state != State.Default && swap2.state != State.Default)
                            {
                                SelectedConfig[i] = swap1;
                                SelectedConfig[i + 1] = swap2;

                                SaveSelectedElementsConfig();
                            }
                        }
                    }

                    // Delete
                    ImGui.SameLine();
                    if(ImGui.Button($"{FontAwesomeIcon.TrashAlt.ToIconString()}##{elementName}-{i}-delete"))
                    {
                        SelectedConfig.RemoveAt(i);

                        SaveSelectedElementsConfig();
                    }

                }
                ImGui.SameLine();
                using var font = ImRaii.PushFont(UiBuilder.IconFont);
                if(ImGui.Button($"{FontAwesomeIcon.Plus.ToIconString()}##{elementName}-add"))
                {
                    // Add the new state then swap it with the existing default state.
                    SelectedConfig.Add(new ConfigEntry(State.None, Setting.Hide));
                    var swap1 = SelectedConfig[^1];
                    var swap2 = SelectedConfig[^2];

                    SelectedConfig[^2] = swap1;
                    SelectedConfig[^1] = swap2;

                    SaveSelectedElementsConfig();
                }


            }
            // At the bottom of the element configuration section, add a warning if needed.
            {
                // Check if the default config entry is disabled.
                var defaultEntry = SelectedConfig.FirstOrDefault(e => e.state == State.Default);
                bool defaultDisabled = defaultEntry != null && defaultEntry.setting == Setting.Hide;

                // Check if any hover state config exists.
                bool hoverPresent = SelectedConfig.Any(e => e.state == State.Hover);

                if (defaultDisabled && hoverPresent)
                {
                    ImGui.Separator();
                    ImGui.TextColored(ImGuiColors.DalamudRed, "Warning: Disabled Elements cannot be hovered!"); // TODO: Localize
                }
            }
        }
    }

    private void SaveSelectedElementsConfig()
    {
        foreach(var element in SelectedElements)
            Configuration.elementsConfig[element] = SelectedConfig;

        Configuration.Save();
    }
}

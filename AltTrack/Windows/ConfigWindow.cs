using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Interface.ImGuiFileDialog;
using Serilog;

namespace AltTrack.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base("AltTrack config##AltTrackConfigWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Checkbox("AUTOSCAN", ref plugin.autoscan);
        ImGui.InputInt("AutoScan", ref plugin.autoscan_time);
    }
}

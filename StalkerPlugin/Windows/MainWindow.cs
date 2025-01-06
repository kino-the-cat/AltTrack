using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace StalkerPlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin plugin;
    private bool show_everything = false;
    private bool show_local = false;

    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin, string goatImagePath)
        : base("MATUNO IS A STALKER##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    private string search_text = "";
    public override void Draw()
    {
        if (ImGui.Button("DEW IT"))
        {
            plugin.Snoop();
        }
        ImGui.SameLine();
        if (plugin.accounts.Count == 0)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("DUMP IT"))
        {
            plugin.Dump("stalk_backup.csv");
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (plugin.accounts.Count != 0)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("RESTORE IT"))
        {
            plugin.Restore();
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("deleet"))
        {
            ImGui.OpenPopup("DeletingWindow");
        }
        if (ImGui.BeginPopup("DeletingWindow"))
        {
            ImGui.Button("NO");
            ImGui.Button("NO");
            if (ImGui.Button("yes"))
            {
                plugin.Destroy();
            }
            ImGui.Button("NO");
            ImGui.Button("NO");
            ImGui.Button("NO");

            ImGui.EndPopup();
        }
        ImGui.Checkbox("SHOW EVERYTHING", ref show_everything);
        ImGui.SameLine();
        ImGui.Checkbox("SHOW LOCAL", ref show_local);

        ImGui.Text($"SNOOPED ACCOUNTS: {plugin.accounts.Count}");
        ImGui.SameLine();
        ImGui.Text($"REFRESH IN: {(300 - plugin.stalk_frame_counter) / 60} (SAVE IN: {12 - plugin.save_frame_coutner})");

        ImGui.InputText("NAME", ref search_text, 32);

        ImGui.BeginChild("table", ImGuiHelpers.ScaledVector2(0, 0), true, ImGuiWindowFlags.AlwaysVerticalScrollbar);
        if (ImGui.BeginTable("accounts", 2, ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("ACCOUNTID", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("NAMES");
            ImGui.TableHeadersRow();

            foreach (var account in plugin.accounts)
            {
                if ((show_everything || account.Value.Count > 1)
                     && (!show_local || plugin.last_snoop.Contains(account.Key)))
                {
                    string joined = $"{string.Join(", ", account.Value)}";
                    if (joined.IndexOf(search_text, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text($"{account.Key}");
                        ImGui.TableNextColumn();
                        ImGui.Text(joined);
                    }
                }
            }
        }
        ImGui.EndTable();
        ImGui.EndChild();
    }
}

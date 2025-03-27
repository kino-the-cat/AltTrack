using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Interface.ImGuiFileDialog;
using Serilog;

namespace AltTrack.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin plugin;
    private bool duplicates_only = false;
    private bool local_only = false;

    FileDialogManager fileDialogManager = new FileDialogManager();

    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin)
        : base("MATUNO IS A STALKER##AltTrack hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
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
        fileDialogManager.Draw();

        if (ImGui.Button("MANUAL SCAN"))
        {
            plugin.Snoop();
        }
        ImGui.SameLine();
        if (ImGui.Button("EXPORT"))
        {
            fileDialogManager.SaveFileDialog("SELECT", ".csv", "alttrack", ".csv", (bool selected, string path) =>
            {
                if (!selected)
                {
                    return;
                }

                plugin.Save(path);
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("IMPORT"))
        {
            fileDialogManager.OpenFileDialog("SELECT", ".csv", (bool selected, string path) =>
            {
                if (!selected)
                {
                    return;
                }

                plugin.Load(path);
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("delete everything"))
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
        ImGui.Checkbox("DUPLICATES ONLY", ref duplicates_only);
        ImGui.SameLine();
        ImGui.Checkbox("LOCAL ONLY", ref local_only);

        ImGui.Text($"SALTY: {plugin.salt}");
        ImGui.SameLine();
        if (ImGui.Button("RESALT!?")) {
            plugin.salt = 0;
        }

        ImGui.Text($"SNOOPED ACCOUNTS: {plugin.accounts.Count}");
        ImGui.SameLine();
        ImGui.Text($"REFRESH IN: {(plugin.stalk_frame_counter)} (SAVE IN: {plugin.save_frame_coutner})");

        ImGui.InputText("NAME", ref search_text, 32);

        ImGui.BeginChild("table", ImGuiHelpers.ScaledVector2(0, 0), true, ImGuiWindowFlags.AlwaysVerticalScrollbar);
        if (ImGui.BeginTable("accounts", 2, ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("ACCOUNTID", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("NAMES");
            ImGui.TableHeadersRow();

            foreach (var account in plugin.accounts)
            {
                if ((!duplicates_only || account.Value.Count > 1)
                     && (!local_only || plugin.last_snoop.Contains(account.Key)))
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

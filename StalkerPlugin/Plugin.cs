using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using StalkerPlugin.Windows;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace StalkerPlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    private const string CommandName = "/stalk";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Stalker");
    private MainWindow MainWindow { get; init; }

    public SortedDictionary<ulong, HashSet<string>> accounts = [];

    GameHooks hooks;

    public HashSet<ulong> last_snoop = [];

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "something something stalk something"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        Framework.Update += AutoSnoop;

        // DutyState.DutyStarted += StopAutoSnoop;
        // DutyState.DutyCompleted += StartAutoSnoop;

        Restore();

        hooks = new GameHooks(GameInteropProvider, (uint account, string name, string world) =>
        {
            AddCharacter(account, $"{name}@{world}");
        });
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();

        hooks.Dispose();

        CommandManager.RemoveHandler(CommandName);

        Dump("stalk.csv");
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();


    private void StartAutoSnoop(object? sender, ushort dunno)
    {
        Framework.Update += AutoSnoop;
    }

    private void StopAutoSnoop(object? sender, ushort dunno)
    {
        Framework.Update -= AutoSnoop;
    }

    public int stalk_frame_counter = 0;
    public int save_frame_coutner = 0;
    private void AutoSnoop(IFramework framework)
    {
        if (stalk_frame_counter++ == 300)
        {
            Snoop();

            if (save_frame_coutner++ == 12)
            {
                Dump("stalk.csv");
                save_frame_coutner = 0;
            }

            stalk_frame_counter = 0;
        }
    }

    public void Snoop()
    {
        var tmp_snoop = new HashSet<ulong>();
        foreach (var obj in Objects)
        {
            if (obj is null)
            {
                continue;
            }

            if (obj!.ObjectKind == ObjectKind.Player)
            {
                var character = (IPlayerCharacter)obj!;
                var accountId = character.GetAccountId();
                Log.Information($"{character.Name}@{character.HomeWorld.Value.Name}: {accountId}");

                AddCharacter(accountId, $"{character.Name}@{character.HomeWorld.Value.Name}");

                tmp_snoop.Add(accountId);
            }
        }

        last_snoop = tmp_snoop;
    }

    public void AddCharacter(ulong accountId, string name)
    {
        if (!accounts.ContainsKey(accountId))
        {
            accounts.Add(accountId, []);
        }
        var found_account = accounts[accountId];

        var split = name.Split("@");
        var char_name = split[0];
        var world_name = split[1];

        // I used @Search when I didn't know I can get home world from search, smh
        // Should be probably removed at some point
        if (world_name == "Search")
        {
            if (!found_account.Any(saved_name => saved_name.Split("@")[0] == char_name))
            {
                found_account.Add(name);
            }
        }
        else
        {
            if (found_account.Contains($"{char_name}@Search"))
            {
                found_account.Remove($"{char_name}@Search");
            }
            found_account.Add(name);
        }
    }

    public void Restore()
    {
        var dbPath = Path.Combine(PluginInterface.ConfigDirectory.FullName!, "stalk.csv");
        if (!File.Exists(dbPath))
        {
            accounts = [];
            return;
        }
        var csv = File.ReadAllLines(dbPath);

        accounts.Clear();

        foreach (var line in csv)
        {
            var values = line.Split(",");
            var accountID = (ulong)decimal.Parse(values[0]);

            HashSet<string> names = [];
            for (var i = 1; i < values.Length; ++i)
            {
                names.Add(values[i]);
            }

            accounts.Add(accountID, names);
        }
    }

    public void Dump(string filename)
    {
        var dbPath = Path.Combine(PluginInterface.ConfigDirectory.FullName!, filename);
        var csv = new StringBuilder();
        foreach (var account in accounts)
        {
            csv.Append(account.Key);
            foreach (var value in account.Value)
            {
                csv.Append($",{value}");
            }
            csv.AppendLine();
        }
        File.WriteAllText(dbPath, csv.ToString());
    }

    public void Destroy()
    {
        accounts.Clear();
    }
}

public static class UnsafeHelper
{
    public static unsafe ulong GetAccountId(this IPlayerCharacter character) => ((Character*)character.Address)->AccountId;
}
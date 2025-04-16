using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using AltTrack.Windows;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace AltTrack;

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

    private const string CommandName = "/alttrack";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("AltTrack");
    private MainWindow MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }

    public bool autoscan = true;

    public SortedDictionary<ulong, HashSet<string>> accounts = [];

    public ulong salt;
    public ulong unsaltedAccountID;

    GameHooks hooks;

    public HashSet<ulong> last_snoop = [];

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        MainWindow = new MainWindow(this);
        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "something something stalk something"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        Framework.Update += AutoSnoop;

        // DutyState.DutyStarted += StopAutoSnoop;
        // DutyState.DutyCompleted += StartAutoSnoop;

        Restore();

        hooks = new GameHooks(GameInteropProvider, (uint accountId, string name, string world) =>
        {
            if (salt == 0) {
                Log.Error("Missing salt?");
                return;
            }
            var accountIdRev = ((accountId >> 31) ^ salt) % 0x100000000;
            AddCharacter(accountIdRev, $"{name}@{world}");
        });
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();

        hooks.Dispose();

        CommandManager.RemoveHandler(CommandName);

        SaveLocal("db.csv");
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();

    public void ToggleConfigUI() => ConfigWindow.Toggle();


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
    public int autosave_time = 3600;
    public int autoscan_time = 300;
    private void AutoSnoop(IFramework framework)
    {
        if (autoscan && stalk_frame_counter++ >= autoscan_time)
        {
            Snoop();

            stalk_frame_counter = 0;
        }


        if (save_frame_coutner++ >= autosave_time)
        {
            SaveLocal("db.csv");

            save_frame_coutner = 0;
        }
    }

    public void Snoop()
    {
        var tmp_snoop = new HashSet<ulong>();

        bool first = true;

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

                if (salt == 0 || (unsaltedAccountID != accountId && first))
                {
                    Log.Information($"\nResalting {character.Name} {accountId}");
                    foreach (var acc in accounts)
                    {

                        if (acc.Value.Contains($"{character.Name}@{character.HomeWorld.Value.Name}"))
                        {
                            if (acc.Key == 0)
                            {
                                Log.Information($"Oops, let's just ignore that. ({acc.Key ^ (accountId >> 31)})");
                                // continue;
                            }
                            salt = acc.Key ^ (accountId >> 31);
                            unsaltedAccountID = accountId;

                            break;
                        }
                    }

                    if (salt == 0)
                    {
                        Log.Error("FAILED, OOPS!");
                        return;
                    }
                }
                first = false;


                var accountIdRev = ((accountId >> 31) ^ salt) % 0x100000000;
                Log.Verbose($"{character.Name}@{character.HomeWorld.Value.Name}: {accountId} -> {accountIdRev}");

                AddCharacter(accountIdRev, $"{character.Name}@{character.HomeWorld.Value.Name}");

                tmp_snoop.Add(accountIdRev);
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
        Load(Path.Combine(PluginInterface.ConfigDirectory.FullName!, "db.csv"));
    }

    public bool Load(string dbPath)
    {

        if (!File.Exists(dbPath))
        {
            return false;
        }

        Log.Info($"Loading {dbPath}");

        var csv = File.ReadAllLines(dbPath);

        foreach (var line in csv)
        {
            var values = line.Split(",");
            var accountID = (ulong)decimal.Parse(values[0]);

            if (!accounts.ContainsKey(accountID))
            {
                accounts.Add(accountID, []);
            }

            for (var i = 1; i < values.Length; ++i)
            {
                accounts[accountID].Add(values[i]);
            }
        }

        return true;
    }

    public void SaveLocal(string filename)
    {
        Save(Path.Combine(PluginInterface.ConfigDirectory.FullName!, filename));
    }

    public void Save(string dbPath)
    {
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
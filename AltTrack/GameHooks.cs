using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System;
using System.Text;
using Dalamud.Utility.Signatures;
using Dalamud.Hooking;
using Serilog;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Dalamud.Memory;

namespace AltTrack;

// Mostly stolen from RetainerTrack


internal sealed class PlayerMapping
{
    public required ulong? AccountId { get; init; }
    public required ulong ContentId { get; init; }
    public required string HomeWorld { get; init; } = string.Empty;
    public required string PlayerName { get; init; } = string.Empty;
}


internal sealed unsafe class GameHooks : IDisposable
{
    Action<ulong, String, String> addCB;
    public GameHooks(IGameInteropProvider GameInteropProvider, Action<ulong, String, String> add_cb)
    {
        addCB = add_cb;

        GameInteropProvider.InitializeFromAttributes(this);
        SocialListResultHook.Enable();
    }

    public void Dispose()
    {
        SocialListResultHook.Dispose();
    }

    private delegate nint SocialListResultDelegate(nint a1, nint dataPtr);
    [Signature("48 89 5C 24 10 56 48 83 EC 20 48 ?? ?? ?? ?? ?? ?? 48 8B F2 E8 ?? ?? ?? ?? 48 8B D8",
    DetourName = nameof(ProcessSocialListResult))]
    private Hook<SocialListResultDelegate> SocialListResultHook { get; init; } = null!;

    private string WorldNumberToString(short num)
    {
        switch (num)
        {
            case 80: return "Cerberus";
            case 83: return "Louisoix";
            case 71: return "Moogle";
            case 39: return "Omega";
            case 401: return "Phantom";
            case 97: return "Ragnarok";
            case 400: return "Sagittarius";
            case 85: return "Spriggan";
            case 402: return "Alpha";
            case 36: return "Lich";
            case 66: return "Odin";
            case 56: return "Phoenix";
            case 403: return "Raiden";
            case 67: return "Shiva";
            case 33: return "Twintania";
            case 42: return "Zodiark";
            default:
                return "HELP";
        }
    }

    private void PrintBytes(SocialListPlayer player)
    {
        int size = Marshal.SizeOf(player);
        byte[] arr = new byte[size];

        IntPtr ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(player, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
        String re = "";
        String res = "";
        for (int i = 0; i < size; ++i)
        {
            if (arr[i] != 0)
            {
                res += ",   " + (char)arr[i];
            }
            else
            {
                res += ",   .";
            }

            if (arr[i] < 10)
            {
                re += ",   " + arr[i];

            }
            else if (arr[i] < 100)
            {
                re += ",  " + arr[i];
            }
            else
            {
                re += ", " + arr[i];
            }
        }
        Log.Error($"{re}");
        Log.Error($"{res}");
    }
    private nint ProcessSocialListResult(nint a1, nint dataPtr)
    {
        try
        {
            var result = Marshal.PtrToStructure<SocialListResultPage>(dataPtr);
            List<PlayerMapping> mappings = new();
            foreach (SocialListPlayer player in result.PlayerSpan)
            {
                if (player.ContentId == 0)
                    continue;

                var mapping = new PlayerMapping
                {
                    ContentId = player.ContentId,
                    AccountId = player.AccountId != 0 ? player.AccountId : null,
                    HomeWorld = WorldNumberToString(player.HomeWorld),
                    PlayerName = MemoryHelper.ReadString(new nint(player.CharacterName), Encoding.ASCII, 32),
                };

                if (!string.IsNullOrEmpty(mapping.PlayerName))
                {
                    Log.Debug("Content id {ContentId} belongs to '{Name}' ({AccountId})", mapping.ContentId,
                        mapping.PlayerName, mapping.AccountId);
                    mappings.Add(mapping);
                }
                else
                {
                    Log.Debug("Content id {ContentId} didn't resolve to a player name, ignoring",
                        mapping.ContentId);
                }
            }

            // if (mappings.Count > 0)
            //     Task.Run(() => _persistenceContext.HandleContentIdMapping(mappings));
            for (int i = 0; i < mappings.Count; ++i)
            {
                var a = mappings[i];
                Log.Information($"{a.PlayerName}@{a.HomeWorld}: {a.AccountId}");
                if (a.AccountId is not null)
                    addCB(a.AccountId.Value, a.PlayerName, a.HomeWorld);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Could not process social list result");
        }

        return SocialListResultHook.Original(a1, dataPtr);
    }



    /// <summary>
    /// There are some caveats here, the social list includes a LOT of things with different types
    /// (we don't care for the result type in this plugin), see sapphire for which field is the type.
    ///
    /// 1 = party
    /// 2 = friend list
    /// 3 = link shell
    /// 4 = player search
    /// 5 = fc short list (first tab, with company board + actions + online members)
    /// 6 = fc long list (members tab)
    ///
    /// Both 1 and 2 are sent to you on login, unprompted.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 0x420)]
    internal struct SocialListResultPage
    {
        [FieldOffset(0x10)] private fixed byte Players[10 * 0x70];

        public Span<SocialListPlayer> PlayerSpan => new(Unsafe.AsPointer(ref Players[0]), 10);
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x70, Pack = 1)]
    internal struct SocialListPlayer
    {
        /// <summary>
        /// If this is set, it means there is a player present in this slot (even if no name can be retrieved),
        /// 0 if empty.
        /// </summary>
        [FieldOffset(0x00)] public readonly ulong ContentId;

        /// <summary>
        /// Only seems to be set for certain kind of social lists, e.g. friend list/FC members doesn't include any.
        /// </summary>
        [FieldOffset(0x18)] public readonly ulong AccountId;

        /// <summary>
        /// Maybe
        /// </summary>
        [FieldOffset(0x42)] public readonly short HomeWorld;

        /// <summary>
        /// This *can* be empty, e.g. if you're querying your friend list, the names are ONLY set for characters on the same world.
        /// </summary>
        [FieldOffset(0x44)] public fixed byte CharacterName[32];
    }

}

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Hacknet;
using Microsoft.Xna.Framework;
using System.Runtime.InteropServices;

namespace HacknetIME;

[BepInPlugin(ModGUID, ModName, ModVer)]
public class HacknetIME : HacknetPlugin
{
    public const string ModGUID = "com.LDTchara.HacknetIME";
    public const string ModName = "HacknetIME";
    public const string ModVer = "1.0.0";

    // ── BepInEx 配置 ──
    public static ConfigEntry<bool> ConfigDebug;
    public static ConfigEntry<string> CandBgColor;
    public static ConfigEntry<string> CandSelTextColor;
    public static ConfigEntry<string> CandSelBgColor;
    public static ConfigEntry<string> CandTextColor;

    public override bool Load()
    {
        ConfigDebug = Config.Bind("Visual", "Debug", false, "Debug logging / 调试日志");
        CandBgColor  = Config.Bind("Visual", "CandBgColor", "#B4000000", "Background color #AARRGGBB / 候选框背景色");
        CandSelTextColor = Config.Bind("Visual", "CandSelTextColor", "#FFFFFFFF", "Selected text color #AARRGGBB / 选中项文字色");
        CandSelBgColor   = Config.Bind("Visual", "CandSelBgColor", "#80007070", "Selected highlight color #AARRGGBB / 选中项背景色");
        CandTextColor    = Config.Bind("Visual", "CandTextColor", "#FFFFFFFF", "Default text color #AARRGGBB / 未选中文字色");

        IMEManager.UseTSF = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        IMEManager.Initialize();
        HarmonyInstance.PatchAll();
        Console.WriteLine("[HacknetIME] Loaded Successfully.");
        return true;
    }

    public override bool Unload()
    {
        IMEManager.Dispose();
        HarmonyInstance.UnpatchSelf();
        return true;
    }

    public static Color ParseColor(string hex, Color defaultColor)
    {
        if (string.IsNullOrEmpty(hex)) return defaultColor;
        try
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length == 6) hex = "FF" + hex;
            if (hex.Length != 8) return defaultColor;
            uint val = Convert.ToUInt32(hex, 16);
            return new Color(
                (byte)((val >> 16) & 0xFF),
                (byte)((val >> 8) & 0xFF),
                (byte)(val & 0xFF),
                (byte)((val >> 24) & 0xFF)
            );
        }
        catch { return defaultColor; }
    }


}

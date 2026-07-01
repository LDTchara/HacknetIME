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
        ConfigDebug = Config.Bind("视觉", "Debug", false, "调试日志开关");
        CandBgColor  = Config.Bind("视觉", "候选框背景色", "#B4000000", "候选框背景色 #AARRGGBB");
        CandSelTextColor = Config.Bind("视觉", "选中项文字色", "#FFFFFFFF", "选中项文字颜色 #AARRGGBB");
        CandSelBgColor   = Config.Bind("视觉", "选中项背景色", "#8000008B", "选中项背景颜色 #AARRGGBB");
        CandTextColor    = Config.Bind("视觉", "未选中文字色", "#FFFFFFFF", "未选中项文字颜色 #AARRGGBB");

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

using Hacknet;
using Microsoft.Xna.Framework;

namespace HacknetIME
{
    /// <summary>
    /// 输入链路诊断（仅当配置 Debug = true 时工作）。
    ///
    /// 目的：定位「某些操作后主菜单打不了字」的真实链路。诊断覆盖面：
    ///   1. IMEManager.IsActive 的判定结果及其全部输入（组合文本长度、OS 引用状态）
    ///   2. SDL 文本输入是否真的把字符送到了游戏（GuiData.TextInputHook.Buffer）
    ///   3. 两道 Prefix（getFilteredKeys / getFilteredStringInput）是拦截还是放行
    ///
    /// 输出策略：只在状态签名发生变化时打印，避免每帧刷屏；一次复现即可得到完整轨迹。
    /// </summary>
    internal static class Diagnostics
    {
        private static string lastSignature = null;
        private static int frameCounter = 0;

        /// <summary>每帧采样（挂在 Game1.Update 上）。</summary>
        internal static void Tick()
        {
            frameCounter++;
            if (frameCounter % 10 != 0) return; // 约每 10 帧采样一次

            try
            {
                var os = OS.currentInstance;
                string osDesc = os == null
                    ? "null"
                    : $"{os.GetType().Name}(state={os.ScreenState},term={(os.terminal != null)},exited={os.HasExitedAndEnded})";

                string buffer = SafeBuffer();
                int keysDown = 0;
                try { keysDown = GuiData.getKeyboadState().GetPressedKeys().Length; } catch { }

                string signature =
                    $"IsActive={IMEManager.IsActive} | comp={IMEManager.CompositionString.Length} | " +
                    $"os={osDesc} | buf={buffer.Length} | keys={keysDown} | ready={TSFManager.Initialized}";

                if (signature == lastSignature) return;
                lastSignature = signature;

                Console.WriteLine($"[IME-DIAG] {signature}");
                if (buffer.Length > 0)
                    Console.WriteLine($"[IME-DIAG]   TextInputHook.Buffer=\"{buffer}\"");
                if (IMEManager.CompositionString.Length > 0)
                    Console.WriteLine($"[IME-DIAG]   Composition=\"{IMEManager.CompositionString}\"");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IME-DIAG] tick failed: {ex.Message}");
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> lastDecision = new();

        /// <summary>记录一次 Prefix 的判定结果（拦截/放行），去重以免刷屏。</summary>
        internal static void LogDecision(string method, bool takenOver, int extraChars = -1)
        {
            if (!HacknetIME.ConfigDebug.Value) return;
            try
            {
                string buffer = SafeBuffer();
                string tail = extraChars >= 0 ? $" injected={extraChars}" : "";
                string signature = $"takeover={takenOver}|buf={buffer.Length}{tail}";

                if (lastDecision.TryGetValue(method, out string prev) && prev == signature) return;
                lastDecision[method] = signature;

                Console.WriteLine(
                    $"[IME-DIAG] {method}: {signature} IsActive={IMEManager.IsActive} " +
                    $"os={(OS.currentInstance == null ? "null" : "set")} comp={IMEManager.CompositionString.Length}");
            }
            catch { }
        }

        private static string SafeBuffer()
        {
            try
            {
                return GuiData.TextInputHook?.Buffer ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}

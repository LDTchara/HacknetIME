using System.Runtime.InteropServices;
using System.Text;
using Hacknet;
using Hacknet.Gui;
using SDL2;

namespace HacknetIME
{
    /// <summary>
    /// SDL 事件过滤器：拦截 SDL 的文本输入事件，防止与 TSF/ImeSharp 重复。
    /// 在 TSF 模式下（UseTSF = true），最终文字由 TSFManager 回调注入，这里只负责阻止原生的 TextInputHook。
    /// </summary>
    public static class IMEManager
    {
        /// <summary>当前组合文本（输入法预览）</summary>
        public static string CompositionString { get; set; } = "";

        /// <summary>是否启用 TSF 模式（影响 SDL 最终文字处理）</summary>
        public static bool UseTSF = true;

        /// <summary>
        /// 动态判断是否应该接管输入：仅在终端已打开、输入启用且未锁定（如密码模式）时才激活。
        /// 主菜单或其他界面的 TextBox 不受影响。
        /// </summary>
        public static bool IsActive
        {
            get
            {
                var os = OS.currentInstance;
                // 终端上下文是否成立：OS 存在、终端已建、未退出、且屏幕仍在 ScreenManager 列表中。
                // ══ 为什么不用 os.IsActive ══
                // ScreenManager.RemoveScreen 只做 UnloadContent + 从列表移除，不更新 screenState；
                // 被移除的屏幕不再收到 Update，screenState 永远停在 TransitionOn/Active，
                // 于是 IsActive 恒为 true（实测：Run Verification Tests 残留的测试 OS
                // 回到主菜单后仍报告 state=TransitionOn）。改为查询屏幕列表才能正确识别残留。
                bool terminalActive = os != null && os.terminal != null && !os.HasExitedAndEnded && IsScreenLive(os);

                // 组合文本只有在终端场景下才代表「正在输入」。
                // 若在非终端场景发现残留（组合被失焦/OS 生命周期变动打断，TSF 不再回调空串），
                // 就地清掉自愈 —— 否则这条短路会让 IsActive 永远为 true，
                // 吞掉主菜单等界面的全部输入。
                if (!string.IsNullOrEmpty(CompositionString))
                {
                    if (terminalActive) return true;
                    CompositionString = "";
                    TSFManager.Candidates.Clear();
                    TSFManager.CandidateSelection = 0;
                    return false;
                }

                if (!terminalActive) return false;
                if (UseTSF) return TSFManager.Initialized;
                // 非 TSF 模式：IME 就绪 + 终端可输入
                if (eventFilterDelegate == null) return false;
                return os.inputEnabled && !os.terminal.inputLocked;
            }
        }

        private static SDL.SDL_EventFilter eventFilterDelegate;
        private static IntPtr filterUserdata = IntPtr.Zero;

        /// <summary>
        /// 该屏幕是否仍在 ScreenManager 的屏幕列表中。
        /// 不能用 GameScreen.IsActive：RemoveScreen 只把屏幕移出列表、不更新 screenState，
        /// 被移除的屏幕不再收到 Update，screenState 永远停在 TransitionOn/Active，
        /// IsActive 于是恒为 true。
        /// </summary>
        private static bool IsScreenLive(GameScreen screen)
        {
            try
            {
                var sm = screen.ScreenManager;
                if (sm == null) return false;
                var screens = sm.GetScreens();
                for (int i = 0; i < screens.Length; i++)
                {
                    if (ReferenceEquals(screens[i], screen)) return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void Initialize()
        {
            if (eventFilterDelegate != null) return;
            eventFilterDelegate = EventFilter;
            SDL.SDL_AddEventWatch(eventFilterDelegate, filterUserdata);
            //if (HacknetIME.Debug) Console.WriteLine("[IMEManager] SDL event watch added.");
        }

        public static void Dispose()
        {
            if (eventFilterDelegate != null)
            {
                SDL.SDL_DelEventWatch(eventFilterDelegate, filterUserdata);
                eventFilterDelegate = null;
            }
        }

        /// <summary>
        /// SDL 事件过滤器回调。
        /// - SDL_TEXTEDITING：组合文本，TSF 模式下忽略（由 TSFManager 管理），非 TSF 模式下记录。
        /// - SDL_TEXTINPUT：最终文字，TSF 模式下直接吞掉（由 TSF 回调注入），非 TSF 模式下自己注入。
        /// </summary>
        private static int EventFilter(IntPtr userdata, IntPtr evtPtr)
        {
            var evt = (SDL.SDL_Event)Marshal.PtrToStructure(evtPtr, typeof(SDL.SDL_Event));
            switch (evt.type)
            {
                case SDL.SDL_EventType.SDL_TEXTEDITING:
                    // 只在终端活跃且 TSF 模式下才拦截（防止原版接收组合文本）
                    if (UseTSF && IsActive)
                        return 0;
                    if (!UseTSF && IsActive)
                        HandleTextEditing(evt.edit);
                    return 1; // 主菜单等其他情况放行

                case SDL.SDL_EventType.SDL_TEXTINPUT:
                    // TSF 模式下，仅在终端活跃时拦截，由 TSF 注入
                    if (UseTSF && IsActive)
                        return 0;
                    // 非 TSF 模式且终端活跃，自己处理注入
                    if (!UseTSF && IsActive)
                    {
                        HandleTextInput(evt.text);
                        return 0;
                    }
                    return 1; // 主菜单等全部放行

                default:
                    return 1;
            }
        }

        private static unsafe void HandleTextEditing(SDL.SDL_TextEditingEvent edit)
        {
            byte* textPtr = edit.text;
            int length = 0;
            while (length < 32 && textPtr[length] != 0) length++;
            CompositionString = Encoding.UTF8.GetString(textPtr, length);
        }

        private static unsafe void HandleTextInput(SDL.SDL_TextInputEvent input)
        {
            byte* textPtr = input.text;
            int length = 0;
            while (length < 32 && textPtr[length] != 0) length++;
            string final = Encoding.UTF8.GetString(textPtr, length);
            InjectText(final);
        }

        /// <summary>
        /// 将文本注入到终端光标位置。可被 TSFManager 等外部模块调用。
        /// </summary>
        public static void InjectText(string text)
        {
            try
            {
                var os = OS.currentInstance;
                if (os?.terminal == null) return;

                Terminal terminal = os.terminal;
                int pos = Math.Max(0, Math.Min(TextBox.cursorPosition, terminal.currentLine.Length));
                terminal.currentLine = terminal.currentLine.Insert(pos, text);
                TextBox.cursorPosition = pos + text.Length;

                //if (HacknetIME.Debug)
                //    Console.WriteLine($"[IMEManager] Injected \"{text}\" at pos {pos}, cursor={TextBox.cursorPosition}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IMEManager] InjectText failed: {ex.Message}");
            }
        }
    }
}
// ============================================================================
// ModSettingsHub — 把 mod 设置**直接做进游戏原生设置窗口**（2026-10-17）
//
// 朋友原话：
//   "最简单的就是根据加载的 mod 来放设置页面"
//   "一些有独立菜单快捷键的 也可以直接绑 入口 就放在设置里"
//   "直接做进设置页面里 就不用外面一排按钮了"
//
// 做法（★不卸载场景、不克隆整窗 ⇒ 没有模态/模糊/退出键三笔账）：
//   ① 设置场景加载后，往左侧图标列 `SideButtons/ButtonLayout`（VLG）里克隆原生页签，
//      每个 mod 一颗（图案由 mod 提供；自动配置页用默认网格图）；
//   ② 点我们的图标 ⇒ 在 `P_SwitchSettingsWindowObject/WindowParent` 上盖一层自己的整页
//      （不透明、最上层、自带 raycast）⇒ 不碰原生 _windowDictionary / UpdateWindow；
//   ③ 点任何原生页签 ⇒ 我们自己加的 onClick 监听先把覆盖层收起来（原生行为不受影响）；
//   ④ 条目多了：左侧列高度自动增长/压缩间距（自动扩充）。
//
// 三个设置来源（优先级从高到低）：
//   A. IModSettingsPage（mod 自己 Build，可实时）  → 注册进 ModSettingsHub.Api
//   B. IModSettingsEntry（mod 已有自己的面板，只给一颗入口）→ 点了交给 mod
//   C. MelonPreferences 自动生成（零改动，改完需重启）
// ============================================================================
using System;
using MelonLoader;
using MelonLoader.Utils;
using ModSettingsHub.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(ModSettingsHub.ModSettingsHubMod), "ModSettingsHub", "0.2.0", "XingMang")]
[assembly: MelonGame("gogh Japan", "gogh")]


namespace ModSettingsHub
{
    public partial class ModSettingsHubMod : MelonMod
    {
        internal const string SettingsSceneName = "S_SettingsScene";

        float _scanAt = -1f;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("v0.2.0 ready. writes ModSettingsHub entries into the game's own settings window.");
            InitInputLock();
            try
            {
                string api = System.IO.Path.Combine(MelonEnvironment.GameRootDirectory, "UserLibs", "ModSettingsHub.Api.dll");
                MelonLogger.Msg("api dll: " + (System.IO.File.Exists(api) ? "ok" : "MISSING") + " (" + api + ")");
            }
            catch { }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                ResetSpriteCache();   // 每个场景都重扫内存里的原生 Sprite（它们带九宫 border）
                if (!string.IsNullOrEmpty(sceneName)
                    && sceneName.IndexOf(SettingsSceneName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // 新设置场景：旧引用全失效
                    ContentRoot = null; MainRoot = null; SideButtons = null; ButtonLayout = null; WindowParent = null;
                    _entrySignature = "";          // ★必须清：否则新场景里 RefreshEntries 认为"没变化"⇒ 不再注入
                    Entries.Clear();
                    ResetOverlay();
                    ResetButtonTracking();
                    _scanAt = Time.unscaledTime + 0.35f;
                    LogOnce("scene", "settings scene loaded -> will inject after 0.35s");
                }
                else if (!string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("S_Room", StringComparison.Ordinal))
                {
                    _roomMode = false;
                    _uiHost = null;
                    _uiCanvas = null;
                    _roomCanvas = null;
                    _roomMenu = null;
                    _roomEntry = null;
                    _roomSceneName = null;
                    ResetOverlay();
                    ResetButtonTracking();
                }
            }
            catch (Exception e) { MelonLogger.Error("scene hook: " + e.Message); }
        }

        public override void OnUpdate()
        {
            try
            {
                TickHotkeyCapture();     // 改键捕获必须每帧（不受 1s tick 限流）

                float now = Time.unscaledTime;
                if (now < _scanAt) return;
                _scanAt = now + 1f;

                TickRoom();          // 房间入口 + 独立设置窗口（全屏遮罩，不再进游戏设置场景）
                TickSettingsScene(); // 游戏原生设置场景：左列红点入口 + 下拉选 mod

                // ★保险：设置场景不在了（点空白/关闭设置）⇒ 立刻解除相机输入锁 + 热键挂起
                if (InputLocked || HotkeyStore.Suspended)
                {
                    var sc = SceneManager.GetSceneByName(SettingsSceneName);
                    bool gone = !sc.IsValid() || !sc.isLoaded || _overlay == null || !_overlay.activeSelf;
                    if (gone) { InputLocked = false; HotkeyStore.Suspended = false; }
                }
            }
            catch (Exception e) { LogOnce("tickerr", "tick err " + e); }
        }

    }
}

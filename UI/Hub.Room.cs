// ============================================================================
// ModSettingsHub — 房间顶部入口 + 独立 MOD 设置窗口
//
// 用户要求（2026-10-17 晚）：
//   "像这个设置一样你自己做一个设置入口，然后在里面放 mod（要自己创建一个设置）"
//   ⇒ 入口放在房间顶部那排原生按钮里；点开是**我们自己的**设置窗口（不碰游戏设置场景）。
//
// 做法：
//   ① 入口：在 `SceneContext/CommonCanvas/UIPartsGroup/Header/P_RoomTopRightMenuObject`（HLG）
//      末尾加一颗 56×56 圆底按钮（原生 T_WhiteCircle 染成齿轮同色 + 网格图标）；
//   ② 窗口：在 `CommonCanvas` 下建 768×840 的独立窗口（原生 BG_Graphics + 标题栏 + 关闭），
//      左边 mod 列表、右边设置内容（复用 Hub.Home 的 BuildList/SelectEntry）；
//   ③ 不改游戏设置场景、不加载/卸载任何场景 ⇒ 没有模态/模糊/退出键三笔账；
//   ④ 入口按钮每进一次房间重建（场景卸载会随场景销毁）。
// ============================================================================
using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ModSettingsHub
{
    public partial class ModSettingsHubMod
    {
        Transform _roomCanvas;       // SceneContext/CommonCanvas
        Transform _roomMenu;         // .../P_RoomTopRightMenuObject
        GameObject _roomEntry;       // 我们加的入口按钮
        string _roomSceneName;

        bool _roomMode;              // true = 窗口挂在房间 Canvas（独立窗口），false = 设置场景覆盖层
        Transform _uiHost;           // 当前窗口的父节点

        void TickRoom()
        {
            // ★已迁移到游戏原生设置（左列入口 + 下拉选 mod）：
            //   房间内不再加药丸入口、也不再浮独立窗口。代码保留以便回滚。
        }

        bool FindRoomUi()
        {
            try
            {
                int n = SceneManager.sceneCount;
                for (int i = 0; i < n; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (!sc.IsValid() || !sc.isLoaded) continue;
                    string sn = sc.name ?? "";
                    if (!sn.StartsWith("S_Room", StringComparison.Ordinal)) continue;
                    if (sn.IndexOf("Snap", StringComparison.OrdinalIgnoreCase) >= 0) continue;   // 拍照场景不算

                    var roots = sc.GetRootGameObjects();
                    if (roots == null || roots.Length == 0) continue;

                    Transform canvas = null;
                    for (int r = 0; r < roots.Length; r++)
                    {
                        try
                        {
                            if (roots[r] == null) continue;
                            var c = roots[r].transform.Find("CommonCanvas");
                            if (c != null) { canvas = c; break; }
                        }
                        catch { }
                    }
                    if (canvas == null) canvas = DeepFind(roots, "CommonCanvas");
                    if (canvas == null) continue;

                    Transform menu = null;
                    try { menu = canvas.Find("UIPartsGroup/Header/P_RoomTopRightMenuObject"); } catch { }
                    if (menu == null) menu = DeepFind(canvas, "P_RoomTopRightMenuObject");
                    if (menu == null) continue;

                    if (_roomSceneName != sn || _roomCanvas == null || _roomCanvas.gameObject == null)
                    {
                        _roomSceneName = sn;
                        _roomCanvas = canvas;
                        _roomMenu = menu;
                        _roomEntry = null;
                        LogOnce("roomui:" + sn, "room ui found: scene='" + sn + "' menu='" + NodePath(menu) + "'");
                    }
                    return true;
                }
            }
            catch (Exception e) { LogOnce("roomfind", "find room err " + e.Message); }
            return false;
        }

        void EnsureRoomEntry()
        {
            try
            {
                if (_roomMenu == null) return;
                Transform host = FindRoomEntryHost();
                if (host == null) return;
                if (_roomEntry != null && _roomEntry.transform.parent == host) return;

                // 清掉可能残留的旧节点（场景重载/换父级/上次建在顶部栏）
                try
                {
                    var old = DeepFind(_roomMenu, "XM_ModSettingsEntry");
                    if (old != null) { UnityEngine.Object.Destroy(old.gameObject); }
                }
                catch { }

                var go = new GameObject("XM_ModSettingsEntry");
                var rt = go.AddComponent<RectTransform>();
                rt.SetParent(host, false);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(56f, 56f);
                rt.anchoredPosition = Vector2.zero;

                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = 56f;
                le.preferredHeight = 56f;
                le.minWidth = 56f;
                le.minHeight = 56f;

                var img = go.AddComponent<Image>();
                // 只留胶囊底：自己的圆底不画（透明点击层）
                img.sprite = null;
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = true;

                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener((UnityAction)ToggleRoomWindow);

                var icon = new GameObject("Icon");
                var irt = icon.AddComponent<RectTransform>();
                irt.SetParent(go.transform, false);
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(28f, 28f);
                irt.anchoredPosition = Vector2.zero;
                var iimg = icon.AddComponent<Image>();
                var grid = GetSprite("XM_ModGrid");
                if (grid != null) { iimg.sprite = grid; iimg.preserveAspect = true; }
                iimg.color = Color.white;
                iimg.raycastTarget = false;

                go.transform.SetAsLastSibling();

                // 挂在 QualityPlus 的展开药丸里：插到【画质】和【设置】之间（= 设置放最右），并收紧间距
                if (host.name == "XMEntryPill")
                {
                    go.transform.SetSiblingIndex(1);
                    float spacing = 8f, padX = 14f;
                    var hlg = host.GetComponent<HorizontalLayoutGroup>();
                    if (hlg != null)
                    {
                        hlg.spacing = spacing;
                        hlg.padding = new RectOffset((int)padX, (int)padX, 0, 0);
                    }
                    var prt = host.GetComponent<RectTransform>();
                    if (prt != null)
                    {
                        int n = host.childCount;
                        float w = 56f * n + spacing * (n - 1) + padX * 2f;
                        prt.sizeDelta = new Vector2(w, prt.sizeDelta.y);
                    }
                }

                _roomEntry = go;
                LogOnce("roomentry:" + host.name, "room entry created under '" + NodePath(host) + "' (sibling=" + go.transform.GetSiblingIndex() + ")");
            }
            catch (Exception e) { MelonLogger.Error("room entry: " + e); }
        }

        // 入口优先放进 QualityPlus 的展开药丸（和【画质】并排）；没装才退回顶部工具栏。
        Transform FindRoomEntryHost()
        {
            try
            {
                var pill = DeepFind(_roomMenu, "XMEntryPill");
                if (pill != null) return pill;
            }
            catch { }
            return _roomMenu;
        }

        void ToggleRoomWindow()
        {
            try
            {
                if (_overlay != null && _overlay.activeSelf)
                {
                    HideOverlay();
                    return;
                }
                _roomMode = true;
                _uiHost = _roomCanvas;
                ShowHubHome();
            }
            catch (Exception e) { MelonLogger.Error("toggle room window: " + e); }
        }

        // 设置场景里的红点入口：再点一次收起
        void ToggleSettingsHub()
        {
            try
            {
                // 原生状态机模式：按钮只负责选中 Hub 页，不再 toggle 关闭。
                // 关闭由关闭按钮或切换到其它原生侧边按钮完成（与原生 tab 行为一致）。
                if (!_roomMode && _nativeHubReady)
                {
                    if (_openId == "hub") return;      // 已经选中：像原生 tab 一样无操作
                    if (OpenHubNative()) return;
                }

                // 手动回退路径（原生注册失败 / room 模式）
                if (!_roomMode && _overlay != null && _overlay.activeSelf) { HideOverlay(); return; }
                _roomMode = false;
                _uiHost = WindowParent;
                ShowHubHome();
            }
            catch (Exception e) { MelonLogger.Error("toggle settings hub: " + e); }
        }

        // 与游戏设置完全一样：在**另一个场景**（S_SettingsScene）里显示，不浮在房间上。
        bool _pendingOpenHub;

        void OpenModSettingsScene()
        {
            try
            {
                _pendingOpenHub = true;
                var sc = SceneManager.GetSceneByName(SettingsSceneName);
                if (sc.IsValid() && sc.isLoaded)
                {
                    LogOnce("opensettings:already", "settings scene already loaded -> open hub page");
                    return;
                }
                Addressables.LoadSceneAsync(SettingsSceneName, LoadSceneMode.Additive, true, 100);
                LogOnce("opensettings:load", "loading " + SettingsSceneName + " for the mod settings page");
            }
            catch (Exception e) { MelonLogger.Error("open settings scene: " + e); }
        }

        void TickSettingsScene()
        {
            try
            {
                if (!FindSettingsUi()) return;

                RefreshEntries();
                if (ButtonLayout != null && ButtonLayout.Find("XM_ModSettings_hub") == null) RebuildButtons();
                EnsureNativeButtonHooks();

                if (_overlay == null) EnsureOverlay();
                TrySetupNativeHubPage();
                SyncNativeHubState();

                if (_pendingOpenHub && (_overlay == null || !_overlay.activeSelf))
                {
                    _pendingOpenHub = false;
                    _roomMode = false;
                    _uiHost = WindowParent;
                    if (!_nativeHubReady || !OpenHubNative()) ShowHubHome();
                    LogOnce("opensettings:shown", "mod settings page shown inside the game's own settings scene");
                }
            }
            catch (Exception e) { LogOnce("ticksettings", "tick settings err " + e.Message); }
        }
    }
}

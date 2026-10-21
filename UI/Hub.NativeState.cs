// ============================================================================
// ModSettingsHub — 把 Hub 页面注册进原生状态机（强类型版）
//   假 WindowType = 1000，向 SwitchSettingsWindowObjectView 的两张表注册：
//     _windowDictionary             : WindowType -> CanvasGroup
//     _canvasGroupAlphaControllers  : WindowType -> CanvasGroupAlphaController
//   之后用原生 IRequestPusher<UpdateCurrentWindowRequest> 切页，
//   淡入淡出 / IsWindowUpdating / 侧边按钮高亮全部由原生状态机负责。
// ============================================================================
using System;
using System.Reflection;
using Il2CppProject;
using Il2CppProject.SettingsScene;
using Il2CppProject.SettingsScene.SettingsSideButtonObject;
using Il2CppProject.SettingsScene.SwitchSettingsWindowObject;
using Il2CppTanitakaTech.UnityProcessManager;
using ModSettingsHub.Api;
using UnityEngine;

namespace ModSettingsHub
{
    public partial class ModSettingsHubMod
    {
        const int FakeWindowTypeValue = 1000;

        bool _nativeHubReady;
        WindowType _fakeWindowType = (WindowType)FakeWindowTypeValue;
        WindowType _lastNativeWindow;
        bool _hasLastNativeWindow;
        SwitchSettingsWindowObjectView _nativeView;
        IRequestPusher<UpdateCurrentWindowRequest> _nativePusher;

        bool IsHubWindow(WindowType w)
        {
            try { return (int)w == FakeWindowTypeValue; }
            catch { return false; }
        }

        WindowType ReadCurrentNativeWindow()
        {
            try { return _nativeView != null ? _nativeView._currentWindow : default(WindowType); }
            catch { return default(WindowType); }
        }

        /// <summary>覆盖层建好后调用；把它的 CanvasGroup 注册成假 WindowType 页面。</summary>
        void TrySetupNativeHubPage()
        {
            if (_nativeHubReady) return;
            try
            {
                if (WindowParent == null || _overlay == null) return;

                var view = WindowParent.GetComponentInParent<SwitchSettingsWindowObjectView>();
                if (view == null)
                {
                    LogOnce("nsh:view", "native hub page: SwitchSettingsWindowObjectView not found");
                    return;
                }

                var dict = view._windowDictionary;
                var controllers = view._canvasGroupAlphaControllers;
                if (dict == null || controllers == null) return;   // 还没初始化完，下个 tick 重试

                var cg = _overlay.GetComponent<CanvasGroup>();
                if (cg == null) return;

                var fake = (WindowType)FakeWindowTypeValue;
                dict[fake] = cg;

                var ctrl = new CanvasGroupAlphaController();
                ctrl.Init(cg, _overlay);
                controllers[fake] = ctrl;

                // 默认隐藏；由原生 UpdateWindow 负责显示
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
                _overlay.SetActive(false);

                _fakeWindowType = fake;
                _nativeView = view;
                TryGetNativePusher();
                _nativeHubReady = true;
                LogOnce("nsh:ok", "native hub page registered (fake WindowType=" + FakeWindowTypeValue
                    + ", pusher=" + (_nativePusher != null) + ")");
            }
            catch (Exception e)
            {
                LogOnce("nsh:err", "native hub page setup failed: " + e.Message);
            }
        }

        void TryGetNativePusher()
        {
            try
            {
                if (ButtonLayout == null || _nativePusher != null) return;

                var installers = ButtonLayout.GetComponentsInChildren<SettingsSideButtonObjectInstaller>(true);
                if (installers == null) return;

                var p = typeof(SettingsSideButtonObjectInstaller).GetProperty(
                    "_updateCurrentWindowRequestPusher",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p == null) return;

                for (int i = 0; i < installers.Length; i++)
                {
                    var inst = installers[i];
                    if (inst == null) continue;
                    var pusher = p.GetValue(inst) as IRequestPusher<UpdateCurrentWindowRequest>;
                    if (pusher != null) { _nativePusher = pusher; return; }
                }
            }
            catch { }
        }

        bool PushNativeWindow(WindowType windowType)
        {
            try
            {
                if (_nativePusher == null) return false;
                _nativePusher.PushRequest(new UpdateCurrentWindowRequest(windowType));
                return true;
            }
            catch (Exception e)
            {
                LogOnce("nsh:push", "native hub page push failed: " + e.Message);
                return false;
            }
        }

        WindowType PickDefaultNativeWindow()
        {
            try
            {
                if (_nativeView == null || _nativeView._windowDictionary == null) return default(WindowType);
                var keys = _nativeView._windowDictionary.Keys;
                if (keys == null) return default(WindowType);

                var enumerable = keys as System.Collections.IEnumerable;
                if (enumerable == null) return default(WindowType);
                foreach (var obj in enumerable)
                {
                    if (!(obj is WindowType)) continue;
                    var k = (WindowType)obj;
                    if (!IsHubWindow(k)) return k;
                }
            }
            catch { }
            return default(WindowType);
        }

        bool OpenHubNative()
        {
            try
            {
                if (!_nativeHubReady) return false;

                var cur = ReadCurrentNativeWindow();
                if (!IsHubWindow(cur))
                {
                    _lastNativeWindow = cur;
                    _hasLastNativeWindow = true;
                }

                BuildHubHomeContent();
                _openId = "hub";
                HotkeyStore.Suspended = true;
                HighlightButton();

                var cg = _overlay != null ? _overlay.GetComponent<CanvasGroup>() : null;
                if (cg != null) { cg.blocksRaycasts = true; cg.interactable = true; }

                if (PushNativeWindow(_fakeWindowType)) return true;

                _openId = null;
                HotkeyStore.Suspended = false;
                return false;
            }
            catch (Exception e)
            {
                LogOnce("nsh:open", "native hub open failed: " + e.Message);
                return false;
            }
        }

        bool CloseHubNative()
        {
            try
            {
                if (!_nativeHubReady || _openId != "hub") return false;

                var back = _hasLastNativeWindow ? _lastNativeWindow : PickDefaultNativeWindow();
                CancelHotkeyCapture();
                CloseModPicker();

                _openId = null;
                HotkeyStore.Suspended = false;
                HighlightButton();

                var cg = _overlay != null ? _overlay.GetComponent<CanvasGroup>() : null;
                if (cg != null) { cg.blocksRaycasts = false; cg.interactable = false; }

                PushNativeWindow(back);
                return true;
            }
            catch (Exception e)
            {
                LogOnce("nsh:close", "native hub close failed: " + e.Message);
                return false;
            }
        }

        /// <summary>每 tick 同步：原生切到/切离假 WindowType 时更新我们的 UI 状态。</summary>
        void SyncNativeHubState()
        {
            if (!_nativeHubReady) return;
            try
            {
                var cur = ReadCurrentNativeWindow();
                bool isHub = IsHubWindow(cur);

                if (isHub)
                {
                    var hubCg = _overlay != null ? _overlay.GetComponent<CanvasGroup>() : null;
                    if (hubCg != null) { hubCg.blocksRaycasts = true; hubCg.interactable = true; }

                    if (_openId != "hub")
                    {
                        _openId = "hub";
                        HotkeyStore.Suspended = true;
                        HighlightButton();
                    }
                    if (hubCg != null && hubCg.alpha < 0.01f)
                        LogOnce("nsh:notshown", "native hub selected but overlay alpha=0 (UpdateWindow ignored fake WindowType?)");
                }
                else if (_openId == "hub")
                {
                    _openId = null;
                    HotkeyStore.Suspended = false;
                    CancelHotkeyCapture();
                    CloseModPicker();
                    HighlightButton();

                    var cg = _overlay != null ? _overlay.GetComponent<CanvasGroup>() : null;
                    if (cg != null) { cg.blocksRaycasts = false; cg.interactable = false; }

                    // 只记录，不强行改 alpha：让原生淡出正常完成
                    if (_overlay != null && _overlay.activeSelf && cg != null && cg.alpha > 0.01f)
                        LogOnce("nsh:leak", "native hub page still visible after switching away, alpha=" + cg.alpha);
                }
            }
            catch { }
        }

        /// <summary>原生侧边按钮点击：原生 Presenter 会自己推送目标页，这里只清我们的 UI 状态。</summary>
        void OnNativeSideButtonClicked()
        {
            if (_nativeHubReady)
            {
                if (_openId == "hub")
                {
                    _openId = null;
                    HotkeyStore.Suspended = false;
                    CancelHotkeyCapture();
                    CloseModPicker();
                    HighlightButton();
                }
                return;
            }
            HideOverlay();
        }

        void ClearNativeHubState()
        {
            _nativeHubReady = false;
            _fakeWindowType = (WindowType)FakeWindowTypeValue;
            _hasLastNativeWindow = false;
            _nativeView = null;
            _nativePusher = null;
        }
    }
}

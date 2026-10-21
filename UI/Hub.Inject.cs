// ============================================================================
// ModSettingsHub — 注入左侧页签 + 整页覆盖层（不碰原生 _windowDictionary）
// ============================================================================
using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using ModSettingsHub.Api;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ModSettingsHub
{
    public partial class ModSettingsHubMod
    {
        // ---- 注入 ----
        const string NativeButtonPrefix = "P_SettingsSideButtonObject_";
        const string OurButtonPrefix = "XM_ModSettings_";

        // ---- 固定窗口尺寸：锚点/位置跟原生页对齐，尺寸由我们决定 ----
        static readonly Vector2 OverlaySize = new Vector2(768f, 840f);

        readonly HashSet<IntPtr> _hookedNativeButtons = new HashSet<IntPtr>();
        Transform _buttonTemplate;

        // ---- 覆盖层 ----
        GameObject _overlay;
        GameObject _backdrop;
        GameObject _uiCanvas;      // ScreenSpaceOverlay Canvas（盖住房间所有 UI）
        RectTransform _overlayRt;
        RectTransform _overlayContent;
        Il2CppTMPro.TextMeshProUGUI _overlayTitle;
        string _openId;

        // ===================== 页签图标 =====================
        void RebuildButtons()
        {
            try
            {
                if (ButtonLayout == null) return;
                _buttonTemplate = FindButtonTemplate();
                if (_buttonTemplate == null)
                {
                    LogOnce("notpl", "no native side-button template found under '" + NodePath(ButtonLayout) + "'");
                    return;
                }

                // 先删掉旧的（我们自己的）
                var old = new List<GameObject>();
                for (int i = 0; i < ButtonLayout.childCount; i++)
                {
                    var ch = ButtonLayout.GetChild(i);
                    if (ch != null && ch.name != null && ch.name.StartsWith(OurButtonPrefix, StringComparison.Ordinal))
                        old.Add(ch.gameObject);
                }
                for (int i = 0; i < old.Count; i++)
                {
                    try { old[i].transform.SetParent(null, false); } catch { }   // 先离开布局，避免本帧高度算多
                    try { UnityEngine.Object.Destroy(old[i]); } catch { }
                }
                _hubButtonImage = null;
                ClearHubButtonWiring();

                // ★只注入**一颗**【模组】图标：点开是"mod 列表 + 选中项设置"，
                //   不再给每个 cfg 分类都插一颗（实测 20+ 颗会把左侧列撑到屏幕外）。
                var go = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, ButtonLayout) as GameObject;
                if (go != null)
                {
                    go.name = OurButtonPrefix + "hub";
                    go.SetActive(true);

                    var btn = go.GetComponentInChildren<Button>(true);
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        try { btn.transition = Selectable.Transition.None; } catch { }

                        var img = btn.GetComponent<Image>();
                        if (img != null)
                        {
                            // 原生侧边按钮的图标位；InitView 会在 matched/unmatched 间切换。
                            var grid = GetSprite("XM_ModGrid");
                            if (grid != null) { img.sprite = grid; img.type = Image.Type.Simple; img.preserveAspect = true; }
                            img.color = new Color(1f, 1f, 1f, 0.55f);
                            img.raycastTarget = true;
                            _hubButtonImage = img;
                        }

                        // 复用原生 View：hover Tips / 点击反馈 / 选中态（用我们自己的 bool）
                        TryWireNativeSideButton(go, _buttonTemplate);

                        // 点击放在最后，避免被 InitView/InitBehaviour 的初始化覆盖
                        btn.onClick.AddListener((UnityAction)ToggleSettingsHub);
                        try
                        {
                            var tip = go.transform.Find("Tips/CommonLocalizeText/Text (TMP)");
                            if (tip != null) SetTmp(tip, "MOD 设置");
                        }
                        catch { }
                    }
                    else LogOnce("nobtn", "cloned side button has no Button");
                }

                ResizeSideColumn();
                HighlightButton();
                LogOnce("injected", "injected hub button; sideButtons size=" + RtSize(SideButtons)
                    + " native=" + CountNativeButtons() + " entries=" + Entries.Count);
            }
            catch (Exception e) { MelonLogger.Error("rebuild buttons: " + e); }
        }

        Transform FindButtonTemplate()
        {
            try
            {
                if (ButtonLayout == null) return null;
                Transform first = null;
                for (int i = 0; i < ButtonLayout.childCount; i++)
                {
                    var ch = ButtonLayout.GetChild(i);
                    if (ch == null || ch.name == null) continue;
                    if (!ch.name.StartsWith(NativeButtonPrefix, StringComparison.Ordinal)) continue;
                    if (first == null) first = ch;
                    if (ch.name.IndexOf("Graphics", StringComparison.Ordinal) >= 0) return ch;   // 有完整图标的那颗
                }
                return first;
            }
            catch { return null; }
        }

        int CountNativeButtons()
        {
            try
            {
                int n = 0;
                for (int i = 0; i < ButtonLayout.childCount; i++)
                {
                    var ch = ButtonLayout.GetChild(i);
                    if (ch != null && ch.name != null && ch.name.StartsWith(NativeButtonPrefix, StringComparison.Ordinal)) n++;
                }
                return n;
            }
            catch { return 0; }
        }

        // 自动扩充：原生 8 颗 + 我们的 N 颗；太高就压缩间距
        void ResizeSideColumn()
        {
            try
            {
                if (SideButtons == null || ButtonLayout == null) return;
                var vlg = ButtonLayout.GetComponent<VerticalLayoutGroup>();
                int n = ButtonLayout.childCount;
                if (n <= 0) return;

                float spacing = 20f;
                float h = n * 24f + (n - 1) * spacing + 36f;      // 36 = 原生件上下留白
                if (h > 900f)
                {
                    spacing = Mathf.Max(6f, (900f - 36f - n * 24f) / Mathf.Max(1, n - 1));
                    h = n * 24f + (n - 1) * spacing + 36f;
                }
                if (vlg != null) vlg.spacing = spacing;
                var rt = SideButtons.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x <= 0f ? 56f : rt.sizeDelta.x, Mathf.Min(h, 900f));
                }
            }
            catch (Exception e) { LogOnce("resize", "resize side col err " + e.Message); }
        }

        // ===================== 原生页签点击 → 收起覆盖层 =====================
        void EnsureNativeButtonHooks()
        {
            try
            {
                if (ButtonLayout == null) return;
                for (int i = 0; i < ButtonLayout.childCount; i++)
                {
                    var ch = ButtonLayout.GetChild(i);
                    if (ch == null || ch.name == null) continue;
                    if (!ch.name.StartsWith(NativeButtonPrefix, StringComparison.Ordinal)) continue;
                    var btn = ch.GetComponentInChildren<Button>(true);
                    if (btn == null) continue;
                    var ob = btn as Il2CppObjectBase;
                    if (ob == null) continue;
                    IntPtr p = ob.Pointer;
                    if (p == IntPtr.Zero || !_hookedNativeButtons.Add(p)) continue;
                    btn.onClick.AddListener((UnityAction)OnNativeSideButtonClicked);
                }
            }
            catch (Exception e) { LogOnce("hooknative", "native hook err " + e.Message); }
        }

        void ResetButtonTracking()
        {
            _hookedNativeButtons.Clear();
            _buttonTemplate = null;
            _hubButtonImage = null;
            ClearHubButtonWiring();
            ClearNativeHubState();
        }

        // ===================== 点击 =====================
        void OnEntryClick(HubEntry e)
        {
            try
            {
                if (e == null) return;
                if (e.Entry != null)
                {
                    HideOverlay();
                    try { e.Entry.OnClick(); } catch (Exception ex) { MelonLogger.Error("entry click '" + e.Id + "': " + ex.Message); }
                    return;
                }
                ShowOverlay(e);
            }
            catch (Exception ex) { MelonLogger.Error("entry click: " + ex); }
        }

        void ShowOverlay(HubEntry e)
        {
            ShowHubHome();
            if (e != null) SelectEntry(e);
        }

        void BuildError(HubEntry e, string msg)
        {
            try { AddLabel(_overlayContent, msg, 20, new Color(1f, 0.55f, 0.55f, 1f)); } catch { }
        }

        void HideOverlay()
        {
            try
            {
                // 原生状态机模式：关闭按钮 = 切回上一个原生页，显隐交给原生 UpdateWindow
                if (!_roomMode && CloseHubNative()) return;

                MelonLogger.Msg("hub: close/hide window");
                CancelHotkeyCapture();
                CloseModPicker();
                HotkeyStore.Suspended = false;
                HideNativePages(false);
                InputLocked = false;
                if (_hiddenRoomCanvases.Count > 0) HideRoomUi(false);
                if (_overlay != null) _overlay.SetActive(false);
                if (_backdrop != null) _backdrop.SetActive(false);
                _openId = null;
                HighlightButton();
            }
            catch { }
        }

        void ResetOverlay()
        {
            ClearNativeHubState();
            InputLocked = false;
            HotkeyStore.Suspended = false;
            if (_hiddenRoomCanvases.Count > 0) HideRoomUi(false);
            _hiddenNativePages.Clear();
            _overlay = null;
            _backdrop = null;
            _uiCanvas = null;
            _overlayRt = null;
            _overlayContent = null;
            _overlayTitle = null;
            _openId = null;
        }

        void EnsureOverlayAlive()
        {
            try
            {
                if (_overlay == null) return;
                Transform host = _roomMode ? (_uiCanvas != null ? _uiCanvas.transform : null) : WindowParent;
                if (host == null || _overlay.transform.parent != host) ResetOverlay();
            }
            catch { ResetOverlay(); }
        }

        void HighlightButton()
        {
            try
            {
                if (_hubButtonImage == null) return;
                bool on = _openId != null;
                _hubButtonImage.color = on ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                SetHubButtonSelected(on);
            }
            catch { }
        }

        // 房间模式：自建 ScreenSpaceOverlay Canvas，盖住房间所有 UI（含顶部工具栏/左右侧栏/底部播放条）
        Transform EnsureUiCanvas()
        {
            try
            {
                if (_uiCanvas != null) return _uiCanvas.transform;
                var go = new GameObject("XM_ModSettingsCanvas");
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 30000;
                go.AddComponent<GraphicRaycaster>();
                var cg = go.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = true;
                cg.interactable = true;
                if (_roomCanvas != null) go.transform.SetParent(_roomCanvas.root, false);
                SetLayerRecursively(go, LayerMask.NameToLayer("UI"));
                _uiCanvas = go;
                LogOnce("uicanvas", "overlay canvas created (ScreenSpaceOverlay, sorting 30000)");
                return go.transform;
            }
            catch (Exception e) { MelonLogger.Error("ui canvas: " + e); return null; }
        }

        // 房间 UI 全部关掉（像设置场景一样只剩 3D 背景 + 我们的窗口）
        readonly List<Canvas> _hiddenRoomCanvases = new List<Canvas>();

        void HideRoomUi(bool hide)
        {
            try
            {
                if (hide)
                {
                    if (_hiddenRoomCanvases.Count > 0) return;
                    var all = Resources.FindObjectsOfTypeAll<Canvas>();
                    if (all == null) return;
                    for (int i = 0; i < all.Length; i++)
                    {
                        var c = all[i];
                        if (c == null || c.gameObject == null) continue;
                        if (_uiCanvas != null && c.gameObject == _uiCanvas) continue;
                        var sc = c.gameObject.scene;
                        if (!sc.IsValid() || !sc.isLoaded) continue;
                        if (string.IsNullOrEmpty(sc.name) || !sc.name.StartsWith("S_Room", StringComparison.Ordinal)) continue;
                        if (!c.enabled) continue;
                        _hiddenRoomCanvases.Add(c);
                        c.enabled = false;
                    }
                    LogOnce("hideui", "room ui hidden: " + _hiddenRoomCanvases.Count + " canvas(es)");
                }
                else
                {
                    for (int i = 0; i < _hiddenRoomCanvases.Count; i++)
                    {
                        try { if (_hiddenRoomCanvases[i] != null) _hiddenRoomCanvases[i].enabled = true; } catch { }
                    }
                    _hiddenRoomCanvases.Clear();
                }
            }
            catch (Exception e) { LogOnce("hideuierr", "hide room ui err " + e.Message); }
        }

        // ===================== 覆盖层骨架 =====================
        // 只对齐原生页的锚点 / pivot / 位置；尺寸固定为 OverlaySize，不跟随不同页面变化。
        void AlignOverlayToActiveNativePage()
        {
            try
            {
                if (WindowParent == null || _overlayRt == null) return;
                Transform best = null;
                float bestArea = 0f;
                for (int i = 0; i < WindowParent.childCount; i++)
                {
                    var ch = WindowParent.GetChild(i);
                    if (ch == null || ch == _overlayRt) continue;
                    if (string.IsNullOrEmpty(ch.name) || !ch.name.StartsWith("P_", StringComparison.Ordinal)) continue;
                    var cg = ch.GetComponent<CanvasGroup>();
                    if (cg != null && cg.alpha < 0.05f) continue;
                    var rt = ch.GetComponent<RectTransform>();
                    if (rt == null) continue;
                    float area = Mathf.Abs(rt.rect.width * rt.rect.height);
                    if (area > bestArea) { bestArea = area; best = ch; }
                }
                if (best != null)
                {
                    var brt = best.GetComponent<RectTransform>();
                    // ★只复制对齐方式：锚点 / pivot / 位置。
                    _overlayRt.anchorMin = brt.anchorMin;
                    _overlayRt.anchorMax = brt.anchorMax;
                    _overlayRt.pivot = brt.pivot;
                    _overlayRt.anchoredPosition = brt.anchoredPosition;
                    LogOnce("align:" + best.name,
                        "overlay aligned to '" + best.name + "' anchors=" + brt.anchorMin + ".." + brt.anchorMax
                        + " pivot=" + brt.pivot);
                }

                // ★尺寸始终固定，跟原生页无关。
                _overlayRt.sizeDelta = OverlaySize;
            }
            catch (Exception e) { LogOnce("alignerr", "align err " + e.Message); }
        }

        void EnsureOverlay()
        {
            try
            {
                Transform host = WindowParent;   // 只做原生设置里的内嵌页，不再建独立 Canvas
                if (host == null) return;
                if (_overlay != null && _overlay.transform.parent == host) return;

                // 房间模式：先铺一层全屏深色遮罩（效果等同"进到另一个场景"，看不到房间草地）
                if (_roomMode && (_backdrop == null || _backdrop == null))
                {
                    var bd = NewRect("XM_ModSettingsBackdrop", host);
                    bd.anchorMin = Vector2.zero;
                    bd.anchorMax = Vector2.one;
                    bd.offsetMin = Vector2.zero;
                    bd.offsetMax = Vector2.zero;
                    var bimg = bd.gameObject.AddComponent<Image>();
                    bimg.color = new Color(0f, 0.02f, 0.04f, 0.55f);
                    bimg.raycastTarget = true;
                    _backdrop = bd.gameObject;
                    SetLayerRecursively(_backdrop, LayerMask.NameToLayer("UI"));
                }

                _overlay = new GameObject("XM_ModSettingsOverlay");
                _overlay.AddComponent<RectTransform>();
                _overlay.AddComponent<Image>();
                _overlay.AddComponent<CanvasGroup>();
                _overlay.transform.SetParent(host, false);
                // 清掉旧版残留的星（用户："mod 名字和星星重叠了"）
                try { var oldStar = _overlay.transform.Find("Star"); if (oldStar != null) UnityEngine.Object.Destroy(oldStar.gameObject); } catch { }
                _overlayRt = _overlay.GetComponent<RectTransform>();
                Transform windowNode = _overlay.transform;
                Transform listNode = _overlay.transform;

                // 固定尺寸 OverlaySize；设置场景的对齐方式随后由
                // AlignOverlayToActiveNativePage() 从当前原生页复制。房间模式（遗留）居中。
                if (_roomMode)
                {
                    _overlayRt.anchorMin = new Vector2(0.5f, 0.5f);
                    _overlayRt.anchorMax = new Vector2(0.5f, 0.5f);
                    _overlayRt.pivot = new Vector2(0.5f, 0.5f);
                }
                else
                {
                    _overlayRt.anchorMin = new Vector2(0f, 0.5f);
                    _overlayRt.anchorMax = new Vector2(0f, 0.5f);
                    _overlayRt.pivot = new Vector2(0f, 0.5f);
                }
                _overlayRt.sizeDelta = OverlaySize;
                _overlayRt.anchoredPosition = Vector2.zero;

                var bg = _overlay.GetComponent<Image>();
                var bgSp = GetSprite("BG_Graphics");
                if (bgSp != null)
                {
                    // ★Simple（整图等比缩放），不要 Sliced：BG_Graphics 的上 border 是 150px，
                    //   Sliced 会把标题栏固定成 150px 高，窗口一高就"标题栏被拉长、内容整体下移"。
                    bg.sprite = bgSp;
                    bg.type = Image.Type.Simple;
                    bg.color = Color.white;
                }
                else
                {
                    bg.sprite = null;
                    bg.color = new Color(0.012f, 0.04f, 0.055f, 1f);
                }
                bg.raycastTarget = true;

                var cg = _overlay.GetComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;

                // 侧栏独立在窗口**外**左侧（★原生设置左列同款：窄胶囊 TabBarBG，只有首字圆底，没有名字）
                if (_roomMode)
                {
                    var sb = NewRect("SideBar", _overlay.transform);
                    sb.anchorMin = new Vector2(0f, 0.5f);
                    sb.anchorMax = new Vector2(0f, 0.5f);
                    sb.pivot = new Vector2(1f, 0.5f);
                    sb.sizeDelta = new Vector2(76f, 840f);       // 高度由 ResizeSideBar() 按条目数收缩
                    sb.anchoredPosition = new Vector2(-14f, 0f);
                    var sImg = sb.gameObject.AddComponent<Image>();
                    var sSp = GetSprite("TabBarBG");              // 原生设置左侧那根竖条（112×496，border 上下 64）
                    if (sSp != null) { sImg.sprite = sSp; sImg.type = Image.Type.Sliced; sImg.pixelsPerUnitMultiplier = 1.6f; sImg.color = Color.white; }
                    else sImg.color = new Color(0.05f, 0.12f, 0.17f, 0.95f);
                    sImg.raycastTarget = true;
                    listNode = sb;
                }

                // 标题栏
                var bar = NewRect("TitleBar", windowNode);
                bar.anchorMin = new Vector2(0f, 1f);
                bar.anchorMax = new Vector2(1f, 1f);
                bar.pivot = new Vector2(0.5f, 1f);
                bar.sizeDelta = new Vector2(0f, 64f);
                bar.anchoredPosition = Vector2.zero;

                // 标题（不要星：用户实测"星星还是重叠"，直接去掉）
                var title = AddLabel(bar, "MOD 设置", 30, Color.white);
                _overlayTitle = title;
                var trt = title.rectTransform;
                trt.anchorMin = new Vector2(0f, 0.5f);
                trt.anchorMax = new Vector2(0f, 0.5f);
                trt.pivot = new Vector2(0f, 0.5f);
                trt.sizeDelta = new Vector2(560f, 40f);
                trt.anchoredPosition = new Vector2(100f, 0f);   // 避开 BG_Graphics 左上角自带的那颗星

                // 关闭按钮（右上角；点它只收覆盖层，不关设置窗口）
                var close = NewRect("Close", bar);
                close.anchorMin = new Vector2(1f, 0.5f);
                close.anchorMax = new Vector2(1f, 0.5f);
                close.pivot = new Vector2(1f, 0.5f);
                close.sizeDelta = new Vector2(36f, 36f);
                close.anchoredPosition = new Vector2(-20f, 0f);
                var cimg = close.gameObject.AddComponent<Image>();
                var closeSp = GetSprite("T_CloseButton_Normal");
                if (closeSp != null)
                {
                    cimg.sprite = closeSp;
                    cimg.type = Image.Type.Simple;
                    cimg.color = Color.white;
                }
                else
                {
                    cimg.sprite = GetSprite("T_WhiteCircle_Mini");
                    cimg.type = Image.Type.Sliced;
                    cimg.pixelsPerUnitMultiplier = 5f;
                    cimg.color = new Color(0f, 0.09f, 0.15f, 0.9f);
                }
                cimg.raycastTarget = true;
                var cbtn = close.gameObject.AddComponent<Button>();
                cbtn.targetGraphic = cimg;
                cbtn.transition = Selectable.Transition.None;
                cbtn.onClick.AddListener((UnityAction)HideOverlay);
                if (closeSp == null)
                {
                    var cx = AddLabel(close, "×", 26, Color.white);
                    cx.alignment = Il2CppTMPro.TextAlignmentOptions.Center;
                    var crt = cx.rectTransform;
                    crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
                    crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
                }

                // 左侧：mod 列表（只在房间窗口用；设置场景改成“像语言页”的下拉框）
                if (_roomMode)
                {
                    var listScroll = NewRect("ListScroll", listNode);
                    listScroll.anchorMin = Vector2.zero;
                    listScroll.anchorMax = Vector2.one;
                    // 上下各让出端头椭圆的高度（40）+ 6 间距：列表最上面/最下面都停在直线段内，
                    // 不滚进椭圆端头里（用户："像联这样就应该是最上面了不要到了那个椭圆的位置"）
                    listScroll.offsetMin = new Vector2(8f, 40f);
                    listScroll.offsetMax = new Vector2(-8f, -40f);
                    var lImg = listScroll.gameObject.AddComponent<Image>();
                    lImg.color = new Color(1f, 1f, 1f, 0.001f);
                    lImg.raycastTarget = true;

                    var listViewport = NewRect("Viewport", listScroll);
                    listViewport.anchorMin = Vector2.zero;
                    listViewport.anchorMax = Vector2.one;
                    listViewport.offsetMin = Vector2.zero;
                    listViewport.offsetMax = Vector2.zero;
                    listViewport.gameObject.AddComponent<RectMask2D>();

                    var listContent = NewRect("Content", listViewport);
                    listContent.anchorMin = new Vector2(0f, 1f);
                    listContent.anchorMax = new Vector2(1f, 1f);
                    listContent.pivot = new Vector2(0.5f, 1f);
                    listContent.sizeDelta = Vector2.zero;
                    listContent.anchoredPosition = Vector2.zero;

                    var lvlg = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
                    lvlg.padding = new RectOffset(4, 4, 0, 0);   // 上下不留 padding：首/末行更贴近端头
                    lvlg.spacing = 8f;
                    lvlg.childControlWidth = true;
                    lvlg.childControlHeight = false;
                    lvlg.childForceExpandWidth = true;
                    lvlg.childForceExpandHeight = false;
                    lvlg.childAlignment = TextAnchor.UpperLeft;

                    var lfit = listContent.gameObject.AddComponent<ContentSizeFitter>();
                    lfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    var lsr = listScroll.gameObject.AddComponent<ScrollRect>();
                    lsr.viewport = listViewport;
                    lsr.content = listContent;
                    lsr.horizontal = false;
                    lsr.vertical = true;
                    lsr.movementType = ScrollRect.MovementType.Clamped;
                    lsr.scrollSensitivity = 30f;
                    lsr.inertia = false;

                    _overlayList = listContent;
                }
                else
                {
                    BuildModPicker();
                }

                // 右侧：选中 mod 的设置内容（滚动）
                var contentScroll = NewRect("ContentScroll", windowNode);
                contentScroll.anchorMin = Vector2.zero;
                contentScroll.anchorMax = Vector2.one;
                contentScroll.offsetMin = new Vector2(24f, 20f);   // 窗口内没有侧栏了，内容从左侧开始，不再留 252 的空白
                contentScroll.offsetMax = new Vector2(-24f, _roomMode ? -72f : -140f);   // 设置场景：给顶部下拉框留 68

                var cImg = contentScroll.gameObject.AddComponent<Image>();
                cImg.color = new Color(1f, 1f, 1f, 0.001f);
                cImg.raycastTarget = true;

                var contentViewport = NewRect("Viewport", contentScroll);
                contentViewport.anchorMin = Vector2.zero;
                contentViewport.anchorMax = Vector2.one;
                contentViewport.offsetMin = Vector2.zero;
                contentViewport.offsetMax = Vector2.zero;
                contentViewport.gameObject.AddComponent<RectMask2D>();

                var content = NewRect("Content", contentViewport);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = Vector2.zero;
                content.anchoredPosition = Vector2.zero;

                var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(4, 4, 4, 4);
                vlg.spacing = 10f;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childAlignment = TextAnchor.UpperLeft;

                var fit = content.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var sr = contentScroll.gameObject.AddComponent<ScrollRect>();
                sr.viewport = contentViewport;
                sr.content = content;
                sr.horizontal = false;
                sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.scrollSensitivity = 30f;
                sr.inertia = false;

                _overlayContent = content;

                // ★UI 层：游戏的输入判定按 layer == "UI" 过滤（不在 UI 层会被当成"点房间"）
                SetLayerRecursively(_overlay, LayerMask.NameToLayer("UI"));
                _overlay.SetActive(false);
                LogOnce("overlay", "overlay built under '" + NodePath(WindowParent) + "'");
            }
            catch (Exception e) { MelonLogger.Error("build overlay: " + e); }
        }

        // 白边圆角描边（给下拉框用）：64×64、圆角 14、边 2px
        Sprite _borderSprite;

        internal Sprite GetBorderSprite()
        {
            try
            {
                if (_borderSprite != null) return _borderSprite;
                const int size = 64, r = 14, t = 2;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        bool outer = InRoundRect(x, y, size, size, r);
                        bool inner = x >= t && y >= t && x < size - t && y < size - t
                                     && InRoundRect(x - t, y - t, size - 2 * t, size - 2 * t, r - t);
                        px[y * size + x] = (outer && !inner)
                            ? new Color32(255, 255, 255, 255)
                            : new Color32(255, 255, 255, 0);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _borderSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _borderSprite;
            }
            catch { return null; }
        }

        static bool InRoundRect(int x, int y, int w, int h, int r)
        {
            try
            {
                int cx = Mathf.Clamp(x, r, w - 1 - r);
                int cy = Mathf.Clamp(y, r, h - 1 - r);
                float dx = x - cx, dy = y - cy;
                return dx * dx + dy * dy <= (float)r * r;
            }
            catch { return true; }
        }

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            if (parent != null) rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        internal static void SetLayerRecursively(GameObject go, int layer)
        {
            try
            {
                if (go == null || layer < 0) return;
                go.layer = layer;
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    var ch = go.transform.GetChild(i);
                    if (ch != null) SetLayerRecursively(ch.gameObject, layer);
                }
            }
            catch { }
        }

        static string NodePath(Transform t)
        {
            try
            {
                if (t == null) return "?";
                var sb = new System.Text.StringBuilder(t.name);
                var p = t.parent;
                int guard = 0;
                while (p != null && guard++ < 8) { sb.Insert(0, p.name + "/"); p = p.parent; }
                return sb.ToString();
            }
            catch { return "?"; }
        }

        static string RtSize(Transform t)
        {
            try
            {
                var rt = t != null ? t.GetComponent<RectTransform>() : null;
                return rt != null ? ("(" + rt.sizeDelta.x.ToString("0") + "," + rt.sizeDelta.y.ToString("0") + ")") : "?";
            }
            catch { return "?"; }
        }
    }
}

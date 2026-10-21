// ============================================================================
// ModSettingsHub — 首页：左侧 mod 列表 + 右侧选中项设置
//   ★只往原生左列注入"一颗"【模组】图标；所有 mod 偏好都在这一页里滚动，
//     不再给每个 cfg 分类插一颗（实测 20+ 颗会把左列撑到屏幕外）。
// ============================================================================
using System;
using System.Collections.Generic;
using MelonLoader;
using ModSettingsHub.Api;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ModSettingsHub
{
    public partial class ModSettingsHubMod
    {
        RectTransform _overlayList;
        Image _hubButtonImage;
        string _selectedId;
        readonly Dictionary<string, Image> _listRows = new Dictionary<string, Image>();
        readonly Dictionary<string, Image> _listDots = new Dictionary<string, Image>();

        // 只构建内容，不负责显隐（原生状态机模式下由 UpdateWindow 显示）
        void BuildHubHomeContent()
        {
            try
            {
                EnsureOverlay();
                if (_overlay == null || _overlayContent == null) return;

                RefreshEntries();                       // 每次打开都重新读注册表/配置（条目可能变了）
                if (!_roomMode) AlignOverlayToActiveNativePage();
                CloseModPicker();
                BuildList();

                if (_overlayTitle != null) SetTmpComp(_overlayTitle, "MOD 设置");

                if (Entries.Count > 0) SelectEntry(Entries[0]);
                else
                {
                    ClearChildren(_overlayContent);
                    AddLabel(_overlayContent, "还没有可显示的 mod 设置", 20, ColDim);
                }
                HighlightButton();
            }
            catch (Exception e) { MelonLogger.Error("hub content: " + e); }
        }

        // 手动显隐（房间模式 / 原生状态机注册失败时的回退路径）
        void ShowHubHome()
        {
            try
            {
                BuildHubHomeContent();
                if (_overlay == null || _overlayContent == null) return;

                var cg = _overlay.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }
                _overlay.SetActive(true);
                _overlay.transform.SetAsLastSibling();
                HotkeyStore.Suspended = true;   // 设置页开着时，别的 mod 热键一律不响应
                HideNativePages(true);          // 设置场景：盖住原生页（否则两套 UI 叠在一起）
                if (_backdrop != null) _backdrop.SetActive(true);
                InputLocked = false;            // 内嵌在原生设置里，不需要锁房间相机（避免退出后视角卡死）
                _openId = "hub";
                HighlightButton();
            }
            catch (Exception e) { MelonLogger.Error("hub home: " + e); }
        }

        void BuildList()
        {
            try
            {
                if (_overlayList == null) return;
                ClearChildren(_overlayList);
                _listRows.Clear();
                _listDots.Clear();
                for (int i = 0; i < Entries.Count; i++) AddListRow(Entries[i]);
                ResizeSideBar();
            }
            catch (Exception e) { MelonLogger.Error("build list: " + e); }
        }

        void AddListRow(HubEntry e)
        {
            try
            {
                var row = NewRect("row", _overlayList);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(0f, 52f);
                row.anchoredPosition = Vector2.zero;

                var le = row.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 52f;
                le.minHeight = 52f;

                var bg = row.gameObject.AddComponent<Image>();
                bg.sprite = null;
                bg.color = new Color(0f, 0f, 0f, 0f);   // 不要白底/灰底（透明但仍然接收点击）
                bg.raycastTarget = true;

                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener((UnityAction)(() => SelectEntry(e)));

                // 图标 + 首字（★原生设置左列同款：只有圆底首字，没有文字名字）
                var dot = NewRect("dot", row);
                dot.anchorMin = new Vector2(0.5f, 0.5f);
                dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = new Vector2(44f, 44f);
                dot.anchoredPosition = Vector2.zero;
                var dimg = dot.gameObject.AddComponent<Image>();
                var circle = GetSprite("T_WhiteCircle");
                if (circle != null) { dimg.sprite = circle; dimg.type = Image.Type.Simple; dimg.preserveAspect = true; }
                dimg.color = TintFor(e.Id);
                dimg.raycastTarget = false;

                var glyph = AddLabel(dot, IconGlyph(e.Name), 18, Color.white);
                glyph.alignment = Il2CppTMPro.TextAlignmentOptions.Center;
                var grt = glyph.rectTransform;
                grt.anchorMin = Vector2.zero;
                grt.anchorMax = Vector2.one;
                grt.offsetMin = Vector2.zero;
                grt.offsetMax = Vector2.zero;

                _listRows[e.Id] = bg;
                _listDots[e.Id] = dimg;
            }
            catch (Exception ex) { LogOnce("listrow:" + e.Id, "list row err " + ex.Message); }
        }

        void SelectEntry(HubEntry e)
        {
            try
            {
                if (e == null || _overlayContent == null) return;
                _selectedId = e.Id;
                ClearChildren(_overlayContent);

                if (e.Entry != null)
                {
                    // 有自己面板的 mod：点了就交给它（Hub 只当入口）
                    HideOverlay();
                    try { e.Entry.OnClick(); }
                    catch (Exception ex) { MelonLogger.Error("entry click '" + e.Id + "': " + ex.Message); }
                    return;
                }

                if (_overlayTitle != null) SetTmpComp(_overlayTitle, _roomMode ? ("MOD 设置 · " + e.Name) : "MOD 设置");
                if (_pickerLabel != null) SetTmpComp(_pickerLabel, e.Name);

                BuildHotkeyHeader(e, _overlayContent);

                if (e.Page != null)
                {
                    try { e.Page.Build(_overlayContent); }
                    catch (Exception ex) { MelonLogger.Error("page build '" + e.Id + "': " + ex); BuildError(e, "页面构建失败：" + ex.Message); }
                }
                else if (e.Cfg != null)
                {
                    BuildCfgPage(e, _overlayContent);
                }
                else
                {
                    BuildError(e, "这条目没有可显示的内容");
                }

                UpdateListHighlight();
                LogOnce("sel:" + e.Id, "select '" + e.Name + "' (" + e.Id + ")");
            }
            catch (Exception ex) { MelonLogger.Error("select: " + ex); }
        }

        // 胶囊高度跟条目数走（原生设置左列就是按图标数量长起来的：24n+20(n−1)+36）
        void ResizeSideBar()
        {
            try
            {
                if (_overlayList == null) return;
                Transform bar = _overlayList;
                while (bar != null && bar.name != "SideBar") bar = bar.parent;
                if (bar == null) return;
                int n = Entries.Count;
                float h = n <= 0 ? 160f : (52f * n + 8f * (n - 1) + 80f);
                h = Mathf.Clamp(h, 160f, 840f);
                var rt = bar.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            }
            catch { }
        }

        void UpdateListHighlight()
        {
            try
            {
                foreach (var kv in _listDots)
                {
                    if (kv.Value == null) continue;
                    bool on = _selectedId != null && kv.Key == _selectedId;
                    var c = TintFor(kv.Key);
                    kv.Value.color = on
                        ? new Color(Mathf.Min(1f, c.r * 1.35f), Mathf.Min(1f, c.g * 1.35f), Mathf.Min(1f, c.b * 1.35f), 1f)
                        : c;
                }
            }
            catch { }
        }

        // ===================== 设置场景：像“语言”页那样的 mod 下拉框 =====================
        RectTransform _pickerBar;
        RectTransform _pickerPopup;
        RectTransform _pickerList;
        Il2CppTMPro.TextMeshProUGUI _pickerLabel;
        bool _pickerOpen;

        void BuildModPicker()
        {
            try
            {
                var bar = NewRect("ModPicker", _overlay.transform);
                bar.anchorMin = new Vector2(0f, 1f);
                bar.anchorMax = new Vector2(1f, 1f);
                bar.pivot = new Vector2(0.5f, 1f);
                bar.offsetMin = new Vector2(24f, -124f);
                bar.offsetMax = new Vector2(-24f, -72f);

                var bg = bar.gameObject.AddComponent<Image>();
                var chip = GetSprite("T_WhiteCircle_Mini");
                if (chip != null) { bg.sprite = chip; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 5f; }
                bg.color = new Color(0.01f, 0.05f, 0.075f, 0.98f);
                bg.raycastTarget = true;

                var btn = bar.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener((UnityAction)ToggleModPicker);

                var lab = AddLabel(bar, "选择 MOD", 20, ColText);
                _pickerLabel = lab;
                var lrt = lab.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.offsetMin = new Vector2(20f, 0f);
                lrt.offsetMax = new Vector2(-64f, 0f);

                var arrow = NewRect("Arrow", bar);
                arrow.anchorMin = new Vector2(1f, 0.5f);
                arrow.anchorMax = new Vector2(1f, 0.5f);
                arrow.pivot = new Vector2(1f, 0.5f);
                arrow.sizeDelta = new Vector2(24f, 24f);
                arrow.anchoredPosition = new Vector2(-18f, 0f);
                var aimg = arrow.gameObject.AddComponent<Image>();
                var asp = GetSprite("T_DropDown_Arrow_White");
                if (asp != null) { aimg.sprite = asp; aimg.type = Image.Type.Simple; aimg.preserveAspect = true; }
                aimg.color = Color.white;
                aimg.raycastTarget = false;

                // 白边描框（原生输入框那种）
                var border = NewRect("Border", bar);
                border.anchorMin = Vector2.zero;
                border.anchorMax = Vector2.one;
                border.offsetMin = Vector2.zero;
                border.offsetMax = Vector2.zero;
                var bimg = border.gameObject.AddComponent<Image>();
                var bsp = GetBorderSprite();
                if (bsp != null) { bimg.sprite = bsp; bimg.type = Image.Type.Sliced; }
                bimg.color = new Color(1f, 1f, 1f, 0.85f);
                bimg.raycastTarget = false;

                var pop = NewRect("PickerPopup", _overlay.transform);
                pop.anchorMin = new Vector2(0f, 1f);
                pop.anchorMax = new Vector2(1f, 1f);
                pop.pivot = new Vector2(0.5f, 1f);
                pop.offsetMin = new Vector2(24f, -424f);
                pop.offsetMax = new Vector2(-24f, -124f);
                var pimg = pop.gameObject.AddComponent<Image>();
                pimg.color = new Color(0.008f, 0.035f, 0.055f, 1f);   // 不透明深底：不要透出下面的设置页
                pimg.raycastTarget = true;

                var vp = NewRect("Viewport", pop);
                vp.anchorMin = Vector2.zero;
                vp.anchorMax = Vector2.one;
                vp.offsetMin = new Vector2(4f, 4f);
                vp.offsetMax = new Vector2(-4f, -4f);
                vp.gameObject.AddComponent<RectMask2D>();

                var list = NewRect("Content", vp);
                list.anchorMin = new Vector2(0f, 1f);
                list.anchorMax = new Vector2(1f, 1f);
                list.pivot = new Vector2(0.5f, 1f);
                list.sizeDelta = Vector2.zero;
                var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(4, 4, 4, 4);
                vlg.spacing = 2f;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childAlignment = TextAnchor.UpperLeft;
                var fit = list.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var sr = pop.gameObject.AddComponent<ScrollRect>();
                sr.viewport = vp;
                sr.content = list;
                sr.horizontal = false;
                sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.scrollSensitivity = 30f;
                sr.inertia = false;

                _pickerBar = bar;
                _pickerPopup = pop;
                _pickerList = list;
                pop.gameObject.SetActive(false);
                LogOnce("picker", "mod picker built (dropdown, like the language page)");
            }
            catch (Exception e) { MelonLogger.Error("build picker: " + e); }
        }

        void ToggleModPicker()
        {
            try
            {
                if (_pickerPopup == null) return;
                _pickerOpen = !_pickerPopup.gameObject.activeSelf;
                if (_pickerOpen)
                {
                    RebuildPickerList();
                    _pickerPopup.SetAsLastSibling();   // ★必须在内容滚动区之上，否则点击/滚轮都被它挡住
                }
                _pickerPopup.gameObject.SetActive(_pickerOpen);
            }
            catch { }
        }

        void CloseModPicker()
        {
            _pickerOpen = false;
            try { if (_pickerPopup != null) _pickerPopup.gameObject.SetActive(false); } catch { }
        }

        void RebuildPickerList()
        {
            try
            {
                if (_pickerList == null || _pickerPopup == null) return;
                ClearChildren(_pickerList);
                for (int i = 0; i < Entries.Count; i++) AddPickerRow(Entries[i]);
                int n = Mathf.Clamp(Entries.Count, 1, 8);
                float h = n * 44f + (n - 1) * 2f + 16f;
                _pickerPopup.offsetMin = new Vector2(24f, -124f - h);
            }
            catch (Exception e) { LogOnce("picklist", "picker list err " + e.Message); }
        }

        void AddPickerRow(HubEntry e)
        {
            try
            {
                var row = NewRect("row", _pickerList);
                row.sizeDelta = new Vector2(0f, 44f);
                var le = row.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 44f;
                le.minHeight = 44f;

                var bg = row.gameObject.AddComponent<Image>();
                bg.color = new Color(1f, 1f, 1f, 1f);
                bg.raycastTarget = true;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                // ★悬停/按下高亮（原生下拉那种灰底）
                btn.transition = Selectable.Transition.ColorTint;
                var cb = btn.colors;
                bool cur = _selectedId != null && _selectedId == e.Id;
                cb.normalColor = cur ? new Color(1f, 1f, 1f, 0.12f) : new Color(1f, 1f, 1f, 0f);
                cb.highlightedColor = new Color(1f, 1f, 1f, 0.22f);
                cb.pressedColor = new Color(1f, 1f, 1f, 0.30f);
                cb.selectedColor = new Color(1f, 1f, 1f, 0.16f);
                cb.disabledColor = cb.normalColor;
                cb.colorMultiplier = 1f;
                cb.fadeDuration = 0.05f;
                btn.colors = cb;
                btn.onClick.AddListener((UnityAction)(() => { SelectEntry(e); CloseModPicker(); }));

                var lab = AddLabel(row, e.Name, 19, ColText);
                var lrt = lab.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.offsetMin = new Vector2(16f, 0f);
                lrt.offsetMax = new Vector2(-16f, 0f);
            }
            catch (Exception ex) { LogOnce("pickrow:" + e.Id, "picker row err " + ex.Message); }
        }

        // ===================== 设置场景：盖住原生页 =====================
        readonly List<CanvasGroup> _hiddenNativePages = new List<CanvasGroup>();

        void HideNativePages(bool hide)
        {
            try
            {
                if (_roomMode) return;
                if (hide)
                {
                    if (_hiddenNativePages.Count > 0) return;
                    if (WindowParent == null) return;
                    for (int i = 0; i < WindowParent.childCount; i++)
                    {
                        var ch = WindowParent.GetChild(i);
                        if (ch == null || ch == _overlayRt) continue;
                        if (string.IsNullOrEmpty(ch.name) || !ch.name.StartsWith("P_", StringComparison.Ordinal)) continue;
                        var cg = ch.GetComponent<CanvasGroup>();
                        if (cg == null || cg.alpha < 0.05f) continue;
                        _hiddenNativePages.Add(cg);
                        cg.alpha = 0f;
                        cg.blocksRaycasts = false;
                        cg.interactable = false;
                    }
                    LogOnce("hidepages", "native pages hidden: " + _hiddenNativePages.Count);
                }
                else
                {
                    for (int i = 0; i < _hiddenNativePages.Count; i++)
                    {
                        var cg = _hiddenNativePages[i];
                        if (cg == null) continue;
                        cg.alpha = 1f;
                        cg.blocksRaycasts = true;
                        cg.interactable = true;
                    }
                    _hiddenNativePages.Clear();
                }
            }
            catch (Exception e) { LogOnce("hidepageserr", "hide pages err " + e.Message); }
        }

        void ClearChildren(Transform t)
        {
            try
            {
                if (t == null) return;
                for (int i = t.childCount - 1; i >= 0; i--)
                {
                    var ch = t.GetChild(i);
                    if (ch == null) continue;
                    try { ch.SetParent(null, false); } catch { }
                    try { UnityEngine.Object.Destroy(ch.gameObject); } catch { }
                }
            }
            catch { }
        }
    }
}

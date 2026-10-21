// ============================================================================
// ModSettingsHub — 控件工厂 + MelonPreferences 自动页
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using MelonLoader;
using ModSettingsHub.Api;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ModSettingsHub
{
    public partial class ModSettingsHubMod
    {
        static readonly Color ColText = new Color(0.93f, 0.96f, 1f, 1f);
        static readonly Color ColDim = new Color(0.62f, 0.70f, 0.78f, 1f);
        static readonly Color ColOn = new Color(0.23f, 0.72f, 0.52f, 0.95f);
        static readonly Color ColOff = new Color(0.10f, 0.16f, 0.22f, 0.95f);

        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Sprite> _iconSprites = new Dictionary<string, Sprite>();
        bool _spriteIndexed;
        Il2CppTMPro.TMP_FontAsset _fontRegular, _fontBold;
        bool _fontTried;

        // ===================== 美术 / 字体 =====================
        internal Sprite GetSprite(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name)) return null;
                Sprite s;
                if (_sprites.TryGetValue(name, out s) && s != null) return s;
                IndexSprites();
                if (_sprites.TryGetValue(name, out s) && s != null) return s;
                s = LoadEmbeddedSprite(name + ".png");
                if (s != null) _sprites[name] = s;
                return s;
            }
            catch { return null; }
        }

        void IndexSprites()
        {
            if (_spriteIndexed) return;
            _spriteIndexed = true;
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Sprite>();
                if (all == null) return;
                for (int i = 0; i < all.Length; i++)
                {
                    var sp = all[i];
                    if (sp == null) continue;
                    string nm = null;
                    try { nm = sp.name; } catch { }
                    if (string.IsNullOrEmpty(nm)) continue;
                    if (!_sprites.ContainsKey(nm)) _sprites[nm] = sp;
                }
                LogOnce("sprites", "sprite index=" + all.Length);
            }
            catch (Exception e) { LogOnce("sprerr", "sprite index err " + e.Message); }
        }

        internal Sprite EntryIconSprite(HubEntry e)
        {
            try
            {
                if (e == null) return GetSprite("XM_ModGrid");
                if (e.IconPng != null && e.IconPng.Length > 8)
                {
                    Sprite s;
                    if (_iconSprites.TryGetValue(e.Id, out s) && s != null) return s;
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, e.IconPng))
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Bilinear;
                        s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        _iconSprites[e.Id] = s;
                        return s;
                    }
                }
            }
            catch { }
            return GetSprite("XM_ModGrid");
        }

        static Sprite LoadEmbeddedSprite(string fileName)
        {
            try
            {
                byte[] bytes = ReadEmbedded(fileName);
                if (bytes == null) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, bytes)) return null;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                // ★九宫 border 必须在这里给：Sprite.Create 默认 border=0 ⇒ Image.type=Sliced 会退化成直角矩形
                var border = BorderFor(fileName);
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, border);
            }
            catch { return null; }
        }

        static Vector4 BorderFor(string fileName)
        {
            try
            {
                string n = (fileName ?? "").ToLowerInvariant();
                // (left, bottom, right, top)，跟 UI_贴图清单.csv 的实测值一致
                if (n.StartsWith("t_whitecircle_mini")) return new Vector4(128f, 128f, 128f, 128f);
                if (n.StartsWith("t_whitecircle.png")) return new Vector4(128f, 128f, 128f, 128f);
                if (n.StartsWith("t_ambientmenu_outside_3button")) return new Vector4(80f, 0f, 80f, 0f);
                if (n.StartsWith("t_ambientmenu_outside_4button")) return new Vector4(0f, 0f, 0f, 0f);
                if (n.StartsWith("tabbarbg")) return new Vector4(0f, 64f, 0f, 64f);
                if (n.StartsWith("t_button_secondary")) return new Vector4(80f, 0f, 80f, 0f);
                if (n.StartsWith("t_button_primary")) return new Vector4(80f, 0f, 80f, 0f);
                if (n.StartsWith("t_button_danger")) return new Vector4(80f, 0f, 80f, 0f);
                if (n.StartsWith("bg_graphics")) return new Vector4(0f, 80f, 0f, 150f);
                if (n.StartsWith("t_selectedborder")) return new Vector4(60f, 60f, 60f, 60f);
            }
            catch { }
            return Vector4.zero;
        }

        internal void ResetSpriteCache()
        {
            try { _spriteIndexed = false; _sprites.Clear(); } catch { }
        }

        static byte[] ReadEmbedded(string suffix)
        {
            try
            {
                var asm = typeof(ModSettingsHubMod).Assembly;
                var names = asm.GetManifestResourceNames();
                if (names == null) return null;
                for (int i = 0; i < names.Length; i++)
                {
                    var n = names[i];
                    if (n == null || !n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                    using (var st = asm.GetManifestResourceStream(n))
                    {
                        if (st == null) return null;
                        var buf = new byte[st.Length];
                        st.Read(buf, 0, buf.Length);
                        return buf;
                    }
                }
            }
            catch { }
            return null;
        }

        internal Il2CppTMPro.TMP_FontAsset GetFont(bool bold)
        {
            try
            {
                if (_fontTried && (bold ? _fontBold != null : _fontRegular != null)) return bold ? _fontBold : _fontRegular;
                var all = Resources.FindObjectsOfTypeAll<Il2CppTMPro.TMP_FontAsset>();
                if (all != null && all.Length > 0)
                {
                    Il2CppTMPro.TMP_FontAsset any = null;
                    for (int i = 0; i < all.Length; i++)
                    {
                        var f = all[i];
                        if (f == null) continue;
                        if (any == null) any = f;
                        string n = null;
                        try { n = f.name; } catch { }
                        if (string.IsNullOrEmpty(n)) continue;
                        if (n.IndexOf("NotoSansSC", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        bool isBold = n.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (bold && isBold && _fontBold == null) _fontBold = f;
                        if (!bold && !isBold && _fontRegular == null) _fontRegular = f;
                    }
                    if (_fontRegular == null) _fontRegular = any;
                    if (_fontBold == null) _fontBold = _fontRegular;
                }
                _fontTried = true;
                LogOnce("fonts", "fonts: regular=" + (_fontRegular != null) + " bold=" + (_fontBold != null));
            }
            catch (Exception e) { LogOnce("fonterr", "font err " + e.Message); }
            return bold ? (_fontBold != null ? _fontBold : _fontRegular) : _fontRegular;
        }

        internal Il2CppTMPro.TextMeshProUGUI AddLabel(Transform parent, string text, float size, Color color)
        {
            var rt = NewRect("label", parent);
            var tmp = rt.gameObject.AddComponent<Il2CppTMPro.TextMeshProUGUI>();
            tmp.text = text ?? "";
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
            var f = GetFont(false);
            if (f != null) tmp.font = f;
            tmp.raycastTarget = false;
            return tmp;
        }

        internal void SetTmp(Transform t, string s)
        {
            try
            {
                if (t == null) return;
                var tmp = t.GetComponent<Il2CppTMPro.TextMeshProUGUI>();
                if (tmp != null) { tmp.text = s ?? ""; }
            }
            catch { }
        }

        internal static void SetTmpComp(Il2CppTMPro.TextMeshProUGUI tmp, string s)
        {
            try { if (tmp != null) tmp.text = s ?? ""; } catch { }
        }

        // 没有自定义图标的 mod：按 id 生成一个稳定的颜色，再叠一个首字
        internal static Color TintFor(string id)
        {
            try
            {
                int h = 17;
                if (!string.IsNullOrEmpty(id))
                    for (int i = 0; i < id.Length; i++) h = unchecked(h * 31 + id[i]);
                float hue = (Math.Abs(h) % 360) / 360f;
                return Color.HSVToRGB(hue, 0.50f, 0.58f);
            }
            catch { return new Color(0.25f, 0.45f, 0.65f, 1f); }
        }

        internal void AddIconGlyph(Transform parent, string name)
        {
            try
            {
                var t = AddLabel(parent, IconGlyph(name), 13, Color.white);
                t.alignment = Il2CppTMPro.TextAlignmentOptions.Center;
                var rt = t.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                t.raycastTarget = false;
            }
            catch { }
        }

        // ===================== 行 =====================
        internal RectTransform NewRow(Transform parent, float height)
        {
            var rt = NewRect("row", parent);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, height);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return rt;
        }

        // 标签太长会换行：行高按估算行数撑开，避免文字叠到下一行（用户："挤一堆字"）
        static int TextLines(string s, float fontSize, float width)
        {
            try
            {
                if (string.IsNullOrEmpty(s) || width < 1f) return 1;
                float w = 0f;
                int lines = 1;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '\n') { lines++; w = 0f; continue; }
                    float cw = c == ' ' ? fontSize * 0.30f : (c >= 0x2E80 ? fontSize : fontSize * 0.56f);
                    if (w + cw > width) { lines++; w = cw; }
                    else w += cw;
                }
                return lines;
            }
            catch { return 1; }
        }

        static float RowHeightFor(string label, float width, float fontSize)
        {
            int lines = TextLines(label, fontSize, width);
            return Mathf.Max(40f, lines * fontSize * 1.35f + 12f);
        }

        // ===================== MelonPreferences 自动页 =====================
        internal void BuildCfgPage(HubEntry e, RectTransform parent)
        {
            var cat = e.Cfg;
            if (cat == null) { AddLabel(parent, "配置分类不存在", 20, ColDim); return; }

            var hint = AddLabel(parent, "自动读取 MelonPreferences · 改完需要重启游戏才生效", 15, ColDim);
            var hrt = hint.rectTransform;
            hrt.sizeDelta = new Vector2(0f, 24f);
            var hle = hint.gameObject.AddComponent<LayoutElement>();
            hle.preferredHeight = 24f;

            int n = 0;
            try
            {
                var list = cat.Entries;
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        try
                        {
                            var ent = list[i];
                            if (ent == null) continue;
                            string id = EntryIdentifier(ent);
                            if (string.IsNullOrEmpty(id) || id.StartsWith("_", StringComparison.Ordinal)) continue;
                            if (IsInternalEntry(id)) continue;
                            Type vt = EntryValueType(ent);
                            if (vt == null) continue;
                            string label = EntryLabel(ent, id);
                            var prop = ent.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                            if (prop == null) continue;

                            if (vt == typeof(bool)) AddBoolRow(parent, label, ent, prop);
                            else if (vt == typeof(int) || vt == typeof(long) || vt == typeof(short)) AddNumberRow(parent, label, ent, prop, true);
                            else if (vt == typeof(float) || vt == typeof(double)) AddNumberRow(parent, label, ent, prop, false);
                            else AddTextRow(parent, label, ent, prop);
                            n++;
                        }
                        catch (Exception ex) { LogOnce("cfgrow:" + i, "cfg row err " + ex.Message); }
                    }
                }
            }
            catch (Exception ex) { AddLabel(parent, "读取配置失败：" + ex.Message, 16, ColDim); }

            if (n == 0) AddLabel(parent, "（这个分类没有可显示的配置项）", 18, ColDim);
            else LogOnce("cfgpage:" + e.Id, "cfg page '" + e.Name + "' rows=" + n);
        }

        static string EntryIdentifier(object ent)
        {
            try { var p = ent.GetType().GetProperty("Identifier", BindingFlags.Public | BindingFlags.Instance); return p != null ? (p.GetValue(ent) as string) : null; }
            catch { return null; }
        }

        static string EntryLabel(object ent, string fallback)
        {
            try
            {
                string id = EntryIdentifier(ent);
                if (!string.IsNullOrEmpty(id)) return ChineseIdentifier(id);
                return fallback ?? "";
            }
            catch { return fallback; }
        }

        static string EntryDescription(object ent)
        {
            try
            {
                var p = ent.GetType().GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
                return p != null ? (p.GetValue(ent) as string) : null;
            }
            catch { return null; }
        }

        static Type EntryValueType(object ent)
        {
            try
            {
                var t = ent.GetType();
                while (t != null)
                {
                    if (t.IsGenericType && t.GetGenericArguments().Length == 1)
                    {
                        var a = t.GetGenericArguments()[0];
                        if (a == typeof(bool) || a == typeof(int) || a == typeof(float) || a == typeof(double)
                            || a == typeof(long) || a == typeof(short) || a == typeof(string)) return a;
                    }
                    t = t.BaseType;
                }
            }
            catch { }
            return null;
        }

        // ---------------- 开关（原生 T_Toggle_BG + T_WhiteCircle） ----------------
        void AddBoolRow(RectTransform parent, string label, object ent, PropertyInfo prop)
        {
            var row = NewRow(parent, RowHeightFor(label, 600f, 20f));

            var lab = AddLabel(row, label, 20, ColText);
            var lrt = lab.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(0f, 0f);
            lrt.offsetMax = new Vector2(-80f, 0f);

            var track = NewRect("toggle", row);
            track.anchorMin = new Vector2(1f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(52f, 28f);
            track.anchoredPosition = new Vector2(-2f, 0f);

            var timg = track.gameObject.AddComponent<Image>();
            var tsp = GetSprite("T_Toggle_BG");
            if (tsp != null) { timg.sprite = tsp; timg.type = Image.Type.Simple; }
            timg.raycastTarget = true;

            var btn = track.gameObject.AddComponent<Button>();
            btn.targetGraphic = timg;
            btn.transition = Selectable.Transition.None;

            var knob = NewRect("knob", track);
            knob.anchorMin = new Vector2(0f, 0.5f);
            knob.anchorMax = new Vector2(0f, 0.5f);
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(22f, 22f);
            knob.anchoredPosition = new Vector2(11f, 0f);

            var kimg = knob.gameObject.AddComponent<Image>();
            var ksp = GetSprite("T_WhiteCircle");
            if (ksp != null) { kimg.sprite = ksp; kimg.type = Image.Type.Simple; kimg.preserveAspect = true; }
            kimg.color = Color.white;
            kimg.raycastTarget = false;

            Action refresh = () =>
            {
                try
                {
                    bool on = (bool)prop.GetValue(ent);
                    timg.color = on ? new Color(1f, 0.22f, 0.48f, 0.95f) : new Color(0.55f, 0.60f, 0.65f, 0.85f);
                    knob.anchoredPosition = new Vector2(on ? 41f : 11f, 0f);
                }
                catch { }
            };
            btn.onClick.AddListener((UnityAction)(() =>
            {
                try
                {
                    bool on = (bool)prop.GetValue(ent);
                    prop.SetValue(ent, !on);
                    MelonPreferences.Save();
                    refresh();
                }
                catch (Exception ex) { MelonLogger.Error("cfg bool write: " + ex.Message); }
            }));
            refresh();
        }

        // ---------------- 数值（滑条 + 只读值） ----------------
        void AddNumberRow(RectTransform parent, string label, object ent, PropertyInfo prop, bool whole)
        {
            var row = NewRow(parent, RowHeightFor(label, 252f, 20f));

            var lab = AddLabel(row, label, 20, ColText);
            var lrt = lab.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(252f, 0f);
            lrt.anchoredPosition = Vector2.zero;

            object raw = prop.GetValue(ent);
            float val = ToFloat(raw);
            float min, max;
            RangeFor(ent, val, out min, out max);

            var slRt = NewRect("slider", row);
            slRt.anchorMin = new Vector2(0f, 0.5f);
            slRt.anchorMax = new Vector2(1f, 0.5f);
            slRt.pivot = new Vector2(0.5f, 0.5f);
            slRt.offsetMin = new Vector2(258f, -8f);
            slRt.offsetMax = new Vector2(-92f, 8f);

            var valLabel = AddLabel(row, FormatNumber(val, whole), 18, ColText);
            valLabel.alignment = Il2CppTMPro.TextAlignmentOptions.Center;
            var vrt = valLabel.rectTransform;
            vrt.anchorMin = new Vector2(1f, 0f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(1f, 0.5f);
            vrt.sizeDelta = new Vector2(84f, 0f);
            vrt.anchoredPosition = new Vector2(-2f, 0f);

            var slider = BuildSlider(slRt, min, max, Mathf.Clamp(val, min, max), whole, (UnityAction<float>)(v =>
            {
                try
                {
                    object w = whole ? (object)(int)Mathf.Round(v) : (object)v;
                    prop.SetValue(ent, ConvertTo(raw, w));
                    MelonPreferences.Save();
                    valLabel.text = FormatNumber(ToFloat(prop.GetValue(ent)), whole);
                }
                catch (Exception ex) { MelonLogger.Error("cfg num write: " + ex.Message); }
            }));
            _ = slider;
        }

        static object ConvertTo(object original, object value)
        {
            try
            {
                if (original is int) return Convert.ToInt32(value);
                if (original is long) return Convert.ToInt64(value);
                if (original is short) return Convert.ToInt16(value);
                if (original is double) return Convert.ToDouble(value);
                return Convert.ToSingle(value);
            }
            catch { return value; }
        }

        static float ToFloat(object o)
        {
            try { return o == null ? 0f : Convert.ToSingle(o); } catch { return 0f; }
        }

        static string FormatNumber(float v, bool whole)
        {
            return whole ? Mathf.RoundToInt(v).ToString() : v.ToString("0.###");
        }

        static readonly Regex RangeRx = new Regex(@"\(\s*(-?\d+(?:\.\d+)?)\s*\.\.\s*(-?\d+(?:\.\d+)?)\s*\)");

        static void RangeFor(object ent, float v, out float min, out float max)
        {
            min = 0f;
            max = 1f;
            try
            {
                string d = EntryDescription(ent);
                if (!string.IsNullOrEmpty(d))
                {
                    var m = RangeRx.Match(d);
                    if (m.Success)
                    {
                        float a = Convert.ToSingle(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                        float b = Convert.ToSingle(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                        if (b > a) { min = a; max = b; return; }
                    }
                }
            }
            catch { }

            if (v >= 0f && v <= 1f) { min = 0f; max = 1f; return; }
            if (v < 0f) { min = v * 2f; max = -v * 2f; if (max < 1f) max = 1f; return; }
            min = 0f;
            max = Mathf.Max(10f, v * 2f);
        }

        // ---------------- 文本（只读） ----------------
        void AddTextRow(RectTransform parent, string label, object ent, PropertyInfo prop)
        {
            var row = NewRow(parent, RowHeightFor(label, 252f, 20f));
            var lab = AddLabel(row, label, 20, ColText);
            var lrt = lab.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(252f, 0f);
            lrt.anchoredPosition = Vector2.zero;

            string v = "";
            try { v = prop.GetValue(ent) as string; } catch { }
            var txt = AddLabel(row, v ?? "", 18, ColDim);
            var trt = txt.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(258f, 0f);
            trt.offsetMax = new Vector2(-2f, 0f);
        }

        // ===================== 快捷键 / 内置 UI（每个 mod 页的第一排） =====================
        string _captureModId;
        HotkeySpec _captureSpec;
        Il2CppTMPro.TextMeshProUGUI _captureValue;
        string _capturePrevText;

        void BuildHotkeyHeader(HubEntry e, RectTransform parent)
        {
            try
            {
                if (e == null || parent == null || string.IsNullOrEmpty(e.ModId)) return;
                var hk = HotkeyStore.Find(e.ModId);
                if (hk == null) return;

                var head = AddLabel(parent, "快捷键", 16, ColDim);
                var hrt = head.rectTransform;
                hrt.sizeDelta = new Vector2(0f, 22f);
                var hle = head.gameObject.AddComponent<LayoutElement>();
                hle.preferredHeight = 22f;

                var keys = hk.Hotkeys;
                if (keys != null)
                    for (int i = 0; i < keys.Count; i++) AddHotkeyRow(parent, hk.ModId, keys[i]);

                if (hk.HasBuiltinUi) AddUiToggleRow(parent, hk.ModId);

                var line = NewRect("sep", parent);
                line.sizeDelta = new Vector2(0f, 1f);
                var li = line.gameObject.AddComponent<Image>();
                li.color = new Color(1f, 1f, 1f, 0.12f);     // 细线：第一排管“按键/内置UI”，下面是设置项
                li.raycastTarget = false;
                var lle = line.gameObject.AddComponent<LayoutElement>();
                lle.preferredHeight = 1f;
                lle.minHeight = 1f;
            }
            catch (Exception ex) { LogOnce("hkhdr:" + e.Id, "hotkey header err " + ex.Message); }
        }

        void AddHotkeyRow(RectTransform parent, string modId, HotkeySpec spec)
        {
            try
            {
                if (spec == null) return;
                var row = NewRow(parent, 46f);
                var bg = row.gameObject.AddComponent<Image>();
                bg.color = new Color(1f, 1f, 1f, 0.05f);
                bg.raycastTarget = true;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;

                var lab = AddLabel(row, spec.Label, 19, ColText);
                var lrt = lab.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.offsetMin = new Vector2(12f, 0f);
                lrt.offsetMax = new Vector2(-270f, 0f);

                var val = AddLabel(row, HotkeyStore.GetBinding(modId, spec), 17, new Color(1f, 0.86f, 0.38f, 1f));
                val.alignment = Il2CppTMPro.TextAlignmentOptions.Center;
                var vrt = val.rectTransform;
                vrt.anchorMin = new Vector2(1f, 0f);
                vrt.anchorMax = new Vector2(1f, 1f);
                vrt.pivot = new Vector2(1f, 0.5f);
                vrt.sizeDelta = new Vector2(140f, 0f);
                vrt.anchoredPosition = new Vector2(-78f, 0f);

                // 重置 = 回到该 mod 注册的默认键（QualityPlus 默认未绑定）
                var rb = NewRect("Reset", row);
                rb.anchorMin = new Vector2(1f, 0.5f);
                rb.anchorMax = new Vector2(1f, 0.5f);
                rb.pivot = new Vector2(1f, 0.5f);
                rb.sizeDelta = new Vector2(60f, 30f);
                rb.anchoredPosition = new Vector2(-8f, 0f);
                var rimg = rb.gameObject.AddComponent<Image>();
                var chip = GetSprite("T_WhiteCircle_Mini");
                if (chip != null) { rimg.sprite = chip; rimg.type = Image.Type.Sliced; rimg.pixelsPerUnitMultiplier = 5f; }
                rimg.color = new Color(0.10f, 0.18f, 0.24f, 0.95f);
                rimg.raycastTarget = true;
                var rbtn = rb.gameObject.AddComponent<Button>();
                rbtn.targetGraphic = rimg;
                rbtn.transition = Selectable.Transition.None;
                rbtn.onClick.AddListener((UnityAction)(() =>
                {
                    try
                    {
                        string def = spec.DefaultText;
                        HotkeyStore.SetBinding(modId, spec, def);
                        if (val != null) val.text = def;
                        MelonLogger.Msg("hotkey reset: " + modId + "/" + spec.Id + " -> " + def);
                    }
                    catch { }
                }));
                var rlab = AddLabel(rb, "重置", 15, ColText);
                rlab.alignment = Il2CppTMPro.TextAlignmentOptions.Center;
                var rrt = rlab.rectTransform;
                rrt.anchorMin = Vector2.zero;
                rrt.anchorMax = Vector2.one;
                rrt.offsetMin = Vector2.zero;
                rrt.offsetMax = Vector2.zero;

                btn.onClick.AddListener((UnityAction)(() => BeginHotkeyCapture(modId, spec, val)));
            }
            catch (Exception ex) { LogOnce("hkrow:" + spec.Id, "hotkey row err " + ex.Message); }
        }

        void AddUiToggleRow(RectTransform parent, string modId)
        {
            try
            {
                var row = NewRow(parent, 40f);

                var lab = AddLabel(row, "内置 UI 入口（关掉后只剩快捷键）", 18, ColText);
                var lrt = lab.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.offsetMin = new Vector2(12f, 0f);
                lrt.offsetMax = new Vector2(-80f, 0f);

                var track = NewRect("toggle", row);
                track.anchorMin = new Vector2(1f, 0.5f);
                track.anchorMax = new Vector2(1f, 0.5f);
                track.pivot = new Vector2(1f, 0.5f);
                track.sizeDelta = new Vector2(52f, 28f);
                track.anchoredPosition = new Vector2(-2f, 0f);

                var timg = track.gameObject.AddComponent<Image>();
                var tsp = GetSprite("T_Toggle_BG");
                if (tsp != null) { timg.sprite = tsp; timg.type = Image.Type.Simple; }
                timg.raycastTarget = true;

                var btn = track.gameObject.AddComponent<Button>();
                btn.targetGraphic = timg;
                btn.transition = Selectable.Transition.None;

                var knob = NewRect("knob", track);
                knob.anchorMin = new Vector2(0f, 0.5f);
                knob.anchorMax = new Vector2(0f, 0.5f);
                knob.pivot = new Vector2(0.5f, 0.5f);
                knob.sizeDelta = new Vector2(22f, 22f);

                var kimg = knob.gameObject.AddComponent<Image>();
                var ksp = GetSprite("T_WhiteCircle");
                if (ksp != null) { kimg.sprite = ksp; kimg.type = Image.Type.Simple; kimg.preserveAspect = true; }
                kimg.color = Color.white;
                kimg.raycastTarget = false;

                Action refresh = () =>
                {
                    try
                    {
                        bool on = HotkeyStore.UiOn(modId, true);
                        timg.color = on ? new Color(1f, 0.22f, 0.48f, 0.95f) : new Color(0.55f, 0.60f, 0.65f, 0.85f);
                        knob.anchoredPosition = new Vector2(on ? 41f : 11f, 0f);
                    }
                    catch { }
                };
                btn.onClick.AddListener((UnityAction)(() =>
                {
                    try
                    {
                        bool next = !HotkeyStore.UiOn(modId, true);
                        MelonLogger.Msg("ui toggle click: " + modId + " -> " + next);
                        HotkeyStore.SetUiOn(modId, next);
                        refresh();
                    }
                    catch (Exception ex) { MelonLogger.Error("ui toggle: " + ex.Message); }
                }));
                refresh();
            }
            catch (Exception ex) { LogOnce("uirow:" + modId, "ui row err " + ex.Message); }
        }

        void BeginHotkeyCapture(string modId, HotkeySpec spec, Il2CppTMPro.TextMeshProUGUI value)
        {
            try
            {
                _captureModId = modId;
                _captureSpec = spec;
                _captureValue = value;
                _capturePrevText = value != null ? value.text : null;
                if (value != null) value.text = "请按键…（Esc 取消）";
            }
            catch { }
        }

        void CancelHotkeyCapture()
        {
            try
            {
                if (_captureValue != null && _capturePrevText != null) _captureValue.text = _capturePrevText;
            }
            catch { }
            _captureModId = null;
            _captureSpec = null;
            _captureValue = null;
            _capturePrevText = null;
        }

        // 每帧跑（不受 1s tick 限流）：等一个键，按下就写进 HotkeyStore
        internal void TickHotkeyCapture()
        {
            if (_captureModId == null || _captureSpec == null) return;
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { CancelHotkeyCapture(); return; }

                var mods = HotkeyMods.None;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) mods |= HotkeyMods.Ctrl;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) mods |= HotkeyMods.Shift;
                if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) mods |= HotkeyMods.Alt;

                foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                {
                    if (k == KeyCode.None || k == KeyCode.Escape) continue;
                    if (k == KeyCode.LeftControl || k == KeyCode.RightControl) continue;
                    if (k == KeyCode.LeftShift || k == KeyCode.RightShift) continue;
                    if (k == KeyCode.LeftAlt || k == KeyCode.RightAlt) continue;
                    if ((int)k >= (int)KeyCode.Mouse0 && (int)k <= (int)KeyCode.Mouse6) continue;
                    if (!Input.GetKeyDown(k)) continue;

                    string text = HotkeyStore.Format(k, mods);
                    HotkeyStore.SetBinding(_captureModId, _captureSpec, text);
                    MelonLogger.Msg("hotkey: " + _captureModId + "/" + _captureSpec.Id + " -> " + text);
                    if (_captureValue != null) _captureValue.text = text;   // 立即显示新键（别被 Cancel 恢复成旧值）
                    _capturePrevText = text;
                    CancelHotkeyCapture();
                    return;
                }
            }
            catch { }
        }

        // ===================== 滑条 =====================
        internal Slider BuildSlider(RectTransform rt, float min, float max, float value, bool whole, UnityAction<float> onChange)
        {
            var slider = rt.gameObject.AddComponent<Slider>();

            var bg = NewRect("Background", rt);
            bg.anchorMin = new Vector2(0f, 0.5f);
            bg.anchorMax = new Vector2(1f, 0.5f);
            bg.sizeDelta = new Vector2(0f, 6f);
            var bgImg = bg.gameObject.AddComponent<Image>();
            var track = GetSprite("T_SliderBase");
            if (track != null) { bgImg.sprite = track; bgImg.type = Image.Type.Sliced; }
            bgImg.color = new Color(1f, 1f, 1f, 0.20f);
            bgImg.raycastTarget = false;

            var fillArea = NewRect("Fill Area", rt);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, 6f);
            fillArea.offsetMin = new Vector2(0f, -3f);
            fillArea.offsetMax = new Vector2(0f, 3f);

            var fill = NewRect("Fill", fillArea);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.sizeDelta = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(1f, 1f, 1f, 0.62f);
            fillImg.raycastTarget = false;

            var handleArea = NewRect("Handle Slide Area", rt);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(4f, 0f);
            handleArea.offsetMax = new Vector2(-4f, 0f);

            var handle = NewRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0.5f);
            handle.anchorMax = new Vector2(0f, 0.5f);
            handle.sizeDelta = new Vector2(13.44f, 13.44f);
            var hImg = handle.gameObject.AddComponent<Image>();
            var hsp = GetSprite("T_RoomLightSlider_Handle");
            if (hsp != null) { hImg.sprite = hsp; hImg.type = Image.Type.Sliced; }
            hImg.color = Color.white;
            hImg.raycastTarget = true;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max > min ? max : min + 1f;
            slider.wholeNumbers = whole;
            slider.value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
            if (onChange != null) slider.onValueChanged.AddListener(onChange);
            return slider;
        }
    }
}

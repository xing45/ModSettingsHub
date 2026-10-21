// ============================================================================
// ModSettingsHub — 条目收集（partial）
//   A. IModSettingsPage（mod 自己 Build）
//   B. IModSettingsEntry（mod 已有面板，只给入口）
//   C. MelonPreferences 自动生成页
// ============================================================================
using System;
using System.Collections.Generic;
using MelonLoader;
using ModSettingsHub.Api;

namespace ModSettingsHub
{
    public partial class ModSettingsHubMod
    {
        internal const string HubName = "ModSettingsHub";

        // ---- 一条设置页（注册页 / 注册入口 / 自动配置分类） ----
        internal sealed class HubEntry
        {
            public string Id = "";
            public string ModId = "";      // = MelonPreferences 分类 id / 注册 id（快捷键与内置 UI 用）
            public string Name = "";
            public byte[] IconPng;
            public IModSettingsPage Page;
            public IModSettingsEntry Entry;
            public MelonPreferences_Category Cfg;
        }

        internal readonly List<HubEntry> Entries = new List<HubEntry>();
        string _entrySignature = "";

        // ===================== 收集条目 =====================
        bool RefreshEntries()
        {
            try
            {
                var list = new List<HubEntry>();
                var seen = new HashSet<string>();

                // A. 注册的自建页
                var pages = ModSettingsRegistry.SnapshotPages();
                for (int i = 0; i < pages.Count; i++)
                {
                    var p = pages[i];
                    if (p == null || string.IsNullOrEmpty(p.Id)) continue;
                    string id = "page:" + p.Id;
                    if (!seen.Add(id)) continue;
                    list.Add(new HubEntry { Id = id, ModId = p.Id, Name = SafeName(p.DisplayName, p.Id), IconPng = p.IconPng, Page = p });
                }

                // B. 注册的"打开我自己面板"入口
                var entries = ModSettingsRegistry.SnapshotEntries();
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                    string id = "entry:" + e.Id;
                    if (!seen.Add(id)) continue;
                    list.Add(new HubEntry { Id = id, ModId = e.Id, Name = SafeName(e.DisplayName, e.Id), IconPng = e.IconPng, Entry = e });
                }

                // C. MelonPreferences 自动页（排除 Hub 自己、排除已经用 A/B 覆盖的 mod）
                try
                {
                    var cats = MelonPreferences.Categories;
                    if (cats != null)
                    {
                        for (int i = 0; i < cats.Count; i++)
                        {
                            var c = cats[i];
                            if (c == null) continue;
                            string cid = c.Identifier;
                            if (string.IsNullOrEmpty(cid)) continue;
                            if (cid == HubName) continue;
                            if (HideCategory(cid)) continue;
                            try { if (c.Entries == null || c.Entries.Count == 0) continue; } catch { continue; }
                            // 已被 A/B 覆盖（按 id 去掉 "page:"/"entry:" 前缀比较）
                            bool covered = false;
                            foreach (var s in seen)
                            {
                                int k = s.IndexOf(':');
                                if (k >= 0 && string.Equals(s.Substring(k + 1), cid, StringComparison.OrdinalIgnoreCase)) { covered = true; break; }
                            }
                            if (covered) continue;
                            string id = "cfg:" + cid;
                            if (!seen.Add(id)) continue;
                            list.Add(new HubEntry { Id = id, ModId = cid, Name = ChineseName(cid), Cfg = c });
                        }
                    }
                }
                catch (Exception e) { LogOnce("cfgerr", "prefs scan err " + e.Message); }

                // 签名变化才替换（避免每次 tick 重建图标）
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < list.Count; i++) sb.Append(list[i].Id).Append('|');
                string sig = sb.ToString();
                if (sig == _entrySignature) return false;

                _entrySignature = sig;
                Entries.Clear();
                Entries.AddRange(list);
                LogOnce("entries:" + list.Count, "entries changed -> " + list.Count + " (" + sig + ")");
                return true;
            }
            catch (Exception e)
            {
                LogOnce("refresherr", "refresh entries err " + e);
                return false;
            }
        }
    }
}

// ============================================================================
// ModSettingsHub — 通用工具方法（partial）
//   日志去重、层级查找、显示名兜底、分类过滤。
// ============================================================================
using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace ModSettingsHub
{
    public partial class ModSettingsHubMod
    {
        // ===================== 日志 =====================
        readonly HashSet<string> _loggedKeys = new HashSet<string>();

        internal void LogOnce(string key, string msg)
        {
            // 只打一次（不跟 verbose 走）
            try
            {
                if (_loggedKeys.Add(key)) MelonLogger.Msg(msg);
            }
            catch { }
        }

        // ===================== 层级查找 =====================
        static Transform DeepFind(IEnumerable<GameObject> roots, string name)
        {
            try
            {
                foreach (var go in roots)
                {
                    var t = DeepFind(go != null ? go.transform : null, name);
                    if (t != null) return t;
                }
            }
            catch { }
            return null;
        }

        static Transform DeepFind(Transform t, string name)
        {
            try
            {
                if (t == null) return null;
                if (t.name == name) return t;
                int n = t.childCount;
                for (int i = 0; i < n; i++)
                {
                    var r = DeepFind(t.GetChild(i), name);
                    if (r != null) return r;
                }
            }
            catch { }
            return null;
        }

        // ===================== 名字兜底 =====================
        static string SafeName(string a, string b)
        {
            if (!string.IsNullOrEmpty(a)) return a;
            return b ?? "";
        }

        // ===================== 分类过滤 =====================
        // 分类里只有日志/调试开关 ⇒ 不值得占一个 mod 页（用户："为什么有个表情导演？"）
        static bool OnlyLogCategory(MelonPreferences_Category c)
        {
            try
            {
                if (c == null || c.Entries == null) return false;
                int useful = 0;
                for (int i = 0; i < c.Entries.Count; i++)
                {
                    var e = c.Entries[i];
                    if (e == null) continue;
                    string id = EntryIdentifier(e);
                    if (string.IsNullOrEmpty(id)) continue;
                    if (id.IndexOf("Verbose", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (id.IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (id.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    useful++;
                }
                return useful == 0;
            }
            catch { return false; }
        }
    }
}

// ============================================================================
// ModSettingsHub — 设置场景节点定位（partial）
//   缓存 P_CommonCanvas_Landscape 下的 ContentRoot / Main / SideButtons /
//   ButtonLayout / WindowParent，供注入和覆盖层使用。
// ============================================================================
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModSettingsHub
{
    public partial class ModSettingsHubMod
    {
        // ---- 设置场景里的节点 ----
        internal Transform ContentRoot;
        internal Transform MainRoot;
        internal Transform SideButtons;
        internal Transform ButtonLayout;
        internal Transform WindowParent;

        // ===================== 找设置场景里的节点 =====================
        bool FindSettingsUi()
        {
            try
            {
                var sc = SceneManager.GetSceneByName(SettingsSceneName);
                if (!sc.IsValid() || !sc.isLoaded) return false;

                var roots = sc.GetRootGameObjects();
                if (roots == null || roots.Length == 0) return false;

                Transform canvas = null;
                for (int i = 0; i < roots.Length; i++)
                {
                    try
                    {
                        if (roots[i] == null) continue;
                        var t = roots[i].transform.Find("P_CommonCanvas_Landscape");
                        if (t != null) { canvas = t; break; }
                    }
                    catch { }
                }
                if (canvas == null) canvas = DeepFind(roots, "P_CommonCanvas_Landscape");
                if (canvas == null) return false;

                var content = canvas.Find("ContentRoot");
                if (content == null) return false;
                var main = content.Find("Main");
                if (main == null) return false;
                var side = main.Find("SideButtons");
                if (side == null) return false;
                var layout = side.Find("ButtonLayout");
                if (layout == null) return false;
                var wp = main.Find("P_SwitchSettingsWindowObject/WindowParent");
                if (wp == null) wp = DeepFind(main, "WindowParent");

                ContentRoot = content;
                MainRoot = main;
                SideButtons = side;
                ButtonLayout = layout;
                WindowParent = wp;

                if (WindowParent == null) return false;
                LogOnce("ui", "settings ui found: side='" + side.name + "' buttons=" + layout.childCount
                    + " windowParent='" + WindowParent.name + "' children=" + WindowParent.childCount);
                return true;
            }
            catch (Exception e)
            {
                LogOnce("finderr", "find ui err " + e.Message);
                return false;
            }
        }
    }
}

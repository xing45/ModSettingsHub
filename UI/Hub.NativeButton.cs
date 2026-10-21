// ============================================================================
// ModSettingsHub — 原生侧边按钮接线（强类型版）
//   克隆按钮直接使用游戏程序集里的 View / ViewModel / CommonButton，
//   选中状态用我们自己的 Subject<bool>，不涉及 WindowType / Presenter。
// ============================================================================
using System;
using Il2CppCommon.Prefabs.CommonButton;
using Il2CppCommon.Prefabs.DialogObject;
using Il2CppProject.SettingsScene.SettingsSideButtonObject;
using Il2CppR3;
using UnityEngine;
using UnityEngine.Localization.Components;

namespace ModSettingsHub
{
    public partial class ModSettingsHubMod
    {
        Subject<bool> _hubButtonSubject;

        void TryWireNativeSideButton(GameObject go, Transform template)
        {
            try
            {
                var view = go.GetComponent<SettingsSideButtonObjectView>();
                var cbbDst = go.GetComponentInChildren<CommonButtonBehaviour>(true);
                var cbbSrc = template != null ? template.GetComponentInChildren<CommonButtonBehaviour>(true) : null;

                if (view == null || cbbDst == null)
                {
                    LogOnce("hubbtn:comp", "native side button: View/CommonButton missing");
                    return;
                }

                // 克隆的 CommonButton 没有注入 AudioManager，从原生模板复制
                if (cbbSrc != null)
                {
                    try { cbbDst._soundEffectPlayer = cbbSrc._soundEffectPlayer; }
                    catch (Exception e) { LogOnce("hubbtn:snd", "native side button: sound copy failed: " + e.Message); }
                }

                // 用我们的图标替换原生 Graphics 的 matched/unmatched
                var icon = GetSprite("XM_ModGrid");
                if (icon != null)
                {
                    view._matchedSprite = icon;
                    view._unmatchedSprite = icon;
                }

                // Tips 文本 + 禁用本地化，避免被模板的“图像”覆盖
                var tip = go.transform.Find("Tips/CommonLocalizeText/Text (TMP)");
                if (tip != null)
                {
                    // Text (TMP) 上还有一个 Unity Localization 的 LocalizeStringEvent，
                    // 它会在启用/刷新时把文本写回模板的 “图像”。
                    var locEvent = tip.GetComponent<LocalizeStringEvent>();
                    if (locEvent != null) locEvent.enabled = false;
                    SetTmp(tip, "MOD 设置");
                }

                var locParent = go.transform.Find("Tips/CommonLocalizeText");
                var loc = locParent != null ? locParent.GetComponent<CommonLocalizeTextBehaviour>() : null;
                if (loc != null) loc.enabled = false;

                // 原生 View 只接收 Observable<bool>：Hub 是否打开
                var subject = new Subject<bool>();
                var vm = new SettingsSideButtonObjectViewModel(subject);
                view.InitView(vm);

                _hubButtonSubject = subject;
                SetHubButtonSelected(_openId != null);
                LogOnce("hubbtn:ok", "native side button: wired with own bool state");
            }
            catch (Exception e)
            {
                LogOnce("hubbtn:err", "native side button wiring failed: " + e.Message);
            }
        }

        void SetHubButtonSelected(bool on)
        {
            try { if (_hubButtonSubject != null) _hubButtonSubject.OnNext(on); }
            catch { }
        }

        void ClearHubButtonWiring()
        {
            _hubButtonSubject = null;
        }
    }
}

using UnityEngine;
using UnityEditor;
using static WeChatWASM.WXConvertCore;

namespace WeChatWASM
{

    public class WXPlayableWin : EditorWindow
    {
        [MenuItem("WeChat 작은게임 / 작은게임 시도용 변환", false, 2)]
        public static void Open()
        {
            var win = GetWindow(typeof(WXPlayableWin), false, "WeChat 작은게임 시도용 변환 도구 패널");
            win.minSize = new Vector2(350, 400);
            win.position = new Rect(200, 200, 600, 300);
            win.Show();
        }

        // 이전 버전과의 호환성을 위해 WXConvertCore.cs를 사용하십시오
        public static WXExportError DoExport(bool buildWebGL = true)
        {
            return WXPlayableConvertCore.DoExport(buildWebGL);
        }

        public void OnFocus()
        {
            WXPlayableSettingsHelperInterface.helper.OnFocus();
        }

        public void OnLostFocus()
        {
            WXPlayableSettingsHelperInterface.helper.OnLostFocus();
        }

        public void OnDisable()
        {
            WXPlayableSettingsHelperInterface.helper.OnDisable();
        }

        public void OnGUI()
        {
            WXPlayableSettingsHelperInterface.helper.OnSettingsGUI(this);
            WXPlayableSettingsHelperInterface.helper.OnBuildButtonGUI(this);
        }
    }
}

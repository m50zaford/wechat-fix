using UnityEngine;
using UnityEditor;
using static WeChatWASM.WXConvertCore;

namespace WeChatWASM
{

    public class WXPlayableWin : EditorWindow
    {
        [MenuItem("위챗 미니게임 / 미니게임 시험판 변환", false, 2)]
        public static void Open()
        {
            var win = GetWindow(typeof(WXPlayableWin), false, "위챗 미니게임 시험판 변환 도구 패널");
            win.minSize = new Vector2(350, 400);
            win.position = new Rect(200, 200, 600, 300);
            win.Show();
        }

        // 向前兼容，请使用 WXConvertCore.cs
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
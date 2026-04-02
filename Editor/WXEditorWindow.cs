using UnityEngine;
using UnityEditor;
using static WeChatWASM.WXConvertCore;

namespace WeChatWASM
{

    public class WXEditorWin : EditorWindow
    {
        [MenuItem("위챗 미니게임 / 미니게임 변환", false, 1)]
        public static void Open()
        {
            var win = GetWindow(typeof(WXEditorWin), false, "위챗 미니게임 변환 도구 패널");
            win.minSize = new Vector2(350, 400);
            win.position = new Rect(100, 100, 600, 700);
            win.Show();
        }

        // 向前兼容，请使用 WXConvertCore.cs
        public static WXExportError DoExport(bool buildWebGL = true)
        {
            return WXConvertCore.DoExport(buildWebGL);
        }

        public void OnFocus()
        {
            WXSettingsHelperInterface.helper.OnFocus();
        }

        public void OnLostFocus()
        {
            WXSettingsHelperInterface.helper.OnLostFocus();
        }

        public void OnDisable()
        {
            WXSettingsHelperInterface.helper.OnDisable();
        }

        public void OnGUI()
        {
            WXSettingsHelperInterface.helper.OnSettingsGUI(this);
            WXSettingsHelperInterface.helper.OnBuildButtonGUI(this);
        }
    }
}
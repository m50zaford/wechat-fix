using System;
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

        // ── DLL(wx-editor.dll) 내부 중국어 메뉴 항목을 한글 메뉴로 우회 호출 ──

        [MenuItem("위챗 미니게임 / 위챗 압축 텍스처", false, 101)]
        public static void OpenTextureEditor()
        {
            OpenDllWindow("WeChatWASM.WXTextureEditorWindow", "위챗 압축 텍스처");
        }

        [MenuItem("위챗 미니게임 / 리소스 최적화 도구", false, 102)]
        public static void OpenAnalysisTool()
        {
            EditorApplication.ExecuteMenuItem("微信小游戏 / 资源优化工具");
        }

        [MenuItem("위챗 미니게임 / PlayerPrefs 최적화", false, 103)]
        public static void OpenPlayerPrefsOptimizer()
        {
            OpenDllWindow("WeChatWASM.WXPlayerPrefsWindow", "PlayerPrefs 최적화");
        }

        [MenuItem("위챗 미니게임 / 플러그인 버전", false, 105)]
        public static void OpenPluginVersion()
        {
            OpenDllWindow("WeChatWASM.WXUpdateWindow", "플러그인 버전");
        }

        private static void OpenDllWindow(string fullTypeName, string fallbackTitle)
        {
            Type windowType = Type.GetType(fullTypeName + ", wx-editor");

            if (windowType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    windowType = asm.GetType(fullTypeName);
                    if (windowType != null) break;
                }
            }

            if (windowType != null && typeof(EditorWindow).IsAssignableFrom(windowType))
            {
                var window = GetWindow(windowType, false, fallbackTitle, true);
                window.Show();
            }
            else
            {
                Debug.LogError($"[WXEditorWin] '{fullTypeName}' 타입을 찾을 수 없습니다. wx-editor.dll이 프로젝트에 포함되어 있는지 확인하세요.");
            }
        }
    }
}
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WeChatWASM
{
    /// <summary>
    /// DLL(wx-editor.dll) 내부에 정의된 중국어 메뉴 항목을 한글 메뉴로 우회 호출하는 래퍼.
    /// 원본 메뉴: "微信小游戏 / 微信压缩纹理", "资源优化工具", "PlayerPrefs优化", "插件版本"
    /// </summary>
    public static class WXMenuKoreanWrapper
    {
        [MenuItem("위챗 미니게임 / 위챗 압축 텍스처", false, 101)]
        public static void OpenTextureEditor()
        {
            OpenDllWindow("WeChatWASM.WXTextureEditorWindow", "위챗 압축 텍스처");
        }

        [MenuItem("위챗 미니게임 / 리소스 최적화 도구", false, 102)]
        public static void OpenAnalysisTool()
        {
            // 리소스 최적화 도구는 여러 하위 윈도우(Audio, Texture, Prefab, Font 등)를 포함하는 통합 도구
            // DLL 내부의 원본 MenuItem을 직접 실행
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

        /// <summary>
        /// wx-editor.dll 내부의 EditorWindow 타입을 Reflection으로 찾아서 열기
        /// </summary>
        private static void OpenDllWindow(string fullTypeName, string fallbackTitle)
        {
            // wx-editor 어셈블리에서 타입 검색
            Type windowType = Type.GetType(fullTypeName + ", wx-editor");

            if (windowType == null)
            {
                // 모든 로드된 어셈블리에서 검색
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    windowType = asm.GetType(fullTypeName);
                    if (windowType != null) break;
                }
            }

            if (windowType != null && typeof(EditorWindow).IsAssignableFrom(windowType))
            {
                var window = EditorWindow.GetWindow(windowType, false, fallbackTitle, true);
                window.Show();
            }
            else
            {
                Debug.LogError($"[WXMenuKoreanWrapper] '{fullTypeName}' 타입을 찾을 수 없습니다. wx-editor.dll이 프로젝트에 포함되어 있는지 확인하세요.");
            }
        }
    }
}

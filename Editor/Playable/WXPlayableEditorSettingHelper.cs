using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WeChatWASM
{
    [InitializeOnLoad]
    public class WXPlayableSettingsHelperInterface
    {
        public static WXPlayableSettingsHelper helper = new WXPlayableSettingsHelper();
    }

    public class WXPlayableSettingsHelper
    {
        public static string projectRootPath;
        private static WXPlayableEditorScriptObject config;
        private static bool m_EnablePerfTool = false;
        public static bool UseIL2CPP
        {
            get
            {
#if TUANJIE_2022_3_OR_NEWER
                return PlayerSettings.GetScriptingBackend(BuildTargetGroup.WeixinMiniGame) == ScriptingImplementation.IL2CPP;
#else
                return true;
#endif
            }
        }

        public WXPlayableSettingsHelper()
        {
            projectRootPath = System.IO.Path.GetFullPath(Application.dataPath + "/../");
        }

        public void OnFocus()
        {
            loadData();
        }

        public void OnLostFocus()
        {
            saveData();
        }

        public void OnDisable()
        {
            EditorUtility.SetDirty(config);
        }

        private Vector2 scrollRoot;
        private bool foldBaseInfo = true;
        private bool foldDebugOptions = true;
        public void OnSettingsGUI(EditorWindow window)
        {
            scrollRoot = EditorGUILayout.BeginScrollView(scrollRoot);
            GUIStyle linkStyle = new GUIStyle(GUI.skin.label);
            linkStyle.normal.textColor = Color.yellow;
            linkStyle.hover.textColor = Color.yellow;
            linkStyle.stretchWidth = false;
            linkStyle.alignment = TextAnchor.UpperLeft;
            linkStyle.wordWrap = true;

            foldBaseInfo = EditorGUILayout.Foldout(foldBaseInfo, "기본 정보");
            if (foldBaseInfo)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));
                this.formInput("appid", "미니게임 시험판 AppID");
                this.formInput("projectName", "미니게임 시험판 프로젝트명");
                this.formIntPopup("orientation", "게임 방향", new[] { "Portrait", "Landscape" }, new[] { 0, 1, 2, 3 });
                this.formInput("memorySize", "UnityHeap 예약 메모리(?)", "단위: MB, 사전 할당 메모리 값. 초경량 게임 256/중경량 496/고사양 게임 768. 게임의 최대 UnityHeap 값을 예측하여 메모리 자동 확장으로 인한 피크 스파이크를 방지해야 합니다. 예측 방법은 GIT 문서 'Unity WebGL 메모리 최적화'를 참조하세요.");

                GUILayout.BeginHorizontal();
                string targetDst = "dst";
                if (!formInputData.ContainsKey(targetDst))
                {
                    formInputData[targetDst] = "";
                }
                EditorGUILayout.LabelField(string.Empty, GUILayout.Width(10));
                GUILayout.Label(new GUIContent("내보내기 경로(?)", "프로젝트 루트 디렉토리 기준 상대 경로 입력 지원, 예: wxbuild"), GUILayout.Width(140));
                formInputData[targetDst] = GUILayout.TextField(formInputData[targetDst], GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 270));
                if (GUILayout.Button(new GUIContent("열기"), GUILayout.Width(40)))
                {
                    if (!formInputData[targetDst].Trim().Equals(string.Empty))
                    {
                        EditorUtility.RevealInFinder(GetAbsolutePath(formInputData[targetDst]));
                    }
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(new GUIContent("선택"), GUILayout.Width(40)))
                {
                    var dstPath = EditorUtility.SaveFolderPanel("게임 내보내기 디렉토리 선택", string.Empty, string.Empty);
                    if (dstPath != string.Empty)
                    {
                        formInputData[targetDst] = dstPath;
                        this.saveData();
                    }
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();


                EditorGUILayout.EndVertical();
            }

            foldDebugOptions = EditorGUILayout.Foldout(foldDebugOptions, "디버그 빌드 옵션");
            if (foldDebugOptions)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));
                this.formCheckbox("developBuild", "Development Build", "", false, null, OnDevelopmentBuildToggleChanged);
                this.formCheckbox("il2CppOptimizeSize", "Il2Cpp Optimize Size(?)", "Il2CppCodeGeneration 옵션에 해당합니다. 체크 시 OptimizeSize 사용(기본 권장), 생성 코드 약 15% 감소. 체크 해제 시 OptimizeSpeed 사용. 대량의 제네릭 컬렉션 고빈도 접근 시 OptimizeSpeed 권장. HybridCLR 등 서드파티 컴포넌트 사용 시 OptimizeSpeed만 가능. (Dotnet Runtime 모드에서는 이 옵션 무효)", !UseIL2CPP);
                this.formCheckbox("profilingFuncs", "Profiling Funcs");
                this.formCheckbox("webgl2", "WebGL2.0");
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }
        public void OnBuildButtonGUI(EditorWindow window)
        {
            GUIStyle linkStyle = new GUIStyle(GUI.skin.label);
            linkStyle.normal.textColor = Color.yellow;
            linkStyle.hover.textColor = Color.yellow;
            linkStyle.stretchWidth = false;
            linkStyle.alignment = TextAnchor.UpperLeft;
            linkStyle.wordWrap = true;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Empty, GUILayout.MinWidth(10));
            if (GUILayout.Button(new GUIContent("빌드 및 변환"), GUILayout.Width(100), GUILayout.Height(25)))
            {
                this.saveData();
                if (WXPlayableConvertCore.DoExport() == WXConvertCore.WXExportError.SUCCEED)
                {
                    window.ShowNotification(new GUIContent("변환 완료"));
                }
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
        private void OnDevelopmentBuildToggleChanged(bool InNewValue)
        {
            // 针对non-dev build，取消性能分析工具的集成
            if (!InNewValue)
            {
                this.setData("enablePerfAnalysis", false);
            }
        }

        private string SDKFilePath;

        private void loadData()
        {
            SDKFilePath = Path.Combine(UnityUtil.GetWxSDKRootPath(), "Runtime", "wechat-playable-default", "unity-sdk", "index.js");
            config = UnityUtil.GetPlayableEditorConf();

            this.setData("projectName", config.ProjectConf.projectName);
            this.setData("appid", config.ProjectConf.Appid);
            this.setData("orientation", (int)config.ProjectConf.Orientation);
            this.setData("dst", config.ProjectConf.relativeDST);

            this.setData("developBuild", config.CompileOptions.DevelopBuild);
            this.setData("il2CppOptimizeSize", config.CompileOptions.Il2CppOptimizeSize);
            this.setData("profilingFuncs", config.CompileOptions.profilingFuncs);
            this.setData("webgl2", config.CompileOptions.Webgl2);
            this.setData("customNodePath", config.CompileOptions.CustomNodePath);

            this.setData("memorySize", config.ProjectConf.MemorySize.ToString());
        }

        private void saveData()
        {
            config.ProjectConf.projectName = this.getDataInput("projectName");
            config.ProjectConf.Appid = this.getDataInput("appid");
            config.ProjectConf.Orientation = (WXScreenOritation)this.getDataPop("orientation");
            config.ProjectConf.relativeDST = this.getDataInput("dst");
            config.ProjectConf.DST = GetAbsolutePath(config.ProjectConf.relativeDST);

            config.CompileOptions.DevelopBuild = this.getDataCheckbox("developBuild");
            config.CompileOptions.Il2CppOptimizeSize = this.getDataCheckbox("il2CppOptimizeSize");
            config.CompileOptions.profilingFuncs = this.getDataCheckbox("profilingFuncs");
            config.CompileOptions.CustomNodePath = this.getDataInput("customNodePath");
            config.CompileOptions.Webgl2 = this.getDataCheckbox("webgl2");
            config.ProjectConf.MemorySize = int.Parse(this.getDataInput("memorySize"));
        }

        private Dictionary<string, string> formInputData = new Dictionary<string, string>();
        private Dictionary<string, int> formIntPopupData = new Dictionary<string, int>();
        private Dictionary<string, bool> formCheckboxData = new Dictionary<string, bool>();

        private string getDataInput(string target)
        {
            if (this.formInputData.ContainsKey(target))
                return this.formInputData[target];
            return "";
        }

        private int getDataPop(string target)
        {
            if (this.formIntPopupData.ContainsKey(target))
                return this.formIntPopupData[target];
            return 0;
        }

        private bool getDataCheckbox(string target)
        {
            if (this.formCheckboxData.ContainsKey(target))
                return this.formCheckboxData[target];
            return false;
        }

        private void formCheckbox(string target, string label, string help = null, bool disable = false, Action<bool> setting = null, Action<bool> onValueChanged = null)
        {
            if (!formCheckboxData.ContainsKey(target))
            {
                formCheckboxData[target] = false;
            }
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Empty, GUILayout.Width(10));
            if (help == null)
            {
                GUILayout.Label(label, GUILayout.Width(140));
            }
            else
            {
                GUILayout.Label(new GUIContent(label, help), GUILayout.Width(140));
            }
            EditorGUI.BeginDisabledGroup(disable);

            // Toggle the checkbox value based on the disable condition
            bool newValue = EditorGUILayout.Toggle(disable ? false : formCheckboxData[target]);
            // Update the checkbox data if the value has changed and invoke the onValueChanged action
            if (newValue != formCheckboxData[target])
            {
                formCheckboxData[target] = newValue;
                onValueChanged?.Invoke(newValue);
            }

            if (setting != null)
            {
                EditorGUILayout.LabelField("", GUILayout.Width(10));
                // 配置按钮
                if (GUILayout.Button(new GUIContent("설정"), GUILayout.Width(40), GUILayout.Height(18)))
                {
                    setting?.Invoke(true);
                }
                EditorGUILayout.LabelField("", GUILayout.MinWidth(10));
            }

            EditorGUI.EndDisabledGroup();

            if (setting == null)
                EditorGUILayout.LabelField(string.Empty);
            GUILayout.EndHorizontal();
        }

        private void setData(string target, string value)
        {
            if (formInputData.ContainsKey(target))
            {
                formInputData[target] = value;
            }
            else
            {
                formInputData.Add(target, value);
            }
        }

        private void setData(string target, bool value)
        {
            if (formCheckboxData.ContainsKey(target))
            {
                formCheckboxData[target] = value;
            }
            else
            {
                formCheckboxData.Add(target, value);
            }
        }

        private void setData(string target, int value)
        {
            if (formIntPopupData.ContainsKey(target))
            {
                formIntPopupData[target] = value;
            }
            else
            {
                formIntPopupData.Add(target, value);
            }
        }

        private void formInput(string target, string label, string help = null)
        {
            if (!formInputData.ContainsKey(target))
            {
                formInputData[target] = "";
            }
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Empty, GUILayout.Width(10));
            if (help == null)
            {
                GUILayout.Label(label, GUILayout.Width(140));
            }
            else
            {
                GUILayout.Label(new GUIContent(label, help), GUILayout.Width(140));
            }
            formInputData[target] = GUILayout.TextField(formInputData[target], GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 195));
            GUILayout.EndHorizontal();
        }

        private void formIntPopup(string target, string label, string[] options, int[] values)
        {
            if (!formIntPopupData.ContainsKey(target))
            {
                formIntPopupData[target] = 0;
            }
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Empty, GUILayout.Width(10));
            GUILayout.Label(label, GUILayout.Width(140));
            formIntPopupData[target] = EditorGUILayout.IntPopup(formIntPopupData[target], options, values, GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 195));
            GUILayout.EndHorizontal();
        }

        public static bool IsAbsolutePath(string path)
        {
            // 检查是否为空或空白
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // 在 Windows 上，检查驱动器字母或网络路径
            if (Application.platform == RuntimePlatform.WindowsEditor && Path.IsPathRooted(path))
            {
                return true;
            }

            // 在 Unix/Linux 和 macOS 上，检查是否以 '/' 开头
            if (Application.platform == RuntimePlatform.OSXEditor && path.StartsWith("/"))
            {
                return true;
            }

            return false; // 否则为相对路径
        }

        public static string GetAbsolutePath(string path)
        {
            if (IsAbsolutePath(path))
            {
                return path;
            }

            return Path.Combine(projectRootPath, path);
        }
    }
}
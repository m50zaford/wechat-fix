#if TUANJIE_1_6_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using static WeChatWASM.WXConvertCore;

namespace WeChatWASM
{
    public class WeixinMiniGameSettingsEditor : MiniGameSettingsEditor
    {
        private Vector2 scrollRoot;
        private bool foldBaseInfo = true;
        private bool foldLoadingConfig = true;
        private bool foldSDKOptions = true;
        private bool foldDebugOptions = true;

        private bool foldInstantGame = false;

        private bool foldFontOptions = false;
        private Dictionary<string, string> formInputData = new Dictionary<string, string>();
        private Dictionary<string, int> formIntPopupData = new Dictionary<string, int>();
        private Dictionary<string, bool> formCheckboxData = new Dictionary<string, bool>();
        public Texture tex;

        public override void OnMiniGameSettingsIMGUI(SerializedObject serializedObject, SerializedProperty miniGameProperty)
        {
            OnSettingsGUI(serializedObject, miniGameProperty);
        }

        public void OnSettingsGUI(SerializedObject serializedObject, SerializedProperty miniGameProperty)
        {
            loadData(serializedObject, miniGameProperty);

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

                formInput("appid", "게임 AppID");
                formInput("cdn", "게임 자원 CDN");
                formInput("projectName", "小游戏 프로젝트명");
                formIntPopup("orientation", "게임 방향", new[] { "Portrait", "Landscape", "LandscapeLeft", "LandscapeRight" }, new[] { 0, 1, 2, 3 });
                this.formInput("memorySize", "UnityHeap 예약 메모리(?)", "단위 MB, 사전 할당 메모리 값. 초闲游戏 256/중경도 496/중무게임 768, 게임 최대 UnityHeap 값을 예측하여 메모리 자동 확장으로 인한 피크 상승을 방지합니다. 예측 방법은 GIT 문서 《Unity WebGL 메모리 최적화》를 참조하세요.");

                EditorGUILayout.EndVertical();
            }

            foldLoadingConfig = EditorGUILayout.Foldout(foldLoadingConfig, "시작 로딩 설정");
            if (foldLoadingConfig)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));

                GUILayout.BeginHorizontal();
                string targetBg = "bgImageSrc";
                EditorGUILayout.LabelField(string.Empty, GUILayout.Width(10));
                tex = (Texture)EditorGUILayout.ObjectField("시작 배경 이미지/비디오 커버", tex, typeof(Texture2D), false);
                var currentBgSrc = AssetDatabase.GetAssetPath(tex);
                if (!string.IsNullOrEmpty(currentBgSrc) && currentBgSrc != formInputData[targetBg])
                {
                    formInputData[targetBg] = currentBgSrc;
                    saveData(serializedObject, miniGameProperty);
                }
                GUILayout.EndHorizontal();

                formInput("videoUrl", "로딩 단계 비디오 URL");
                formIntPopup("assetLoadType", "첫 번째 패키지 자원 로딩 방식", new[] { "CDN", "작은게임 패키지 내" }, new[] { 0, 1 });
                formCheckbox("compressDataPackage", "첫 번째 패키지 자원 압축(?)", "첫 번째 패키지 자원을 Brotli로 압축하여 자원 크기를 줄입니다. 참고: 초기 시작 시간은 약 200ms 증가할 수 있습니다.小游戏 분할 로딩 사용 시 패키지 크기 절감에만 추천합니다.");
                formInput("bundleExcludeExtensions", "자동 캐시하지 않을 파일 유형(?)", "(;로 구분) 요청 URL에 'cdn+StreamingAssets'가 포함되면 자동으로 캐시되지만, StreamingAssets 디렉토리의 모든 파일을 캐시할 필요는 없습니다. 이 옵션은 자동 캐시하지 않을 파일 확장자를 설정합니다. 기본값: json");
                formInput("bundleHashLength", "Bundle 이름 Hash 길이(?)", "Bundle 파일 이름의 Hash 부분 길이를 사용자 정의합니다. 기본값은 32이며, 캐시 제어에 사용됩니다.");
                this.formInput("preloadFiles", "사전 다운로드 파일 목록(?)", ";로 구분하며, 와일드카드 매칭을 지원합니다.");

                EditorGUILayout.EndVertical();
            }

            foldSDKOptions = EditorGUILayout.Foldout(foldSDKOptions, "SDK 기능 옵션");
            if (foldSDKOptions)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));

                formCheckbox("useFriendRelation", "친구 관계망 기능 사용");
                formCheckbox("useMiniGameChat", "소셜 구성 요소 사용");
                formCheckbox("preloadWXFont", "WeChat 글꼴 사전 로드(?)", "game.js 실행 시작 시 WeChat 시스템 글꼴을 사전 로드하며, 실행 중에 WX.GetWXFont를 사용하여 WeChat 글꼴을 가져올 수 있습니다.");
                formCheckbox("disableMultiTouch", "다중 터치 금지");

                EditorGUILayout.EndVertical();
            }

            foldDebugOptions = EditorGUILayout.Foldout(foldDebugOptions, "디버그 빌드 옵션");
            if (foldDebugOptions)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));
                // formCheckbox("developBuild", "Development Build");
                formCheckbox("autoProfile", "Auto connect Profiler");
                formCheckbox("scriptOnly", "Scripts Only Build");
#if TUANJIE_2022_3_OR_NEWER
                // TODO: if overwrite by OverwritePlayerSettings
                bool UseIL2CPP = PlayerSettings.GetScriptingBackend(BuildTargetGroup.WeixinMiniGame) == ScriptingImplementation.IL2CPP;
#else
                bool UseIL2CPP = true;
#endif
                formCheckbox("il2CppOptimizeSize", "Il2Cpp 최적화 크기(?)", "Il2CppCodeGeneration 옵션에 해당하며, 선택 시 OptimizeSize(기본 추천)를 사용하여 코드 크기를 약 15% 줄이고, 선택 해제 시 OptimizeSpeed를 사용합니다. 게임 내에서 제네릭 컬렉션의 고빈도 액세스는 OptimizeSpeed를 권장하며, HybridCLR 등 타사 구성 요소 사용 시에는 OptimizeSpeed만 사용할 수 있습니다. (Dotnet Runtime 모드에서는 이 옵션이 유효하지 않습니다)", !UseIL2CPP);
                formCheckbox("profilingFuncs", "Profiling Funcs");
                formCheckbox("profilingMemory", "Profiling Memory");

                formCheckbox("webgl2", "WebGL 2.0(베타)");
                formCheckbox("iOSPerformancePlus", "iOS 성능 모드+(?)", "iOS 고성능+ 렌더링 방안을 사용할지 여부로, 렌더링 호환성을 향상시키고 WebContent 프로세스 메모리를 줄이는 데 도움이 됩니다.");
                formCheckbox("EmscriptenGLX", "EmscriptenGLX(?)", "EmscriptenGLX 렌더링 방안을 사용할지 여부");
                formCheckbox("iOSMetal", "iOS Metal(?)", "iOS Metal 렌더링 방안을 사용할지 여부, iOS 고성능+ 모드가 활성화되어 있어야 하며, 실행 성능 향상과 iOS 전력 소비 감소에 도움이 됩니다.");
                formCheckbox("deleteStreamingAssets", "Clear Streaming Assets");
                 formCheckbox("cleanBuild", "WebGL 빌드 정리");
                // formCheckbox("cleanCloudDev", "Clean Cloud Dev");
                formCheckbox("fbslim", "첫 번째 패키지 자원 최적화(?)", "내보낼 때 UnityEditor 기본 패키징이지만 게임 프로젝트에서 사용하지 않은 자원을 자동으로 정리하여 첫 번째 패키지 자원 크기를 줄입니다. (团结 엔진은 이 기능이 더 이상 필요하지 않습니다)", UnityUtil.GetEngineVersion() > 0, (res) =>
                {
                    var fbWin = EditorWindow.GetWindow(typeof(WXFbSettingWindow), false, "첫 번째 패키지 자원 최적화 구성 패널", true);
                    fbWin.minSize = new Vector2(680, 350);
                    fbWin.Show();
                });
                formCheckbox("autoAdaptScreen", "화면 크기 자동 조정(?)", "모바일 기기에서 화면 회전 및 PC에서 창 크기 조정 시 캔버스 크기를 자동으로 조정합니다.");
                formCheckbox("showMonitorSuggestModal", "최적화 제안 팝업 표시");
                formCheckbox("enableProfileStats", "성능 패널 표시");
                formCheckbox("enableRenderAnalysis", "렌더링 로그 표시(개발자 전용)");

                {
                    this.formCheckbox("brotliMT", "brotli 다중 스레드 압축(?)", "다중 스레드 압축을 사용하면 패키지 생성 속도가 향상되지만 압축률이 낮아집니다. wasm 코드 분할 패키지를 사용하지 않는 경우 다중 스레드로 패키지를 생성하지 마십시오.");
                }
                EditorGUILayout.EndVertical();
            }

            if (WXConvertCore.IsInstantGameAutoStreaming())
            {
                foldInstantGame = EditorGUILayout.Foldout(foldInstantGame, "Instant Game - AutoStreaming");
                if (foldInstantGame)
                {
                    var automaticfillinstantgame = miniGameProperty.FindPropertyRelative("m_AutomaticFillInstantGame");
                    EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(string.Empty, GUILayout.Width(10));
                    formCheckbox("m_AutomaticFillInstantGame", "AutoStreaming 자동 작성", "AutoStreaming이 활성화된 경우에만 적용됩니다.");
                    GUILayout.EndHorizontal();
                    formInput("bundlePathIdentifier", "Bundle 경로 식별자");
                    formInput("dataFileSubPrefix", "데이터 파일 하위 접두사");

                    EditorGUI.BeginDisabledGroup(true);
                    formCheckbox("autoUploadFirstBundle", "빌드 후 첫 번째 패키지 자동 업로드(?)", "AutoStreaming이 활성화된 경우에만 적용됩니다", true);
                    EditorGUI.EndDisabledGroup();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(string.Empty, GUILayout.Width(10));
                    GUILayout.Label(new GUIContent("AS 구성 정리(?)", "AutoStreaming을 비활성화하고 기본 게시 방안을 사용하려면 AS 구성 항목을 정리해야 합니다."), GUILayout.Width(140));
                    EditorGUI.BeginDisabledGroup(WXConvertCore.IsInstantGameAutoStreaming());
                    if (GUILayout.Button(new GUIContent("복원"), GUILayout.Width(60)))
                    {
                        var ProjectConf = miniGameProperty.FindPropertyRelative("ProjectConf");
                        string identifier = ProjectConf.FindPropertyRelative("bundlePathIdentifier").stringValue;
                        string[] identifiers = identifier.Split(";");
                        string idStr = "";
                        foreach (string id in identifiers)
                        {
                            if (id != "AS" && id != "CUS/CustomAB")
                            {
                                idStr += id + ";";
                            }
                        }
                        ProjectConf.FindPropertyRelative("bundlePathIdentifier").stringValue = idStr.Trim(';');

                        if (ProjectConf.FindPropertyRelative("dataFileSubPrefix").stringValue == "CUS")
                        {
                            ProjectConf.FindPropertyRelative("dataFileSubPrefix").stringValue = "";
                        }
                        loadData(serializedObject, miniGameProperty);
                    }
                    EditorGUI.EndDisabledGroup();
                    GUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(string.Empty);
                    if (GUILayout.Button(new GUIContent("Instant Game AutoStreaming 이해하기", ""), linkStyle))
                    {
                        Application.OpenURL("https://github.com/wechat-miniprogram/minigame-unity-webgl-transform/blob/main/Design/InstantGameGuide.md");
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
            }

            {
                foldFontOptions = EditorGUILayout.Foldout(foldFontOptions, "글꼴 구성");
                if (foldFontOptions)
                {
                    EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));
                    formCheckbox("CJK_Unified_Ideographs", "기본 한자(?)", "Unicode [0x4e00, 0x9fff]");
                    formCheckbox("C0_Controls_and_Basic_Latin", "기본 라틴어(영문 대소문자, 숫자, 영문 구두점)(?)", "Unicode [0x0, 0x7f]");
                    formCheckbox("CJK_Symbols_and_Punctuation", "중국어 구두점 기호(?)", "Unicode [0x3000, 0x303f]");
                    formCheckbox("General_Punctuation", "일반 구두점 기호(?)", "Unicode [0x2000, 0x206f]");
                    formCheckbox("Enclosed_CJK_Letters_and_Months", "CJK 알파벳 및 월(?)", "Unicode [0x3200, 0x32ff]");
                    formCheckbox("Vertical_Forms", "중국어 세로 배열 구두점(?)", "Unicode [0xfe10, 0xfe1f]");
                    formCheckbox("CJK_Compatibility_Forms", "CJK 호환 기호(?)", "Unicode [0xfe30, 0xfe4f]");
                    formCheckbox("Miscellaneous_Symbols", "기타 기호(?)", "Unicode [0x2600, 0x26ff]");
                    formCheckbox("CJK_Compatibility", "CJK 특수 기호(?)", "Unicode [0x3300, 0x33ff]");
                    formCheckbox("Halfwidth_and_Fullwidth_Forms", "전각 ASCII, 전각 중영문 구두점, 반폭 카타카나, 반폭 히라가나, 반폭 한글 자모(?)", "Unicode [0xff00, 0xffef]");
                    formCheckbox("Dingbats", "장식 기호(?)", "Unicode [0x2700, 0x27bf]");
                    formCheckbox("Letterlike_Symbols", "문자형 기호(?)", "Unicode [0x2100, 0x214f]");
                    formCheckbox("Enclosed_Alphanumerics", "동그라미 또는 괄호로 감싸진 알파벳 숫자(?)", "Unicode [0x2460, 0x24ff]");
                    formCheckbox("Number_Forms", "숫자 형식(?)", "Unicode [0x2150, 0x218f]");
                    formCheckbox("Currency_Symbols", "통화 기호(?)", "Unicode [0x20a0, 0x20cf]");
                    formCheckbox("Arrows", "화살표(?)", "Unicode [0x2190, 0x21ff]");
                    formCheckbox("Geometric_Shapes", "기하 도형(?)", "Unicode [0x25a0, 0x25ff]");
                    formCheckbox("Mathematical_Operators", "수학 연산 기호(?)", "Unicode [0x2200, 0x22ff]");
                    formInput("CustomUnicode", "사용자 정의 Unicode(?)", "입력한 모든 문자를 글꼴 사전 로드 목록에 강제로 추가합니다.");
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndScrollView();
            saveData(serializedObject, miniGameProperty);
        }

        private void loadData(SerializedObject serializedObject, SerializedProperty miniGameProperty)
        {
            serializedObject.UpdateIfRequiredOrScript();
            var ProjectConf = miniGameProperty.FindPropertyRelative("ProjectConf");

            // Instant Game
            if (WXConvertCore.IsInstantGameAutoStreaming())
            {
                var automaticfillinstantgame = miniGameProperty.FindPropertyRelative("m_AutomaticFillInstantGame");
                if (automaticfillinstantgame.boolValue)
                {
                    ProjectConf.FindPropertyRelative("CDN").stringValue = WXConvertCore.GetInstantGameAutoStreamingCDN();
                    if (!ProjectConf.FindPropertyRelative("bundlePathIdentifier").stringValue.Contains("AS;"))
                    {
                        ProjectConf.FindPropertyRelative("bundlePathIdentifier").stringValue += "AS;";
                    }
                    if (!ProjectConf.FindPropertyRelative("bundlePathIdentifier").stringValue.Contains("CUS/CustomAB;"))
                    {
                        ProjectConf.FindPropertyRelative("bundlePathIdentifier").stringValue += "CUS/CustomAB;";
                    }
                    ProjectConf.FindPropertyRelative("dataFileSubPrefix").stringValue = "CUS";
                }
            }

            setData("projectName", ProjectConf.FindPropertyRelative("projectName").stringValue);
            setData("appid", ProjectConf.FindPropertyRelative("Appid").stringValue);
            setData("cdn", ProjectConf.FindPropertyRelative("CDN").stringValue);
            setData("assetLoadType", ProjectConf.FindPropertyRelative("assetLoadType").intValue);
            setData("compressDataPackage", ProjectConf.FindPropertyRelative("compressDataPackage").boolValue);
            setData("videoUrl", ProjectConf.FindPropertyRelative("VideoUrl").stringValue);
            setData("orientation", (int)ProjectConf.FindPropertyRelative("Orientation").enumValueIndex);
            //setData("dst", ProjectConf.FindPropertyRelative("relativeDST").stringValue);
            setData("bundleHashLength", ProjectConf.FindPropertyRelative("bundleHashLength").intValue.ToString());
            setData("bundlePathIdentifier", ProjectConf.FindPropertyRelative("bundlePathIdentifier").stringValue);
            setData("bundleExcludeExtensions", ProjectConf.FindPropertyRelative("bundleExcludeExtensions").stringValue);
            setData("preloadFiles", ProjectConf.FindPropertyRelative("preloadFiles").stringValue);

            var CompileOptions = miniGameProperty.FindPropertyRelative("CompileOptions");
            // setData("developBuild", CompileOptions.FindPropertyRelative("DevelopBuild").boolValue);
            setData("autoProfile", CompileOptions.FindPropertyRelative("AutoProfile").boolValue);
            setData("scriptOnly", CompileOptions.FindPropertyRelative("ScriptOnly").boolValue);
            setData("il2CppOptimizeSize", CompileOptions.FindPropertyRelative("Il2CppOptimizeSize").boolValue);
            setData("profilingFuncs", CompileOptions.FindPropertyRelative("profilingFuncs").boolValue);
            setData("profilingMemory", CompileOptions.FindPropertyRelative("ProfilingMemory").boolValue);
            setData("deleteStreamingAssets", CompileOptions.FindPropertyRelative("DeleteStreamingAssets").boolValue);
            setData("cleanBuild", CompileOptions.FindPropertyRelative("CleanBuild").boolValue);
            setData("customNodePath", CompileOptions.FindPropertyRelative("CustomNodePath").stringValue);
            setData("webgl2", CompileOptions.FindPropertyRelative("Webgl2").boolValue);
            setData("iOSPerformancePlus", CompileOptions.FindPropertyRelative("enableIOSPerformancePlus").boolValue);
            setData("iOSMetal", CompileOptions.FindPropertyRelative("enableiOSMetal").boolValue);
            setData("EmscriptenGLX", CompileOptions.FindPropertyRelative("enableEmscriptenGLX").boolValue);
            setData("fbslim", CompileOptions.FindPropertyRelative("fbslim").boolValue);

            var SDKOptions = miniGameProperty.FindPropertyRelative("SDKOptions");
            setData("useFriendRelation", SDKOptions.FindPropertyRelative("UseFriendRelation").boolValue);
            setData("useMiniGameChat", SDKOptions.FindPropertyRelative("UseMiniGameChat").boolValue);
            setData("preloadWXFont", SDKOptions.FindPropertyRelative("PreloadWXFont").boolValue);
            setData("disableMultiTouch", SDKOptions.FindPropertyRelative("disableMultiTouch").boolValue);
            setData("bgImageSrc", ProjectConf.FindPropertyRelative("bgImageSrc").stringValue);
            tex = AssetDatabase.LoadAssetAtPath<Texture>(ProjectConf.FindPropertyRelative("bgImageSrc").stringValue);
            setData("memorySize", ProjectConf.FindPropertyRelative("MemorySize").intValue.ToString());
            setData("hideAfterCallMain", ProjectConf.FindPropertyRelative("HideAfterCallMain").boolValue);

            setData("dataFileSubPrefix", ProjectConf.FindPropertyRelative("dataFileSubPrefix").stringValue);
            setData("maxStorage", ProjectConf.FindPropertyRelative("maxStorage").intValue.ToString());
            setData("defaultReleaseSize", ProjectConf.FindPropertyRelative("defaultReleaseSize").intValue.ToString());
            setData("texturesHashLength", ProjectConf.FindPropertyRelative("texturesHashLength").intValue.ToString());
            setData("texturesPath", ProjectConf.FindPropertyRelative("texturesPath").stringValue);
            setData("needCacheTextures", ProjectConf.FindPropertyRelative("needCacheTextures").boolValue);
            setData("loadingBarWidth", ProjectConf.FindPropertyRelative("loadingBarWidth").intValue.ToString());
            setData("needCheckUpdate", ProjectConf.FindPropertyRelative("needCheckUpdate").boolValue);
            setData("disableHighPerformanceFallback", ProjectConf.FindPropertyRelative("disableHighPerformanceFallback").boolValue);
            setData("autoAdaptScreen", CompileOptions.FindPropertyRelative("autoAdaptScreen").boolValue);
            setData("showMonitorSuggestModal", CompileOptions.FindPropertyRelative("showMonitorSuggestModal").boolValue);
            setData("enableProfileStats", CompileOptions.FindPropertyRelative("enableProfileStats").boolValue);
            setData("enableRenderAnalysis", CompileOptions.FindPropertyRelative("enableRenderAnalysis").boolValue);
            setData("brotliMT", CompileOptions.FindPropertyRelative("brotliMT").boolValue);
            setData("autoUploadFirstBundle", true);
            setData("m_AutomaticFillInstantGame", miniGameProperty.FindPropertyRelative("m_AutomaticFillInstantGame").boolValue);

            // font options
            var FontOptions = miniGameProperty.FindPropertyRelative("FontOptions");
            setData("CJK_Unified_Ideographs", FontOptions.FindPropertyRelative("CJK_Unified_Ideographs").boolValue);
            setData("C0_Controls_and_Basic_Latin", FontOptions.FindPropertyRelative("C0_Controls_and_Basic_Latin").boolValue);
            setData("CJK_Symbols_and_Punctuation", FontOptions.FindPropertyRelative("CJK_Symbols_and_Punctuation").boolValue);
            setData("General_Punctuation", FontOptions.FindPropertyRelative("General_Punctuation").boolValue);
            setData("Enclosed_CJK_Letters_and_Months", FontOptions.FindPropertyRelative("Enclosed_CJK_Letters_and_Months").boolValue);
            setData("Vertical_Forms", FontOptions.FindPropertyRelative("Vertical_Forms").boolValue);
            setData("CJK_Compatibility_Forms", FontOptions.FindPropertyRelative("CJK_Compatibility_Forms").boolValue);
            setData("Miscellaneous_Symbols", FontOptions.FindPropertyRelative("Miscellaneous_Symbols").boolValue);
            setData("CJK_Compatibility", FontOptions.FindPropertyRelative("CJK_Compatibility").boolValue);
            setData("Halfwidth_and_Fullwidth_Forms", FontOptions.FindPropertyRelative("Halfwidth_and_Fullwidth_Forms").boolValue);
            setData("Dingbats", FontOptions.FindPropertyRelative("Dingbats").boolValue);
            setData("Letterlike_Symbols", FontOptions.FindPropertyRelative("Letterlike_Symbols").boolValue);
            setData("Enclosed_Alphanumerics", FontOptions.FindPropertyRelative("Enclosed_Alphanumerics").boolValue);
            setData("Number_Forms", FontOptions.FindPropertyRelative("Number_Forms").boolValue);
            setData("Currency_Symbols", FontOptions.FindPropertyRelative("Currency_Symbols").boolValue);
            setData("Arrows", FontOptions.FindPropertyRelative("Arrows").boolValue);
            setData("Geometric_Shapes", FontOptions.FindPropertyRelative("Geometric_Shapes").boolValue);
            setData("Mathematical_Operators", FontOptions.FindPropertyRelative("Mathematical_Operators").boolValue);
            setData("CustomUnicode", FontOptions.FindPropertyRelative("CustomUnicode").stringValue);
        }

        private void saveData(SerializedObject serializedObject, SerializedProperty miniGameProperty)
        {
            serializedObject.UpdateIfRequiredOrScript();

            var ProjectConf = miniGameProperty.FindPropertyRelative("ProjectConf");
            ProjectConf.FindPropertyRelative("projectName").stringValue = getDataInput("projectName");
            ProjectConf.FindPropertyRelative("Appid").stringValue = getDataInput("appid");
            ProjectConf.FindPropertyRelative("CDN").stringValue = getDataInput("cdn");
            ProjectConf.FindPropertyRelative("assetLoadType").intValue = getDataPop("assetLoadType");
            ProjectConf.FindPropertyRelative("compressDataPackage").boolValue = getDataCheckbox("compressDataPackage");
            ProjectConf.FindPropertyRelative("VideoUrl").stringValue = getDataInput("videoUrl");
            ProjectConf.FindPropertyRelative("Orientation").enumValueIndex = getDataPop("orientation");
            ProjectConf.FindPropertyRelative("relativeDST").stringValue = serializedObject.FindProperty("m_BuildPath").stringValue;
            ProjectConf.FindPropertyRelative("DST").stringValue = GetAbsolutePath(config.ProjectConf.relativeDST);

            ProjectConf.FindPropertyRelative("bundleHashLength").intValue = int.Parse(getDataInput("bundleHashLength"));
            ProjectConf.FindPropertyRelative("bundlePathIdentifier").stringValue = getDataInput("bundlePathIdentifier");
            ProjectConf.FindPropertyRelative("bundleExcludeExtensions").stringValue = getDataInput("bundleExcludeExtensions");
            ProjectConf.FindPropertyRelative("preloadFiles").stringValue = getDataInput("preloadFiles");

            var CompileOptions = miniGameProperty.FindPropertyRelative("CompileOptions");

            CompileOptions.FindPropertyRelative("DevelopBuild").boolValue = serializedObject.FindProperty("m_PlatformSettings").FindPropertyRelative("m_Development").boolValue;
            CompileOptions.FindPropertyRelative("AutoProfile").boolValue = getDataCheckbox("autoProfile");
            CompileOptions.FindPropertyRelative("ScriptOnly").boolValue = getDataCheckbox("scriptOnly");
            CompileOptions.FindPropertyRelative("Il2CppOptimizeSize").boolValue = getDataCheckbox("il2CppOptimizeSize");
            CompileOptions.FindPropertyRelative("profilingFuncs").boolValue = getDataCheckbox("profilingFuncs");
            CompileOptions.FindPropertyRelative("ProfilingMemory").boolValue = getDataCheckbox("profilingMemory");
            CompileOptions.FindPropertyRelative("DeleteStreamingAssets").boolValue = getDataCheckbox("deleteStreamingAssets");
            CompileOptions.FindPropertyRelative("CleanBuild").boolValue = getDataCheckbox("cleanBuild");
            CompileOptions.FindPropertyRelative("CustomNodePath").stringValue = getDataInput("customNodePath");
            CompileOptions.FindPropertyRelative("Webgl2").boolValue = getDataCheckbox("webgl2");
            CompileOptions.FindPropertyRelative("enableIOSPerformancePlus").boolValue = getDataCheckbox("iOSPerformancePlus");
            CompileOptions.FindPropertyRelative("enableiOSMetal").boolValue = getDataCheckbox("iOSMetal");
            CompileOptions.FindPropertyRelative("enableEmscriptenGLX").boolValue = getDataCheckbox("EmscriptenGLX");
            CompileOptions.FindPropertyRelative("fbslim").boolValue = getDataCheckbox("fbslim");

            var SDKOptions = miniGameProperty.FindPropertyRelative("SDKOptions");
            SDKOptions.FindPropertyRelative("UseFriendRelation").boolValue = getDataCheckbox("useFriendRelation");
            SDKOptions.FindPropertyRelative("UseMiniGameChat").boolValue = getDataCheckbox("useMiniGameChat");
            SDKOptions.FindPropertyRelative("PreloadWXFont").boolValue = getDataCheckbox("preloadWXFont");
            SDKOptions.FindPropertyRelative("disableMultiTouch").boolValue = getDataCheckbox("disableMultiTouch");
            ProjectConf.FindPropertyRelative("bgImageSrc").stringValue = getDataInput("bgImageSrc");
            ProjectConf.FindPropertyRelative("MemorySize").intValue = int.Parse(getDataInput("memorySize"));
            ProjectConf.FindPropertyRelative("HideAfterCallMain").boolValue = getDataCheckbox("hideAfterCallMain");
            ProjectConf.FindPropertyRelative("dataFileSubPrefix").stringValue = getDataInput("dataFileSubPrefix");
            ProjectConf.FindPropertyRelative("maxStorage").intValue = int.Parse(getDataInput("maxStorage"));
            ProjectConf.FindPropertyRelative("defaultReleaseSize").intValue = int.Parse(getDataInput("defaultReleaseSize"));
            ProjectConf.FindPropertyRelative("texturesHashLength").intValue = int.Parse(getDataInput("texturesHashLength"));
            ProjectConf.FindPropertyRelative("texturesPath").stringValue = getDataInput("texturesPath");
            ProjectConf.FindPropertyRelative("needCacheTextures").boolValue = getDataCheckbox("needCacheTextures");
            ProjectConf.FindPropertyRelative("loadingBarWidth").intValue = int.Parse(getDataInput("loadingBarWidth"));
            ProjectConf.FindPropertyRelative("needCheckUpdate").boolValue = getDataCheckbox("needCheckUpdate");
            ProjectConf.FindPropertyRelative("disableHighPerformanceFallback").boolValue = getDataCheckbox("disableHighPerformanceFallback");
            CompileOptions.FindPropertyRelative("autoAdaptScreen").boolValue = getDataCheckbox("autoAdaptScreen");
            CompileOptions.FindPropertyRelative("showMonitorSuggestModal").boolValue = getDataCheckbox("showMonitorSuggestModal");
            CompileOptions.FindPropertyRelative("enableProfileStats").boolValue = getDataCheckbox("enableProfileStats");
            CompileOptions.FindPropertyRelative("enableRenderAnalysis").boolValue = getDataCheckbox("enableRenderAnalysis");
            CompileOptions.FindPropertyRelative("brotliMT").boolValue = getDataCheckbox("brotliMT");

            // font options
            var FontOptions = miniGameProperty.FindPropertyRelative("FontOptions");
            FontOptions.FindPropertyRelative("CJK_Unified_Ideographs").boolValue = getDataCheckbox("CJK_Unified_Ideographs");
            FontOptions.FindPropertyRelative("C0_Controls_and_Basic_Latin").boolValue = getDataCheckbox("C0_Controls_and_Basic_Latin");
            FontOptions.FindPropertyRelative("CJK_Symbols_and_Punctuation").boolValue = getDataCheckbox("CJK_Symbols_and_Punctuation");
            FontOptions.FindPropertyRelative("General_Punctuation").boolValue = getDataCheckbox("General_Punctuation");
            FontOptions.FindPropertyRelative("Enclosed_CJK_Letters_and_Months").boolValue = getDataCheckbox("Enclosed_CJK_Letters_and_Months");
            FontOptions.FindPropertyRelative("Vertical_Forms").boolValue = getDataCheckbox("Vertical_Forms");
            FontOptions.FindPropertyRelative("CJK_Compatibility_Forms").boolValue = getDataCheckbox("CJK_Compatibility_Forms");
            FontOptions.FindPropertyRelative("Miscellaneous_Symbols").boolValue = getDataCheckbox("Miscellaneous_Symbols");
            FontOptions.FindPropertyRelative("CJK_Compatibility").boolValue = getDataCheckbox("CJK_Compatibility");
            FontOptions.FindPropertyRelative("Halfwidth_and_Fullwidth_Forms").boolValue = getDataCheckbox("Halfwidth_and_Fullwidth_Forms");
            FontOptions.FindPropertyRelative("Dingbats").boolValue = getDataCheckbox("Dingbats");
            FontOptions.FindPropertyRelative("Letterlike_Symbols").boolValue = getDataCheckbox("Letterlike_Symbols");
            FontOptions.FindPropertyRelative("Enclosed_Alphanumerics").boolValue = getDataCheckbox("Enclosed_Alphanumerics");
            FontOptions.FindPropertyRelative("Number_Forms").boolValue = getDataCheckbox("Number_Forms");
            FontOptions.FindPropertyRelative("Currency_Symbols").boolValue = getDataCheckbox("Currency_Symbols");
            FontOptions.FindPropertyRelative("Arrows").boolValue = getDataCheckbox("Arrows");
            FontOptions.FindPropertyRelative("Geometric_Shapes").boolValue = getDataCheckbox("Geometric_Shapes");
            FontOptions.FindPropertyRelative("Mathematical_Operators").boolValue = getDataCheckbox("Mathematical_Operators");
            FontOptions.FindPropertyRelative("CustomUnicode").stringValue = getDataInput("CustomUnicode");
            FontOptions.FindPropertyRelative("Arrows").boolValue = getDataCheckbox("Arrows");
            FontOptions.FindPropertyRelative("Geometric_Shapes").boolValue = getDataCheckbox("Geometric_Shapes");
            FontOptions.FindPropertyRelative("Mathematical_Operators").boolValue = getDataCheckbox("Mathematical_Operators");
            FontOptions.FindPropertyRelative("CustomUnicode").stringValue = getDataInput("CustomUnicode");

            miniGameProperty.FindPropertyRelative("m_AutomaticFillInstantGame").boolValue = getDataCheckbox("m_AutomaticFillInstantGame");

            serializedObject.ApplyModifiedProperties();
        }

        private bool getDataCheckbox(string target)
        {
            if (formCheckboxData.ContainsKey(target))
                return formCheckboxData[target];
            return false;
        }

        private string getDataInput(string target)
        {
            if (formInputData.ContainsKey(target))
                return formInputData[target];
            return "";
        }

        private int getDataPop(string target)
        {
            if (formIntPopupData.ContainsKey(target))
                return formIntPopupData[target];
            return 0;
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

        private void formCheckbox(string target, string label, string help = null, bool disable = false, Action<bool> setting = null)
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
            formCheckboxData[target] = EditorGUILayout.Toggle(disable ? false : formCheckboxData[target]);

            if (setting != null)
            {
                EditorGUILayout.LabelField("", GUILayout.Width(10));
                // ���ð�ť
                if (GUILayout.Button(new GUIContent("����"), GUILayout.Width(40), GUILayout.Height(18)))
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
            // 비어있거나 공백인지 확인
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // Windows에서 드라이브 문자 또는 네트워크 경로인지 확인
            if (Application.platform == RuntimePlatform.WindowsEditor && Path.IsPathRooted(path))
            {
                return true;
            }

            // Unix/Linux 및 macOS에서 '/'로 시작하는지 확인
            if (Application.platform == RuntimePlatform.OSXEditor && path.StartsWith("/"))
            {
                return true;
            }

            return false; // 그렇지 않으면 상대 경로
        }

        public static string GetAbsolutePath(string path)
        {
            if (IsAbsolutePath(path))
            {
                return path;
            }
            string projectRootPath = System.IO.Path.GetFullPath(Application.dataPath + "/../");
            return Path.Combine(projectRootPath, path);
        }
    }
}
#endif

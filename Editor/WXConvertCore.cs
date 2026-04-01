using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using LitJson;
using UnityEditor.Build;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using static WeChatWASM.LifeCycleEvent;
namespace WeChatWASM
{
    public class WXConvertCore
    {

        static WXConvertCore()
        {

        }

        public static void Init()
        {
            string templateHeader = "PROJECT:";
#if TUANJIE_2022_3_OR_NEWER
            PlayerSettings.WeixinMiniGame.threadsSupport = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.WeixinMiniGame.compressionFormat = WeixinMiniGameCompressionFormat.Disabled;
            if(UnityUtil.GetEngineVersion() == UnityUtil.EngineVersion.Tuanjie)
            {
                var absolutePath = Path.GetFullPath("Packages/com.qq.weixin.minigame/WebGLTemplates/WXTemplate2022TJ");
                if (!Directory.Exists(absolutePath))
                {
                    PlayerSettings.WeixinMiniGame.template = $"{templateHeader}WXTemplate2022TJ";
                }
                else
                {
                    PlayerSettings.WeixinMiniGame.template = $"PATH:{absolutePath}";
                }
            }
            else
            {
                PlayerSettings.WeixinMiniGame.template = $"{templateHeader}WXTemplate2022TJ";
            }
            PlayerSettings.WeixinMiniGame.linkerTarget = WeixinMiniGameLinkerTarget.Wasm;
            PlayerSettings.WeixinMiniGame.dataCaching = false;
            PlayerSettings.WeixinMiniGame.debugSymbolMode = WeixinMiniGameDebugSymbolMode.External;
#else
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
#if UNITY_2022_3_OR_NEWER
        PlayerSettings.WebGL.template = $"{templateHeader}WXTemplate2022";
#elif UNITY_2020_1_OR_NEWER
            PlayerSettings.WebGL.template = $"{templateHeader}WXTemplate2020";
#else
            PlayerSettings.WebGL.template = $"{templateHeader}WXTemplate";
#endif
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.dataCaching = false;
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.External;
#else
            PlayerSettings.WebGL.debugSymbols = true;
#endif
#endif
        }

        public enum WXExportError
        {
            SUCCEED = 0,
            NODE_NOT_FOUND = 1,
            BUILD_WEBGL_FAILED = 2,
        }

        public static WXEditorScriptObject config => isPlayableBuild ? WXPlayableConvertCore.GetFakeScriptObject() : UnityUtil.GetEditorConf();

        public static string defaultTemplateDir => isPlayableBuild ? "playable-default" : "wechat-default";
        public static string webglDir = "webgl"; // 내보내기 webgl 디렉토리
        public static string miniGameDir = "minigame"; // 생성小游戏 디렉토리
        public static string audioDir = "Assets"; // 오디오 자원 디렉토리
        public static string frameworkDir = "framework";
        public static string dataFileSize = string.Empty;
        public static string codeMd5 = string.Empty;
        public static string dataMd5 = string.Empty;
        public static string defaultImgSrc = "Assets/WX-WASM-SDK-V2/Runtime/wechat-default/images/background.jpg";
        /// <summary>
        /// 시도용 빌드 중인지 여부, 빌드 시작 전에 값을 수정하고 빌드结束后에 값을 복원합니다
        /// </summary>
        public static bool isPlayableBuild = false;

        private static bool lastBrotliType = false;
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
        // iOS Metal 렌더링을 사용할지 여부
        public static bool UseiOSMetal
        {
            get
            {
                return config.CompileOptions.enableiOSMetal;
            }
        }
        // replaceRules에서 관련 수정을 주입할지 여부를 판단합니다
        public static bool UseEmscriptenGLX
        {
            get
            {
                return config.CompileOptions.enableEmscriptenGLX;
            }
        }
        // public static void SetPlayableEnabled(bool enabled)
        // {
        //     isPlayableBuild = enabled;
        // }
        /// <summary>
        /// 내보내기 전의 초기 설정
        /// 작은게임 모드와 시도용 모드 모두 이 함수를 사용합니다. 이 함수에 새로운 메서드를 추가할 경우, 시도용 모드와 호환되지 않는다고 간주하는 것이 좋습니다
        /// </summary>
        public static void PreInit()
        {
            CheckBuildTarget();
            Init();
            // 순서가 필요한 경우가 있을 수 있습니다? 필요하지 않다면 이 함수 외부로 이동할 수 있습니다
            if (!isPlayableBuild)
            {
                ProcessWxPerfBinaries();
            }
            // iOS Metal 관련 기능
            ProcessWxiOSMetalBinaries();
            // emscriptenglx 관련 기능
            ProcessWxEmscriptenGLXBinaries();
            MakeEnvForLuaAdaptor();
            // JSLib
            SettingWXTextureMinJSLib();
            UpdateGraphicAPI();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        public static WXExportError PreCheck()
        {
            if (!CheckSDK())
            {
                Debug.LogError("게임에서 이전 버전의 WeChat SDK를 사용한 경우, Assets/WX-WASM-SDK 폴더를 삭제한 후 최신 도구 패키지를 다시 가져와야 합니다.");
                return WXExportError.BUILD_WEBGL_FAILED;
            }
            if (!isPlayableBuild && !CheckBuildTemplate())
            {
                Debug.LogError("빌드 템플릿 확인에 실패하여 내보내기를 중단합니다.");
                return WXExportError.BUILD_WEBGL_FAILED;
            }
            if (!isPlayableBuild && CheckInvalidPerfIntegration())
            {
                Debug.LogError("성능 분석 도구는 Development Build에서만 사용할 수 있습니다. 내보내기를 중단합니다!");
                return WXExportError.BUILD_WEBGL_FAILED;
            }
            dynamic config = isPlayableBuild ? UnityUtil.GetPlayableEditorConf() : UnityUtil.GetEditorConf();
            if (config.ProjectConf.relativeDST == string.Empty)
            {
                Debug.LogError("먼저 게임 내보내기 경로를 설정하십시오");
                return WXExportError.BUILD_WEBGL_FAILED;
            }
            return WXExportError.SUCCEED;
        }
        // 통합을 위해 이 함수를 호출할 수 있습니다
        public static WXExportError DoExport(bool buildWebGL = true)
        {
            LifeCycleEvent.Init();
            Emit(LifeCycle.beforeExport);
            var preCheckResult = PreCheck();
            if (preCheckResult != WXExportError.SUCCEED)
            {
                return preCheckResult;
            }

            PreInit();

            // 마지막 내보내기의 brotliType 기록
            {
                var filePath = Path.Combine(config.ProjectConf.DST, miniGameDir, "unity-namespace.js");
                string content = string.Empty;
                if (File.Exists(filePath))
                {
                    content = File.ReadAllText(filePath, Encoding.UTF8);
                }
                Regex regex = new Regex("brotliMT\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
                Match match = regex.Match(content);
                if (match.Success)
                {
                    lastBrotliType = match.Groups[1].Value == "true";
                }
            }

            {
                // StreamingAssets 디렉토리만 삭제
                if (config.CompileOptions.DeleteStreamingAssets)
                {
                    UnityUtil.DelectDir(Path.Combine(config.ProjectConf.DST, webglDir + "/StreamingAssets"));
                }

                if (buildWebGL && Build() != 0)
                {
                    return WXExportError.BUILD_WEBGL_FAILED;
                }

                if (WXExtEnvDef.GETDEF("UNITY_2021_2_OR_NEWER") && !config.CompileOptions.DevelopBuild)
                {
                    // 2021 버전인 경우, 공식 symbols 생성에 버그가 있으므로 도구를 사용하여 함수 이름을 추출해야 합니다
                    var symFile1 = "";
                    if (!UseIL2CPP)
                    {
                        symFile1 = Path.Combine(config.ProjectConf.DST, webglDir, "Code", "wwwroot", "_framework", "dotnet.native.js.symbols");
                    }
                    else
                    {
                        var rootPath = Directory.GetParent(Application.dataPath).FullName;
                        string webglDir = WXExtEnvDef.GETDEF("WEIXINMINIGAME") ? "WeixinMiniGame" : "WebGL";
#if PLATFORM_PLAYABLEADS
                        webglDir = "PlayableAds";
#endif
                        symFile1 = Path.Combine(rootPath, "Library", "Bee", "artifacts", webglDir, "build", "debug_WebGL_wasm", "build.js.symbols");
                    }
                    WeChatWASM.UnityUtil.preprocessSymbols(symFile1, GetWebGLSymbolPath());
                    // WeChatWASM.UnityUtil.preprocessSymbols(GetWebGLSymbolPath());
                }

                ConvertCode();
                if (!UseIL2CPP)
                {
                    ConvertDotnetCode();
                }
                string dataFilePath = GetWebGLDataPath();
                string wxTextDataDir = WXAssetsTextTools.GetTextMinDataDir();
                string dataFilePathBackupDir = $"{wxTextDataDir}{WXAssetsTextTools.DS}slim";
                string dataFilePathBackupPath = $"{dataFilePathBackupDir}{WXAssetsTextTools.DS}backup.txt";
                if (!Directory.Exists(dataFilePathBackupDir))
                {
                    Directory.CreateDirectory(dataFilePathBackupDir);
                }
                if (File.Exists(dataFilePathBackupPath))
                {
                    File.Delete(dataFilePathBackupPath);
                }
                File.Copy(dataFilePath, dataFilePathBackupPath);

                if (UnityUtil.GetEngineVersion() == 0 && config.CompileOptions.fbslim && !IsInstantGameAutoStreaming())
                {
                    WXAssetsTextTools.FirstBundleSlim(dataFilePath, (result, info) =>
                    {
                        if (!result)
                        {
                            Debug.LogWarning("[첫 번째 자원 패키지 최적화 건너뛰기] : 처리 실패로 인해 자동으로 건너뜁니다" + info);
                        }

                        finishExport();
                    });
                }
                else
                {
                    finishExport();
                }
            }
            return WXExportError.SUCCEED;
        }

        private static int GetEnabledFlagStringIndex(string inAllText, string inTagStr)
        {
            try
            {
                int tagStrIdx = inAllText.IndexOf(inTagStr);
                if (tagStrIdx == -1) throw new Exception($"Tag string '{inTagStr}' not found.");

                int enabledStrIdx = inAllText.IndexOf("enabled: ", tagStrIdx);
                if (enabledStrIdx == -1) throw new Exception("'enabled: ' string not found after tag.");

                // inAllText[enabledStrIdx] == 'e'
                // And that is to say, inAllText[enabledStrIdx + 9] should be 0 or 1
                return enabledStrIdx + 9;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to get enabled flag string index: {ex.Message}");
                return -1; // -1 means failed
            }
        }

        private static void SetPluginCompatibilityByModifyingMetadataFile(string inAssetPath, bool inEnabled)
        {
            try
            {
                string metaPath = AssetDatabase.GetTextMetaFilePathFromAssetPath(inAssetPath); // .meta 파일 경로 가져오기
                string enableFlagStr = inEnabled ? "1" : "0";

                // .meta 파일 읽기
                // WebGL 처리
                string metaContent = File.ReadAllText(metaPath);
                int idxWebGLEnableFlag = GetEnabledFlagStringIndex(metaContent, "WebGL: WebGL");

                metaContent = metaContent.Remove(idxWebGLEnableFlag, 1).Insert(idxWebGLEnableFlag, enableFlagStr);
                // WeixinMiniGame
                int idxWeixinMiniGameEnableFlag = GetEnabledFlagStringIndex(metaContent, "WeixinMiniGame: WeixinMiniGame");

                metaContent = metaContent.Remove(idxWeixinMiniGameEnableFlag, 1).Insert(idxWeixinMiniGameEnableFlag, enableFlagStr);

                // .meta 파일에 쓰기

                File.WriteAllText(metaPath, metaContent);
                AssetDatabase.ImportAsset(inAssetPath, ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex)
            {
                // Error 로그가 패키징 프로세스를 차단하지 않도록 합니다
                UnityEngine.Debug.LogWarning($"Failed to enable plugin asset: {ex.Message}");
            }
        }

        private static void ProcessWxPerfBinaries()
        {
            string[] wxPerfPlugins;
            string DS = WXAssetsTextTools.DS;
            if (UnityUtil.GetSDKMode() == UnityUtil.SDKMode.Package)
            {
                wxPerfPlugins = new string[]
                {
                    $"Packages{DS}com.qq.weixin.minigame{DS}Runtime{DS}Plugins{DS}WxPerfJsBridge.jslib",
                    $"Packages{DS}com.qq.weixin.minigame{DS}Runtime{DS}Plugins{DS}wx_perf_2022.a",
                    $"Packages{DS}com.qq.weixin.minigame{DS}Runtime{DS}Plugins{DS}wx_perf_2021.a",
                };
            }
            else
            {
                string jsLibRootDir = $"Assets{DS}WX-WASM-SDK-V2{DS}Runtime{DS}Plugins{DS}";

                // 아래 순서는 변경할 수 없습니다
                wxPerfPlugins = new string[]
                {
                     $"{jsLibRootDir}WxPerfJsBridge.jslib",
                     $"{jsLibRootDir}wx_perf_2022.a",
                     $"{jsLibRootDir}wx_perf_2021.a",
                };
            }

            {
                // WxPerfJsBridge.jslib
                var wxPerfJSBridgeImporter = AssetImporter.GetAtPath(wxPerfPlugins[0]) as PluginImporter;
#if PLATFORM_PLAYABLEADS
				wxPerfJSBridgeImporter.SetCompatibleWithPlatform(BuildTarget.PlayableAds, config.CompileOptions.enablePerfAnalysis);
#elif PLATFORM_WEIXINMINIGAME
                wxPerfJSBridgeImporter.SetCompatibleWithPlatform(BuildTarget.WeixinMiniGame, config.CompileOptions.enablePerfAnalysis);
#else
                wxPerfJSBridgeImporter.SetCompatibleWithPlatform(BuildTarget.WebGL, config.CompileOptions.enablePerfAnalysis);
#endif
                SetPluginCompatibilityByModifyingMetadataFile(wxPerfPlugins[0], config.CompileOptions.enablePerfAnalysis);
            }

            {
                // wx_perf_2022.a
                bool bShouldEnablePerf2022Plugin = config.CompileOptions.enablePerfAnalysis && IsCompatibleWithUnity202203OrNewer();

                var wxPerf2022Importer = AssetImporter.GetAtPath(wxPerfPlugins[1]) as PluginImporter;

#if PLATFORM_PLAYABLEADS
				wxPerf2022Importer.SetCompatibleWithPlatform(BuildTarget.PlayableAds, bShouldEnablePerf2022Plugin);
#elif PLATFORM_WEIXINMINIGAME
                wxPerf2022Importer.SetCompatibleWithPlatform(BuildTarget.WeixinMiniGame, bShouldEnablePerf2022Plugin);
#else
                wxPerf2022Importer.SetCompatibleWithPlatform(BuildTarget.WebGL, bShouldEnablePerf2022Plugin);
#endif
                SetPluginCompatibilityByModifyingMetadataFile(wxPerfPlugins[1], bShouldEnablePerf2022Plugin);
            }

            {
                // wx_perf_2021.a
                bool bShouldEnablePerf2021Plugin = config.CompileOptions.enablePerfAnalysis && IsCompatibleWithUnity202102To202203();

                var wxPerf2021Importer = AssetImporter.GetAtPath(wxPerfPlugins[2]) as PluginImporter;
#if PLATFORM_PLAYABLEADS
                wxPerf2021Importer.SetCompatibleWithPlatform(BuildTarget.PlayableAds, bShouldEnablePerf2021Plugin);
#elif PLATFORM_WEIXINMINIGAME
                wxPerf2021Importer.SetCompatibleWithPlatform(BuildTarget.WeixinMiniGame, bShouldEnablePerf2021Plugin);
#else
                wxPerf2021Importer.SetCompatibleWithPlatform(BuildTarget.WebGL, bShouldEnablePerf2021Plugin);
#endif
                SetPluginCompatibilityByModifyingMetadataFile(wxPerfPlugins[2], bShouldEnablePerf2021Plugin);
            }
            AssetDatabase.Refresh();
        }

        private static void ProcessWxEmscriptenGLXBinaries()
        {
            string[] glLibs;
            string DS = WXAssetsTextTools.DS;
            if (UnityUtil.GetSDKMode() == UnityUtil.SDKMode.Package)
            {
                glLibs = new string[]
                {
                $"Packages{DS}com.qq.weixin.minigame{DS}Runtime{DS}Plugins{DS}libemscriptenglx.a",
                $"Packages{DS}com.qq.weixin.minigame{DS}Runtime{DS}Plugins{DS}libemscriptenglx_2021.a",
                };
            }
            else
            {
                string glLibRootDir = $"Assets{DS}WX-WASM-SDK-V2{DS}Runtime{DS}Plugins{DS}";

                // 아래 순서는 변경하지 마십시오
                glLibs = new string[]
                {
                    $"{glLibRootDir}libemscriptenglx.a",
                    $"{glLibRootDir}libemscriptenglx_2021.a",
                };
            }

            {
                // unity2022, tuanjie lib 도입
                bool showEnableGLX2022Plugin = config.CompileOptions.enableEmscriptenGLX && IsCompatibleWithUnity202203OrNewer();

                var glx2022Importer = AssetImporter.GetAtPath(glLibs[0]) as PluginImporter;
#if PLATFORM_WEIXINMINIGAME
                    glx2022Importer.SetCompatibleWithPlatform(BuildTarget.WeixinMiniGame, showEnableGLX2022Plugin);
#else
                glx2022Importer.SetCompatibleWithPlatform(BuildTarget.WebGL, showEnableGLX2022Plugin);
#endif
                SetPluginCompatibilityByModifyingMetadataFile(glLibs[0], showEnableGLX2022Plugin);
            }

            {
                // unity2021 lib 도입
                bool showEnableGLX2021Plugin = config.CompileOptions.enableEmscriptenGLX && IsCompatibleWithUnity202102To202203();

                var glx2021Importer = AssetImporter.GetAtPath(glLibs[1]) as PluginImporter;
#if PLATFORM_WEIXINMINIGAME
                    glx2021Importer.SetCompatibleWithPlatform(BuildTarget.WeixinMiniGame, showEnableGLX2021Plugin);
#else
                glx2021Importer.SetCompatibleWithPlatform(BuildTarget.WebGL, showEnableGLX2021Plugin);
#endif
                SetPluginCompatibilityByModifyingMetadataFile(glLibs[1], showEnableGLX2021Plugin);
            }

            AssetDatabase.Refresh();
        }

        /**
         * Lua Adaptor Settings.
         */

        private static bool GetRequiredLuaHeaderFiles(out Dictionary<string, string> luaPaths)
        {
            luaPaths = new Dictionary<string, string>()
            {
                {"lua.h", null},
                {"lobject.h", null},
                {"lstate.h", null},
                {"lfunc.h", null},
                {"lapi.h", null},
                {"lstring.h", null},
                {"ltable.h", null},
                {"lauxlib.h", null},
            };

            string rootPath = Directory.GetParent(Application.dataPath).ToString();
            string[] paths = Directory.GetFiles(rootPath, "*.h", SearchOption.AllDirectories);
            foreach (var path in paths)
            {
                string filename = Path.GetFileName(path);
                if (luaPaths.ContainsKey(Path.GetFileName(path)))
                {
                    luaPaths[filename] = path;
                }
            }

            foreach (var expectFile in luaPaths)
            {
                if (expectFile.Value == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ProcessWxiOSMetalBinaries()
        {
            string[] glLibs;
            string DS = WXAssetsTextTools.DS;
            if (UnityUtil.GetSDKMode() == UnityUtil.SDKMode.Package)
            {
                glLibs = new string[]
                {
                $"Packages{DS}com.qq.weixin.minigame{DS}Editor{DS}BuildProfile{DS}lib{DS}libwx-metal-cpp.bc",
                $"Packages{DS}com.qq.weixin.minigame{DS}Editor{DS}BuildProfile{DS}lib{DS}mtl_library.jslib",
                };
            }
            else
            {
                string glLibRootDir = $"Assets{DS}WX-WASM-SDK-V2{DS}Editor{DS}BuildProfile{DS}lib{DS}";
                glLibs = new string[]
                {
                    $"{glLibRootDir}libwx-metal-cpp.bc",
                    $"{glLibRootDir}mtl_library.jslib",
                };
            }
            for (int i = 0; i < glLibs.Length; i++)
            {
                var importer = AssetImporter.GetAtPath(glLibs[i]) as PluginImporter;
#if PLATFORM_WEIXINMINIGAME
                    importer.SetCompatibleWithPlatform(BuildTarget.WeixinMiniGame, config.CompileOptions.enableiOSMetal);
#else
                importer.SetCompatibleWithPlatform(BuildTarget.WebGL, config.CompileOptions.enableiOSMetal);
#endif
                // importer.SaveAndReimport();
                SetPluginCompatibilityByModifyingMetadataFile(glLibs[i], config.CompileOptions.enableiOSMetal);
            }
            AssetDatabase.Refresh();
        }

        private static string GetLuaAdaptorPath(string filename)
        {
            string DS = WXAssetsTextTools.DS;
            if (UnityUtil.GetSDKMode() == UnityUtil.SDKMode.Package)
            {
                return $"Packages{DS}com.qq.weixin.minigame{DS}Runtime{DS}Plugins{DS}LuaAdaptor{DS}{filename}";
            }

            return $"Assets{DS}WX-WASM-SDK-V2{DS}Runtime{DS}Plugins{DS}LuaAdaptor{DS}{filename}";
        }

        private static void MakeLuaImport(Dictionary<string, string> luaPaths)
        {
            string luaAdaptorImportHeaderPath = GetLuaAdaptorPath("lua_adaptor_import.h");
            if (!File.Exists(luaAdaptorImportHeaderPath))
            {
                Debug.LogError("Lua Adaptor File Not Found: " + luaAdaptorImportHeaderPath);
                return;
            }

            string includeLuaContent = "//EMSCRIPTEN_ENV_LUA_IMPORT_LOGIC_START";
            foreach (var luaPath in luaPaths)
            {
                includeLuaContent += $"\n#include \"{luaPath.Value.Replace("\\", "\\\\")}\"";
            }
            includeLuaContent += "\n//EMSCRIPTEN_ENV_LUA_IMPORT_LOGIC_END";

            string importHeaderContent = File.ReadAllText(luaAdaptorImportHeaderPath);
            importHeaderContent = Regex.Replace(
                importHeaderContent,
                "//EMSCRIPTEN_ENV_LUA_IMPORT_LOGIC_START([\\s\\S]*?)//EMSCRIPTEN_ENV_LUA_IMPORT_LOGIC_END",
                includeLuaContent
            );

            File.WriteAllText(luaAdaptorImportHeaderPath, importHeaderContent);
        }

        private static void ManageLuaAdaptorBuildOptions(bool shouldBuild)
        {
            string[] maybeBuildFiles = new string[]
            {
                "lua_adaptor_501.c",
                "lua_adaptor_503.c",
                "lua_adaptor_comm.c",
                "lua_adaptor_import.h",
            };

            foreach (var maybeBuildFile in maybeBuildFiles)
            {
                string path = GetLuaAdaptorPath(maybeBuildFile);
                if (!File.Exists(path) && shouldBuild)
                {
                    Debug.LogError("Lua Adaptor File Not Found: " + maybeBuildFile);
                    continue;
                }

                var wxPerfJSBridgeImporter = AssetImporter.GetAtPath(path) as PluginImporter;
                if (wxPerfJSBridgeImporter == null)
                {
                    Debug.LogError("Lua Adaptor Importer Not Found: " + maybeBuildFile);
                    continue;
                }
#if PLATFORM_PLAYABLEADS
                wxPerfJSBridgeImporter.SetCompatibleWithPlatform(BuildTarget.PlayableAds, shouldBuild);
#elif PLATFORM_WEIXINMINIGAME
                wxPerfJSBridgeImporter.SetCompatibleWithPlatform(BuildTarget.WeixinMiniGame, shouldBuild);
#else
                wxPerfJSBridgeImporter.SetCompatibleWithPlatform(BuildTarget.WebGL, shouldBuild);
#endif
                SetPluginCompatibilityByModifyingMetadataFile(path, shouldBuild);
            }
        }

        private static void MakeEnvForLuaAdaptor()
        {
            bool hasLuaEnv = GetRequiredLuaHeaderFiles(out var luaPaths);
            if (hasLuaEnv)
            {
                MakeLuaImport(luaPaths);
            }

            ManageLuaAdaptorBuildOptions(hasLuaEnv && config.CompileOptions.enablePerfAnalysis);
        }

        private static bool IsCompatibleWithUnity202203OrNewer()
        {
#if UNITY_2022_3_OR_NEWER
            return true;
#else
            return false;
#endif
        }

        static bool IsCompatibleWithUnity202102To202203()
        {
#if UNITY_2022_3_OR_NEWER
            return false;
#elif !UNITY_2021_2_OR_NEWER
            return false;
#else
            return true;
#endif
        }

        private static void CheckBuildTarget()
        {
            Emit(LifeCycle.beforeSwitchActiveBuildTarget);
            if (UnityUtil.GetEngineVersion() == UnityUtil.EngineVersion.Unity)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }
            else
            {
#if TUANJIE_2022_3_OR_NEWER
                if(EditorUserBuildSettings.activeBuildTarget != BuildTarget.WeixinMiniGame
#if PLATFORM_PLAYABLEADS
                    && EditorUserBuildSettings.activeBuildTarget != BuildTarget.PlayableAds
#endif
                    )
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WeixinMiniGame, BuildTarget.WeixinMiniGame);
#endif
            }
            Emit(LifeCycle.afterSwitchActiveBuildTarget);
        }

        public static void UpdateGraphicAPI()
        {
            GraphicsDeviceType[] targets = new GraphicsDeviceType[] { };
#if PLATFORM_WEIXINMINIGAME
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WeixinMiniGame, false);
            // iOS Metal 렌더링 활성화
            if (UseiOSMetal)
            {
                if (config.CompileOptions.Webgl2)
                {
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.WeixinMiniGame, new GraphicsDeviceType[] { GraphicsDeviceType.Metal, GraphicsDeviceType.OpenGLES3 });
                }
                else
                {
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.WeixinMiniGame, new GraphicsDeviceType[] { GraphicsDeviceType.Metal, GraphicsDeviceType.OpenGLES2 });
                }
            }
            else
            {
                if (config.CompileOptions.Webgl2)
                {
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.WeixinMiniGame, new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLES3 });
                }
                else
                {
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.WeixinMiniGame, new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLES2 });
                }
            }
#else
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            if (config.CompileOptions.Webgl2)
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLES3 });
            }
            else
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLES2 });
            }
#endif
        }

        /// <summary>
        /// 입력 js 코드 문자열에서 prefix로 시작하는 모든 함수의 본문을 제거합니다. function과 함수 이름 사이에는 공백이 하나만 허용됩니다
        /// </summary>
        /// <param name="input">입력 문자열</param>
        /// <param name="prefix">함수 접두사</param>
        /// <returns>처리된 문자열</returns>
        public static string RemoveFunctionsWithPrefix(string input, string prefix)
        {
            StringBuilder output = new StringBuilder();

            int braceCount = 0;
            int lastIndex = 0;
            int index = input.IndexOf("function " + prefix);

            while (index != -1)
            {
                output.Append(input, lastIndex, index - lastIndex);
                lastIndex = index;

                while (input[lastIndex] != '{')
                {
                    lastIndex++;
                }

                braceCount = 1;
                ++lastIndex;

                while (braceCount > 0)
                {
                    if (input[lastIndex] == '{')
                    {
                        ++braceCount;
                    }
                    else if (input[lastIndex] == '}')
                    {
                        --braceCount;
                    }
                    ++lastIndex;
                }

                index = input.IndexOf("function " + prefix, lastIndex);
            }

            output.Append(input, lastIndex, input.Length - lastIndex);

            return output.ToString();
        }

        private static bool CheckBuildTemplate()
        {
            string[] res = BuildTemplate.CheckCustomCoverBaseConflict(
                Path.Combine(UnityUtil.GetWxSDKRootPath(), "Runtime", defaultTemplateDir),
                Path.Combine(Application.dataPath, "WX-WASM-SDK-V2", "Editor", "template"),
                new string[] { @"\.(js|ts|json)$" }
                );
            if (res.Length != 0)
            {
                Debug.LogError("시스템에서 사용자 정의 빌드 템플릿의 기본 템플릿이 업데이트되었습니다. 게임 내보내기의 정상 작성을 위해 충돌을 해결하십시오.");
                for (int i = 0; i < res.Length; i++)
                {
                    Debug.LogError($"사용자 정의 템플릿 파일 [{i}]: [ {res[i]} ]");
                }
                Debug.LogError("위 경고의 원인 및 처리 방법에 대해서는 다음 링크를 참조하세요: https://wechat-miniprogram.github.io/minigame-unity-webgl-transform/Design/BuildTemplate.html#%E6%96%B0%E7%89%88%E6%9C%ACsdk%E5%BC%95%E8%B5%B7%E7%9A%84%E5%86%B2%E7%AA%81%E6%8F%90%E9%86%92");
                return false;
            }
            return true;
        }


        // Assert when release + Perf-feature
        private static bool CheckInvalidPerfIntegration()
        {
            const string MACRO_ENABLE_WX_PERF_FEATURE = "ENABLE_WX_PERF_FEATURE";
            string defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

            return (!config.CompileOptions.DevelopBuild) && (defineSymbols.IndexOf(MACRO_ENABLE_WX_PERF_FEATURE) != -1);
        }

        private static void ConvertDotnetCode()
        {
            CompressAssemblyBrotli();
            ConvertDotnetRuntimeCode();
            ConvertDotnetFrameworkCode();
        }

        private static void ConvertDotnetRuntimeCode()
        {
            var runtimePath = GetWeixinMiniGameFilePath("jsModuleRuntime")[0];
            var dotnetJs = File.ReadAllText(runtimePath, Encoding.UTF8);

            Rule[] rules =
            {
                new Rule()
                {
                    old = "await *WebAssembly\\.instantiate\\(\\w*,",
                    newStr = $"await WebAssembly.instantiate(Module[\"wasmPath\"],",
                },
                new Rule()
                {
                    old = "['\"]Expected methodFullName if trace is instrumented['\"]\\);?",
                    newStr = "'Expected methodFullName if trace is instrumented'); return;",
                }
            };
            foreach (var rule in rules)
            {
                if (ShowMatchFailedWarning(dotnetJs, rule.old, "runtime") == false)
                {
                    dotnetJs = Regex.Replace(dotnetJs, rule.old, rule.newStr);
                }
            }

            File.WriteAllText(Path.Combine(config.ProjectConf.DST, miniGameDir, frameworkDir, Path.GetFileName(runtimePath)), dotnetJs, new UTF8Encoding(false));
        }

        private static void CompressAssemblyBrotli()
        {
            GetWeixinMiniGameFilePath("assembly").ToList().ForEach(assembly => UnityUtil.brotli(assembly, assembly + ".br", "8"));
        }

        private static void ConvertDotnetFrameworkCode()
        {
            var target = "webgl.wasm.framework.unityweb.js";
            var dotnetJsPath =
                Path.Combine(config.ProjectConf.DST, webglDir, "Code", "wwwroot", "_framework", "dotnet.js");
            var dotnetJs = File.ReadAllText(dotnetJsPath, Encoding.UTF8);
            // todo: handle dotnet js
            foreach (var rule in ReplaceRules.DoenetRules(new string[] { frameworkDir,
                Path.GetFileName(GetWeixinMiniGameFilePath("jsModuleRuntime")[0]),
                Path.GetFileName(GetWeixinMiniGameFilePath("jsModuleNative")[0]),
            }))
            {
                if (ShowMatchFailedWarning(dotnetJs, rule.old, "dotnet") == false)
                {
                    dotnetJs = Regex.Replace(dotnetJs, rule.old, rule.newStr);
                }
            }
            File.WriteAllText(Path.Combine(config.ProjectConf.DST, miniGameDir, frameworkDir, target), ReplaceRules.DotnetHeader + dotnetJs + ReplaceRules.DotnetFooter, new UTF8Encoding(false));
        }

        private static void ConvertCode()
        {
            UnityEngine.Debug.LogFormat("[Converter] Starting to adapt framework. Dst: " + config.ProjectConf.DST);

            UnityUtil.DelectDir(Path.Combine(config.ProjectConf.DST, miniGameDir));
            string text = String.Empty;
            var target = "webgl.wasm.framework.unityweb.js";
            if (WXExtEnvDef.GETDEF("UNITY_2020_1_OR_NEWER"))
            {
                if (UseIL2CPP)
                {
                    text = File.ReadAllText(Path.Combine(config.ProjectConf.DST, webglDir, "Build", "webgl.framework.js"), Encoding.UTF8);
                }
                else
                {
                    var frameworkPath = GetWeixinMiniGameFilePath("jsModuleNative")[0];
                    target = Path.GetFileName(frameworkPath);
                    text = File.ReadAllText(frameworkPath, Encoding.UTF8);
                }
            }
            else
            {
                text = File.ReadAllText(Path.Combine(config.ProjectConf.DST, webglDir, "Build", "webgl.wasm.framework.unityweb"), Encoding.UTF8);
            }
            int i;
            for (i = 0; i < ReplaceRules.rules.Length; i++)
            {
                var current = i + 1;
                var total = ReplaceRules.rules.Length;
                EditorUtility.DisplayProgressBar($"Converting...，{current}/{total}", "Replace holder...", current * 1.0f / total);
                var rule = ReplaceRules.rules[i];
                // text = Regex.Replace(text, rule.old, rule.newStr);
                if (ShowMatchFailedWarning(text, rule.old, "WXReplaceRules") == false)
                {
                    text = Regex.Replace(text, rule.old, rule.newStr);
                }
            }
            EditorUtility.ClearProgressBar();
            string[] prefixs =
             {
                "_JS_Video_",
                //"jsVideo",
                "_JS_Sound_",
                "jsAudio",
                "_JS_MobileKeyboard_",
                "_JS_MobileKeybard_"
            };
            foreach (var prefix in prefixs)
            {
                text = RemoveFunctionsWithPrefix(text, prefix);
            }
#if PLATFORM_WEIXINMINIGAME
            if (PlayerSettings.WeixinMiniGame.exceptionSupport == WeixinMiniGameExceptionSupport.None)
#else
            if (PlayerSettings.WebGL.exceptionSupport == WebGLExceptionSupport.None)
#endif
            {
                Rule[] rules =
                {
                    new Rule()
                    {
                        old = "console.log\\(\"Exception at",
                        newStr = "if(Module.IsWxGame);console.log(\"Exception at",
                    },
                    new Rule()
                    {
                        old = "throw ptr",
                        newStr = "if(Module.IsWxGame)window.WXWASMSDK.WXUncaughtException(true);else throw ptr",
                    },
                };
                foreach (var rule in rules)
                {
                    text = Regex.Replace(text, rule.old, rule.newStr);
                }
            }

            if (text.Contains("UnityModule"))
            {
                text += ";GameGlobal.unityNamespace.UnityModule = UnityModule;";
            }
            else if (text.Contains("unityFramework"))
            {
                text += ";GameGlobal.unityNamespace.UnityModule = unityFramework;";
            }
            else if (text.Contains("tuanjieFramework"))
            {
                text += ";GameGlobal.unityNamespace.UnityModule = tuanjieFramework;";
            }
            else if (UseIL2CPP)
            {
                if (text.StartsWith("(") && text.EndsWith(")"))
                {
                    text = text.Substring(1, text.Length - 2);
                }

                text = "GameGlobal.unityNamespace.UnityModule = " + text;
            }

            if (!Directory.Exists(Path.Combine(config.ProjectConf.DST, miniGameDir)))
            {
                Directory.CreateDirectory(Path.Combine(config.ProjectConf.DST, miniGameDir));
            }

            if (!Directory.Exists(Path.Combine(config.ProjectConf.DST, miniGameDir, frameworkDir)))
            {
                Directory.CreateDirectory(Path.Combine(config.ProjectConf.DST, miniGameDir, frameworkDir));
            }

            var header = "var OriginalAudioContext = window.AudioContext || window.webkitAudioContext;window.AudioContext = function() {if (this instanceof window.AudioContext) {return wx.createWebAudioContext();} else {return new OriginalAudioContext();}};";

            if (config.CompileOptions.DevelopBuild && config.CompileOptions.enablePerfAnalysis)
            {
                header = header + RenderAnalysisRules.header;
                for (i = 0; i < RenderAnalysisRules.rules.Length; i++)
                {
                    var rule = RenderAnalysisRules.rules[i];
                    text = Regex.Replace(text, rule.old, rule.newStr);
                }
            }

            text = header + text;

            var targetPath = Path.Combine(config.ProjectConf.DST, miniGameDir, target);
            if (!UseIL2CPP)
            {
                targetPath = Path.Combine(config.ProjectConf.DST, miniGameDir, frameworkDir, target);

                foreach (var rule in ReplaceRules.NativeRules)
                {
                    if (ShowMatchFailedWarning(text, rule.old, "native") == false)
                    {
                        text = Regex.Replace(text, rule.old, rule.newStr);
                    }
                }
            }

            {
                Rule[] rules =
                {
                    new Rule()
                    {
                        old = "if (GameGlobal.unityNamespace.enableProfileStats)",
                        newStr = "if (GameGlobal.unityNamespace.enableProfileStats || (typeof GameGlobal.manager.getWXAppCheatMonitor === 'function' && GameGlobal.manager.getWXAppCheatMonitor().shouldForceShowPerfMonitor()))"
                    }
                };
                foreach (var rule in rules)
                {
                    text = text.Replace(rule.old, rule.newStr);
                }
            }


            File.WriteAllText(targetPath, text, new UTF8Encoding(false));

            UnityEngine.Debug.LogFormat("[Converter]  adapt framework done! ");
        }

        private static int Build()
        {
#if PLATFORM_WEIXINMINIGAME
            PlayerSettings.WeixinMiniGame.emscriptenArgs = string.Empty;
            if (WXExtEnvDef.GETDEF("UNITY_2021_2_OR_NEWER"))
            {
                PlayerSettings.WeixinMiniGame.emscriptenArgs += " -s EXPORTED_FUNCTIONS=_main,_sbrk,_emscripten_stack_get_base,_emscripten_stack_get_end";
                if (config.CompileOptions.enablePerfAnalysis)
                {
                    PlayerSettings.WeixinMiniGame.emscriptenArgs += ",_WxPerfFrameIntervalCallback";
                }
                PlayerSettings.WeixinMiniGame.emscriptenArgs += " -s ERROR_ON_UNDEFINED_SYMBOLS=0";
            }

#else
            PlayerSettings.WebGL.emscriptenArgs = string.Empty;
            if (WXExtEnvDef.GETDEF("UNITY_2021_2_OR_NEWER"))
            {
                PlayerSettings.WebGL.emscriptenArgs += " -s EXPORTED_FUNCTIONS=_sbrk,_emscripten_stack_get_base,_emscripten_stack_get_end";
                if (config.CompileOptions.enablePerfAnalysis)
                {
                    PlayerSettings.WebGL.emscriptenArgs += ",_WxPerfFrameIntervalCallback";
                }
#if UNITY_2021_2_5
                PlayerSettings.WebGL.emscriptenArgs += ",_main";
#endif
                PlayerSettings.WebGL.emscriptenArgs += " -s ERROR_ON_UNDEFINED_SYMBOLS=0";
            }
#endif
            PlayerSettings.runInBackground = false;
            if (config.ProjectConf.MemorySize != 0)
            {
                if (config.ProjectConf.MemorySize >= 1024)
                {
                    UnityEngine.Debug.LogErrorFormat($"UnityHeap는 1024보다 작아야 합니다. GIT 문서<a href=\"https://github.com/wechat-miniprogram/minigame-unity-webgl-transform/blob/main/Design/OptimizationMemory.md\">Unity WebGL 메모리 최적화</a>를 참조하세요.");
                    return -1;
                }
                else if (config.ProjectConf.MemorySize >= 500)
                {
                    UnityEngine.Debug.LogWarningFormat($"UnityHeap가 500M 이상일 경우, 32비트 Android 및 iOS 일반 모드에서 대부분 시작에 실패합니다. 중경도 게임은 이 값을 아래로 설정하는 것을 권장합니다. GIT 문서<a href=\"https://github.com/wechat-miniprogram/minigame-unity-webgl-transform/blob/main/Design/OptimizationMemory.md\">Unity WebGL 메모리 최적화</a>를 참조하세요.");
                }
#if PLATFORM_WEIXINMINIGAME
                PlayerSettings.WeixinMiniGame.emscriptenArgs += $" -s TOTAL_MEMORY={config.ProjectConf.MemorySize}MB";
#else
                PlayerSettings.WebGL.emscriptenArgs += $" -s TOTAL_MEMORY={config.ProjectConf.MemorySize}MB";
#endif
            }

            string original_EXPORTED_RUNTIME_METHODS = "\"ccall\",\"cwrap\",\"stackTrace\",\"addRunDependency\",\"removeRunDependency\",\"FS_createPath\",\"FS_createDataFile\",\"stackTrace\",\"writeStackCookie\",\"checkStackCookie\"";
            // 추가 EXPORTED_RUNTIME_METHODS
            string additional_EXPORTED_RUNTIME_METHODS = ",\"lengthBytesUTF8\",\"stringToUTF8\"";

#if PLATFORM_WEIXINMINIGAME
            PlayerSettings.WeixinMiniGame.emscriptenArgs += " -s EXPORTED_RUNTIME_METHODS='[" + original_EXPORTED_RUNTIME_METHODS + additional_EXPORTED_RUNTIME_METHODS + "]'";

            if (config.CompileOptions.ProfilingMemory)
            {
                PlayerSettings.WeixinMiniGame.emscriptenArgs += " --memoryprofiler ";
            }

            if (config.CompileOptions.profilingFuncs)
            {
                PlayerSettings.WeixinMiniGame.emscriptenArgs += " --profiling-funcs ";
            }

#if UNITY_2021_2_OR_NEWER
#if UNITY_2022_1_OR_NEWER
            // 기본적으로 OptimizeSize로 변경하여 코드 패키지 크기를 줄입니다
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WeixinMiniGame, config.CompileOptions.Il2CppOptimizeSize ? Il2CppCodeGeneration.OptimizeSize : Il2CppCodeGeneration.OptimizeSpeed);
#else
            EditorUserBuildSettings.il2CppCodeGeneration = config.CompileOptions.Il2CppOptimizeSize ? Il2CppCodeGeneration.OptimizeSize : Il2CppCodeGeneration.OptimizeSpeed;
#endif
#endif
            UnityEngine.Debug.Log("[Builder] Starting to build WeixinMiniGame project ... ");
            UnityEngine.Debug.Log("PlayerSettings.WeixinMiniGame.emscriptenArgs : " + PlayerSettings.WeixinMiniGame.emscriptenArgs);
#else
            PlayerSettings.WebGL.emscriptenArgs += " -s EXPORTED_RUNTIME_METHODS='[" + original_EXPORTED_RUNTIME_METHODS + additional_EXPORTED_RUNTIME_METHODS + "]'";

            if (config.CompileOptions.ProfilingMemory)
            {
                PlayerSettings.WebGL.emscriptenArgs += " --memoryprofiler ";
            }

            if (config.CompileOptions.profilingFuncs)
            {
                PlayerSettings.WebGL.emscriptenArgs += " --profiling-funcs ";
            }

#if UNITY_6000_0_OR_NEWER
            // 작은게임 변환 도구에서는 wasm2023 기능을 직접 활성화할 수 없으며, 이는 내보낸 webgl에 문제가 발생하므로 강제로 비활성화합니다
           	PlayerSettings.WebGL.wasm2023 = false;
#endif

#if UNITY_2021_2_OR_NEWER
#if UNITY_2022_1_OR_NEWER
                // 기본적으로 OptimizeSize로 변경하여 코드 패키지 크기를 줄입니다
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, config.CompileOptions.Il2CppOptimizeSize ? Il2CppCodeGeneration.OptimizeSize : Il2CppCodeGeneration.OptimizeSpeed);
#else
            EditorUserBuildSettings.il2CppCodeGeneration = config.CompileOptions.Il2CppOptimizeSize ? Il2CppCodeGeneration.OptimizeSize : Il2CppCodeGeneration.OptimizeSpeed;
#endif
#endif
            UnityEngine.Debug.Log("[Builder] Starting to build WebGL project ... ");
            UnityEngine.Debug.Log("PlayerSettings.WebGL.emscriptenArgs : " + PlayerSettings.WebGL.emscriptenArgs);
#endif


            // PlayerSettings.WebGL.memorySize = memorySize;
            BuildOptions option = BuildOptions.None;

            if (config.CompileOptions.DevelopBuild)
            {
                option |= BuildOptions.Development;
            }

            if (config.CompileOptions.AutoProfile)
            {
                option |= BuildOptions.ConnectWithProfiler;
            }

            if (config.CompileOptions.ScriptOnly)
            {
                option |= BuildOptions.BuildScriptsOnly;
            }
#if UNITY_2021_2_OR_NEWER
            if (config.CompileOptions.CleanBuild)
            {
                option |= BuildOptions.CleanBuildCache;
            }
#endif
#if TUANJIE_2022_3_OR_NEWER
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WeixinMiniGame
#if PLATFORM_PLAYABLEADS
                && EditorUserBuildSettings.activeBuildTarget != BuildTarget.PlayableAds
#endif
                )
            {
                UnityEngine.Debug.LogFormat("[Builder] Current target is: {0}, switching to: {1}", EditorUserBuildSettings.activeBuildTarget, BuildTarget.WeixinMiniGame);
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WeixinMiniGame, BuildTarget.WeixinMiniGame))
                {
                    UnityEngine.Debug.LogFormat("[Builder] Switching to {0}/{1} failed!", BuildTargetGroup.WeixinMiniGame, BuildTarget.WeixinMiniGame);
                    return -1;
                }
            }

            var projDir = Path.Combine(config.ProjectConf.DST, webglDir);
#if PLATFORM_PLAYABLEADS
            var result = BuildPipeline.BuildPlayer(GetScenePaths(), projDir, BuildTarget.PlayableAds, option);
#else
            var result = BuildPipeline.BuildPlayer(GetScenePaths(), projDir, BuildTarget.WeixinMiniGame, option);
#endif
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                UnityEngine.Debug.LogFormat("[Builder] BuildPlayer failed. emscriptenArgs:{0}", PlayerSettings.WeixinMiniGame.emscriptenArgs);
                return -1;
            }
#else
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                UnityEngine.Debug.LogFormat("[Builder] Current target is: {0}, switching to: {1}", EditorUserBuildSettings.activeBuildTarget, BuildTarget.WebGL);
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                {
                    UnityEngine.Debug.LogFormat("[Builder] Switching to {0}/{1} failed!", BuildTargetGroup.WebGL, BuildTarget.WebGL);
                    return -1;
                }
            }

            var projDir = Path.Combine(config.ProjectConf.DST, webglDir);

            var result = BuildPipeline.BuildPlayer(GetScenePaths(), projDir, BuildTarget.WebGL, option);
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                UnityEngine.Debug.LogFormat("[Builder] BuildPlayer failed. emscriptenArgs:{0}", PlayerSettings.WebGL.emscriptenArgs);
                return -1;
            }
#endif
            UnityEngine.Debug.LogFormat("[Builder] Done: " + projDir);
            return 0;
        }

        private static string GetWebGLDataPath()
        {
            if (WXExtEnvDef.GETDEF("UNITY_2020_1_OR_NEWER"))
            {
                return Path.Combine(config.ProjectConf.DST, webglDir, "Build", "webgl.data");
            }
            else
            {
                return Path.Combine(config.ProjectConf.DST, webglDir, "Build", "webgl.data.unityweb");
            }
        }

        private static string[] GetWeixinMiniGameFilePath(string key)
        {
            var bootJson = Path.Combine(config.ProjectConf.DST, webglDir, "Code", "wwwroot", "_framework", "blazor.boot.json");
            var boot = JsonMapper.ToObject(File.ReadAllText(bootJson, Encoding.UTF8));
            // Disable jiterpreter if haven't set
            if (!boot.ContainsKey("environmentVariables"))
            {
                var jd = new JsonData();
                jd["INTERP_OPTS"] = "-jiterp";
                boot["environmentVariables"] = jd;
                JsonWriter writer = new JsonWriter();
                boot.ToJson(writer);
                File.WriteAllText(bootJson, writer.TextWriter.ToString());
                Debug.Log("Env INTERP_OPTS added to blazor.boot.json");
            }
            else if (!boot["environmentVariables"].ContainsKey("INTERP_OPTS"))
            {
                boot["environmentVariables"]["INTERP_OPTS"] = "-jiterp";
                JsonWriter writer = new JsonWriter();
                boot.ToJson(writer);
                File.WriteAllText(bootJson, writer.TextWriter.ToString());
                Debug.Log("Env INTERP_OPTS added to blazor.boot.json");
            }
            return boot["resources"][key].Keys.Select(file => Path.Combine(config.ProjectConf.DST, webglDir, "Code", "wwwroot", "_framework", file)).ToArray();
        }

        [DllImport("newstatehooker.dll", EntryPoint = "add_lua_newstate_hook")]
        private static extern int add_lua_newstate_hook_win(string filename);

        [DllImport("newstatehooker", EntryPoint = "add_lua_newstate_hook")]
        private static extern int add_lua_newstate_hook_osx(string filename);

        private static int add_lua_newstate_hook(string filename)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return add_lua_newstate_hook_win(filename);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return add_lua_newstate_hook_osx(filename);
            }

            throw new System.NotSupportedException($"add_lua_newstate_hook not supported on: {RuntimeInformation.OSDescription}");
        }

        private static void MaybeInstallLuaNewStateHook()
        {
            // 현재 버전은 Windows 및 macOS만 지원하며, 조건을 충족하지 않을 경우 건너뜁니다.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Debug.LogWarning($"MaybeInstallLuaNewStateHook:: Cannot install lua runtime on {RuntimeInformation.OSDescription}");
                return;
            }

            // perf 도구가 활성화되지 않아 newstate hook을 도입하지 않습니다.
            if (!config.CompileOptions.enablePerfAnalysis)
            {
                return;
            }

            string codePath = GetWebGLCodePath();
            try
            {
                var ret = add_lua_newstate_hook(codePath);
                if (ret != 0)
                {
                    Debug.LogWarning($"cannot add lua new state hook: {ret}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"cannot add lua new state hook: {e}");
            }
        }

        private static void finishExport()
        {
            MaybeInstallLuaNewStateHook();

            int code = GenerateBinFile();
            if (code == 0)
            {
                convertDataPackage(false);
                UnityEngine.Debug.LogFormat("[Converter] All done!");
                //ShowNotification(new GUIContent("변환 완료"));
                Emit(LifeCycle.exportDone);
            }
            else
            {
                convertDataPackage(true);
            }
        }
        /// <summary>
        /// brotli 압축 후 자원 패키지와 brotli 압축后的 코드 패키지의 총 크기가 30M(작은게임 코드 분할 패키지 총 크기 제한)를 초과하는지 확인합니다
        /// </summary>
        private static void convertDataPackage(bool brotliError)
        {
            var baseDataFilename = dataMd5 + ".webgl.data.unityweb.bin";
            var webglDirPath = Path.Combine(config.ProjectConf.DST, webglDir);
            var minigameDirPath = Path.Combine(config.ProjectConf.DST, miniGameDir);
            var minigameDataPath = Path.Combine(minigameDataPath, "data-package");
            // 압축되지 않은 패키지 이름
            var originDataFilename = baseDataFilename + ".txt";
            var originMinigameDataPath = Path.Combine(minigameDataPath, originDataFilename);
            var originTempDataPath = Path.Combine(webglDirPath, originDataFilename);
            // br 압축된 자원 패키지 이름
            var brDataFilename = baseDataFilename + ".br";
            var brMinigameDataPath = Path.Combine(minigameDataPath, brDataFilename);
            var tempDataBrPath = Path.Combine(webglDirPath, brDataFilename);

            // 자원 파일 이름
            var dataFilename = originDataFilename;
            // 원본 webgl 자원 경로, 즉 webgl/build 디렉토리의 자원 이름
            var sourceDataPath = GetWebGLDataPath();
            // webgl 디렉토리의 자원 경로
            var tempDataPath = originTempDataPath;
            var dataPackageBrotliRet = 0;
            // brotli가 실패하면 CDN으로 로드합니다
            if (brotliError)
            {
                // brotli 실패 후, wasmcode 크기를 알 수 없어 최종 작은게임 총 패키지 크기를 알 수 없습니다. 작은게임 분할 패키지로 자원을 로드할 수 없으므로 CDN 방식으로 복원합니다.
                if (config.ProjectConf.assetLoadType == 1)
                {
                    UnityEngine.Debug.LogWarning("brotli 실패, 파일 크기를 측정할 수 없습니다. 자원 파일을 CDN에 업로드하십시오.");
                    config.ProjectConf.assetLoadType = 0;
                }

                // ShowNotification(new GUIContent("Brotli 압축 실패, 내보낸 디렉토리로 이동하여 수동으로 압축하십시오!!!"));
                Debug.LogError("Brotli 압축 실패, 내보낸 디렉토리로 이동하여 수동으로 압축하십시오!");
            }
            // 자원 패키지 압축이 필요합니다
            if (!!config.ProjectConf.compressDataPackage)
            {
                dataFilename = brDataFilename;
                tempDataPath = tempDataBrPath;
                UnityEngine.Debug.LogFormat("[Compressing] Starting to compress datapackage");
                dataPackageBrotliRet = Brotlib(dataFilename, sourceDataPath, tempDataPath);
                Debug.Log("[Compressing] compress ret = " + dataPackageBrotliRet);
                // 자원 패키지 압축에 실패하면 압축되지 않은 상태로 되돌립니다
                if (dataPackageBrotliRet != 0)
                {
                    config.ProjectConf.compressDataPackage = false;
                    dataFilename = originDataFilename;
                    tempDataPath = originTempDataPath;
                }
            }

            // 자원 패키지 압축이 불필요하거나 압축에 실패했습니다
            if (!config.ProjectConf.compressDataPackage || dataPackageBrotliRet != 0)
            {
                // 자원 패키지를 Build 디렉토리에서 복사하여 압축되지 않은 자원으로 사용합니다
                // 자원 패키지를 Build 디렉토리에서 복사하여 압축되지 않은 자원으로 사용합니다
                File.Copy(sourceDataPath, tempDataPath, true);
            }

            // 작은게임 분할 패키지로 로드할 경우, 20M를 초과하지 않는지 계산해야 합니다
            if (config.ProjectConf.assetLoadType == 1)
            {
                // wasm 패키지 크기 계산
                var brcodePath = Path.Combine(minigameDirPath, "wasmcode", codeMd5 + ".webgl.wasm.code.unityweb.wasm.br");
                var brcodeInfo = new FileInfo(brcodePath);
                var brcodeSize = brcodeInfo.Length;
                // 첫 번째 자원 패키지 크기 계산
                var tempDataInfo = new FileInfo(tempDataPath);
                var tempFileSize = tempDataInfo.Length.ToString();
                // 글루 레이어 및 SDK가 일정 크기를 차지할 수 있으므로 대략 1M로 계산하면 남은 공간은 29M입니다
                if (brcodeSize + int.Parse(tempFileSize) > (30 - 1) * 1024 * 1024)
                {
                    config.ProjectConf.assetLoadType = 0;
                    Debug.LogError("자원 파일이 너무 큽니다. 작은게임 패키지 내 로드에 적합하지 않습니다. 자원 파일을 CDN에 업로드하십시오.");
                }
                else
                {
                    // 작은게임 분할 패키지로 로드할 경우, 압축 성공 및 총 크기 조건을 충족하면 br 파일을 작은게임 디렉토리에 복사합니다
                    File.Copy(tempDataPath, config.ProjectConf.compressDataPackage ? brMinigameDataPath : originMinigameDataPath, true);
                }
            }
            // InstantGame의 첫 번째 자원 패키지 경로를 설정하여 업로드에 사용합니다
            FirstBundlePath = tempDataPath;

            convertDataPackageJS();
        }

        public static void convertDataPackageJS()
        {
            if (!isPlayableBuild)
            {
                checkNeedRmovePackageParallelPreload();
            }

            var loadDataFromCdn = config.ProjectConf.assetLoadType == 0;
            Rule[] rules =
            {
                new Rule()
                {
                    old = "$DEPLOY_URL",
                    newStr = config.ProjectConf.CDN,
                },
                new Rule()
                {
                    old = "$LOAD_DATA_FROM_SUBPACKAGE",
                    newStr = loadDataFromCdn ? "false" : "true",
                },
                new Rule()
                {
                    old = "$COMPRESS_DATA_PACKAGE",
                    newStr = config.ProjectConf.compressDataPackage ? "true" : "false",
                }
            };
            string[] files = { "game.js", "game.json", "project.config.json", "check-version.js" };
            if (WXRuntimeExtEnvDef.IsPreviewing)
            {
                ReplaceFileContent(files, rules, WXRuntimeExtEnvDef.PreviewDst);
            }
            else
            {
                ReplaceFileContent(files, rules);
            }
        }

        private static void checkNeedRmovePackageParallelPreload()
        {
            string dst;
            if (WXRuntimeExtEnvDef.IsPreviewing)
            {
                dst = WXRuntimeExtEnvDef.PreviewDst;
            }
            else
            {
                dst = Path.Combine(config.ProjectConf.DST, miniGameDir);
            }
            // CDN 다운로드 시 병렬 다운로드 설정을 작성할 필요가 없습니다
            if (config.ProjectConf.assetLoadType == 0)
            {
                var filePath = Path.Combine(dst, "game.json");

                string content = File.ReadAllText(filePath, Encoding.UTF8);
                JsonData gameJson = JsonMapper.ToObject(content);
                JsonWriter writer = new JsonWriter();
                writer.IndentValue = 2;
                writer.PrettyPrint = true;
                gameJson["parallelPreloadSubpackages"].Remove(gameJson["parallelPreloadSubpackages"][1]);

                // 설정을 폴더에 다시 저장합니다
                gameJson.ToJson(writer);
                File.WriteAllText(filePath, writer.TextWriter.ToString());
            }
        }

        /// <summary>
        /// 파일의 내용을 교체합니다
        /// </summary>
        /// <param name="files"></param>
        /// <param name="replaceList"></param>
        public static void ReplaceFileContent(string[] files, Rule[] replaceList, string fileDir = null)
        {
            if (files.Length != 0 && replaceList.Length != 0)
            {
                var dstPath = fileDir != null ? fileDir : Path.Combine(config.ProjectConf.DST, miniGameDir);
                for (int i = 0; i < files.Length; i++)
                {
                    var filePath = Path.Combine(dstPath, files[i]);
                    string text = File.ReadAllText(filePath, Encoding.UTF8);
                    for (int j = 0; j < replaceList.Length; j++)
                    {
                        var rule = replaceList[j];
                        text = text.Replace(rule.old, rule.newStr);
                    }

                    File.WriteAllText(filePath, text, new UTF8Encoding(false));
                }
            }
        }

        private static string GetWebGLCodePath()
        {
            if (WXExtEnvDef.GETDEF("UNITY_2020_1_OR_NEWER"))
            {
                if (UseIL2CPP)
                {
                    return Path.Combine(config.ProjectConf.DST, webglDir, "Build", "webgl.wasm");
                }
                else
                {
                    return GetWeixinMiniGameFilePath("wasmNative")[0];
                }
            }
            else
            {
                return Path.Combine(config.ProjectConf.DST, webglDir, "Build", "webgl.wasm.code.unityweb");
            }
        }

        public static string FirstBundlePath = "";
        public static int GenerateBinFile(bool isFromConvert = false)
        {
            UnityEngine.Debug.LogFormat("[Converter] Starting to genarate md5 and copy files");

            var codePath = GetWebGLCodePath();
            codeMd5 = UnityUtil.BuildFileMd5(codePath);
            var dataPath = GetWebGLDataPath();
            dataMd5 = UnityUtil.BuildFileMd5(dataPath);
            var symbolPath = GetWebGLSymbolPath();

            RemoveOldAssetPackage(Path.Combine(config.ProjectConf.DST, webglDir));
            RemoveOldAssetPackage(Path.Combine(config.ProjectConf.DST, webglDir + "-min"));
            var buildTemplate = new BuildTemplate(
                Path.Combine(UnityUtil.GetWxSDKRootPath(), "Runtime", defaultTemplateDir),
                Path.Combine(Application.dataPath, "WX-WASM-SDK-V2", "Editor", "template"),
                Path.Combine(config.ProjectConf.DST, miniGameDir)
                );
            buildTemplate.start();
            // FIX: 2021.2 버전에서 symbol 생성에 버그가 있어 내보낼 때 symbol 생성 오류가 발생합니다. symbol이 있을 때만 복사합니다
            // 코드 분할 패키지는 symbol 파일이 필요합니다. 증분 업데이트를 위해 필요합니다
            if (File.Exists(symbolPath))
            {
                File.Copy(symbolPath, Path.Combine(config.ProjectConf.DST, miniGameDir, "webgl.wasm.symbols.unityweb"), true);
                // gen symbols.br
                Brotlib("webgl.wasm.symbols.unityweb.br", symbolPath, Path.Combine(config.ProjectConf.DST, miniGameDir, "webgl.wasm.symbols.unityweb.br"));
            }

            var info = new FileInfo(dataPath);
            dataFileSize = info.Length.ToString();
            UnityEngine.Debug.LogFormat("[Converter] md5 생성 및 파일 복사가 완료되었습니다");
            // APPID가 빠른 적응 작은게임 예제인 경우, 프리뷰 박스를 삽입합니다
            if (config.ProjectConf.Appid == "wx7c792ca878775717")
            {
                InsertPreviewCode();
            }
            ModifyWeChatConfigs(isFromConvert);
            ModifySDKFile();
            ClearFriendRelationCode();
            GameJsPlugins();

            // StreamingAssets 디렉토리가 없으면 기본 생성
            if (!Directory.Exists(Path.Combine(config.ProjectConf.DST, webglDir, "StreamingAssets")))
            {
                Directory.CreateDirectory(Path.Combine(config.ProjectConf.DST, webglDir, "StreamingAssets"));
            }
            return Brotlib(codeMd5 + ".webgl.wasm.code.unityweb.wasm.br", codePath, Path.Combine(config.ProjectConf.DST, miniGameDir, "wasmcode", codeMd5 + ".webgl.wasm.code.unityweb.wasm.br"));
        }

        private static void InsertPreviewCode()
        {
            Debug.LogWarning("[WeChat Preview] InsertPreviewCode Start");
            Rule[] rules =
            {
                // game.json에 프리뷰 플러그인을 추가합니다
                new Rule()
                {
                    old = "\"plugins\": {",
                    newStr = "\"plugins\": {\n" +
                    "    \"MiniGamePreviewPlugin\": {\n" +
                    "      \"version\": \"latest\",\n" + // 여기에서 버전 번호를 변경합니다
                    "      \"provider\": \"wx7c792ca878775717\",\n" +
                    "      \"contexts\": [\n" +
                    "        {\n" +
                    "          \"type\": \"isolatedContext\"\n" +
                    "        }\n" +
                    "      ]\n" +
                    "    },"
                },
                // game.js에 url 시작 매개변수로 프리뷰 박스 진입
                new Rule()
                {
                    old = "const managerConfig = {",
                    newStr =
                    "export let minigamePreview;\n" +
                    "let isStarted = false;\n" +
                    "wx.onShow((res) => {\n" +
                    "  console.warn('onShow: ' + JSON.stringify(res));\n" +
                    "  // res.query.url = 'localhost:8044';\n" +
                    "  if (!isStarted) {\n" +
                    "    isStarted = true;\n" +
                    "    if (res.query.url) {\n" +
                    "      startPreview(res.query.url);\n" +
                    "    } else {\n" +
                    "      startGame();\n" +
                    "    }\n" +
                    "  } else if (res.query.url) { // 프리뷰 코드 스캔으로 진입\n" +
                    "    wx.restartMiniProgram({\n" +
                    "      path: `/?url=${res.query.url}`\n" +
                    "    });\n" +
                    "  }\n" +
                    "})\n" +
                    "function startPreview(url) {\n" +
                    "  wx.setEnableDebug({ enableDebug: true });\n" +
                    "  const [ip, port] = url.split(':');\n" +
                    "  let MiniGamePreview;\n" +
                    "  if (requirePlugin) {\n" +
                    "    try {\n" +
                    "      MiniGamePreview = requirePlugin('MiniGamePreviewPlugin', {\n" +
                    "        enableRequireHostModule: true,\n" +
                    "        customEnv: {\n" +
                    "          wx,\n" +
                    "          canvas,\n" +
                    "          gameGlobal: {...GameGlobal},\n" +
                    "        },\n" +
                    "      }).default;\n" +
                    "    } catch (e) {\n" +
                    "      console.error(e);\n" +
                    "    }\n" +
                    "    minigamePreview = new MiniGamePreview({\n" +
                    "      ip: ip,\n" +
                    "      port: port\n" +
                    "    })\n" +
                    "    minigamePreview.initStartPage();\n" +
                    "  }\n" +
                    "}\n" +
                    "function startGame() {\n" +
                    "const managerConfig = {",
                },
                // game.js 괄호 보충
                new Rule()
                {
                    old = "    }\n});",
                    newStr = "    }\n});}",
                },
                // unity-sdk/module-helper.js 프리뷰 플러그인 추가
                new Rule()
                {
                    old = "import { MODULE_NAME } from './conf';",
                    newStr = "import { MODULE_NAME } from './conf';\n" +
                    "import { minigamePreview } from '../game';",
                },
                // unity-sdk/module-helper.js 프리뷰 환경에서 hookAPI
                new Rule()
                {
                    old = "this._send = GameGlobal.Module.SendMessage;",
                    newStr = "if (minigamePreview) {\n" +
                    "        this._send = minigamePreview.getPreviewSend();\n" +
                    "      } else {\n" +
                    "        this._send = GameGlobal.Module.SendMessage;\n" +
                    "      }",
                }
            };
            string[] files = { "game.js", "game.json", "unity-sdk/module-helper.js" };
            ReplaceFileContent(files, rules);
            Debug.LogWarning("[WeChat Preview] InsertPreviewCode End");
        }

        private static int Brotlib(string filename, string sourcePath, string targetPath)
        {
            UnityEngine.Debug.LogFormat("[Converter] Starting to generate Brotlib file");
            var cachePath = Path.Combine(config.ProjectConf.DST, webglDir, filename);
            var shortFilename = filename.Substring(filename.IndexOf('.') + 1);

            // 코드가 변경되지 않고 압축 방식이 동일하면 br 압축을 다시 하지 않습니다
            if (cachePath.Contains("wasm.code") && File.Exists(cachePath) && lastBrotliType == config.CompileOptions.brotliMT)
            {
                File.Copy(cachePath, targetPath, true);
                return 0;
            }
            // 이전 br 압축 파일 삭제
            if (Directory.Exists(Path.Combine(config.ProjectConf.DST, webglDir)))
            {
                foreach (string path in Directory.GetFiles(Path.Combine(config.ProjectConf.DST, webglDir)))
                {
                    FileInfo fileInfo = new FileInfo(path);
                    if (fileInfo.Name.Contains(shortFilename))
                    {
                        File.Delete(fileInfo.FullName);
                    }
                }
            }
            if (config.CompileOptions.brotliMT)
            {
                MultiThreadBrotliCompress(sourcePath, targetPath);
            }
            else
            {
                UnityUtil.brotli(sourcePath, targetPath);
            }

            if (targetPath != cachePath)
            {
                File.Copy(targetPath, cachePath, true);
            }
            return 0;
        }

        public static bool MultiThreadBrotliCompress(string sourcePath, string dstPath, int quality = 11, int window = 21, int maxCpuThreads = 0)
        {
            if (maxCpuThreads == 0) maxCpuThreads = Environment.ProcessorCount;
            var sourceBuffer = File.ReadAllBytes(sourcePath);
            byte[] outputBuffer = new byte[0];
            int ret = 0;
            if (sourceBuffer.Length > 50 * 1024 * 1024 && Path.GetExtension(sourcePath) == ".wasm") // 50MB 이상의 wasm은 압축률이 낮아 작은게임 패키지가 20MB를 초과할 수 있으므로 압축률을 높여야 합니다
            {
                ret = BrotliEnc.CompressWasmMT(sourceBuffer, ref outputBuffer, quality, window, maxCpuThreads);
            }
            else
            {
                ret = BrotliEnc.CompressBufferMT(sourceBuffer, ref outputBuffer, quality, window, maxCpuThreads);
            }

            if (ret == 0)
            {
                using (FileStream fileStream = new FileStream(dstPath, FileMode.Create, FileAccess.Write))
                {
                    fileStream.Write(outputBuffer, 0, outputBuffer.Length);
                }
                return true;
            }
            else
            {
                Debug.LogError("CompressWasmMT failed");
                return false;
            }
        }


        /// <summary>
        /// game.json 업데이트
        /// </summary>
        public static void ClearFriendRelationCode()
        {
            string dst;
            if (WXRuntimeExtEnvDef.IsPreviewing)
            {
                dst = WXRuntimeExtEnvDef.PreviewDst;
            }
            else
            {
                dst = Path.Combine(config.ProjectConf.DST, miniGameDir);
            }
            var filePath = Path.Combine(dst, "game.json");

            string content = File.ReadAllText(filePath, Encoding.UTF8);
            JsonData gameJson = JsonMapper.ToObject(content);

            if (!config.SDKOptions.UseFriendRelation || !config.SDKOptions.UseMiniGameChat || config.CompileOptions.autoAdaptScreen)
            {
                JsonWriter writer = new JsonWriter();
                writer.IndentValue = 2;
                writer.PrettyPrint = true;

                // game.json 내의 관계망 관련 설정을 삭제합니다
                // 시도용 game.json에는 다른 설정이 포함되어 있지 않습니다
                if (!config.SDKOptions.UseFriendRelation && gameJson.ContainsKey("openDataContext") && gameJson.ContainsKey("plugins"))
                {
                    gameJson.Remove("openDataContext");
                    gameJson["plugins"].Remove("Layout");

                    // open-data 관련 폴더 삭제
                    string openDataDir = Path.Combine(dst, "open-data");
                    UnityUtil.DelectDir(openDataDir);
                    Directory.Delete(openDataDir, true);
                }

                if (!config.SDKOptions.UseMiniGameChat && gameJson.ContainsKey("plugins"))
                {
                    gameJson["plugins"].Remove("MiniGameChat");
                    UnityEngine.Debug.Log(gameJson["plugins"]);
                }

                if (config.CompileOptions.autoAdaptScreen)
                {
                    gameJson["displayMode"] = "desktop";
                }

                // 설정을 폴더에 다시 저장합니다
                gameJson.ToJson(writer);
                File.WriteAllText(filePath, writer.TextWriter.ToString());
            }
        }

        /// <summary>
        /// game.js 업데이트
        /// </summary>
        public static void GameJsPlugins()
        {
            string dst;
            if (WXRuntimeExtEnvDef.IsPreviewing)
            {
                dst = WXRuntimeExtEnvDef.PreviewDst;
            }
            else
            {
                dst = Path.Combine(config.ProjectConf.DST, miniGameDir);
            }
            var filePath = Path.Combine(dst, "game.js");

            string content = File.ReadAllText(filePath, Encoding.UTF8);

            Regex regex = new Regex(@"^import .*;$", RegexOptions.Multiline);
            MatchCollection matches = regex.Matches(content);

            int lastIndex = 0;
            if (matches.Count > 0)
            {
                lastIndex = matches[matches.Count - 1].Index + matches[matches.Count - 1].Length;
            }

            bool changed = false;
            StringBuilder sb = new StringBuilder(content);
            if (config.ProjectConf.needCheckUpdate)
            {
                sb.Insert(lastIndex, Environment.NewLine + "import './plugins/check-update';");
                changed = true;
            }
            else
            {
                File.Delete(Path.Combine(dst, "plugins", "check-update.js"));
            }
            if (config.CompileOptions.autoAdaptScreen)
            {
                sb.Insert(lastIndex, Environment.NewLine + "import './plugins/screen-adapter';");
                changed = true;
            }
            else
            {
                File.Delete(Path.Combine(dst, "plugins", "screen-adapter.js"));
            }

            if (changed)
            {
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            else
            {
                Directory.Delete(Path.Combine(dst, "plugins"), true);
            }
        }


        public static void ModifySDKFile()
        {
            string dst;
            if (WXRuntimeExtEnvDef.IsPreviewing)
            {
                dst = WXRuntimeExtEnvDef.PreviewDst;
            }
            else
            {
                dst = Path.Combine(config.ProjectConf.DST, miniGameDir);
            }
            string content = File.ReadAllText(Path.Combine(UnityUtil.GetWxSDKRootPath(), "Runtime", defaultTemplateDir, "unity-sdk", "index.js"), Encoding.UTF8);
            content = content.Replace("$unityVersion$", Application.unityVersion);
            File.WriteAllText(Path.Combine(dst, "unity-sdk", "index.js"), content, Encoding.UTF8);
            // content = File.ReadAllText(Path.Combine(Application.dataPath, "WX-WASM-SDK-V2", "Runtime", "wechat-default", "unity-sdk", "storage.js"), Encoding.UTF8);
            if (!isPlayableBuild)
            {
                content = File.ReadAllText(Path.Combine(UnityUtil.GetWxSDKRootPath(), "Runtime", defaultTemplateDir, "unity-sdk", "storage.js"), Encoding.UTF8);
                var PreLoadKeys = config.PlayerPrefsKeys.Count > 0 ? JsonMapper.ToJson(config.PlayerPrefsKeys) : "[]";
                content = content.Replace("'$PreLoadKeys'", PreLoadKeys);
                File.WriteAllText(Path.Combine(dst, "unity-sdk", "storage.js"), content, Encoding.UTF8);
            }
            // 텍스처 dxt 수정
            // content = File.ReadAllText(Path.Combine(Application.dataPath, "WX-WASM-SDK-V2", "Runtime", "wechat-default", "unity-sdk", "texture.js"), Encoding.UTF8);
            content = File.ReadAllText(Path.Combine(UnityUtil.GetWxSDKRootPath(), "Runtime", defaultTemplateDir, "unity-sdk", "texture.js"), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dst, "unity-sdk", "texture.js"), content, Encoding.UTF8);
        }

        public static string HandleLoadingImage()
        {
            var info = AssetDatabase.LoadAssetAtPath<Texture>(config.ProjectConf.bgImageSrc);
            var oldFilename = Path.GetFileName(defaultImgSrc);
            var newFilename = Path.GetFileName(config.ProjectConf.bgImageSrc);
            if (config.ProjectConf.bgImageSrc != defaultImgSrc)
            {
                // 이미지 가로 세로 크기는 2048을 초과할 수 없습니다
                if (info.width > 2048 || info.height > 2048)
                {
                    throw new Exception("표지 이미지의 가로 세로 크기는 2048을 초과할 수 없습니다");
                }

                File.Delete(Path.Combine(config.ProjectConf.DST, miniGameDir, "images", oldFilename));
                File.Copy(config.ProjectConf.bgImageSrc, Path.Combine(config.ProjectConf.DST, miniGameDir, "images", newFilename), true);
                return "images/" + Path.GetFileName(config.ProjectConf.bgImageSrc);
            }
            else
            {
                return "images/" + Path.GetFileName(defaultImgSrc);
            }
        }
        /// <summary>
        /// ;로 문자열을 분리하고, 분리된 각 항목을 콤마로 연결합니다
        /// 예: 입력 "i1;i2;i3" => 출력: `"i1", "i2", "i3"`
        /// </summary>
        /// <param name="inp"></param>
        /// <returns></returns>
        public static string GetArrayString(string inp)
        {
            var result = string.Empty;
            var iterms = new List<string>(inp.Split(new char[] { ';' }));
            iterms.ForEach((iterm) =>
            {
                if (!string.IsNullOrEmpty(iterm.Trim()))
                {
                    result += "\"" + iterm.Trim() + "\", ";
                }
            });
            if (!string.IsNullOrEmpty(result))
            {
                result = result.Substring(0, result.Length - 2);
            }

            return result;
        }
        private class PreloadFile
        {
            public PreloadFile(string fn, string rp)
            {
                fileName = fn;
                relativePath = rp;
            }

            public string fileName;
            public string relativePath;
        }

        /// <summary>
        /// webgl 디렉토리에서 preloadfiles의 파일을 패턴 매칭하여 사전 다운로드 목록으로 사용합니다
        /// </summary>
        private static string GetPreloadList(string strPreloadfiles)
        {
            if (strPreloadfiles == string.Empty)
            {
                return string.Empty;
            }

            string preloadList = string.Empty;
            var streamingAssetsPath = Path.Combine(config.ProjectConf.DST, webglDir + "/StreamingAssets");
            var fileNames = strPreloadfiles.Split(new char[] { ';' });
            List<PreloadFile> preloadFiles = new List<PreloadFile>();
            foreach (var fileName in fileNames)
            {
                if (fileName.Trim() == string.Empty)
                {
                    continue;
                }

                preloadFiles.Add(new PreloadFile(fileName, string.Empty));
            }

            if (Directory.Exists(streamingAssetsPath))
            {
                foreach (string path in Directory.GetFiles(streamingAssetsPath, "*", SearchOption.AllDirectories))
                {
                    FileInfo fileInfo = new FileInfo(path);
                    foreach (var preloadFile in preloadFiles)
                    {
                        if (fileInfo.Name.Contains(preloadFile.fileName))
                        {
                            // StreamingAssets에 대한 상대 경로
                            var relativePath = path.Substring(streamingAssetsPath.Length + 1).Replace('\\', '/');
                            preloadFile.relativePath = relativePath;
                            break;
                        }
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogError("StreamingAssets 디렉토리를 찾을 수 없습니다. 사전 다운로드 목록을 생성할 수 없습니다");
            }

            foreach (var preloadFile in preloadFiles)
            {
                if (preloadFile.relativePath == string.Empty)
                {
                    UnityEngine.Debug.LogError($"모든 사전 다운로드 파일이 찾겨지지 않았습니다. 남은 파일: {preloadFile.fileName}");
                    continue;
                }

                preloadList += "\"" + preloadFile.relativePath + "\", \r";
            }

            return preloadList;
        }

        private static string GetCustomUnicodeRange(string customUnicode)
        {
            if (customUnicode == string.Empty)
            {
                return "[]";
            }

            List<int> unicodeCodes = new List<int>();
            // 문자열의 각 문자를 Unicode 인코딩으로 변환하여 배열에 저장합니다
            foreach (char c in customUnicode)
            {
                unicodeCodes.Add(char.ConvertToUtf32(c.ToString(), 0));
            }

            // 배열을 정렬합니다
            unicodeCodes.Sort();

            // 연속된 인코딩을 범위로 병합합니다
            List<Tuple<int, int>> ranges = new List<Tuple<int, int>>();
            int startRange = unicodeCodes[0];
            int endRange = unicodeCodes[0];

            for (int i = 1; i < unicodeCodes.Count; i++)
            {
                if (unicodeCodes[i] == endRange)
                {
                    continue;
                }
                else if (unicodeCodes[i] == endRange + 1)
                {
                    endRange = unicodeCodes[i];
                }
                else
                {
                    ranges.Add(Tuple.Create(startRange, endRange));
                    startRange = endRange = unicodeCodes[i];
                }
            }
            ranges.Add(Tuple.Create(startRange, endRange));

            StringBuilder ret = new StringBuilder();
            // 범위 출력
            foreach (var range in ranges)
            {
                ret.AppendFormat("[0x{0:X}, 0x{1:X}], ", range.Item1, range.Item2);
            }
            // 문자열 끝의 불필요한 ", " 제거
            ret.Length -= 2;
            ret.Insert(0, "[");
            ret.Append("]");

            return ret.ToString();
        }

        /// <summary>
        /// Unitynamespace 하위의 bootconfig 생성
        /// </summary>
        private static string GenerateBootInfo()
        {
            StringBuilder sb = new StringBuilder();
            // player-connection-ip 정보 추가
            try
            {
                var ips = Dns.GetHostEntry("").AddressList
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ip => ip.ToString())
                    .ToList();

                // 로컬 네트워크 IP(192.168.x.x, 10.x.x.x, 172.16.x.x)를 우선 선택합니다
                var localNetworkIps = ips.Where(ip =>
                    ip.StartsWith("192.168.") ||
                    ip.StartsWith("10.") ||
                    (ip.StartsWith("172.") && int.Parse(ip.Split('.')[1]) >= 16 && int.Parse(ip.Split('.')[1]) <= 31))
                    .ToList();

                // 로컬 네트워크 IP가 있으면 사용하고, 없으면 다른 IP를 사용하며, 마지막으로 127.0.0.1로 되돌립니다
                var selectedIp = localNetworkIps.Any() ? localNetworkIps.First() :
                               ips.Any() ? ips.First() : "127.0.0.1";

                sb.Append($"player-connection-ip={selectedIp}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[선택 사항] Boot info 생성 실패! 오류: " + e.Message);
            }


            return sb.ToString();
        }

        public static void ModifyWeChatConfigs(bool isFromConvert = false)
        {
            UnityEngine.Debug.LogFormat("[Converter] Starting to modify configs");

            var PRELOAD_LIST = GetPreloadList(config.ProjectConf.preloadFiles);
            // 시도용에는 표지 이미지가 없습니다
            var imgSrc = isPlayableBuild ? "" : HandleLoadingImage();

            var bundlePathIdentifierStr = GetArrayString(config.ProjectConf.bundlePathIdentifier);
            var excludeFileExtensionsStr = GetArrayString(config.ProjectConf.bundleExcludeExtensions);

            var screenOrientation = new List<string>() { "portrait", "landscape", "landscapeLeft", "landscapeRight" }[(int)config.ProjectConf.Orientation];
            // 试玩不支持系统字体
            var customUnicodeRange = isPlayableBuild ? "[]" : GetCustomUnicodeRange(config.FontOptions.CustomUnicode);
            Debug.Log("[Converter] customUnicodeRange: " + customUnicodeRange);

            var boolConfigInfo = GenerateBootInfo();

            Rule[] replaceArrayList = ReplaceRules.GenRules(new string[] {
                config.ProjectConf.projectName == string.Empty ? "webgl" : config.ProjectConf.projectName,
                config.ProjectConf.Appid,
                screenOrientation,
                config.CompileOptions.enableIOSPerformancePlus ? "true" : "false",
                config.ProjectConf.VideoUrl,
                codeMd5,
                dataMd5,
                config.ProjectConf.StreamCDN,
                config.ProjectConf.CDN + "/Assets",
                PRELOAD_LIST,
                imgSrc,
                config.ProjectConf.HideAfterCallMain ? "true" : "false",
                config.ProjectConf.bundleHashLength.ToString(),
                bundlePathIdentifierStr,
                excludeFileExtensionsStr,
                config.CompileOptions.Webgl2 ? "2" : "1",
                Application.unityVersion,
                WXExtEnvDef.pluginVersion,
                config.ProjectConf.dataFileSubPrefix,
                config.ProjectConf.maxStorage.ToString(),
                config.ProjectConf.defaultReleaseSize.ToString(),
                config.ProjectConf.texturesHashLength.ToString(),
                config.ProjectConf.texturesPath,
                config.ProjectConf.needCacheTextures ? "true" : "false",
                config.ProjectConf.loadingBarWidth.ToString(),
                GetColorSpace(),
                config.ProjectConf.disableHighPerformanceFallback ? "true" : "false",
                config.SDKOptions.PreloadWXFont ? "true" : "false",
                config.CompileOptions.showMonitorSuggestModal ? "true" : "false",
                config.CompileOptions.enableProfileStats ? "true" : "false",
                config.CompileOptions.iOSAutoGCInterval.ToString(),
                dataFileSize,
                IsInstantGameAutoStreaming() ? "true" : "false",
                (config.CompileOptions.DevelopBuild && config.CompileOptions.enableRenderAnalysis) ? "true" : "false",
                config.ProjectConf.IOSDevicePixelRatio.ToString(),
                UseIL2CPP ? "" : "/framework",
                UseIL2CPP ? "false" : "true",
                config.CompileOptions.brotliMT ? "true" : "false",
                // FontOptions
                config.FontOptions.CJK_Unified_Ideographs ? "true" : "false",
                config.FontOptions.C0_Controls_and_Basic_Latin ? "true" : "false",
                config.FontOptions.CJK_Symbols_and_Punctuation ? "true" : "false",
                config.FontOptions.General_Punctuation ? "true" : "false",
                config.FontOptions.Enclosed_CJK_Letters_and_Months ? "true" : "false",
                config.FontOptions.Vertical_Forms ? "true" : "false",
                config.FontOptions.CJK_Compatibility_Forms ? "true" : "false",
                config.FontOptions.Miscellaneous_Symbols ? "true" : "false",
                config.FontOptions.CJK_Compatibility ? "true" : "false",
                config.FontOptions.Halfwidth_and_Fullwidth_Forms ? "true" : "false",
                config.FontOptions.Dingbats ? "true" : "false",
                config.FontOptions.Letterlike_Symbols ? "true" : "false",
                config.FontOptions.Enclosed_Alphanumerics ? "true" : "false",
                config.FontOptions.Number_Forms ? "true" : "false",
                config.FontOptions.Currency_Symbols ? "true" : "false",
                config.FontOptions.Arrows ? "true" : "false",
                config.FontOptions.Geometric_Shapes ? "true" : "false",
                config.FontOptions.Mathematical_Operators ? "true" : "false",
                customUnicodeRange,
                boolConfigInfo,
                config.CompileOptions.DevelopBuild ? "true" : "false",
                config.CompileOptions.enablePerfAnalysis ? "true" : "false",
                config.ProjectConf.MemorySize.ToString(),
                config.SDKOptions.disableMultiTouch ? "true" : "false",
                // Perfstream, 임시로 false로 설정
                "false",
                config.CompileOptions.enableEmscriptenGLX ? "true" : "false",
                config.CompileOptions.enableiOSMetal ? "true" : "false"
            });

            List<Rule> replaceList = new List<Rule>(replaceArrayList);
            List<string> files = new List<string> { "game.js", "game.json", "project.config.json", "unity-namespace.js", "check-version.js", "unity-sdk/font/index.js" };
            if (isPlayableBuild)
            {
                files = new List<string> { "game.js", "game.json", "project.config.json", "unity-namespace.js", "check-version.js" };
            }

            if (WXRuntimeExtEnvDef.IsPreviewing)
            {
                ReplaceFileContent(files.ToArray(), replaceList.ToArray(), WXRuntimeExtEnvDef.PreviewDst);
                BuildTemplate.mergeJSON(
                    Path.Combine(Application.dataPath, "WX-WASM-SDK-V2", "Editor", "template", "minigame"),
                    WXRuntimeExtEnvDef.PreviewDst
                );
            }
            else
            {
                ReplaceFileContent(files.ToArray(), replaceList.ToArray());
                BuildTemplate.mergeJSON(
                    Path.Combine(Application.dataPath, "WX-WASM-SDK-V2", "Editor", "template", "minigame"),
                    Path.Combine(config.ProjectConf.DST, miniGameDir)
                );
            }
            Emit(LifeCycle.afterBuildTemplate);

            UnityEngine.Debug.LogFormat("[Converter] that to modify configs ended");
        }

        /// <summary>
        /// 현재 프로젝트의 색상 공간을 가져옵니다
        /// </summary>
        /// <returns></returns>
        private static string GetColorSpace()
        {
            switch (PlayerSettings.colorSpace)
            {
                case ColorSpace.Gamma:
                    return "Gamma";
                case ColorSpace.Linear:
                    return "Linear";
                case ColorSpace.Uninitialized:
                    return "Uninitialized";
                default:
                    return "Unknow";
            }
        }

        /// <summary>
        /// 내보내기 디렉토리 webgl 디렉토리의 이전 자원 패키지를 삭제합니다
        /// </summary>
        private static void RemoveOldAssetPackage(string dstDir)
        {
            try
            {
                if (Directory.Exists(dstDir))
                {
                    foreach (string path in Directory.GetFiles(dstDir))
                    {
                        FileInfo fileInfo = new FileInfo(path);
                        if (fileInfo.Name.Contains("webgl.data.unityweb.bin.txt") || fileInfo.Name.Contains("webgl.data.unityweb.bin.br"))
                        {
                            File.Delete(fileInfo.FullName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(ex);
            }
        }

        private static string GetWebGLSymbolPath()
        {
            if (WXExtEnvDef.GETDEF("UNITY_2020_1_OR_NEWER"))
            {
                return Path.Combine(config.ProjectConf.DST, webglDir, "Build", "webgl.symbols.json");
            }
            else
            {
                return Path.Combine(config.ProjectConf.DST, webglDir, "Build", "webgl.wasm.symbols.unityweb");
            }
        }

        private static string[] GetScenePaths()
        {
            List<string> scenes = new List<string>();
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                var scene = EditorBuildSettings.scenes[i];
                UnityEngine.Debug.LogFormat("[Builder] Scenes [{0}]: {1}, [{2}]", i, scene.path, scene.enabled ? "x" : " ");

                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }

        /// <summary>
        /// WebGL1 WebGL2 Linear Gamma 설정을 호환하도록 Assets/WX-WASM-SDK/Plugins
        /// </summary>
        private static void SettingWXTextureMinJSLib()
        {
            string[] jsLibs;
            string DS = WXAssetsTextTools.DS;
            if (UnityUtil.GetSDKMode() == UnityUtil.SDKMode.Package)
            {
                jsLibs = new string[]
                {
                $"Packages{DS}com.qq.weixin.minigame{DS}Runtime{DS}Plugins{DS}SDK-WX-TextureMin-JS-WEBGL1.jslib",
                $"Packages{DS}com.qq.weixin.minigame{DS}Runtime{DS}Plugins{DS}SDK-WX-TextureMin-JS-WEBGL2.jslib",
                $"Packages{DS}com.qq.weixin.minigame{DS}Runtime{DS}Plugins{DS}SDK-WX-TextureMin-JS-WEBGL2-Linear.jslib",
                };
            }
            else
            {
                string jsLibRootDir = $"Assets{DS}WX-WASM-SDK-V2{DS}Runtime{DS}Plugins{DS}";

                // 아래 순서는 변경할 수 없습니다
                jsLibs = new string[]
                {
                     $"{jsLibRootDir}SDK-WX-TextureMin-JS-WEBGL1.jslib",
                     $"{jsLibRootDir}SDK-WX-TextureMin-JS-WEBGL2.jslib",
                     $"{jsLibRootDir}SDK-WX-TextureMin-JS-WEBGL2-Linear.jslib",
                };
            }
            int index = 0;
            if (config.CompileOptions.Webgl2)
            {
                if (PlayerSettings.colorSpace == ColorSpace.Linear)
                {
                    index = 2;
                }
                else
                {
                    index = 1;
                }
            }

            for (int i = 0; i < jsLibs.Length; i++)
            {
                var importer = AssetImporter.GetAtPath(jsLibs[i]) as PluginImporter;
                bool value = i == index;
#if PLATFORM_PLAYABLEADS
                importer.SetCompatibleWithPlatform(BuildTarget.PlayableAds, value);
#elif PLATFORM_WEIXINMINIGAME
                importer.SetCompatibleWithPlatform(BuildTarget.WeixinMiniGame, value);
#else
                importer.SetCompatibleWithPlatform(BuildTarget.WebGL, value);
#endif
                importer.SaveAndReimport();
            }
        }

        public static bool IsInstantGameAutoStreaming()
        {
            if (string.IsNullOrEmpty(GetInstantGameAutoStreamingCDN()))
            {
                return false;
            }
            return true;
        }

        public static bool CheckSDK()
        {
            string dir = Path.Combine(Application.dataPath, "WX-WASM-SDK");
            if (Directory.Exists(dir))
            {
                return false;
            }
            return true;
        }

        public static string GetInstantGameAutoStreamingCDN()
        {
#if UNITY_INSTANTGAME
            string cdn = Unity.InstantGame.IGBuildPipeline.GetInstantGameCDNRoot();
            return cdn;
#else
            return "";
#endif
        }

        public static bool ShowMatchFailedWarning(string text, string rule, string file)
        {
            if (Regex.IsMatch(text, rule) == false)
            {
                Debug.Log($"UnMatched {file} rule: {rule}");
                return true;
            }
            return false;
        }
    }

}

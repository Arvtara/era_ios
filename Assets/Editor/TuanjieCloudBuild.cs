using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// uEmuera 团结云构建入口脚本。
///
/// 供云构建工作流通过 buildMethod 调用：
///   UEmueraBuilder.TuanjieCloudBuild.BuildProject
///
/// 说明：iOS 平台参数（IL2CPP / arm64 / .NET Standard 2.0 / 最低版本）
/// 请在 Unity 的 PlayerSettings 中配置一次并提交到仓库，
/// 本脚本不再通过 API 强制设置，以兼容 Unity 2019.4（部分
/// PlayerSettings 重载在 2019.4 不存在，会引发编译错误）。
/// </summary>
namespace UEmueraBuilder
{
    public static class TuanjieCloudBuild
    {
        static string GetArgValue(string arg, string defaultValue = "")
        {
            var args = Environment.GetCommandLineArgs();
            for(var i = 0; i < args.Length; ++i)
            {
                if(string.Equals(args[i], "-" + arg, StringComparison.OrdinalIgnoreCase) && i < args.Length - 1)
                    return args[i + 1];
            }
            return defaultValue;
        }

        public static void BuildProject()
        {
            var outputDir = GetArgValue("buildsPath", "CloudBuild");
            var projectPath = GetArgValue("projectPath", ".");
            if(!Path.IsPathRooted(outputDir))
                outputDir = Path.Combine(projectPath, outputDir);
            Directory.CreateDirectory(outputDir);

            var buildVersion = GetArgValue("version", Application.version);
            var exportPath = Path.Combine(outputDir, "uEmuera_" + buildVersion);

            Debug.Log("[TuanjieCloudBuild] projectPath = " + projectPath);
            Debug.Log("[TuanjieCloudBuild] outputDir   = " + outputDir);
            Debug.Log("[TuanjieCloudBuild] scenes      = " + EditorBuildSettings.scenes.Length);

            // ------------------------------------------------------------------
            // 强制设置 iOS 图形 API = Metal
            //
            // 【根因】本工程的 ProjectSettings.asset 里 m_BuildTargetGraphicsAPIs 是空数组。
            // 空数组会让 Unity 在导出时走“兜底”分支，把渲染 API 列表填成
            // OpenGLES2。反汇编 UnityFramework 里的 UnityGetRenderingAPIs 可确认：
            //
            //     ; 配置数量为 0 时的兜底分支
            //     mov  w8, #0x2                 ; 2 = apiOpenGLES2
            //     str  w8, [apis]               ; apis[0] = OpenGLES2
            //     mov  w0, #0x1                 ; 返回数量 1
            //
            // 而 iOS 16 之后的 A 系列设备上 OpenGLES 早已不可用，EAGLContext
            // 创建失败，于是 SelectRenderingAPIImpl() 遍历完所有候选后返回 0，
            // 最终 _renderingAPI 保持 0，在 renderingAPI 断言处崩溃：
            //
            //     NSAssert(_renderingAPI != 0,
            //         @"[UnityAppController renderingAPI] called before "
            //          "[UnityAppController selectRenderingApi]");
            //
            // 实测崩溃栈（iPhone SE 3 / iOS 16.3）：
            //     -[UnityAppController(Rendering) renderingAPI]
            //     -[UnityAppController application:didFinishLaunchingWithOptions:]
            //     _userInfoForFileAndLine  →  NSInternalInconsistencyException → abort()
            //
            // 注意 Unity 内部枚举与 GraphicsDeviceType 并不是同一套编号，
            // 对应关系：apiOpenGLES2=2、apiOpenGLES3=3、apiMetal=4，
            // 而 ProjectSettings 里序列化的是 0x08→GLES2 / 0x0b→GLES3 / 0x10→Metal。
            // 所以这里不手写数值，直接调 API 让 Unity 自己转换。
            // ------------------------------------------------------------------
            ForceIOSMetalGraphicsAPI();

            Debug.Log("[TuanjieCloudBuild] scenes      = " + EditorBuildSettings.scenes.Length);

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            // 兜底：EditorBuildSettings.asset 里 m_Scenes 可能为空
            // （本地手工加过场景但未提交该文件时会这样），
            // 此时按约定自动查找 Assets/Main.unity。
            if(scenes.Length == 0)
            {
                Debug.LogWarning("[TuanjieCloudBuild] EditorBuildSettings 场景列表为空，启用兜底查找。");

                // 1) 约定路径优先
                const string conventional = "Assets/Main.unity";
                if(File.Exists(conventional))
                {
                    scenes = new[] { conventional };
                    EditorBuildSettings.scenes = new[]
                    {
                        new EditorBuildSettingsScene(conventional, true)
                    };
                    Debug.Log("[TuanjieCloudBuild] 使用约定场景: " + conventional);
                }
                else
                {
                    // 2) 全盘扫描 .unity（排除 Package 缓存）
                    var found = AssetDatabase.FindAssets("t:Scene")
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Where(p => !string.IsNullOrEmpty(p)
                                    && p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                                    && !p.Contains("/Library/")
                                    && !p.Contains("/PackageCache/"))
                        .OrderBy(p => p)
                        .ToArray();

                    if(found.Length > 0)
                    {
                        scenes = found;
                        EditorBuildSettings.scenes = found
                            .Select(p => new EditorBuildSettingsScene(p, true))
                            .ToArray();
                        Debug.Log("[TuanjieCloudBuild] 扫描到 " + found.Length + " 个场景，已全部启用。");
                        foreach(var s in found) Debug.Log("    " + s);
                    }
                }
            }

            if(scenes.Length == 0)
                throw new Exception(
                    "[TuanjieCloudBuild] 工程内找不到任何 .unity 场景文件，" +
                    "请确认 Assets/Main.unity 已提交到仓库。");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exportPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log(string.Format(
                "[TuanjieCloudBuild] result={0} size={1} time={2}",
                summary.result, summary.totalSize, summary.totalTime));

            if(summary.result != BuildResult.Succeeded)
                throw new Exception("[TuanjieCloudBuild] 构建失败: " + summary.result);

            File.WriteAllText(
                Path.Combine(outputDir, "build-info.txt"),
                string.Format(
                    "version={0}\nunity={1}\nresult={2}\ntime={3}\nios_graphics_api={4}\n",
                    buildVersion, Application.unityVersion, summary.result, DateTime.Now,
                    string.Join("|", PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS)
                        .Select(a => a.ToString()))));

            Debug.Log("[TuanjieCloudBuild] done -> " + exportPath);
        }

        /// <summary>
        /// 把 iOS 平台的图形 API 强制设为 Metal（不启用"自动选择"），并当场校验。
        ///
        /// 只调用 Unity 2019.4 就已存在的 API：
        ///   PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget, bool)
        ///   PlayerSettings.SetGraphicsAPIs(BuildTarget, GraphicsDeviceType[])
        ///   PlayerSettings.GetGraphicsAPIs(BuildTarget)
        ///
        /// 校验失败会直接抛异常中断构建——宁可构建失败，也不要打出一个
        /// 一启动就 abort 的包（此前已经因此浪费了多次 30 分钟的云构建）。
        /// </summary>
        static void ForceIOSMetalGraphicsAPI()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.iOS,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Metal });

            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS);
            var desc = (apis == null || apis.Length == 0)
                ? "<空>"
                : string.Join(", ", apis.Select(a => a.ToString()));

            Debug.Log("[TuanjieCloudBuild] 校验 iOS GraphicsAPIs = " + desc);

            if(apis == null || apis.Length == 0
               || apis[0] != UnityEngine.Rendering.GraphicsDeviceType.Metal)
            {
                throw new Exception(
                    "[TuanjieCloudBuild] iOS 图形 API 设置失败，当前为: " + desc +
                    "。若放任这样导出，Unity 会把候选渲染 API 兜底成 OpenGLES2，" +
                    "在 iOS 16+ 的 A 系列设备上 EAGLContext 创建失败，最终 " +
                    "_renderingAPI 保持 0，App 启动即触发 renderingAPI 断言崩溃。");
            }

            Debug.Log("[TuanjieCloudBuild] iOS 图形 API 已确认为 Metal。");

            // 把 PlayerSettings 的改动落盘，避免 BuildPlayer 内部重新读盘时
            // 又拿到旧的空配置。
            AssetDatabase.SaveAssets();
        }
    }
}

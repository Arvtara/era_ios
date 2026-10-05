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
                string.Format("version={0}\nunity={1}\nresult={2}\ntime={3}\n",
                    buildVersion, Application.unityVersion, summary.result, DateTime.Now));

            Debug.Log("[TuanjieCloudBuild] done -> " + exportPath);
        }
    }
}

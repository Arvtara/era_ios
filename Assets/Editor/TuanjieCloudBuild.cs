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

            if(scenes.Length == 0)
                throw new Exception("[TuanjieCloudBuild] 没有启用的场景，无法构建");

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

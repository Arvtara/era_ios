using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// uEmuera iOS 构建后处理：
/// 1) 自动开启文件共享（Files App / Finder 可见沙盒 Documents，游戏导入主通道）
/// 2) 固化最低 iOS 版本（PlayerSettings 中亦需设置，此处双保险）
/// </summary>
public static class iOSBuildPostprocess
{
    [PostProcessBuild(999)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if(target != BuildTarget.iOS)
            return;

        var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        plist.root.SetBoolean("UIFileSharingEnabled", true);
        plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
        plist.root.SetBoolean("UIRequiresFullScreen", true);
        plist.root.SetString("MinimumOSVersion", "14.0");

        plist.WriteToFile(plistPath);
        Debug.Log("[uEmuera-iOS] Info.plist patched: file sharing enabled, MinimumOSVersion=14.0");
    }
}

using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MinorShift._Library;

public class FirstWindow : MonoBehaviour
{
    public static void Show()
    {
        var obj = Resources.Load<GameObject>("Prefab/FirstWindow");
        obj = GameObject.Instantiate(obj);
        obj.name = "FirstWindow";
    }
    static System.Collections.IEnumerator Run(string workspace, string era)
    {
        var async = Resources.UnloadUnusedAssets();
        while(!async.isDone)
            yield return null;

        var ow = EmueraContent.instance.option_window;
        ow.gameObject.SetActive(true);
        ow.ShowGameButton(true);
        ow.ShowInProgress(true);
        yield return null;

        System.GC.Collect();
        SpriteManager.Init();

        Sys.SetWorkFolder(workspace);
        Sys.SetSourceFolder(era);
        uEmuera.Utils.ResourcePrepare();

        async = Resources.UnloadUnusedAssets();
        while(!async.isDone)
            yield return null;

        EmueraContent.instance.SetNoReady();
        var emuera = GameObject.FindObjectOfType<EmueraMain>();
        emuera.Run();
    }

    void Start()
    {
        if(!string.IsNullOrEmpty(MultiLanguage.FirstWindowTitlebar))
            titlebar.text = MultiLanguage.FirstWindowTitlebar;  

        scroll_rect_ = GenericUtils.FindChildByName<ScrollRect>(gameObject, "ScrollRect");
        item_ = GenericUtils.FindChildByName(gameObject, "Item", true);
        setting_ = GenericUtils.FindChildByName(gameObject, "optionbtn", true);
        GenericUtils.SetListenerOnClick(setting_, OnOptionClick);

        GenericUtils.FindChildByName<Text>(gameObject, "version")
            .text = Application.version + " ";

#if UNITY_IOS && !UNITY_EDITOR
        setting_.SetActive(true);
        StartCoroutine(StartIOS());
#else
        GetList(Application.persistentDataPath);
        setting_.SetActive(true);
#endif

#if UNITY_EDITOR
        var main_entry = GameObject.FindObjectOfType<MainEntry>();
        if(!string.IsNullOrEmpty(main_entry.era_path))
            GetList(main_entry.era_path);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        GetList("storage/emulated/0/emuera");
        GetList("storage/emulated/1/emuera");
        GetList("storage/emulated/2/emuera");

        GetList("storage/sdcard0/emuera");
        GetList("storage/sdcard1/emuera");
        GetList("storage/sdcard2/emuera");
#endif
#if UNITY_STANDALONE && !UNITY_EDITOR
        GetList(Path.GetFullPath(Application.dataPath + "/.."));
#endif
    }

    void OnOptionClick()
    {
        var ow = EmueraContent.instance.option_window;
        ow.ShowMenu();
    }

    void AddItem(string folder, string workspace)
    {
        var rrt = item_.transform as UnityEngine.RectTransform;
        var obj = GameObject.Instantiate(item_);
        var text = GenericUtils.FindChildByName<UnityEngine.UI.Text>(obj, "name");
        text.text = folder;
        text = GenericUtils.FindChildByName<UnityEngine.UI.Text>(obj, "path");
        text.text = workspace + "/" + folder;

        GenericUtils.SetListenerOnClick(obj, () =>
        {
            scroll_rect_ = null;
            item_ = null;
            GameObject.Destroy(gameObject);
            //Start Game
            GenericUtils.StartCoroutine(Run(workspace, folder));
        });

        var rt = obj.transform as UnityEngine.RectTransform;
        var content = scroll_rect_.content;
        rt.SetParent(content);
        rt.localScale = Vector3.one;
        rt.anchorMax = rrt.anchorMax;
        rt.anchorMin = rrt.anchorMin;
        rt.offsetMax = rrt.offsetMax;
        rt.offsetMin = rrt.offsetMin;
        rt.sizeDelta = rrt.sizeDelta;
        rt.localPosition = new Vector2(0, -rt.sizeDelta.y * itemcount_);
        itemcount_ += 1;

        var ih = rt.sizeDelta.y * itemcount_;
        if(ih > content.sizeDelta.y)
        {
            content.sizeDelta = new Vector2(content.sizeDelta.x, ih);
        }
        obj.SetActive(true);
    }

    void GetList(string workspace)
    {
        workspace = uEmuera.Utils.NormalizePath(workspace);
        if(!Directory.Exists(workspace))
            return;
        try
        {
            var paths = Directory.GetDirectories(workspace, "*", SearchOption.TopDirectoryOnly);
            foreach(var p in paths)
            {
                var path = uEmuera.Utils.NormalizePath(p);
                if(File.Exists(path + "/emuera.config") || Directory.Exists(path + "/ERB"))
                    AddItem(path.Substring(workspace.Length + 1), workspace);
            }
        }
        catch(DirectoryNotFoundException e)
        { }
    }

    public Text titlebar = null;
    ScrollRect scroll_rect_ = null;
    GameObject item_ = null;
    GameObject setting_ = null;
    int itemcount_ = 0;

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>
    /// iOS 启动：导入 Documents 下的 zip 游戏包，然后扫描游戏目录。
    /// 主路径为沙盒 Documents/emuera（Files App / Finder 可见）；
    /// 在 TrollStore（no-sandbox entitlement）环境下额外扫描公共目录 /var/mobile/Documents/emuera。
    /// </summary>
    System.Collections.IEnumerator StartIOS()
    {
        var docs = Application.persistentDataPath;
        var emuera_dir = System.IO.Path.Combine(docs, "emuera");
        if(!Directory.Exists(emuera_dir))
        {
            try { Directory.CreateDirectory(emuera_dir); }
            catch(System.Exception) {}
        }

        yield return ImportZips(emuera_dir);
        yield return ImportZips(docs);

        GetList(emuera_dir);
        GetList(docs);

        if(HasNoSandboxAccess())
        {
            var public_dir = "/var/mobile/Documents/emuera";
            if(!Directory.Exists(public_dir))
            {
                try { Directory.CreateDirectory(public_dir); }
                catch(System.Exception) {}
            }
            yield return ImportZips(public_dir);
            GetList(public_dir);
        }
    }

    static bool sandbox_probed_ = false;
    static bool no_sandbox_ = false;

    /// <summary>
    /// 探测是否拥有沙盒豁免（TrollStore 注入 no-sandbox entitlement 后生效）。
    /// 探测失败自动回退为纯沙盒模式，功能不受影响。
    /// </summary>
    public static bool HasNoSandboxAccess()
    {
        if(!sandbox_probed_)
        {
            sandbox_probed_ = true;
            try
            {
                var probe = "/var/mobile/Documents/.uemuera_probe";
                Directory.CreateDirectory("/var/mobile/Documents");
                File.WriteAllText(probe, "1");
                File.Delete(probe);
                no_sandbox_ = true;
            }
            catch(System.Exception) { no_sandbox_ = false; }
        }
        return no_sandbox_;
    }

    /// <summary>
    /// 扫描目录下的 zip 游戏包并解压为同名文件夹，解压完成后 zip 改名 *.zip.imported 避免重复导入。
    /// </summary>
    System.Collections.IEnumerator ImportZips(string dir)
    {
        if(!Directory.Exists(dir))
            yield break;

        foreach(var zip in CollectZips(dir))
        {
            var target = Path.Combine(dir, Path.GetFileNameWithoutExtension(zip));
            try
            {
                ExtractZip(zip, target);
                File.Move(zip, zip + ".imported");
                uEmuera.Logger.Info("[uEmuera-iOS] Imported: " + zip);
            }
            catch(System.Exception e)
            {
                uEmuera.Logger.Error("[uEmuera-iOS] Import failed: " + zip + " / " + e.Message);
            }
            yield return null;  // 每个包解压后让出一帧，避免 UI 卡顿
        }
    }

    static List<string> CollectZips(string dir)
    {
        var zips = new List<string>();
        try
        {
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach(var pattern in new string[] { "*.zip", "*.ZIP" })
            {
                foreach(var f in Directory.GetFiles(dir, pattern))
                {
                    if(!f.EndsWith(".imported", System.StringComparison.OrdinalIgnoreCase) && seen.Add(f))
                        zips.Add(f);
                }
            }
        }
        catch(System.Exception) {}
        return zips;
    }

    static void ExtractZip(string zip, string target)
    {
        Directory.CreateDirectory(target);
        var target_full = Path.GetFullPath(target);
        using(var fs = File.OpenRead(zip))
        using(var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read))
        {
            foreach(var entry in archive.Entries)
            {
                var dest = Path.GetFullPath(Path.Combine(target, entry.FullName));
                if(!dest.StartsWith(target_full))
                    continue;   // 防路径穿越
                if(string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(dest);
                    continue;
                }
                var parent = Path.GetDirectoryName(dest);
                if(!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);
                using(var entry_stream = entry.Open())
                using(var out_stream = File.Create(dest))
                    entry_stream.CopyTo(out_stream);
            }
        }
    }
#endif
}

# uEmuera iOS 版 — 构建与使用手册

> 本源码包已在官方 uEmuera v0.2.9d 基础上完成全部 iOS 移植代码改动。
> 目标：iPhone SE 3（iOS 16.3）/ iPad mini 5（iOS 15）+ TrollStore（巨魔），无需开发者账号。
> 完整背景与选型分析见《uEmuera_iOS移植方案.md》。

## 本次改动清单

| 文件 | 改动 |
|---|---|
| `Assets/Scripts/FirstWindow.cs` | iOS 启动流程：扫描沙盒 `Documents/emuera` 与公共目录；**zip 游戏包自动导入**（放 zip 进目录即自动解压） |
| `Assets/Scripts/Emuera/GameView/EmueraConsole.cs` | 禁用 iOS 上不可用的「用外部编辑器打开脚本」功能 |
| `Assets/Editor/iOSBuildPostprocess.cs` | **新增**：Unity 构建后自动给 Info.plist 开启文件共享等三项配置 |
| `Assets/link.xml` | **新增**：IL2CPP 裁剪保护（zip 解压依赖） |
| `iOS/entitlements.plist` | **新增**：TrollStore 沙盒豁免声明 |
| `iOS/build_ipa.sh` | **新增**：黑苹果一键构建脚本（xcodebuild → ldid → tipa） |

改动 diff 见 `changes.patch`。

---

## 第一步：Windows 上导出 Xcode 工程（约 10 分钟）

1. 用 **Unity 2019.4.2f1**（需勾选 **iOS Build Support** 模块）打开本项目
2. `File → Build Settings → iOS → Switch Platform`
3. PlayerSettings 设置：
   - Scripting Backend：**IL2CPP**
   - Architecture：**arm64**
   - Api Compatibility Level：**.NET Standard 2.0**
   - Minimum iOS Version：**14.0**
   - Target Device：**iPhone + iPad**
   - Default Orientation：**Auto Rotation**（或锁定 Landscape）
4. 勾选 **Export Project**，点 Build，导出到任意目录（得到含 `Unity-iPhone.xcworkspace` 的文件夹）
5. 把导出的工程整个拷贝到黑苹果

> 若 Xcode 编译报错，改用 **Unity 2019.4.40f1**（同大系 LTS 末版）重新导出，工程无需其他改动。

## 第二步：黑苹果上构建 ipa（约 30–60 分钟）

### Xcode 版本要求（重要）

**固定使用 Xcode 14.2**（iOS 16.2 SDK），这是与本工程 Unity 2019.4.4x + IL2CPP 组合兼容性验证最充分的版本：

| Xcode | 需要 macOS | iOS SDK | 与本工程兼容性 | 结论 |
|---|---|---|---|---|
| 13.4.1 | 12.0+ | iOS 15.5 | 官方支持 | 可用（两台目标设备都能跑） |
| **14.2** | **12.5+** | **iOS 16.2** | **社区验证最充分** | **✅ 首选** |
| 14.3.1 | 13.0+ | iOS 16.4 | 多数可行，个别告警 | 备选 |
| 15.x | 13.5+ | iOS 17 | 无官方支持：PNG 符号重复、链接器变化等已知冲突 | ❌ 避免 |
| 16.x | 14.5+ | iOS 18 | 旧工程几乎必挂 | ❌ 避免 |

说明：

- **iOS 16.3 的 SE 3 不需要更新 SDK**：用 16.2 SDK 构建的 App 在 16.3 设备上正常运行（ TrollStore 不校验 SDK 版本，仅要求 arm64 + MinimumOSVersion ≤ 设备版本）
- **iOS 15 的 iPad mini 5 任何 Xcode 13+ 都支持**
- Xcode 14.2 起 bitcode 已弃用——本工程默认关闭 bitcode，无影响
- **下载**：macOS ≥ 12.5 的黑苹果上，用免费 Apple ID 登录 <https://developer.apple.com/download/all/> 搜索下载 `Xcode_14.2.xip`（约 7GB；App Store 只给最新版），解压后拖进 /Applications
- 构建前自检：`xcodebuild -version` 应显示 `Build version 14C18`

```bash
# 环境准备（一次）
brew install ldid
sudo xcode-select -s /Applications/Xcode.app

# 把 iOS/ 目录下的 build_ipa.sh 和 entitlements.plist 放到 Xcode 工程根目录，然后：
cd <Xcode工程目录>
chmod +x build_ipa.sh
./build_ipa.sh
```

脚本自动完成：无签名 xcodebuild → 组装 Payload → ldid 注入巨魔 entitlements → 打包 `dist/uEmuera.tipa`。

> 没有 ldid 也能构建，App 会以纯沙盒模式运行（少公共目录增强，其他功能不变）。

## 第三步：安装到设备（约 5 分钟）

1. `uEmuera.tipa` 通过 **AirDrop** / 文件 App / 网页下载传到 iPhone 或 iPad
2. 在文件 App 中长按该文件 → 分享 → **TrollStore** → Install
3. 巨魔永久签名，无过期、无续签

## 第四步：导入游戏

三条通道任选：

| 通道 | 操作 | 特点 |
|---|---|---|
| **Files App / Finder 拖入** | 把游戏文件夹放进 `uEmuera` App 的 `Documents/emuera/` 下（emuera 目录不存在则先启动一次 App 自动创建） | 最直接，电脑/文件 App 均可操作 |
| **zip 自动导入** | 把游戏 zip 直接放进 `Documents/` 或 `Documents/emuera/`，**启动 App 自动解压**（解压后 zip 改名 `.zip.imported`） | 手机端独立完成，无需电脑 |
| **公共目录（巨魔增强）** | 带 entitlements 构建的版本自动探测 no-sandbox；用 Filza 把游戏放 `/var/mobile/Documents/emuera/` | 体验与 Android 版一致，游戏更新不进沙盒 |

游戏文件夹的判定规则与 Android 版一致：目录内含 `emuera.config` 或 `ERB/` 文件夹。

> **日文文件名注意**：Windows 右键压缩的 zip 为 ANSI/Shift-JIS 编码文件名，导入后可能显示乱码（不影响游戏运行与识别）。如需正常显示，用 7-Zip 以 UTF-8 重打包。

---

## 附：GitHub Actions 云构建（无需 macOS，推荐）⭐

**适用场景**：Unity 已在本机导出 Xcode 工程，但无法接触到 macOS 机器（黑苹果不在身边、不想装等）。

**原理**：把导出的 Xcode 工程推到 GitHub，用 Actions 的 macOS runner 编译 + 注入 TrollStore entitlements + 打包 tipa。

**环境组合**（已按 Unity 2019.4.40f1c1 选定，可直接用）：

| 项 | 值 | 说明 |
|---|---|---|
| runner | `macos-13` | 该 runner 上可用 Xcode 14.2 |
| Xcode | `14.2` | 与 Unity 2019.4.40f1 兼容性最好 |
| 免费额度 | 私有库 2000 分钟/月（macOS 按 10x 计费≈200 分钟）；公开库不限量 | 单次构建约 20-30 分钟 |

### 操作步骤

**1. 在工程目录放好 4 个文件**

```text
<导出的Xcode工程目录>/
├── .github/workflows/build-ios.yml     ← 工作流定义（本包 .github/workflows/）
├── .gitignore                          ← 内容用本包 gitignore-for-xcode-project
├── entitlements.plist                  ← 巨魔 entitlements（本包 iOS/）
└── push_to_github.sh                   ← 推送脚本（本包 .github/）
```

**2. 在 GitHub 建一个空仓库**（私有库即可，不要勾初始化 README）

**3. 推送工程**（875MB 首次推送约几分钟到十几分钟）

```bash
cd <导出的Xcode工程目录>
mv gitignore-for-xcode-project .gitignore
chmod +x push_to_github.sh
./push_to_github.sh git@github.com:你的用户名/uemuera-ios.git
```

> 脚本会自动检查超过 95MB 的单文件——GitHub 硬上限是 100MB，超了会推送失败。

**4. 触发构建**

GitHub 仓库 → **Actions** 标签 → 左侧选 `Build iOS IPA` → **Run workflow**。

**5. 下载产物**

构建完成（约 20-30 分钟）→ 点进该次 run → 页面底部 **Artifacts** → 下载 `uEmuera-tipa`。

**6. 安装**

解压得到 `.tipa` → AirDrop / 文件 App 传到设备 → 分享给 **TrollStore** → 安装。

### 注意事项

| 事项 | 说明 |
|---|---|
| **首次构建必失败的可能** | 875MB 工程的 `Classes/`、`Libraries/` 是 IL2CPP 中间产物，若某些路径在压缩传输中丢失，编译会报文件缺失。看 `build-log` artifact 定位 |
| **不要忽略关键目录** | `.gitignore` 里注释掉的 `Classes/`、`Libraries/`、`Data/`、`UnityFramework.framework/` **必须提交**，它们不是缓存而是构建输入 |
| **LFS 未使用** | 若脚本提示有 >95MB 文件，用 `git lfs track` 逐个处理后再推 |
| **Xcode 版本固定** | workflow 里已 `xcode-select -s /Applications/Xcode_14.2.app`，不要改成 latest（会引入 15/16 的兼容问题） |
| **改代码后重新构建** | 修改 Unity 侧代码需重新导出 Xcode 工程并推送，不能只改 Actions |

### 与本地方案的对比

| | 本地黑苹果 | GitHub Actions |
|---|---|---|
| 需要接触 macOS | 是 | 不需要 |
| 单次耗时 | 30-60 分钟 | 20-30 分钟 + 上传/下载 |
| 费用 | 免费 | 私有库约 200 分钟/月额度 |
| 可重复性 | 随时 | 每次推送自动构建 |
| 首次折腾成本 | 中（装 Xcode） | 低（推代码） |

---

## 附：团结引擎云构建（备选路线）

如果不想在黑苹果上装 Xcode 14.2，可以用团结云构建代跑 macOS —— **但注意两个限制**：
① 云端只能产出 **Xcode 工程**（无签名、无法执行 ldid），TrollStore 的 entitlements 注入和 ipa 打包仍需在 macOS 上补做；② 团结引擎基线是 2022.3，与本工程的 Unity 2019.4 不一致（见《移植方案》选型章节），**若仍用官方 Unity 2019.4 则云上需能选择对应引擎版本**，否则要把工程升级到 2022.3 基线。

本包已内置云构建所需文件：

| 文件 | 作用 |
|---|---|
| `.workflows/build-ios.yaml` | 团结云构建工作流定义（放在项目根 `.workflows/` 下） |
| `Assets/Editor/TuanjieCloudBuild.cs` | 构建入口脚本，被 yaml 的 `buildMethod` 调用 |

操作步骤：

1. **代码入库**：把工程推到团结 DevOps 的 Plastic SCM 仓库（或用 TD 客户端「连接本地项目」同步）
2. **放置配置**：确认 `.workflows/build-ios.yaml` 在**项目文件根目录**下（不在根目录云开发 App 检测不到）
3. **对齐 runs-on 标签**：打开 <https://devops.unity.cn/help/docs/reference/config> 查当前可用的 **macOS 镜像标签**，替换 yaml 中的 `runs-on`（示例值 `macos-tuanjie-1.1.0-8c-16g` 仅作占位，必须替换为文档中实际存在的标签，且镜像的团结版本要与工程一致）
4. **触发构建**：团结云开发 App → 项目 → 构建管理 → 开始构建；产物在「构建制品」中下载
5. **落地本地补两步**（macOS，约 10 分钟）：
   ```bash
   # 下载的制品是导出的 Xcode 工程，不是 ipa
   cd <导出的Xcode工程目录>
   cp <本包>/iOS/build_ipa.sh <本包>/iOS/entitlements.plist .
   chmod +x build_ipa.sh && ./build_ipa.sh
   ```

> 若报错 `runs-on 标签无效`：说明镜像列表已更新，按第 3 步查文档替换即可。
> 若报错找不到构建方法：确认 `Assets/Editor/TuanjieCloudBuild.cs` 已入库，且 `buildMethod` 为 `Editor.Build.TuanjieCloudBuild.BuildProject`。

---

## 常见问题

| 问题 | 处理 |
|---|---|
| Xcode 编译 IL2CPP 产物报错 | 黑苹果固定用 Xcode 14.x；Unity 升级到 2019.4.40f1 重新导出 |
| `UNITY_IOS` 分支没生效 | 确认 Build Settings 已切到 iOS 平台再构建 |
| zip 导入后列表没有游戏 | 确认解压出的文件夹内含 `ERB/` 或 `emuera.config`；若 zip 内部多层嵌套目录，手动把含 `ERB/` 的一层移到 `emuera/` 直下 |
| 大型游戏（eraTW）启动慢/闪退 | 首启解析数千 ERB 属正常（数秒到数十秒）；mini5 若内存吃紧，关掉高资源游戏的其他后台应用 |
| 存档位置 | 游戏目录内 `sav/`，随游戏目录走；两台设备各自独立 |
| 重新构建时 Xcode 缓存异常 | `rm -rf build/` 后重跑脚本 |

## 后续优化路线（P2）

- `ResolutionHelper.cs` 接入 `Screen.safeArea` 自动适配（当前两台设备均有 Home 键，手动缩进设置可兜底）
- zip 文件名 CP932 手动解码（彻底解决日文乱码）
- 内核升级：将 Emuera.EM+EE 内核（纯 C#，GitLab EvilMask/emuera.em）替换进 Unity 壳，获得 EM/EE 扩展指令

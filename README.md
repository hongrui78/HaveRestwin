# 休休 Windows 版（Have A Rest for Windows）

「休休 / Have A Break」macOS 版（`../Have_a_rest`）的 Windows 移植，功能一致：
护眼倒计时休息、站立提醒、空闲自动暂停、全屏休息遮罩、简/繁/英三语、开机自启、单实例。

与 Mac 版的唯一交互差异：Mac 常驻菜单栏，Windows 改为**桌面悬浮计时球**——
一个置顶的小圆角胶囊显示倒计时，鼠标移上去向下展开控制面板，面板里有一排小图标：
**暂停/继续、重置、立刻休息、设置、退出**。计时球可用鼠标拖动换位。

## 功能对照

| Mac 版 | Windows 版 |
| --- | --- |
| 菜单栏计时器 | 悬浮计时球（可在设置里关闭文字，只显示月亮图标） |
| 点击菜单栏弹出控制面板 | 鼠标悬停计时球展开下拉面板（透明底，默认与计时球居中对齐，靠近屏幕边缘时自动改为左/右对齐不出屏） |
| 圆环点击暂停/继续 | 面板内「暂停/继续」图标按钮 |
| 全屏休息遮罩（多显示器） | 相同（每显示器一个 90% 白色遮罩，倒计时结束自动关闭） |
| 站立提醒、合并窗口、离开 5 分钟重计 | 相同 |
| UserDefaults 存设置 | JSON 存 `%APPDATA%\HaveRestwin\settings.json` |
| SMAppService 开机自启 | 注册表 `HKCU\...\Run` 开机自启 |
| flock 文件锁单实例 | 命名 Mutex 单实例 |
| NSSound "Glass" 提示音 | `C:\Windows\Media` 系统提示音 |

## 你需要安装什么

任选其一：

### 方式 A：Visual Studio（推荐新手，图形界面点按钮即可）

1. 下载 [Visual Studio 2022 Community](https://visualstudio.microsoft.com/zh-hans/vs/older-downloads/)（免费，登录微软账号即可）。
2. 安装时勾选工作负载「**.NET 桌面开发**」（Desktop development with .NET），它会自动带上 .NET 8 SDK。

### 方式 B：只装 .NET 8 SDK（更小，命令行编译）

在「开始」搜 `powershell` 执行：

```powershell
winget install Microsoft.DotNet.SDK.8
```

或到 https://dotnet.microsoft.com/download/dotnet/8.0 下载 SDK 安装包（Windows x64 / ARM64 按自己机器选）。

> 本机（Windows on ARM）验证时用的就是 SDK 8.0.426。

## 编译

在项目目录（本 README 所在目录）打开终端：

```powershell
# 调试编译，产物在 bin\Debug\net8.0-windows\HaveARest.exe（需要机器装有 .NET 运行时）
dotnet build -c Debug

# 发布：免安装单文件 exe（推荐给其他人用，体积约 170MB，含运行时）
# IncludeNativeLibrariesForSelfExtract 必须加上：否则 WPF 的原生 DLL 会留在 exe 旁边，
# 单独拷贝/改名这个 exe 会启动即崩（DllNotFoundException）。
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
# ARM64 机器（如 Parallels 虚拟机）把 win-x64 换成 win-arm64
```

用 Visual Studio 的话：打开 `HaveRestwin.csproj` → 顶部菜单「生成」→「生成解决方案」，
或「扩展 → 生成 → 发布」选单文件模式。

> 注意：项目放在网络共享盘（`\\Mac\...`）上时，dotnet 编译可能很慢或失败，
> 建议先把目录拷到本地磁盘（如 `C:\Projects\HaveRestwin`）再编译。

## 现成的成品

`dist\HaveARest-win-arm64.exe` 是在本机（Windows 11 ARM64）编译好的免安装单文件版，
双击即可运行；x64 物理机请用上面 `win-x64` 命令自行编译（该 exe 未入库，体积太大）。

## 使用方法

1. 运行后桌面右上角出现计时球（默认每 22 分钟休息 20 秒，即 20-20-20 法则）。
2. 鼠标移到计时球上展开面板（深色半透明底，1 秒淡入 / 1 秒淡出）：「护眼」+ 大号护眼倒计时、「站立」+ 大号站立倒计时，下方一排图标：暂停/继续、重置、立刻休息、设置、退出。面板默认与计时球居中对齐，若超出屏幕则自动贴左/右边缘，保证完整可见。
3. 到点自动弹全屏遮罩提示休息（消失时淡出），倒计时结束自动关闭；勾选「提示面板显示退出按钮」后会出现「立即退出」。
4. 齿轮（设置图标）打开设置窗口：通用 / 休息 / 关于三个标签，语言、间隔、时长、提示文字、预览都在这里。「休息」页眼睛/站立各自有「面板颜色」「文字颜色」两行：直接点击色块即弹系统取色器；「面板颜色」行右侧是透明度输入框+滑块（10% 一档，默认 50%），遮罩底色按所选颜色与透明度实时渲染。底部居中的「预览」按钮按当前选中的眼睛/站立类型弹出全屏预览。两套配置互相独立、即刻生效。空闲自动暂停不可配置：无操作满 1 分钟自动暂停并冻结计时，离开 5 分钟后再回来，护眼与站立周期都会从头开始。
5. 电源（退出图标）退出程序。

## 项目结构

| 文件 | 作用（对应 Mac 版） |
| --- | --- |
| `App.xaml(.cs)` | 入口、单实例、悬浮球与设置窗管理（AppDelegate.swift） |
| `RestEngine.cs` | 计时核心：周期、空闲检测、暂停恢复（RestEngine.swift） |
| `RestOverlayController.cs` + `RestOverlayWindow.xaml` | 全屏休息遮罩（RestOverlayController.swift） |
| `FloatingWidgetWindow.xaml(.cs)` | 悬浮计时球 + 悬停下拉面板，含居中/贴边对齐算法（MenuContentView.swift） |
| `SettingsWindow.xaml(.cs)` | 设置窗口三标签页（SettingsView.swift） |
| `SettingsStore.cs` | JSON 设置存储 + 开机自启（SettingsStore.swift） |
| `SingleInstance.cs` | Mutex 单实例（SingleInstance.swift） |
| `Localization.cs` | 简/繁/英运行时多语言（Localization.swift） |
| `NativeMethods.cs` | 空闲检测、显示器枚举、DPI 的 Win32 调用 |

## 体积与内存（实测数据）

WPF 应用有固定底座：本机实测运行内存约 95~100MB（工作集），无论怎么发布都差不多，程序自身逻辑占用可忽略。
文件体积则取决于发布方式（均为 ARM64 实测）：

| 方式 | 文件 | 内存 | 说明 |
| --- | --- | --- | --- |
| 自包含单文件（默认，dist 里就是这个） | 171MB | ~96MB | 免安装，拷给任何人直接跑 |
| 自包含 + 单文件压缩（加 `-p:EnableCompressionInSingleFile=true`） | 68MB | ~197MB | 文件小一半，但解压加载导致内存翻倍，常驻小工具不划算 |
| 框架依赖（去掉 `--self-contained`） | ~2MB | ~92MB | 最小，但对方机器要先装「.NET 8 Desktop Runtime」（约 60MB 安装包） |

想进一步压文件还可以加 `-p:PublishSingleFile` 之外再删 pdb（默认就不带）；WPF 不支持 Trim（裁剪），这是 .NET 官方限制。

## 系统要求

Windows 10 / 11（x64 或 ARM64），无需额外运行时（自包含版）。

## 许可

MIT，与 Mac 版一致。

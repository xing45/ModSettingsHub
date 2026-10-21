# ModSettingsHub

把每个 mod 的设置接进 **gogh 游戏原生设置窗口** 的框架（作者：XingMang）。

- 入口：游戏设置左侧图标列最下面一颗「四格网格」图标（右上角那格是空心）。
- 选 mod：页面顶部是下拉框（像原生“语言”页），列出所有有配置的 mod。
- 每个 mod 页第一排：**快捷键**（可改键 / 重置）和 **内置 UI 入口** 开关。
- 配置读 MelonPreferences；带 `###` 注释的控件声明是后续方向（朋友建议：开发者主动声明控件，别依赖自动分类）。

## 目录

```
ModSettingsHub/            主 mod（Mods\ModSettingsHub.dll）
  Api/                     ModSettingsHub.Api 源码副本（共享契约，放 UserLibs）
  UI/                      UI 绘制相关的 partial 文件
  Sprites/                 内嵌的原生 UI 贴图
  *.cs                     其余 partial 文件（入口、场景、条目、工具、输入锁、中文名）
```

## 构建

需要 .NET 6 SDK 与 MelonLoader 0.7.x 的游戏目录。`GameDir` 按以下优先级取值：
命令行 `-p:GameDir="..."` → 环境变量 `GOGH_DIR` → csproj 里的默认路径。

推荐用环境变量（Windows，设置后新开终端生效）：
```
setx GOGH_DIR target path
```
临时使用：PowerShell `$env:GOGH_DIR="..."`，Git Bash `export GOGH_DIR="..."`。

1. 先构建共享契约（会生成 `Api\bin\<Configuration>\net6.0\ModSettingsHub.Api.dll`）：
   ```
   dotnet build Api\ModSettingsHub.Api.csproj -c Release -p:GameDir="你的游戏目录"
   ```
2. 再构建主 mod：
   ```
   dotnet build ModSettingsHub.csproj -c Release -p:GameDir="你的游戏目录"
   ```
   `ApiDir` 默认指向本仓库的 `Api\bin\<Configuration>\net6.0`，一般不需要覆盖；
   如果 Api 输出在别处，可加 `-p:ApiDir=...`。

## 安装

- `Mods\ModSettingsHub.dll`
- `UserLibs\ModSettingsHub.Api.dll`（没有 `UserLibs` 文件夹就自己新建一个，名字必须是 `UserLibs`）

## 让自己的 mod 兼容（热键 / 内置 UI 开关）

引用 `ModSettingsHub.Api.dll`（`Private=false`），然后：

```csharp
using ModSettingsHub.Api;

static readonly HotkeySpec HkPanel = new HotkeySpec("panel", "打开面板", KeyCode.F6);
static readonly HotkeySpec HkSave  = new HotkeySpec("save",  "保存",     KeyCode.S, HotkeyMods.Ctrl);

// OnInitializeMelon
HotkeyStore.Register(new SimpleHotkeys("MyMod", false, HkPanel, HkSave));

// OnUpdate
if (HotkeyStore.Down("MyMod", HkPanel)) TogglePanel();

// 有内置入口的 mod
if (HotkeyStore.UiOn("MyMod", true)) BuildMyEntry();
```

说明：
- `HotkeyStore.Down/Held` 自动校验 Ctrl/Shift/Alt；Hub 打开期间自动挂起，不会误触发。
- 绑定存 `MelonPreferences` 分类 `ModSettingsHub`，键名 `hk_<ModId>_<热键Id>`，文本如 `Ctrl+Shift+F6`。
- 点「重置」恢复你注册的默认键。
- 你的 mod 有 MelonPreferences 分类时，Hub 会自动生成配置页（bool→开关、数字→滑条、string→只读）。

### 坑

1. `MelonPreferences_Category.CreateEntry` 在条目已存在时会**抛异常**（不是返回已有）；先查 `category.Entries` 再建。
2. 所有 mod 引用 API 必须 `Private=false`，否则会有多份 `HotkeyStore` 静态表。
3. `ModId` 要与 MelonPreferences 分类名一致，Hub 才能把热键挂到对应页面。

## 贡献者
- XingMang（作者）
- luminelcy（源码拆分 / 原生设置状态机接入）

## 版本

- 0.2.0（2026-10-18）：原生设置入口 + 下拉选 mod + 快捷键改绑/重置 + 内置 UI 开关。

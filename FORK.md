# 自维护 Fork 说明

## 基线

- 上游 tag：`v2026.19`（`5c53601a`，2026-08-31）
- 维护分支：`fork/2026.19`
- 本 Fork 版本：`1.0`（定义在 `src/SourceFork.csproj`，两位版本号）
- 发布 tag 使用 `sf` 前缀，首个版本为 `sf1.0`
- 主题只保留“系统 / 浅色 / 深色”三种；已移除 ThemeOverrides、自定义主题文件加载和主题仓库入口。
- “第三方工具集成”和“自定义操作”合并为“集成与操作”页，内部再分为终端、外部工具、对比/合并、自定义操作四个子页。
- 终端改为扫描本机可用终端，扫描不到时可用 Custom 手动填写路径。Windows Terminal 和 macOS Terminal 作为默认候选。
- 外部工具改为自动扫描，可逐项勾选是否显示在“打开方式”菜单；支持手动添加和移除自定义工具。
- `herdr` 特殊适配：检测到 herdr 后默认启用；打开仓库时优先聚焦已存在的 herdr workspace，没有则创建新的 workspace。herdr 不会出现在文件级“打开方式”菜单。
- 子模块：`depends/AvaloniaEdit` 固定在 `358f2c9e`（上游 `patch-on-12.x` 分支，Avalonia 12 适配版，已包含不把单个 `\r` 当换行的补丁）

选择 2026.19 的原因：侧边栏/仓库页布局重构从 `02799ba6 ux: repository page layout (#2660)` 开始，
在 2026.19 之后才合入。2026.20 ~ 2026.24 包含大量 `ux`/`style`/布局类提交，
因此把 2026.19 作为长期基线，后续只选择性向前移植功能与修复。

`master` 保持跟踪上游，未做改动，可随时用来查看/对比新版本。

## 本 Fork 的定制

- 品牌与标识整体改为 `SourceFork`：命名空间、程序集/可执行文件、项目文件、资源 URI、UI 文案、macOS bundle id（`me.whynbnb.SourceFork`）、配置目录和打包元数据均使用 SourceFork；仅保留上游链接与版权归属中的 SourceGit。
- 左侧仓库侧边栏：`src/Views/WorkspaceSidebar.axaml`，把已注册的仓库平铺显示，替代顶部 tab；侧边栏与 Welcome 页共用同一份仓库列表，扫描发现的新仓库会立即同步出现。
- 不再有“新标签页/空白页”概念：侧边栏只显示仓库，唯一的欢迎页由侧边栏底部“欢迎页”按钮（⌘+T / Ctrl+T）打开，打开仓库时会保留这个欢迎页。
- 仓库列表完全扁平化：不再有分组、子分组、移动分组或工作区（Workspace）概念；Welcome 页和侧边栏都显示同一份平铺仓库列表。侧边栏不再只显示当前打开的仓库。
- 侧边栏顶部是仓库搜索框（按名称、路径和 tag 过滤），右侧按钮可多选 tag 进行筛选（多个 tag 取交集）。
- 仓库列表支持完整排序：添加时间、名称、更新时间、自定义（拖动），均可切换正序/倒序；Welcome 和侧边栏共用同一套排序设置。
- 支持按主要 Tag 分组显示；主要 Tag 为空的仓库归入“无 Tag”分组，自定义拖动只调整当前分组内的顺序，同时保留全局自定义顺序。
- 每个仓库支持多个 tag；在 Welcome 页或侧边栏右键 `Edit Tags...` 打开 Tag 编辑器，可逐个添加/删除、上下调整顺序，并指定主要 Tag（默认第一个，选中后以强调色显示）。tag 会随偏好设置和仓库内的 `sourcefork.node` 一起保存。
- 自动扫描目录：从 Welcome 工具栏的扫描按钮打开独立窗口（不再放在偏好设置中），可添加和管理多个扫描目录；每个目录可分别设置“仅单层/递归”和“永不自动扫描/打开软件时扫描/实时监听”。目录会永久保存，扫描只把仓库注册到仓库列表，不会自动打开；实时监听会合并短时间内的文件系统变更后重新扫描。目录发现过程在后台线程执行，扫描期间可随时点击“停止扫描”中断。自动扫描会移除由扫描添加、但已从磁盘消失的仓库；手动添加的仓库不会被自动删除。
- 清理仓库历史：Welcome 工具栏和自动扫描窗口均可打开独立的“清理仓库历史”窗口，逐项排除不再需要的仓库，支持带确认的“全部排除”和“全部恢复”。只有记录了扫描来源的仓库才会进入隐藏列表；没有来源的手动仓库只会从列表中移除。窗口提供“清理未知来源”，可一次性移除所有没有扫描来源的条目。扫描结果会显示被隐藏列表跳过的仓库数量。
- 带 Tag 的仓库视为重要/高频仓库：单独删除或排除前会再次确认；批量“全部排除”和“清理未知来源”会跳过这类仓库，并在状态栏显示跳过数量。
- 侧边栏仓库行的关闭按钮现在执行“从列表中移除”：同时关闭已打开的页面、写入隐藏列表，并把焦点移到相邻仓库，避免跳到列表末尾。
- 首次启动时会读取旧偏好中的分组/工作区数据，把所有仓库路径扁平化迁移到新的仓库列表；旧的分组和工作区层级直接丢弃。
- 侧边栏可折叠/展开，状态保存在 `LayoutInfo.WorkspaceSidebarCollapsed`。折叠后 macOS 红绿灯/Windows 菜单会移到内容区顶栏最左侧，工具栏按钮紧随其后。
- 侧边栏宽度保存在 `LayoutInfo.WorkspaceSidebarWidth`；拖动分隔线结束、折叠/展开时会立即保存偏好，避免异常退出丢失宽度。
- 顶部菜单按钮抽成了 `LauncherMenuButton` 控件，方便侧边栏展开和折叠两种位置复用。
- 偏好设置窗口可自由调整宽高，初始尺寸 860×520。
- 移植上游 `Launcher.axaml`、`LauncherPage.axaml`、`RepositoryToolbar.axaml`、`Repository.axaml` 等布局相关提交时，不要直接覆盖，保留本 Fork 的侧边栏和统一顶栏结构。
- 发布流程按 `framework` / `aot` / `aot-static` 三种模式构建；`aot-static` 通过 `StaticLink.Avalonia` 和 `StaticLink.libonigwrap` 静态链接 Avalonia/Skia/HarfBuzz/Oniguruma，输出单文件。
- Release 暂只产出 Linux x64、Windows x64 和 macOS arm64，共 9 种资产；RPM、Flatpak 等暂不构建。
- UI 框架升级到 Avalonia `12.1.6`（DataGrid `12.1.2`），同步完成 Avalonia 12 适配：
  - 移除已废弃的 `Avalonia.Diagnostics`（12.x 起由 Avalonia Plus 的 DiagnosticsSupport 取代）。
  - `ExtendClientAreaChromeHints` 改为 `Window.WindowDecorations`；`SystemDecorations` 改为 `WindowDecorations`。
  - `ContextRequestedEventArgs` 迁移到 `Avalonia.Input`，`GotFocus/LostFocus` 使用 `FocusChangedEventArgs`。
  - `TextBox.Watermark` 改用 `PlaceholderText`。
  - 剪贴板 `SetTextAsync` 使用 `ClipboardExtensions`。
  - 代码中的反射绑定改为 `CompiledBinding`，保证 NativeAOT 下绑定可用。
  - `aot-static` 静态链接包切换为 `StaticLink.Avalonia 3.119.4.1` + `StaticLink.Avalonia.Native 12.1.6.1`（SkiaSharp 3.119.4）。
- Linux 启用原生 Wayland：引入 `Avalonia.Wayland 12.1.6`，使用 `UseWaylandWithFallback()`；检测到可用的 Wayland 合成器时使用原生后端，否则自动回退 X11/XWayland。Wayland 的 `AppId` 设为 `me.whynbnb.SourceFork`。

## 上游关系

已配置为标准 fork 布局：

```
origin    https://github.com/whynbnb/SourceFork          # 自己的仓库，日常 push
upstream  https://github.com/sourcegit-org/sourcegit.git # 官方仓库，只读
```

首次推送分支（需要先登录 GitHub）：

```sh
git push -u origin fork/2026.19
```

拉取上游更新：

```sh
git fetch upstream --tags
```

## 上游同步

`sync/` 保存每个上游版本的逐提交同步评估，规则、判断标记和状态见 `sync/README.md`。
当前已评估到 `v2026.24`，并已选择性应用 128 个上游提交（含适配）；后续继续按提交移植，不做整体 merge。

## 本地工具链（mise）

本仓库通过根目录的 `mise.toml` 固定 .NET SDK `10.0.404`，与全局 Homebrew 的 .NET 11 隔离：

```sh
mise install
dotnet --version        # 应为 10.0.404
dotnet --list-runtimes  # 应包含 Microsoft.NETCore.App 10.0.x
```

如果 `dotnet --version` 仍然是 11：

- 确认 `~/.zshrc` 里有 `eval "$(mise activate zsh)"`
- `which -a dotnet` 的第一项应该是 `~/.local/share/mise/shims/dotnet`
- 或者显式用 `mise exec -- dotnet ...`

`global.json` 也固定到 `10.0.404`（`latestFeature`），避免以后机器上出现 .NET 11 时被自动选中。

## 本地构建 / 运行

```sh
git submodule update --init --recursive
dotnet build src/SourceFork.csproj -c Debug
dotnet run --project src/SourceFork.csproj
```

发布 macOS arm64 版本（推荐 self-contained，最终用户无需安装 .NET）：

```sh
dotnet publish src/SourceFork.csproj \
  -c Release -r osx-arm64 \
  --self-contained true -p:PublishAot=false \
  -o ./dist
```

## 后续 port 上游更新的流程（不急，按需做）

原则：**不合并上游的 UI/布局重构，只按提交选择性移植。**

1. 拉取新版本：
   ```sh
   git fetch upstream --tags
   ```
2. 查看新 tag 相对上一个已移植版本的提交：
   ```sh
   git log --oneline v2026.19..v2026.24
   ```
3. 从 `fork/2026.19` 建一个集成分支，例如：
   ```sh
   git switch -c port/2026.24 fork/2026.19
   ```
4. 按类别 cherry-pick，优先级从高到低：
   - `fix:` —— 优先移植，尤其是崩溃、数据错误、diff 计算错误
   - `project:` —— 依赖/安全升级
   - `enhance:` / `feature:` —— 逐个评估；和布局无关的可以移植
   - `localization:` —— 可整体取，但注意与旧 UI 的 key 差异
   - 跳过：`ux:`、`style:`、`refactor:` 中涉及仓库页布局、侧边栏、页面切换器的提交
5. 冲突高发文件（说明该提交和布局强相关，优先放弃或手工拆分）：
   - `src/Views/Repository.axaml`
   - `src/Views/WorkingCopy.axaml`
   - `src/ViewModels/Repository.cs`
   - `src/Resources/Styles.axaml`
6. 每批移植完成后编译、自测，再合并回 `fork/2026.19`，并在下方记录。

### 已知需要避开的布局/侧边栏提交（2026.19 之后）

```
02799ba6 ux: repository page layout (#2660)
bec580eb feature: supports to collapse/expand the left-sidebar of repository page (#2368)
a7dbf6c4 ux: re-design layout for repository page
db1143a9 ux: re-design repository page
90d9e2dd ux: re-design repository's page
d7a5586b ux: re-design `Local Changes` page
104ed5be ux: repository page main layout
e29a7802 refactor: repository sidebar
060c05cb style: always show repository sidebar
```

## Port 记录

| 日期 | 目标版本 | 集成分支 | 已移植提交 | 跳过/备注 |
| --- | --- | --- | --- | --- |
| 2026-11-20 | v2026.19 | `fork/2026.19` | 基线 | 初始 fork |

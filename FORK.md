# 自维护 Fork 说明

## 基线

- 上游 tag：`v2026.19`（`5c53601a`，2026-08-31）
- 维护分支：`fork/2026.19`
- `VERSION`：`2026.19`
- 子模块：`depends/AvaloniaEdit` 固定在 `11bf2b6a`（v2026.19 对应的版本）

选择 2026.19 的原因：侧边栏/仓库页布局重构从 `02799ba6 ux: repository page layout (#2660)` 开始，
在 2026.19 之后才合入。2026.20 ~ 2026.24 包含大量 `ux`/`style`/布局类提交，
因此把 2026.19 作为长期基线，后续只选择性向前移植功能与修复。

`master` 保持跟踪上游，未做改动，可随时用来查看/对比新版本。

## 本 Fork 的定制

- 左侧工作区侧边栏：`src/Views/WorkspaceSidebar.axaml`，把所有打开的仓库平铺显示，替代顶部 tab。
- 工作区切换移到侧边栏顶部；`src/Views/Launcher.axaml` 中已移除 `LauncherTabBar`。
- 仓库工具栏（`RepositoryToolbar` / `WelcomeToolbar`）已并入顶部标题栏，`LauncherPage` 不再有独立的 36px 工具栏行。
- 顶部标题栏按侧边栏宽度切分：左侧只放窗口控件，仓库工具栏从右侧内容区开始，不跨越左侧栏边界。
- 顶栏原本的页面列表按钮已移除；`Ctrl+P` / `⌘+P` 仍可打开页面列表。
- 侧边栏顶部是仓库搜索框（按名称或路径过滤），最右侧按钮用于切换工作区。
- 侧边栏可折叠/展开，状态保存在 `LayoutInfo.WorkspaceSidebarCollapsed`。折叠后 macOS 红绿灯/Windows 菜单会移到内容区顶栏最左侧，工具栏按钮紧随其后。
- 侧边栏宽度保存在 `LayoutInfo.WorkspaceSidebarWidth`；拖动分隔线结束、折叠/展开时会立即保存偏好，避免异常退出丢失宽度。
- 顶部菜单按钮抽成了 `LauncherMenuButton` 控件，方便侧边栏展开和折叠两种位置复用。
- 偏好设置新增 `Workspaces` 页，工作区管理 UI 抽成 `WorkspaceManagement` 控件；工作区菜单里的“配置工作区”改为直接打开该页。
- 偏好设置窗口可自由调整宽高，初始尺寸 860×520。
- `WorkspaceManagement` 支持导入/导出工作区 JSON（全部或当前选中），导入时自动处理重名。
- 工作区支持 `Hidden` 状态：隐藏后不出现在主界面的工作区切换菜单，但数据保留；管理页用“隐藏”徽标、灰斜体名称和眼睛关闭图标标记，可随时取消隐藏。
- 旧的 `ConfigureWorkspace` 窗口保留为同一控件的轻量宿主，避免重复维护管理界面。
- 侧边栏宽度保存在 `LayoutInfo.WorkspaceSidebarWidth`，可拖动分隔线调整。
- 旧的 `LauncherTabBar` 文件保留但不再引用，方便对照上游。
- 移植上游 `Launcher.axaml`、`LauncherPage.axaml`、`RepositoryToolbar.axaml`、`Repository.axaml` 等布局相关提交时，不要直接覆盖，保留本 Fork 的侧边栏和统一顶栏结构。

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
dotnet build src/SourceGit.csproj -c Debug
dotnet run --project src/SourceGit.csproj
```

发布 macOS arm64 版本（推荐 self-contained，最终用户无需安装 .NET）：

```sh
dotnet publish src/SourceGit.csproj \
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

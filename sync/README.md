# 上游同步

本目录用于记录 SourceFork 与上游 [sourcegit-org/sourcegit](https://github.com/sourcegit-org/sourcegit) 的同步评估和移植结果。

同步原则是**单向、按提交选择性拉取**：只移植 bug 修复、数据正确性修复、性能改进和与当前 Fork 不冲突的新功能；不把上游整体 merge 进来，也不覆盖 SourceFork 已经重做的界面、品牌和集成逻辑。

## 目录结构

```
sync/
├── README.md              # 本文件：规则、状态、工作流
├── TEMPLATE.md            # 新版本评估模板
├── v2026.19.md            # 基线说明
├── v2026.20.md            # v2026.19..v2026.20 逐提交评估
├── v2026.21.md            # v2026.20..v2026.21
├── v2026.22.md            # v2026.21..v2026.22
├── v2026.23.md            # v2026.22..v2026.23
├── v2026.24.md            # v2026.23..v2026.24
└── patches/               # 实际应用过的参考补丁（按版本/提交命名）
```

每个上游版本一个文档。文档至少包含：

- 上游 tag、日期、提交范围
- 该版本的整体结论和优先级
- 每个非合并提交的判断、优先级和原因
- 实际同步记录（日期、提交数、验证结果）

## 版本状态

最近评估日期：2026-11-21。

| 版本 | 上游范围 | 非合并提交 | 评估状态 | 已应用 | 文档 |
| --- | --- | ---: | --- | ---: | --- |
| v2026.19 | 基线 | — | 已确定基线 | 0 | [v2026.19.md](v2026.19.md) |
| v2026.20 | `v2026.19..v2026.20` | 105 | 已评估，部分同步 | 15 | [v2026.20.md](v2026.20.md) |
| v2026.21 | `v2026.20..v2026.21` | 29 | 已评估，部分同步 | 11 | [v2026.21.md](v2026.21.md) |
| v2026.22 | `v2026.21..v2026.22` | 51 | 已评估，部分同步 | 4 | [v2026.22.md](v2026.22.md) |
| v2026.23 | `v2026.22..v2026.23` | 18 | 已评估，部分同步 | 8 | [v2026.23.md](v2026.23.md) |
| v2026.24 | `v2026.23..v2026.24` | 51 | 已评估，部分同步 | 10 | [v2026.24.md](v2026.24.md) |

> 表中“已应用”指真正进入 `fork/2026.19` 的提交数量，不是评估文档数量。截至 2026-11-21 共应用 48 个提交，另有 2 个已被 Fork 现有实现覆盖。
> 评估基于 2026-11-21 的 Fork 状态；如果 Fork 后续又改动了同一区域，需要重新确认冲突。

## 判断标记

| 标记 | 含义 | 处理方式 |
| --- | --- | --- |
| **同步** | 与 Fork 当前定制没有结构冲突 | 优先 `git cherry-pick -x <hash>`，冲突时按 Fork 结构手工解决 |
| **修改后同步** | 修复/功能有价值，但命中 Fork 已重写的文件或交互 | 先看 diff，再手工移植；不要直接 checkout 上游文件 |
| **跳过** | 属于上游布局/主题/文档，或与 Fork 方案冲突 | 不应用；只保留评估记录 |
| **已覆盖** | Fork 已经用另一种方式实现，或依赖版本已更新 | 不重复应用；在文档里记录等价实现 |
| **仅记录** | 纯代码风格/整理/PR 附属修改 | 不单独同步；随相关功能提交一起处理 |

## 优先级

| 优先级 | 范围 | 建议 |
| --- | --- | --- |
| **P0** | 崩溃、数据错误、安全问题、git 行为错误 | 尽快处理，单独验证 |
| **P1** | 重要 bug、正确性、核心功能增强 | 每个版本排期处理 |
| **P2** | 体验、性能、外围功能、可选增强 | 有空再处理 |
| **—** | 文档、翻译、样式、跳过项 | 不进入代码排期 |

## 工作流

1. 拉取上游 tag：

   ```sh
   git fetch upstream --tags
   ```

2. 确认新版本范围，例如：

   ```sh
   git log --oneline --no-merges v2026.24..v2026.25
   ```

3. 复制模板生成新文档：

   ```sh
   cp sync/TEMPLATE.md sync/v2026.25.md
   ```

4. 逐个提交查看 `git show --stat <hash>` 和必要的 diff，填写判断、优先级、原因。涉及多个提交的功能（例如 worktree、API Key/keychain、Focus 模式）要标注“成组移植”。

5. 在独立分支上应用：

   ```sh
   git switch -c sync/2026.25 fork/2026.19
   git cherry-pick -x <hash>
   ```

   不能直接 cherry-pick 的，手工移植后把参考补丁存到 `sync/patches/v2026.25/`。

6. 每批同步后至少执行：

   ```sh
   dotnet build src/SourceFork.csproj -c Debug
   ```

   涉及发布/打包的改动，再执行对应的 `build/scripts/publish.sh` 验证。

7. 更新对应版本文档的“同步记录”和本文件的版本状态。

8. 不把 `sync/` 文档和业务代码混在一个提交里；文档评估和实际移植分开提交，便于回滚。

## Fork 定制红线

以下文件/区域已经与上游明显分叉。上游提交命中这些位置时，默认按“修改后同步”或“跳过”处理，禁止直接覆盖：

- 仓库页与顶栏：`src/Views/Repository.axaml(.cs)`、`RepositoryToolbar.axaml(.cs)`、`WorkingCopy.axaml(.cs)`
- 启动器/侧边栏：`Launcher.axaml(.cs)`、`LauncherPage.axaml(.cs)`、`WorkspaceSidebar.axaml(.cs)`、`LauncherTabBar.*`
- 偏好设置/工作区：`Preferences.axaml(.cs)`、`ViewModels/Preferences.cs`、`Workspace*`、`ConfigureWorkspace.*`
- 集成与工具：`Models/ExternalTool.cs`、`Models/ShellOrTerminal.cs`、`Models/HerdrTool.cs`、`Native/OS.cs`、`Native/MacOS.cs`、`Native/Linux.cs`、`Native/Windows.cs`
- 应用入口：`App.axaml(.cs)`、`App.Commands.cs`
- 主题/样式：`Resources/Styles.axaml`、`Resources/Themes.axaml`
- 本地化：`Resources/Locales/*`（保留 SourceFork 自定义 key 和品牌文案）
- 发布与品牌：`SourceFork.csproj`、`.github/workflows/release.yml`、`build/`、`README*`、`FORK.md`
- 编辑器子模块：`depends/AvaloniaEdit`（固定在 `patch-on-12.x`，不要退回上游 11.x 版本）

## 补丁目录

`sync/patches/` 只存放**已经评估并实际应用/参考过**的补丁，不存放整包上游快照。

命名：

```
sync/patches/v2026.24/8189114b-push-full-refname.patch
sync/patches/v2026.24/4cbe50e3-api-key-keychain.patch
```

生成：

```sh
mkdir -p sync/patches/v2026.24
git format-patch -1 8189114b --stdout > sync/patches/v2026.24/8189114b-push-full-refname.patch
```

补丁可能需要按 Fork 结构改写；如果改写过，在对应版本文档里注明“补丁已适配”。

<p align="center">
  <img src="docs/logo.png" width="128" height="128" alt="SourceFork logo">
</p>

<h1 align="center">SourceFork</h1>

<p align="center"><a href="README.md">English</a> | 简体中文</p>

> **SourceFork 是 [SourceGit](https://github.com/sourcegit-scm/sourcegit) 的个人分支，基于 `v2026.19`。** 本项目与上游项目无隶属关系。

> 使用 LLM 维护（DeepSeek V4.1 Flash 1017 / OpenCode）。

SourceFork 保留 SourceGit 的 Git 客户端能力，并围绕常驻的工作区侧边栏重构了主界面。目标是在多个仓库之间快速切换，而不是在标签栏里来回寻找。

## 截图

**浅色**

![浅色主题](docs/screenshot-zh-light.png)

**深色**

![深色主题](docs/screenshot-zh-dark.png)

## 主要改进

- **工作区侧边栏** — 仓库平铺在左侧栏中，替代顶部标签页；支持搜索、折叠，并记住宽度。
- **工作区管理** — 在偏好设置中新增、编辑、排序、导入/导出或隐藏工作区。
- **偏好设置** — 左侧导航，窗口可自由调整大小。
- **图标** — 橙色替换为蓝色。

## 为什么基于 v2026.19？

`v2026.19` 是上游重构仓库页面布局之前的最后一个版本。本分支保留这套布局，并在其上实现工作区侧边栏，因此后续的上游修复和功能只做选择性移植，而不是整体合并。

## 构建

需要 `git` 和 .NET 10 SDK。仓库通过 `mise` 固定 SDK 版本：

```sh
mise install
git submodule update --init --recursive
dotnet build src/SourceFork.csproj -c Debug
dotnet run --project src/SourceFork.csproj
```

## 说明

- 移植流程和详细改动记录见 [FORK.md](FORK.md)。
- 上游项目：<https://github.com/sourcegit-scm/sourcegit>
- 许可证：[MIT](LICENSE)

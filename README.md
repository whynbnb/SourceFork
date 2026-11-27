<p align="center">
  <img src="docs/logo.png" width="128" height="128" alt="SourceFork logo">
</p>

<h1 align="center">SourceFork</h1>

<p align="center">English | <a href="README.zh-CN.md">简体中文</a></p>

> **SourceFork is a personal fork of [SourceGit](https://github.com/sourcegit-scm/sourcegit), based on `v2026.19`.** It is not affiliated with the upstream project.

> Maintained with an LLM (DeepSeek V4.1 Flash 1017 / OpenCode).

SourceFork keeps SourceGit's Git client and rebuilds the main window around a persistent repository sidebar. The goal is to make switching between repositories faster and more direct, instead of hunting through tabs.

## Screenshots

**Light**

![Light theme](docs/screenshot-en-light.png)

**Dark**

![Dark theme](docs/screenshot-en-dark.png)

## What's different

- **Flat repository sidebar** — all registered repositories are listed flat on the left and stay in sync with the Welcome page instead of using top tabs. The sidebar is searchable, collapsible, and remembers its width. Drag reordering shows an insertion line, and right click opens the row menu without changing the selection.
- **Repository status in the sidebar** — the repository name is taken from its folder, the row shows the name and secondary tags while the primary tag is shown by the group header, and the branch pill is followed by a freshness label based on the latest commit date (today, this week, this month, or long ago) on the second line; commit dates are cached with the HEAD SHA, so `git log` only runs when HEAD moves, and repository statuses refresh in parallel, and the dirty indicator sits before the name. Paths are hidden by default and can be enabled in preferences. Statuses are refreshed in the background after startup.
- **Simplified repository metadata** — custom bookmarks, the separate edit-repository dialog, and the bookmark icons were removed. Repository names always follow the folder name.
- **Single Welcome page** — there is no blank-page or new-tab concept. A dedicated button in the title bar opens the one Welcome page (at the right edge of the sidebar title bar on macOS, or leftmost next to the menu button on other platforms). The page uses a left-right layout: tags on the left (including All repositories and No tag) and the matching repositories on the right, with the search box spanning both columns.
- **Tags** — give each repository multiple ordered tags with a primary tag. The sidebar filters by one or more tags (a repository matches when it has any selected tag); the Welcome page lists tags on the left and shows the selected tag group on the right. The tag editor lists tags already used by other repositories so they can be reused with one click.
- **Grouping and per-group sorting** — repositories are always grouped by primary tag. Each group can sort its repositories by added time, name, or updated time (with an optional reverse order), or use a custom order defined by dragging inside that group. The group menu keeps collapse/expand and the sort order for that group; visibility, group ordering, and moving groups are configured in the “Manage tag groups…” window, which also exposes each group’s sort order. Everything is remembered.
- **Configurable repository toolbar** — File Explorer and Terminal sit on the left and Fetch/Pull/Push with the middle More button in the middle by default; stash, apply patch, GitFlow, LFS, bisect, custom actions and cleanup live in the middle More menu, while open-with, logs, statistics, settings and focus-current-branch start hidden. Each of the left, middle and right zones supports its own More menu; the left and right More menus are off by default. Choose Hidden, Left, Middle, Right, Left More menu, Middle More menu or Right More menu per action under Preferences → Integrations → Repository toolbar. The Welcome toolbar is unaffected.
- **Auto-scan folders** — open a standalone scan window from the Welcome toolbar and register multiple folders. Each folder can scan only its direct children or recurse into subdirectories, and can scan on startup, only manually, or watch for changes in real time. Discovery runs on a background thread and can be stopped at any time; discovered repositories are added without being opened, and scanned repositories that disappear from disk are removed automatically.
- **Drag-and-drop import** — dropping folders dims the app and shows an import panel with each repository's name, path and status (will import, already managed, not a Git repository, or path not found); a workspace folder is expanded into its repositories. A normal drop imports everything and closes the panel. Dropping on the small pin area keeps the panel open so repositories can be checked individually, then “Import selected” applies the choice. The panel closes with the close button or Esc.
- **Clean repository history** — review the repository list and exclude entries one by one, or exclude/restore all entries with one confirmed action. Only repositories with a recorded scan source enter the hidden list; manually added repositories without a source are simply removed. A separate action cleans up every unknown-origin entry at once.
- **Tagged repositories are protected** — repositories with tags are treated as important. Removing one individually requires an extra confirmation, and bulk cleanup skips them automatically.
- **Remove from list** — remove a repository from the row context menu; it closes its page, adds it to the hidden list, and keeps focus on the adjacent repository. The close-tab / close-other-tabs actions and the Ctrl/⌘+W shortcut were removed.
- **No repository groups or workspaces** — manual repository groups and workspaces were removed; all repositories live in one list and are always grouped automatically by primary tag.
- **Preferences** — left-side navigation and a resizable window.
- **Icon** — orange replaced with blue.

## Why v2026.19?

`v2026.19` is the last release before upstream reworked the repository page layout. This fork keeps that layout and builds the flat repository sidebar on top of it, so later upstream fixes and features are ported selectively instead of merged wholesale.

## Build

Requires `git` and the .NET 10 SDK. The repository pins the SDK with `mise`:

```sh
mise install
git submodule update --init --recursive
dotnet build src/SourceFork.csproj -c Debug
dotnet run --project src/SourceFork.csproj
```

## Notes

- See [FORK.md](FORK.md) for the porting workflow and a detailed change log (Chinese).
- Upstream project: <https://github.com/sourcegit-scm/sourcegit>
- License: [MIT](LICENSE)

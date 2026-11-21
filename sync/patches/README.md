# 同步补丁

本目录存放从上游评估后**实际应用或参考过**的补丁，按版本和提交号组织。

## 规则

- 只存放已经评估、确认要同步的提交，不存放整包上游快照。
- 先按版本建目录，例如 `v2026.24/`。
- 文件名使用 `<短提交号>-<简短主题>.patch`。
- 如果补丁按 SourceFork 结构做过改写，在文件头或对应版本文档里注明“已适配”。
- 不提交二进制产物、构建缓存或完整仓库压缩包。

## 生成

```sh
mkdir -p sync/patches/v2026.24
git format-patch -1 8189114b --stdout \
  > sync/patches/v2026.24/8189114b-push-full-refname.patch
```

## 应用

```sh
git switch -c sync/2026.24 fork/2026.19
git am sync/patches/v2026.24/8189114b-push-full-refname.patch
```

如果补丁与 Fork 冲突，不要强行 `git am`；改为手工移植，并保留 Fork 的侧边栏、顶栏、偏好设置和工具集成结构。

## 当前状态

截至 2026-11-21，评估文档已建立，但尚未实际应用任何上游补丁，因此目录中还没有 `.patch` 文件。

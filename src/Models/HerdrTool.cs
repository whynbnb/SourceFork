using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace SourceFork.Models
{
    public static class HerdrTool
    {
        public const string Name = "herdr";

        public static bool Launch(string execFile, string args)
        {
            var repo = (args ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrEmpty(repo) || !Directory.Exists(repo))
                return false;

            var workspaceID = FindInWorktrees(execFile, repo) ?? FindInSnapshot(execFile, repo);
            if (!string.IsNullOrEmpty(workspaceID) &&
                Run(execFile, repo, "workspace", "focus", workspaceID) != null)
                return true;

            var label = Path.GetFileName(repo.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(label))
                label = repo;

            return Run(execFile, repo, "workspace", "create", "--cwd", repo, "--label", label, "--focus") != null;
        }

        private static string FindInWorktrees(string execFile, string repo)
        {
            var json = Run(execFile, repo, "worktree", "list", "--cwd", repo);
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("result", out var result) ||
                    !result.TryGetProperty("worktrees", out var worktrees))
                    return null;

                foreach (var worktree in worktrees.EnumerateArray())
                {
                    if (!worktree.TryGetProperty("path", out var path) ||
                        !worktree.TryGetProperty("open_workspace_id", out var workspace))
                        continue;

                    var workspaceID = workspace.GetString();
                    if (!string.IsNullOrEmpty(workspaceID) && IsSamePath(path.GetString(), repo))
                        return workspaceID;
                }
            }
            catch
            {
                // Ignore malformed output and let the caller create a new workspace.
            }

            return null;
        }

        private static string FindInSnapshot(string execFile, string repo)
        {
            var json = Run(execFile, repo, "api", "snapshot");
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("result", out var result) ||
                    !result.TryGetProperty("snapshot", out var snapshot) ||
                    !snapshot.TryGetProperty("panes", out var panes))
                    return null;

                foreach (var pane in panes.EnumerateArray())
                {
                    if (!pane.TryGetProperty("workspace_id", out var workspace))
                        continue;

                    var workspaceID = workspace.GetString();
                    if (string.IsNullOrEmpty(workspaceID))
                        continue;

                    var cwd = pane.TryGetProperty("cwd", out var cwdValue) ? cwdValue.GetString() : null;
                    var foreground = pane.TryGetProperty("foreground_cwd", out var fgValue) ? fgValue.GetString() : null;
                    if (IsSamePath(cwd, repo) || IsSamePath(foreground, repo))
                        return workspaceID;
                }
            }
            catch
            {
                // Ignore malformed output and let the caller create a new workspace.
            }

            return null;
        }

        private static bool IsSamePath(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return false;

            try
            {
                var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                var a = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var b = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(a, b, comparison);
            }
            catch
            {
                return false;
            }
        }

        private static string Run(string execFile, string workdir, params string[] args)
        {
            try
            {
                var startInfo = new ProcessStartInfo()
                {
                    FileName = execFile,
                    WorkingDirectory = workdir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                foreach (var arg in args)
                    startInfo.ArgumentList.Add(arg);

                using var process = Process.Start(startInfo);
                if (process == null)
                    return null;

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);
                return process.HasExited && process.ExitCode == 0 ? output : null;
            }
            catch
            {
                return null;
            }
        }
    }
}

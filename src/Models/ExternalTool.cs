using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SourceFork.Models
{
    public class ExternalTool
    {
        public class LaunchOption
        {
            public string Title { get; set; }
            public string Args { get; set; }

            public LaunchOption(string title, string args)
            {
                Title = title;
                Args = args;
            }
        }

        public string Name { get; }
        public string ExecFile { get; }
        public Bitmap IconImage { get; }
        public bool SupportOpenFolder { get; }
        public bool IsEnabled { get; set; } = true;
        public bool IsCustom { get; set; } = false;

        public ExternalTool(string name, string icon, string execFile, Func<string, List<LaunchOption>> optionsGenerator, bool supportOpenFolder)
        {
            Name = name;
            ExecFile = execFile;
            SupportOpenFolder = supportOpenFolder;

            _optionsGenerator = optionsGenerator;

            try
            {
                var asset = AssetLoader.Open(new Uri($"avares://SourceFork/Resources/Images/ExternalToolIcons/{icon}.png",
                    UriKind.RelativeOrAbsolute));
                IconImage = new Bitmap(asset);
            }
            catch
            {
                // ignore
            }
        }

        public List<LaunchOption> MakeLaunchOptions(string repo)
        {
            return _optionsGenerator?.Invoke(repo);
        }

        public void Launch(string args)
        {
            if (File.Exists(ExecFile))
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = ExecFile,
                    Arguments = args,
                    UseShellExecute = false,
                });
            }
        }

        private Func<string, List<LaunchOption>> _optionsGenerator = null;
    }

    public class VisualStudioInstance
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("productPath")]
        public string ProductPath { get; set; } = string.Empty;

        [JsonPropertyName("isPrerelease")]
        public bool IsPrerelease { get; set; } = false;
    }

    public class JetBrainsState
    {
        [JsonPropertyName("tools")]
        public List<JetBrainsTool> Tools { get; set; } = new List<JetBrainsTool>();
    }

    public class JetBrainsTool
    {
        [JsonPropertyName("productCode")]
        public string ProductCode { get; set; }
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }
        [JsonPropertyName("displayVersion")]
        public string DisplayVersion { get; set; }
        [JsonPropertyName("installLocation")]
        public string InstallLocation { get; set; }
        [JsonPropertyName("launchCommand")]
        public string LaunchCommand { get; set; }
    }

    public class ExternalToolCustomization
    {
        [JsonPropertyName("tools")]
        public Dictionary<string, string> Tools { get; set; } = new Dictionary<string, string>();
        [JsonPropertyName("excludes")]
        public List<string> Excludes { get; set; } = new List<string>();
    }

    public class ExternalToolsFinder
    {
        public List<ExternalTool> Tools
        {
            get;
            private set;
        } = new List<ExternalTool>();

        public ExternalToolsFinder()
        {
            _customization = LoadCustomization();
        }

        public static string CustomizationFile => Path.Combine(Native.OS.BasicDirectories.ConfigDir, "external_editors.json");

        public static ExternalToolCustomization LoadCustomization()
        {
            try
            {
                if (File.Exists(CustomizationFile))
                {
                    using var stream = File.OpenRead(CustomizationFile);
                    return JsonSerializer.Deserialize(stream, JsonCodeGen.Default.ExternalToolCustomization) ??
                           new ExternalToolCustomization();
                }
            }
            catch
            {
                // Ignore
            }

            return new ExternalToolCustomization();
        }

        public static void SaveCustomization(ExternalToolCustomization customization)
        {
            using var stream = File.Create(CustomizationFile);
            JsonSerializer.Serialize(stream, customization, JsonCodeGen.Default.ExternalToolCustomization);
        }

        public void TryAdd(string name, string icon, Func<string> finder, Func<string, List<ExternalTool.LaunchOption>> optionsGenerator = null, bool supportOpenFolder = true)
        {
            string path = null;
            if (_customization.Tools.TryGetValue(name, out var customPath) && File.Exists(customPath))
                path = customPath;
            else
                path = finder();

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var tool = new ExternalTool(name, icon, path, optionsGenerator, supportOpenFolder);
                tool.IsEnabled = !_customization.Excludes.Contains(name);
                Tools.Add(tool);
            }
        }

        public void AddCustomTools()
        {
            foreach (var kv in _customization.Tools)
            {
                if (Tools.Any(x => x.Name.Equals(kv.Key, StringComparison.Ordinal)))
                    continue;

                if (!File.Exists(kv.Value))
                    continue;

                var tool = new ExternalTool(kv.Key, "custom", kv.Value, null, true)
                {
                    IsCustom = true,
                    IsEnabled = !_customization.Excludes.Contains(kv.Key),
                };
                Tools.Add(tool);
            }
        }

        public void VSCode(Func<string> platformFinder)
        {
            TryAdd("Visual Studio Code", "vscode", platformFinder, GenerateVSCodeLaunchOptions);
        }

        public void VSCodeInsiders(Func<string> platformFinder)
        {
            TryAdd("Visual Studio Code - Insiders", "vscode_insiders", platformFinder, GenerateVSCodeLaunchOptions);
        }

        public void VSCodium(Func<string> platformFinder)
        {
            TryAdd("VSCodium", "codium", platformFinder, GenerateVSCodeLaunchOptions);
        }

        public void SublimeText(Func<string> platformFinder)
        {
            TryAdd("Sublime Text", "sublime_text", platformFinder);
        }

        public void Zed(Func<string> platformFinder)
        {
            TryAdd("Zed", "zed", platformFinder);
        }

        public void Cursor(Func<string> platformFinder)
        {
            TryAdd("Cursor", "cursor", platformFinder);
        }

        public void FindJetBrainsFromToolbox(Func<string> platformFinder)
        {
            var supported = new List<string> { "AI", "CL", "DB", "DL", "DS", "GO", "IC", "IU", "JB", "PC", "PS", "PY", "QA", "QD", "RD", "RM", "RR", "WRS", "WS" };
            var state = Path.Combine(platformFinder(), "state.json");
            if (File.Exists(state))
            {
                try
                {
                    using var stream = File.OpenRead(state);
                    var stateData = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.JetBrainsState);
                    foreach (var tool in stateData.Tools)
                    {
                        if (!supported.Contains(tool.ProductCode))
                            continue;

                        Tools.Add(new ExternalTool(
                            $"{tool.DisplayName} {tool.DisplayVersion}",
                            $"JetBrains/{tool.ProductCode}",
                            Path.Combine(tool.InstallLocation, tool.LaunchCommand),
                            null,
                            true));
                    }
                }
                catch
                {
                    // Ignore exceptions.
                }
            }
        }

        private List<ExternalTool.LaunchOption> GenerateVSCodeLaunchOptions(string path)
        {
            var root = new DirectoryInfo(path);
            if (!root.Exists)
                return null;

            var options = new List<ExternalTool.LaunchOption>();
            var prefixLen = root.FullName.Length;
            root.WalkFiles(f =>
            {
                if (f.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase))
                {
                    var display = f.Substring(prefixLen).TrimStart(Path.DirectorySeparatorChar);
                    options.Add(new(display, f.Quoted()));
                }
            }, 2);
            return options;
        }

        private ExternalToolCustomization _customization = null;
    }
}

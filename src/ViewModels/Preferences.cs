using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class Preferences : ObservableObject
    {
        [JsonIgnore]
        public static Preferences Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = Load();
                _instance._isLoading = false;

                _instance.PrepareGit();
                _instance.PrepareShellOrTerminal();
                _instance.PrepareExternalTools();
                _instance.PrepareExternalDiffMergeTool();
                _instance.PrepareRepositoryNodes();
                _instance.PrepareOpenRepositories();
                _instance.PrepareScanFolders();
                _instance.PrepareHiddenRepositories();
                _instance.PrepareScannedRepositories();

                return _instance;
            }
        }

        public string Locale
        {
            get => _locale;
            set
            {
                if (SetProperty(ref _locale, value) && !_isLoading)
                    App.SetLocale(value);
            }
        }

        public string Theme
        {
            get => _theme;
            set
            {
                if (SetProperty(ref _theme, value) && !_isLoading)
                    App.SetTheme(_theme);
            }
        }

        public string DefaultFontFamily
        {
            get => _defaultFontFamily;
            set
            {
                if (SetProperty(ref _defaultFontFamily, value) && !_isLoading)
                    App.SetFonts(value, _monospaceFontFamily);
            }
        }

        public string MonospaceFontFamily
        {
            get => _monospaceFontFamily;
            set
            {
                if (SetProperty(ref _monospaceFontFamily, value) && !_isLoading)
                    App.SetFonts(_defaultFontFamily, value);
            }
        }

        public bool UseSystemWindowFrame
        {
            get => Native.OS.UseSystemWindowFrame;
            set => Native.OS.UseSystemWindowFrame = value;
        }

        public double DefaultFontSize
        {
            get => _defaultFontSize;
            set => SetProperty(ref _defaultFontSize, value);
        }

        public double EditorFontSize
        {
            get => _editorFontSize;
            set => SetProperty(ref _editorFontSize, value);
        }

        public int EditorTabWidth
        {
            get => _editorTabWidth;
            set => SetProperty(ref _editorTabWidth, value);
        }

        public double Zoom
        {
            get => _zoom;
            set => SetProperty(ref _zoom, value);
        }

        public LayoutInfo Layout
        {
            get => _layout;
            set => SetProperty(ref _layout, value);
        }

        public bool ShowLocalChangesByDefault
        {
            get;
            set;
        } = false;

        public bool ShowChangesInCommitDetailByDefault
        {
            get;
            set;
        } = false;

        public int MaxHistoryCommits
        {
            get => _maxHistoryCommits;
            set => SetProperty(ref _maxHistoryCommits, value);
        }

        public int SubjectGuideLength
        {
            get => _subjectGuideLength;
            set => SetProperty(ref _subjectGuideLength, value);
        }

        public int DateTimeFormat
        {
            get => Models.DateTimeFormat.ActiveIndex;
            set
            {
                if (value != Models.DateTimeFormat.ActiveIndex &&
                    value >= 0 &&
                    value < Models.DateTimeFormat.Supported.Count)
                {
                    Models.DateTimeFormat.ActiveIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool Use24Hours
        {
            get => Models.DateTimeFormat.Use24Hours;
            set
            {
                if (value != Models.DateTimeFormat.Use24Hours)
                {
                    Models.DateTimeFormat.Use24Hours = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseFixedTabWidth
        {
            get => _useFixedTabWidth;
            set => SetProperty(ref _useFixedTabWidth, value);
        }

        public bool UseAutoHideScrollBars
        {
            get => _useAutoHideScrollBars;
            set => SetProperty(ref _useAutoHideScrollBars, value);
        }

        public bool UseGitHubStyleAvatar
        {
            get => _useGitHubStyleAvatar;
            set => SetProperty(ref _useGitHubStyleAvatar, value);
        }

        public bool Check4UpdatesOnStartup
        {
            get => _check4UpdatesOnStartup;
            set => SetProperty(ref _check4UpdatesOnStartup, value);
        }

        public string IgnoreUpdateTag
        {
            get => _ignoreUpdateTag;
            set => SetProperty(ref _ignoreUpdateTag, value);
        }

        public bool ShowTagsInGraph
        {
            get => _showTagsInGraph;
            set => SetProperty(ref _showTagsInGraph, value);
        }

        public bool UseCompactBranchNamesInGraph
        {
            get => _useCompactBranchNamesInGraph;
            set => SetProperty(ref _useCompactBranchNamesInGraph, value);
        }

        public bool UseTwoColumnsLayoutInHistories
        {
            get => _useTwoColumnsLayoutInHistories;
            set => SetProperty(ref _useTwoColumnsLayoutInHistories, value);
        }

        public bool DisplayTimeAsPeriodInHistories
        {
            get => _displayTimeAsPeriodInHistories;
            set => SetProperty(ref _displayTimeAsPeriodInHistories, value);
        }

        public bool UseSideBySideDiff
        {
            get => _useSideBySideDiff;
            set => SetProperty(ref _useSideBySideDiff, value);
        }

        public bool UseSyntaxHighlighting
        {
            get => _useSyntaxHighlighting;
            set => SetProperty(ref _useSyntaxHighlighting, value);
        }

        public bool IgnoreCRAtEOLInDiff
        {
            get => _ignoreCRAtEOLInDiff;
            set => SetProperty(ref _ignoreCRAtEOLInDiff, value);
        }

        public bool UseStashAndReapplyByDefault
        {
            get;
            set;
        } = false;

        public bool EnableAutoFetch
        {
            get;
            set;
        } = false;

        public int AutoFetchInterval
        {
            get;
            set;
        } = 10;

        public bool IgnoreWhitespaceChangesInDiff
        {
            get => _ignoreWhitespaceChangesInDiff;
            set => SetProperty(ref _ignoreWhitespaceChangesInDiff, value);
        }

        public bool EnableDiffViewWordWrap
        {
            get => _enableDiffViewWordWrap;
            set => SetProperty(ref _enableDiffViewWordWrap, value);
        }

        public bool ShowHiddenSymbolsInDiffView
        {
            get => _showHiddenSymbolsInDiffView;
            set => SetProperty(ref _showHiddenSymbolsInDiffView, value);
        }

        public bool UseFullTextDiff
        {
            get => _useFullTextDiff;
            set => SetProperty(ref _useFullTextDiff, value);
        }

        public int LFSImageActiveIdx
        {
            get => _lfsImageActiveIdx;
            set => SetProperty(ref _lfsImageActiveIdx, value);
        }

        public int ImageDiffActiveIdx
        {
            get => _imageDiffActiveIdx;
            set => SetProperty(ref _imageDiffActiveIdx, value);
        }

        public bool EnableCompactFoldersInChangesTree
        {
            get => _enableCompactFoldersInChangesTree;
            set => SetProperty(ref _enableCompactFoldersInChangesTree, value);
        }

        public Models.ChangeViewMode UnstagedChangeViewMode
        {
            get => _unstagedChangeViewMode;
            set => SetProperty(ref _unstagedChangeViewMode, value);
        }

        public Models.ChangeViewMode StagedChangeViewMode
        {
            get => _stagedChangeViewMode;
            set => SetProperty(ref _stagedChangeViewMode, value);
        }

        public Models.ChangeViewMode CommitChangeViewMode
        {
            get => _commitChangeViewMode;
            set => SetProperty(ref _commitChangeViewMode, value);
        }

        public Models.ChangeViewMode StashChangeViewMode
        {
            get => _stashChangeViewMode;
            set => SetProperty(ref _stashChangeViewMode, value);
        }

        public string GitInstallPath
        {
            get => Native.OS.GitExecutable;
            set
            {
                if (Native.OS.GitExecutable != value)
                {
                    Native.OS.GitExecutable = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GitDefaultCloneDir
        {
            get => _gitDefaultCloneDir;
            set => SetProperty(ref _gitDefaultCloneDir, value);
        }

        public bool UseLibsecretInsteadOfGCM
        {
            get => Native.OS.CredentialHelper.Equals("libsecret", StringComparison.Ordinal);
            set
            {
                var helper = value ? "libsecret" : "manager";
                if (OperatingSystem.IsLinux() && !Native.OS.CredentialHelper.Equals(helper, StringComparison.Ordinal))
                {
                    Native.OS.CredentialHelper = helper;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public List<Models.ExternalTool> ExternalTools
        {
            get => _externalTools;
            private set => SetProperty(ref _externalTools, value);
        }

        [JsonIgnore]
        public string NewExternalToolName
        {
            get => _newExternalToolName;
            set => SetProperty(ref _newExternalToolName, value);
        }

        [JsonIgnore]
        public string NewExternalToolPath
        {
            get => _newExternalToolPath;
            set => SetProperty(ref _newExternalToolPath, value);
        }

        [JsonIgnore]
        public List<Models.ShellOrTerminal> AvailableShellOrTerminals
        {
            get => _availableShellOrTerminals;
            private set => SetProperty(ref _availableShellOrTerminals, value);
        }

        [JsonIgnore]
        public Models.ShellOrTerminal SelectedShellOrTerminal
        {
            get => _selectedShellOrTerminal;
            set
            {
                if (SetProperty(ref _selectedShellOrTerminal, value) && !_isLoading && value != null)
                {
                    Native.OS.SetShellOrTerminal(value);
                    OnPropertyChanged(nameof(ShellOrTerminalPath));
                    OnPropertyChanged(nameof(ShellOrTerminalArgs));
                }
            }
        }

        public string ShellOrTerminalPath
        {
            get => Native.OS.ShellOrTerminal;
            set
            {
                if (value != Native.OS.ShellOrTerminal)
                {
                    Native.OS.ShellOrTerminal = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ShellOrTerminalArgs
        {
            get => Native.OS.ShellOrTerminalArgs;
            set
            {
                if (value != Native.OS.ShellOrTerminalArgs)
                {
                    Native.OS.ShellOrTerminalArgs = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ExternalMergeToolType
        {
            get => Native.OS.ExternalMergerType;
            set
            {
                if (Native.OS.ExternalMergerType != value)
                {
                    Native.OS.ExternalMergerType = value;
                    OnPropertyChanged();

                    if (!_isLoading)
                    {
                        Native.OS.AutoSelectExternalMergeToolExecFile();
                        OnPropertyChanged(nameof(ExternalMergeToolPath));
                        OnPropertyChanged(nameof(ExternalMergeToolDiffArgs));
                        OnPropertyChanged(nameof(ExternalMergeToolMergeArgs));
                    }
                }
            }
        }

        public string ExternalMergeToolPath
        {
            get => Native.OS.ExternalMergerExecFile;
            set
            {
                if (!Native.OS.ExternalMergerExecFile.Equals(value, StringComparison.Ordinal))
                {
                    Native.OS.ExternalMergerExecFile = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ExternalMergeToolDiffArgs
        {
            get => Native.OS.ExternalDiffArgs;
            set
            {
                if (!Native.OS.ExternalDiffArgs.Equals(value, StringComparison.Ordinal))
                {
                    Native.OS.ExternalDiffArgs = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ExternalMergeToolMergeArgs
        {
            get => Native.OS.ExternalMergeArgs;
            set
            {
                if (!Native.OS.ExternalMergeArgs.Equals(value, StringComparison.Ordinal))
                {
                    Native.OS.ExternalMergeArgs = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<RepositoryNode> RepositoryNodes
        {
            get;
            set;
        } = [];

        public List<string> OpenRepositories
        {
            get;
            set;
        } = [];

        public List<Models.ScanFolder> ScanFolders
        {
            get;
            set;
        } = [];

        public List<string> HiddenRepositories
        {
            get;
            set;
        } = [];

        public List<Models.ScannedRepository> ScannedRepositories
        {
            get;
            set;
        } = [];

        public string ActiveRepositoryPath
        {
            get;
            set;
        } = string.Empty;

        public Models.RepositorySortMode RepositorySort
        {
            get;
            set;
        } = Models.RepositorySortMode.Custom;

        public bool RepositorySortDescending
        {
            get;
            set;
        } = false;

        public bool RepositoryGroupByPrimaryTag
        {
            get;
            set;
        } = false;

        public Models.RepositoryGroupSortMode RepositoryGroupSort
        {
            get;
            set;
        } = Models.RepositoryGroupSortMode.Name;

        public bool RepositoryGroupSortDescending
        {
            get;
            set;
        } = false;

        public List<string> CollapsedRepositoryGroups
        {
            get;
            set;
        } = [];

        public AvaloniaList<Models.CustomAction> CustomActions
        {
            get;
            set;
        } = [];

        public AvaloniaList<AI.Service> OpenAIServices
        {
            get;
            set;
        } = [];

        public double LastCheckUpdateTime
        {
            get => _lastCheckUpdateTime;
            set => SetProperty(ref _lastCheckUpdateTime, value);
        }

        public void SetCanModify()
        {
            _isReadonly = false;
        }

        public bool IsGitConfigured()
        {
            var path = GitInstallPath;
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public bool ShouldCheck4UpdateOnStartup()
        {
            if (!_check4UpdatesOnStartup)
                return false;

            var lastCheck = DateTime.UnixEpoch.AddSeconds(LastCheckUpdateTime).ToLocalTime();
            var now = DateTime.Now;

            if (lastCheck.Year == now.Year && lastCheck.Month == now.Month && lastCheck.Day == now.Day)
                return false;

            LastCheckUpdateTime = now.Subtract(DateTime.UnixEpoch.ToLocalTime()).TotalSeconds;
            return true;
        }

        public void AddNode(RepositoryNode node, bool save)
        {
            if (!RepositoryNodes.Contains(node))
                RepositoryNodes.Add(node);

            SortNodes(RepositoryNodes);

            if (save)
                Save();
        }

        public void SortNodes(List<RepositoryNode> collection)
        {
            collection?.Sort((l, r) => Models.NumericSort.Compare(l.Name, r.Name));
        }

        public RepositoryNode FindNode(string id)
        {
            return RepositoryNodes.Find(x => x.Id == id);
        }

        public RepositoryNode FindOrAddNodeByRepositoryPath(string repo, bool save = true)
        {
            var normalized = repo.Replace('\\', '/').TrimEnd('/');

            var node = FindNode(normalized);
            if (node == null)
            {
                node = new RepositoryNode()
                {
                    Id = normalized,
                    Name = Path.GetFileName(normalized),
                    Bookmark = 0,
                    IsRepository = true,
                    CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                };

                AddNode(node, save);
            }

            if (HiddenRepositories.RemoveAll(x => IsSamePath(x, normalized)) > 0 && save)
                Save();

            return node;
        }

        public void RemoveNode(RepositoryNode node, bool save)
        {
            RepositoryNodes.Remove(node);
            OpenRepositories.Remove(node.Id);

            if (save)
                Save();
        }

        public void SortByRenamedNode(RepositoryNode node)
        {
            SortNodes(RepositoryNodes);
            Save();
        }

        public void AutoRemoveInvalidNode()
        {
            for (var i = RepositoryNodes.Count - 1; i >= 0; i--)
            {
                var node = RepositoryNodes[i];
                if (node.IsInvalid)
                {
                    RepositoryNodes.RemoveAt(i);
                    OpenRepositories.Remove(node.Id);
                }
            }

            for (var i = OpenRepositories.Count - 1; i >= 0; i--)
            {
                if (FindNode(OpenRepositories[i]) == null)
                    OpenRepositories.RemoveAt(i);
            }
        }

        public void SetOpenRepositories(IEnumerable<string> repositories)
        {
            OpenRepositories.Clear();
            foreach (var repo in repositories)
            {
                if (!OpenRepositories.Contains(repo))
                    OpenRepositories.Add(repo);
            }
        }

        public void MoveRepositoryNodeBefore(string sourceId, string targetId)
        {
            var source = FindNode(sourceId);
            var target = FindNode(targetId);
            if (source == null || target == null || source == target)
                return;

            RepositoryNodes.Remove(source);
            var index = RepositoryNodes.IndexOf(target);
            if (index < 0)
                index = RepositoryNodes.Count;

            RepositoryNodes.Insert(index, source);
            Save();
        }

        public void MoveRepositoryNodeToEnd(string sourceId)
        {
            var source = FindNode(sourceId);
            if (source == null)
                return;

            RepositoryNodes.Remove(source);
            RepositoryNodes.Add(source);
            Save();
        }

        public void MoveRepositoryNodeAfter(string sourceId, string targetId)
        {
            var source = FindNode(sourceId);
            var target = FindNode(targetId);
            if (source == null || target == null || source == target)
                return;

            RepositoryNodes.Remove(source);
            var index = RepositoryNodes.IndexOf(target);
            RepositoryNodes.Insert(index < 0 ? RepositoryNodes.Count : index + 1, source);
            Save();
        }

        public bool IsRepositoryGroupCollapsed(string key)
        {
            var normalized = key ?? string.Empty;
            return CollapsedRepositoryGroups.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public void ToggleRepositoryGroupCollapsed(string key)
        {
            var normalized = key ?? string.Empty;
            var index = CollapsedRepositoryGroups.FindIndex(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                CollapsedRepositoryGroups.RemoveAt(index);
            else
                CollapsedRepositoryGroups.Add(normalized);
        }

        public void Save()
        {
            if (_isLoading || _isReadonly)
                return;

            var tmpfile = Path.Combine(Native.OS.BasicDirectories.ConfigDir, "preference_tmp.json");
            var content = JsonSerializer.Serialize(this, JsonCodeGen.Default.Preferences);
            File.WriteAllText(tmpfile, content);

            var finalFile = Path.Combine(Native.OS.BasicDirectories.ConfigDir, "preference.json");
            File.Move(tmpfile, finalFile, true);
        }

        private static Preferences Load()
        {
            var path = Path.Combine(Native.OS.BasicDirectories.ConfigDir, "preference.json");
            if (!File.Exists(path))
                return new Preferences();

            try
            {
                var json = File.ReadAllText(path);
                var pref = JsonSerializer.Deserialize(json, JsonCodeGen.Default.Preferences) ?? new Preferences();
                pref.ReadLegacyWorkspaces(json);
                return pref;
            }
            catch
            {
                return new Preferences();
            }
        }

        private void PrepareGit()
        {
            var path = Native.OS.GitExecutable;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                GitInstallPath = Native.OS.FindGitExecutable();
        }

        private void PrepareShellOrTerminal()
        {
            RescanShellOrTerminals();

            var saved = Native.OS.ShellOrTerminal;
            if (!string.IsNullOrEmpty(saved))
            {
                var matched = AvailableShellOrTerminals.FirstOrDefault(x =>
                    !x.Type.Equals("custom", StringComparison.Ordinal) &&
                    string.Equals(Native.OS.GetShellOrTerminalPath(x), saved, StringComparison.Ordinal));

                _selectedShellOrTerminal = matched ??
                                           AvailableShellOrTerminals.FirstOrDefault(x => x.Type.Equals("custom", StringComparison.Ordinal));
                OnPropertyChanged(nameof(SelectedShellOrTerminal));
                return;
            }

            var preferred = AvailableShellOrTerminals.FirstOrDefault(x => x.Type == "wt") ??
                            AvailableShellOrTerminals.FirstOrDefault(x => x.Type != "custom") ??
                            AvailableShellOrTerminals.FirstOrDefault();
            if (preferred != null)
                SelectedShellOrTerminal = preferred;
        }

        public void RescanExternalTools()
        {
            Native.OS.SetupExternalTools();
            RefreshExternalTools();
        }

        public void RefreshExternalTools()
        {
            ExternalTools = Native.OS.AllExternalTools.ToList();
        }

        public void SetExternalToolEnabled(Models.ExternalTool tool, bool enabled)
        {
            tool.IsEnabled = enabled;

            var customization = Models.ExternalToolsFinder.LoadCustomization();
            customization.Excludes.RemoveAll(x => x.Equals(tool.Name, StringComparison.Ordinal));
            if (!enabled)
                customization.Excludes.Add(tool.Name);

            Models.ExternalToolsFinder.SaveCustomization(customization);
            Native.OS.ExternalTools = Native.OS.AllExternalTools.Where(x => x.IsEnabled).ToList();
        }

        public bool AddExternalTool()
        {
            var name = NewExternalToolName?.Trim();
            var path = NewExternalToolPath?.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (ExternalTools.Any(x => x.Name.Equals(name, StringComparison.Ordinal)))
                return false;

            var customization = Models.ExternalToolsFinder.LoadCustomization();
            customization.Tools[name] = path;
            customization.Excludes.RemoveAll(x => x.Equals(name, StringComparison.Ordinal));
            Models.ExternalToolsFinder.SaveCustomization(customization);

            NewExternalToolName = string.Empty;
            NewExternalToolPath = string.Empty;
            RescanExternalTools();
            return true;
        }

        public void RemoveExternalTool(Models.ExternalTool tool)
        {
            if (tool == null || !tool.IsCustom)
                return;

            var customization = Models.ExternalToolsFinder.LoadCustomization();
            customization.Tools.Remove(tool.Name);
            customization.Excludes.RemoveAll(x => x.Equals(tool.Name, StringComparison.Ordinal));
            Models.ExternalToolsFinder.SaveCustomization(customization);
            RescanExternalTools();
        }

        private void PrepareExternalTools()
        {
            ExternalTools = Native.OS.AllExternalTools.ToList();
        }

        public void RescanShellOrTerminals()
        {
            var available = new List<Models.ShellOrTerminal>();
            foreach (var terminal in Models.ShellOrTerminal.Supported)
            {
                if (terminal.Type.Equals("custom", StringComparison.Ordinal) ||
                    Native.OS.TestShellOrTerminal(terminal))
                    available.Add(terminal);
            }

            var selected = _selectedShellOrTerminal;
            AvailableShellOrTerminals = available;

            if (selected != null && available.Contains(selected))
            {
                SelectedShellOrTerminal = selected;
            }
            else if (selected != null)
            {
                _selectedShellOrTerminal = available.FirstOrDefault(x => x.Type.Equals("custom", StringComparison.Ordinal));
                OnPropertyChanged(nameof(SelectedShellOrTerminal));
            }
        }

        private void PrepareExternalDiffMergeTool()
        {
            var mergerType = Native.OS.ExternalMergerType;
            if (mergerType > 0 && mergerType < Models.ExternalMerger.Supported.Count)
            {
                var merger = Models.ExternalMerger.Supported[mergerType];
                if (string.IsNullOrEmpty(Native.OS.ExternalDiffArgs))
                    Native.OS.ExternalDiffArgs = merger.DiffCmd;
                if (string.IsNullOrEmpty(Native.OS.ExternalMergeArgs))
                    Native.OS.ExternalMergeArgs = merger.MergeCmd;
            }
        }

        private void PrepareRepositoryNodes()
        {
            var flattened = new List<RepositoryNode>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Collect(List<RepositoryNode> nodes)
            {
                if (nodes == null)
                    return;

                foreach (var node in nodes)
                {
                    if (node.IsRepository)
                    {
                        if (!string.IsNullOrEmpty(node.Id) && seen.Add(node.Id))
                        {
                            node.Tags ??= [];
                            node.SubNodes = null;
                            flattened.Add(node);
                        }
                    }
                    else
                    {
                        Collect(node.SubNodes);
                    }
                }
            }

            Collect(RepositoryNodes);
            RepositoryNodes = flattened;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (var i = 0; i < RepositoryNodes.Count; i++)
            {
                if (RepositoryNodes[i].CreateTime <= 0)
                    RepositoryNodes[i].CreateTime = now - RepositoryNodes.Count + i;
            }

            foreach (var path in _legacyRepositories)
            {
                if (seen.Add(path))
                {
                    RepositoryNodes.Add(new RepositoryNode()
                    {
                        Id = path,
                        Name = Path.GetFileName(path),
                        IsRepository = true,
                        CreateTime = now - RepositoryNodes.Count,
                    });
                }
            }

            SortNodes(RepositoryNodes);
        }

        private void PrepareOpenRepositories()
        {
            if (OpenRepositories.Count == 0)
            {
                foreach (var repo in _legacyOpenRepositories)
                {
                    if (FindNode(repo) != null)
                        OpenRepositories.Add(repo);
                }
            }

            if (string.IsNullOrEmpty(ActiveRepositoryPath))
                ActiveRepositoryPath = _legacyActiveRepository;

            for (var i = OpenRepositories.Count - 1; i >= 0; i--)
            {
                if (FindNode(OpenRepositories[i]) == null)
                    OpenRepositories.RemoveAt(i);
            }

            if (!string.IsNullOrEmpty(ActiveRepositoryPath) && FindNode(ActiveRepositoryPath) == null)
                ActiveRepositoryPath = string.Empty;
        }

        private void PrepareScanFolders()
        {
            ScanFolders ??= [];

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = ScanFolders.Count - 1; i >= 0; i--)
            {
                var folder = ScanFolders[i];
                if (folder == null || string.IsNullOrWhiteSpace(folder.Path))
                {
                    ScanFolders.RemoveAt(i);
                    continue;
                }

                folder.Path = RepositoryScanner.NormalizePath(folder.Path);

                var key = OperatingSystem.IsLinux() ? folder.Path : folder.Path.ToLowerInvariant();
                if (!seen.Add(key))
                {
                    ScanFolders.RemoveAt(i);
                    continue;
                }

                if (!Enum.IsDefined(folder.Mode))
                    folder.Mode = Models.ScanFolderMode.OnStartup;
            }
        }

        private void PrepareHiddenRepositories()
        {
            HiddenRepositories ??= [];

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = HiddenRepositories.Count - 1; i >= 0; i--)
            {
                var path = HiddenRepositories[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    HiddenRepositories.RemoveAt(i);
                    continue;
                }

                path = RepositoryScanner.NormalizePath(path);
                HiddenRepositories[i] = path;

                var key = OperatingSystem.IsLinux() ? path : path.ToLowerInvariant();
                if (!seen.Add(key) || RepositoryNodes.Any(x => IsSamePath(x.Id, path)))
                    HiddenRepositories.RemoveAt(i);
            }
        }

        private void PrepareScannedRepositories()
        {
            ScannedRepositories ??= [];

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = ScannedRepositories.Count - 1; i >= 0; i--)
            {
                var entry = ScannedRepositories[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Path))
                {
                    ScannedRepositories.RemoveAt(i);
                    continue;
                }

                entry.Path = RepositoryScanner.NormalizePath(entry.Path);
                entry.ScanFolder = RepositoryScanner.NormalizePath(entry.ScanFolder);

                var key = OperatingSystem.IsLinux() ? entry.Path : entry.Path.ToLowerInvariant();
                if (!seen.Add(key) ||
                    (!RepositoryNodes.Any(x => IsSamePath(x.Id, entry.Path)) && !IsRepositoryHidden(entry.Path)))
                    ScannedRepositories.RemoveAt(i);
            }
        }

        public bool HasScannedSource(string path)
        {
            return ScannedRepositories.Any(x => IsSamePath(x.Path, path));
        }

        public void RecordScannedRepository(string path, string scanFolder, bool save = true)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(scanFolder))
                return;

            var normalizedPath = RepositoryScanner.NormalizePath(path);
            var normalizedFolder = RepositoryScanner.NormalizePath(scanFolder);

            foreach (var entry in ScannedRepositories)
            {
                if (IsSamePath(entry.Path, normalizedPath))
                {
                    entry.ScanFolder = normalizedFolder;
                    if (save)
                        Save();
                    return;
                }
            }

            ScannedRepositories.Add(new Models.ScannedRepository()
            {
                Path = normalizedPath,
                ScanFolder = normalizedFolder,
            });

            if (save)
                Save();
        }

        public void RemoveScannedRepository(string path, bool save = true)
        {
            ScannedRepositories.RemoveAll(x => IsSamePath(x.Path, path));

            if (save)
                Save();
        }

        public bool IsRepositoryHidden(string path)
        {
            if (HiddenRepositories.Count == 0 || string.IsNullOrWhiteSpace(path))
                return false;

            foreach (var hidden in HiddenRepositories)
            {
                if (IsSamePath(hidden, path))
                    return true;
            }

            return false;
        }

        public void HideRepository(string path, bool save = true)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var normalized = RepositoryScanner.NormalizePath(path);
            if (!IsRepositoryHidden(normalized))
                HiddenRepositories.Add(normalized);

            if (save)
                Save();
        }

        public void ShowRepository(string path, bool save = true)
        {
            HiddenRepositories.RemoveAll(x => IsSamePath(x, path));

            if (save)
                Save();
        }

        public static bool IsSamePath(string left, string right)
        {
            var l = RepositoryScanner.NormalizePath(left);
            var r = RepositoryScanner.NormalizePath(right);
            return OperatingSystem.IsLinux()
                ? l.Equals(r, StringComparison.Ordinal)
                : l.Equals(r, StringComparison.OrdinalIgnoreCase);
        }

        private void ReadLegacyWorkspaces(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("Workspaces", out var workspaces) ||
                    workspaces.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var workspace in workspaces.EnumerateArray())
                {
                    if (!workspace.TryGetProperty("Repositories", out var repos) ||
                        repos.ValueKind != JsonValueKind.Array)
                        continue;

                    var isActive = workspace.TryGetProperty("IsActive", out var activeElement) &&
                        activeElement.ValueKind == JsonValueKind.True;
                    var activeIdx = 0;
                    if (workspace.TryGetProperty("ActiveIdx", out var idxElement))
                        idxElement.TryGetInt32(out activeIdx);

                    var index = 0;
                    foreach (var repo in repos.EnumerateArray())
                    {
                        if (repo.ValueKind != JsonValueKind.String)
                            continue;

                        var path = repo.GetString()?.Replace('\\', '/').TrimEnd('/');
                        if (string.IsNullOrEmpty(path))
                            continue;

                        if (!_legacyRepositories.Contains(path))
                            _legacyRepositories.Add(path);

                        if (isActive)
                        {
                            _legacyOpenRepositories.Add(path);
                            if (index == activeIdx)
                                _legacyActiveRepository = path;
                        }

                        index++;
                    }
                }
            }
            catch
            {
                // Ignore malformed legacy data.
            }
        }

        private static Preferences _instance = null;

        [JsonIgnore]
        private readonly List<string> _legacyRepositories = [];

        [JsonIgnore]
        private readonly List<string> _legacyOpenRepositories = [];

        [JsonIgnore]
        private string _legacyActiveRepository = string.Empty;

        private bool _isLoading = true;
        private bool _isReadonly = true;
        private string _locale = "en_US";
        private string _theme = "System";
        private string _defaultFontFamily = string.Empty;
        private string _monospaceFontFamily = string.Empty;
        private double _defaultFontSize = 13;
        private double _editorFontSize = 13;
        private int _editorTabWidth = 4;
        private double _zoom = 1.0;
        private LayoutInfo _layout = new();

        private int _maxHistoryCommits = 20000;
        private int _subjectGuideLength = 50;
        private bool _useFixedTabWidth = true;
        private bool _useAutoHideScrollBars = true;
        private bool _useGitHubStyleAvatar = true;
        private bool _useCompactBranchNamesInGraph = true;

        private bool _check4UpdatesOnStartup = true;
        private double _lastCheckUpdateTime = 0;
        private string _ignoreUpdateTag = string.Empty;

        private bool _showTagsInGraph = true;
        private bool _useTwoColumnsLayoutInHistories = false;
        private bool _displayTimeAsPeriodInHistories = false;
        private bool _useSideBySideDiff = false;
        private bool _ignoreWhitespaceChangesInDiff = false;
        private bool _ignoreCRAtEOLInDiff = true;
        private bool _useSyntaxHighlighting = false;
        private bool _enableDiffViewWordWrap = false;
        private bool _showHiddenSymbolsInDiffView = false;
        private bool _useFullTextDiff = false;
        private int _lfsImageActiveIdx = 0;
        private int _imageDiffActiveIdx = 0;
        private bool _enableCompactFoldersInChangesTree = false;

        private Models.ChangeViewMode _unstagedChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _stagedChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _commitChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _stashChangeViewMode = Models.ChangeViewMode.List;

        private string _gitDefaultCloneDir = string.Empty;
        private List<Models.ExternalTool> _externalTools = [];
        private string _newExternalToolName = string.Empty;
        private string _newExternalToolPath = string.Empty;
        private List<Models.ShellOrTerminal> _availableShellOrTerminals = [];
        private Models.ShellOrTerminal _selectedShellOrTerminal = null;
    }
}

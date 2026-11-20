using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceFork.Views
{
    public partial class WorkspaceManagement : UserControl
    {
        public WorkspaceManagement()
        {
            InitializeComponent();
        }

        private async void SelectDefaultCloneDir(object _, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.ConfigureWorkspace manager || manager.Selected == null)
                return;

            if (TopLevel.GetTopLevel(this) is not Window owner)
                return;

            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            try
            {
                var selected = await owner.StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var folder = selected[0];
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                    manager.Selected.DefaultCloneDir = folderPath;
                }
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(owner, $"Failed to select default clone directory: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private async void OnImportWorkspaces(object _, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.ConfigureWorkspace manager ||
                TopLevel.GetTopLevel(this) is not Window owner)
                return;

            var options = new FilePickerOpenOptions()
            {
                Title = App.Text("Workspace.Import"),
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
            };

            try
            {
                var files = await owner.StorageProvider.OpenFilePickerAsync(options);
                if (files.Count != 1)
                    return;

                await using var stream = await files[0].OpenReadAsync();
                var imported = await JsonSerializer.DeserializeAsync(stream, JsonCodeGen.Default.ListWorkspace);
                if (imported == null)
                    return;

                ViewModels.Workspace first = null;
                foreach (var workspace in imported)
                {
                    workspace.IsActive = false;
                    if (string.IsNullOrWhiteSpace(workspace.Name))
                        workspace.Name = "Imported";

                    var baseName = workspace.Name;
                    var suffix = 1;
                    while (manager.Workspaces.Any(x => x.Name == workspace.Name))
                        workspace.Name = $"{baseName} ({suffix++})";

                    ViewModels.Preferences.Instance.Workspaces.Add(workspace);
                    manager.Workspaces.Add(workspace);
                    first ??= workspace;
                }

                if (first != null)
                    manager.Selected = first;

                ViewModels.Preferences.Instance.Save();
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(owner, $"Failed to import workspaces: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private async void OnExportSelectedWorkspace(object _, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.ConfigureWorkspace manager ||
                manager.Selected == null ||
                TopLevel.GetTopLevel(this) is not Window owner)
                return;

            await ExportWorkspacesAsync(owner, [manager.Selected], $"{SafeFileName(manager.Selected.Name)}.json", "Workspace.ExportSelected");
            e.Handled = true;
        }

        private async void OnExportAllWorkspaces(object _, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.ConfigureWorkspace manager ||
                TopLevel.GetTopLevel(this) is not Window owner)
                return;

            await ExportWorkspacesAsync(owner, manager.Workspaces.ToList(), "workspaces.json", "Workspace.ExportAll");
            e.Handled = true;
        }

        private static async Task ExportWorkspacesAsync(Window owner, List<ViewModels.Workspace> workspaces, string suggestedName, string titleKey)
        {
            var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = App.Text(titleKey),
                SuggestedFileName = suggestedName,
                DefaultExtension = ".json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
                ShowOverwritePrompt = true,
            });

            if (file == null)
                return;

            await using var stream = await file.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(stream, workspaces, JsonCodeGen.Default.ListWorkspace);
        }

        private static string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "workspace";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars).Trim();
        }
    }
}

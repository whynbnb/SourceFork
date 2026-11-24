using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace SourceFork.ViewModels
{
    public class OpenLocalRepository : Popup
    {
        [Required(ErrorMessage = "Repository folder is required")]
        [CustomValidation(typeof(OpenLocalRepository), nameof(ValidateRepoPath))]
        public string RepoPath
        {
            get => _repoPath;
            set => SetProperty(ref _repoPath, value, true);
        }

        public int Bookmark
        {
            get => _bookmark;
            set => SetProperty(ref _bookmark, value);
        }

        public OpenLocalRepository(string pageId)
        {
            _pageId = pageId;
        }

        public static ValidationResult ValidateRepoPath(string folder, ValidationContext _)
        {
            if (!Directory.Exists(folder))
                return new ValidationResult("Given path can NOT be found");
            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            var isBare = await new Commands.IsBareRepository(_repoPath).GetResultAsync();
            var repoRoot = _repoPath;
            if (!isBare)
            {
                var test = await new Commands.QueryRepositoryRootPath(_repoPath).GetResultAsync();
                if (test.IsSuccess && !string.IsNullOrWhiteSpace(test.StdOut))
                {
                    repoRoot = test.StdOut.Trim();
                }
                else
                {
                    var launcher = App.GetLauncher();
                    if (Models.SafeDirectories.IsUntrustedRepository(test.StdErr) &&
                        Models.SafeDirectories.TryGetSafeDirectoryValue(_repoPath, test.StdErr, out var safeDirectory))
                    {
                        foreach (var page in launcher.Pages)
                        {
                            if (page.Node.Id.Equals(_pageId, StringComparison.Ordinal))
                            {
                                page.Popup = new TrustRepository(page.Node.Id, _repoPath, test.StdErr, safeDirectory, true, _bookmark);
                                break;
                            }
                        }
                    }
                    else
                    {
                        foreach (var page in launcher.Pages)
                        {
                            if (page.Node.Id.Equals(_pageId, StringComparison.Ordinal))
                            {
                                page.Popup = new Init(page.Node.Id, _repoPath, _bookmark, test.StdErr);
                                break;
                            }
                        }
                    }

                    return false;
                }
            }

            var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(repoRoot);
            node.Bookmark = _bookmark;
            await node.UpdateStatusAsync(false, null);
            RepositoryListSorter.NotifyChanged();
            node.Open();
            return true;
        }

        private string _pageId = string.Empty;
        private string _repoPath = string.Empty;
        private int _bookmark = 0;
    }
}

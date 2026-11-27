using System.Collections.Generic;

namespace SourceFork.Models
{
    public static class RepositoryToolbarZones
    {
        public const string Hidden = "Hidden";
        public const string Left = "Left";
        public const string Middle = "Middle";
        public const string Right = "Right";
        public const string LeftMore = "LeftMore";
        public const string MiddleMore = "MiddleMore";
        public const string RightMore = "RightMore";
    }

    public class RepositoryToolbarActionPreference
    {
        public string Id { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
    }

    public class RepositoryToolbarActionInfo
    {
        public string Id
        {
            get;
        }

        public string TooltipKey
        {
            get;
        }

        public string IconKey
        {
            get;
        }

        public string HintKey
        {
            get;
        }

        public string DefaultZone
        {
            get;
        }

        public bool IsToggle
        {
            get;
        }

        public RepositoryToolbarActionInfo(string id, string tooltipKey, string iconKey,
            string hintKey, string defaultZone, bool isToggle = false)
        {
            Id = id;
            TooltipKey = tooltipKey;
            IconKey = iconKey;
            HintKey = hintKey;
            DefaultZone = defaultZone;
            IsToggle = isToggle;
        }
    }

    public static class RepositoryToolbarCatalog
    {
        public static readonly IReadOnlyList<RepositoryToolbarActionInfo> Actions =
        [
            new("explore", "Repository.Explore", "Icons.Explore", null, RepositoryToolbarZones.Left),
            new("terminal", "Repository.Terminal", "Icons.Terminal", null, RepositoryToolbarZones.Left),

            new("fetch", "Fetch", "Icons.Fetch", "CtrlClickTip", RepositoryToolbarZones.Middle),
            new("pull", "Pull", "Icons.Pull", "CtrlClickTip", RepositoryToolbarZones.Middle),
            new("push", "Push", "Icons.Push", "CtrlClickTip", RepositoryToolbarZones.Middle),

            new("stash", "Stash", "Icons.Stashes.Add", "CtrlClickTip", RepositoryToolbarZones.MiddleMore),
            new("applyPatch", "Apply.Title", "Icons.ApplyPatch", null, RepositoryToolbarZones.MiddleMore),
            new("gitflow", "GitFlow", "Icons.GitFlow", null, RepositoryToolbarZones.MiddleMore),
            new("lfs", "GitLFS", "Icons.LFS", null, RepositoryToolbarZones.MiddleMore),
            new("bisect", "Bisect", "Icons.Bisect", null, RepositoryToolbarZones.MiddleMore),
            new("customAction", "Repository.CustomActions", "Icons.Action", null, RepositoryToolbarZones.MiddleMore),
            new("clean", "Repository.Clean", "Icons.Clean", null, RepositoryToolbarZones.MiddleMore),

            new("openWith", "Repository.OpenWithExternalTools", "Icons.OpenWith", null, RepositoryToolbarZones.Hidden),
            new("logs", "Repository.ViewLogs", "Icons.Logs", null, RepositoryToolbarZones.Hidden),
            new("statistics", "Repository.Statistics", "Icons.Statistics", null, RepositoryToolbarZones.Hidden),
            new("settings", "Repository.Configure", "Icons.Settings", null, RepositoryToolbarZones.Hidden),

            new("magic", "Repository.FocusOnCurrentBranch", "Icons.Magic", null, RepositoryToolbarZones.Hidden, true),
        ];

        public static RepositoryToolbarActionInfo Find(string id)
        {
            foreach (var action in Actions)
            {
                if (action.Id == id)
                    return action;
            }

            return null;
        }
    }
}

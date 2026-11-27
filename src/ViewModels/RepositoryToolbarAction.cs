using System.Collections.Generic;

using Avalonia;
using Avalonia.Media;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class RepositoryToolbarAction : ObservableObject
    {
        public Models.RepositoryToolbarActionInfo Info
        {
            get;
        }

        public string Id
        {
            get => Info.Id;
        }

        public string IconKey
        {
            get => Info.IconKey;
        }

        public string Tooltip
        {
            get;
        }

        public string Hint
        {
            get;
        }

        public bool HasHint
        {
            get => !string.IsNullOrEmpty(Hint);
        }

        public Geometry Icon
        {
            get;
        }

        public bool IsToggle
        {
            get => Info.IsToggle;
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public bool IsToggled
        {
            get => _isToggled;
            set => SetProperty(ref _isToggled, value);
        }

        public bool ShowBadge
        {
            get => _showBadge;
            set => SetProperty(ref _showBadge, value);
        }

        public RepositoryToolbarAction(Models.RepositoryToolbarActionInfo info)
        {
            Info = info;
            Tooltip = App.Text(info.TooltipKey);
            Hint = string.IsNullOrEmpty(info.HintKey) ? string.Empty : App.Text(info.HintKey);
            Icon = Application.Current != null &&
                   Application.Current.TryGetResource(info.IconKey, Application.Current.ActualThemeVariant, out var resource)
                ? resource as Geometry
                : null;
        }

        private bool _isVisible = true;
        private bool _isEnabled = true;
        private bool _isToggled;
        private bool _showBadge;
    }

    public class RepositoryToolbarSetting : ObservableObject
    {
        public string Id
        {
            get;
        }

        public string Name
        {
            get;
        }

        public Geometry Icon
        {
            get;
        }

        public IReadOnlyList<string> Zones
        {
            get;
        }

        public int ZoneIndex
        {
            get => _zoneIndex;
            set
            {
                if (!SetProperty(ref _zoneIndex, value))
                    return;

                if (value >= 0 && value < _zoneKeys.Length)
                    Preferences.Instance.SetRepositoryToolbarZone(Id, _zoneKeys[value]);
            }
        }

        public RepositoryToolbarSetting(Models.RepositoryToolbarActionInfo info)
        {
            Id = info.Id;
            Name = App.Text(info.TooltipKey);
            Icon = Application.Current != null &&
                   Application.Current.TryGetResource(info.IconKey, Application.Current.ActualThemeVariant, out var resource)
                ? resource as Geometry
                : null;

            Zones =
            [
                App.Text("RepositoryToolbar.Hidden"),
                App.Text("RepositoryToolbar.Left"),
                App.Text("RepositoryToolbar.Middle"),
                App.Text("RepositoryToolbar.Right"),
                App.Text("RepositoryToolbar.LeftMore"),
                App.Text("RepositoryToolbar.MiddleMore"),
                App.Text("RepositoryToolbar.RightMore"),
            ];

            var zone = Preferences.Instance.GetRepositoryToolbarZone(info.Id);
            for (var i = 0; i < _zoneKeys.Length; i++)
            {
                if (_zoneKeys[i] == zone)
                {
                    _zoneIndex = i;
                    break;
                }
            }
        }

        private static readonly string[] _zoneKeys =
        [
            Models.RepositoryToolbarZones.Hidden,
            Models.RepositoryToolbarZones.Left,
            Models.RepositoryToolbarZones.Middle,
            Models.RepositoryToolbarZones.Right,
            Models.RepositoryToolbarZones.LeftMore,
            Models.RepositoryToolbarZones.MiddleMore,
            Models.RepositoryToolbarZones.RightMore,
        ];

        private int _zoneIndex;
    }
}

using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia.Interactivity;

namespace SourceFork.Views
{
    public partial class About : ChromelessWindow
    {
        public About()
        {
            CloseOnESC = true;
            InitializeComponent();

            var assembly = Assembly.GetExecutingAssembly();
            var meta = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            var foundFriendlyVersion = false;
            foreach (var attr in meta)
            {
                if (attr.Key.Equals("BuildDate", StringComparison.OrdinalIgnoreCase) && DateTime.TryParse(attr.Value, out var date))
                {
                    TxtReleaseDate.Text = App.Text("About.ReleaseDate", Models.DateTimeFormat.Format(date, true));
                }
                else if (attr.Key.Equals("FriendlyVersion", StringComparison.OrdinalIgnoreCase) && REG_FRIENDLY_VERSION().IsMatch(attr.Value))
                {
                    foundFriendlyVersion = true;
                    TxtVersion.Text = attr.Value;
                }
            }

            if (!foundFriendlyVersion)
            {
                var ver = assembly.GetName().Version;
                if (ver != null)
                    TxtVersion.Text = $"v{ver.Major}.{ver.Minor}";
            }

            var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
            if (copyright != null)
                TxtCopyright.Text = copyright.Copyright;
        }

        private void OnVisitReleaseNotes(object _, RoutedEventArgs e)
        {
            var ver = TxtVersion.Text ?? string.Empty;
            var endOfTagIdx = ver.IndexOf('-');
            if (endOfTagIdx > 0)
                ver = ver.Substring(0, endOfTagIdx);

            // SourceFork release tags use the "sf" prefix (sf1.0), not "v".
            if (ver.StartsWith("sf", StringComparison.OrdinalIgnoreCase))
                ver = ver.Substring(2);
            else if (ver.StartsWith('v') || ver.StartsWith('V'))
                ver = ver.Substring(1);

            Native.OS.OpenBrowser($"https://github.com/whynbnb/SourceFork/releases/tag/sf{ver}");
            e.Handled = true;
        }

        private void OnVisitSourceCode(object _, RoutedEventArgs e)
        {
            Native.OS.OpenBrowser("https://github.com/whynbnb/SourceFork");
            e.Handled = true;
        }

        [GeneratedRegex(@"^v\d+\.\d+(?:\-[0-9A-Za-z.]+)?$")]
        private static partial Regex REG_FRIENDLY_VERSION();
    }
}

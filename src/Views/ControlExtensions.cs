using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SourceFork.Views
{
    public static class ControlExtensions
    {
        public static Control CreateFromViewModels(object data)
        {
            var dataTypeName = data.GetType().FullName;
            if (string.IsNullOrEmpty(dataTypeName) || !dataTypeName.Contains(".ViewModels.", StringComparison.Ordinal))
                return null;

            var viewTypeName = dataTypeName.Replace(".ViewModels.", ".Views.");
            var viewType = Type.GetType(viewTypeName);
            if (viewType != null)
                return Activator.CreateInstance(viewType) as Control;

            return null;
        }

        public static async Task CopyTextAsync(this Control control, string text)
        {
            var topLevel = TopLevel.GetTopLevel(control) as Window;
            if (topLevel == null)
                return;

            try
            {
                if (topLevel.Clipboard is { } clipboard)
                    await clipboard.SetTextAsync(text);
            }
            catch (Exception e)
            {
                await new Alert().ShowAsync(topLevel, "Failed to copy text to clipboard: " + e.Message, true);
            }
        }

        public static void ToggleGroupPreservingScroll(this ListBox list, string groupKey, Action toggle)
        {
            var scroll = list.FindDescendantOfType<ScrollViewer>();
            if (scroll == null)
            {
                toggle();
                return;
            }

            var offset = scroll.Offset;
            var beforeY = FindGroupHeader(list, groupKey)?.TranslatePoint(new Point(0, 0), scroll)?.Y;

            toggle();

            // Rebuilding the row list can change the layout of the virtualizing panel. Keep
            // the clicked group header at the same viewport position, falling back to the
            // previous scroll offset when its container is no longer realized.
            var corrected = false;
            void RestoreScrollOffset()
            {
                if (corrected)
                    return;

                var afterY = FindGroupHeader(list, groupKey)?.TranslatePoint(new Point(0, 0), scroll)?.Y;
                if (beforeY is { } y0 && afterY is { } y1)
                {
                    var delta = y1 - y0;
                    if (Math.Abs(delta) > 0.01)
                    {
                        corrected = true;
                        scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Offset.Y + delta));
                    }
                }
                else if (scroll.Offset != offset)
                {
                    scroll.Offset = offset;
                }
            }

            Dispatcher.UIThread.Post(RestoreScrollOffset, DispatcherPriority.Default);
            Dispatcher.UIThread.Post(RestoreScrollOffset, DispatcherPriority.Background);
        }

        private static Control FindGroupHeader(ListBox list, string groupKey)
        {
            for (var i = 0; i < list.ItemCount; i++)
            {
                if (list.Items[i] is ViewModels.RepositoryGroupRow group &&
                    string.Equals(group.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase))
                    return list.ContainerFromIndex(i);
            }

            return null;
        }

        public static Path CreateMenuIcon(this Control control, string iconKey)
        {
            if (control?.FindResource(iconKey) is StreamGeometry geo)
            {
                return new Path()
                {
                    Data = geo,
                    Width = 12,
                    Height = 12,
                    Stretch = Stretch.Uniform
                };
            }

            return null;
        }

        public static void ShowWindow(this Control control, object data)
        {
            if (data == null)
                return;

            if (data is not ChromelessWindow window)
            {
                window = CreateFromViewModels(data) as ChromelessWindow;
                if (window == null)
                    return;

                window.DataContext = data;
            }

            do
            {
                var owner = TopLevel.GetTopLevel(control) as Window;
                if (owner != null)
                {
                    // Get the screen where current window locates.
                    var screen = owner.Screens.ScreenFromWindow(owner) ?? owner.Screens.Primary;
                    if (screen == null)
                        break;

                    // Calculate the startup position (Center Screen Mode) of target window
                    var rect = new PixelRect(PixelSize.FromSize(window.ClientSize, owner.DesktopScaling));
                    var centeredRect = screen.WorkingArea.CenterRect(rect);
                    if (owner.Screens.ScreenFromPoint(centeredRect.Position) == null)
                        break;

                    // Use the startup position
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Position = centeredRect.Position;
                }
            } while (false);

            window.Show();
        }

        public static Task ShowDialogAsync(this Control control, object data)
        {
            var owner = TopLevel.GetTopLevel(control) as Window;
            if (owner == null)
                return null;

            if (data is ChromelessWindow window)
                return window.ShowDialog(owner);

            window = CreateFromViewModels(data) as ChromelessWindow;
            if (window != null)
            {
                window.DataContext = data;
                return window.ShowDialog(owner);
            }

            return null;
        }
    }
}

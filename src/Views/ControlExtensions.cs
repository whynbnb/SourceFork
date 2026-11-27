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

        public static void ToggleGroupPreservingScroll(this ListBox list, Action toggle)
        {
            var scroll = list.FindDescendantOfType<ScrollViewer>();
            if (scroll == null)
            {
                toggle();
                return;
            }

            var offset = scroll.Offset;
            toggle();

            // Inserting or removing rows makes the virtualizing panel re-anchor the scroll
            // offset, and the new extent can lag a frame behind. Restore the previous offset
            // until it sticks, otherwise expanding the bottom groups drifts the viewport and
            // shows up as a flicker.
            var attempts = 0;
            void Restore()
            {
                if (scroll.Offset == offset || attempts >= 4)
                    return;

                attempts++;
                scroll.Offset = offset;

                if (attempts < 4)
                    Dispatcher.UIThread.Post(Restore, DispatcherPriority.Background);
            }

            list.UpdateLayout();
            Restore();
            Dispatcher.UIThread.Post(Restore, DispatcherPriority.Render);
            Dispatcher.UIThread.Post(Restore, DispatcherPriority.Background);
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

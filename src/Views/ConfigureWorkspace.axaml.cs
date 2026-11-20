using Avalonia.Controls;

namespace SourceFork.Views
{
    public partial class ConfigureWorkspace : ChromelessWindow
    {
        public ConfigureWorkspace()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (!Design.IsDesignMode)
                ViewModels.Preferences.Instance.Save();
        }
    }
}

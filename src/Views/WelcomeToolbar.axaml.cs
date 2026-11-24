using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceFork.Views
{
    public partial class WelcomeToolbar : UserControl
    {
        public WelcomeToolbar()
        {
            InitializeComponent();
        }

        private async void OnCleanHistory(object _, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is Window owner)
                await new RepositoryHistory().ShowDialog(owner);

            e.Handled = true;
        }
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceFork.Views
{
    public partial class ManageRepositoryGroups : ChromelessWindow
    {
        public ManageRepositoryGroups()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        private void OnMoveUp(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ManageRepositoryGroups vm &&
                sender is Button { DataContext: ViewModels.RepositoryGroupSetting group })
                vm.MoveUp(group);

            e.Handled = true;
        }

        private void OnMoveDown(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ManageRepositoryGroups vm &&
                sender is Button { DataContext: ViewModels.RepositoryGroupSetting group })
                vm.MoveDown(group);

            e.Handled = true;
        }

        private void OnDone(object sender, RoutedEventArgs e)
        {
            Close();
            e.Handled = true;
        }
    }
}

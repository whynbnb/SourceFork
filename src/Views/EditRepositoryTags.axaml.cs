using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceFork.Views
{
    public partial class EditRepositoryTags : ChromelessWindow
    {
        public EditRepositoryTags()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            NewTagBox.Focus();
            NewTagBox.CaretIndex = NewTagBox.Text?.Length ?? 0;
        }

        private void OnAddTag(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.EditRepositoryTags vm)
            {
                vm.AddTag();
                NewTagBox.Focus();
                NewTagBox.CaretIndex = NewTagBox.Text?.Length ?? 0;
            }

            e.Handled = true;
        }

        private void OnSuggestionClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.EditRepositoryTags vm &&
                sender is Button { Tag: string tag })
            {
                vm.AcceptSuggestion(tag);
                NewTagBox.Focus();
                NewTagBox.CaretIndex = NewTagBox.Text?.Length ?? 0;
            }

            e.Handled = true;
        }

        private void OnNewTagKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ViewModels.EditRepositoryTags vm)
            {
                vm.AddTag();
                e.Handled = true;
            }
        }

        private void OnSetPrimary(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.EditRepositoryTags vm &&
                sender is Button { DataContext: ViewModels.RepositoryTagItem tag })
                vm.SetPrimary(tag);

            e.Handled = true;
        }

        private void OnMoveUp(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.EditRepositoryTags vm &&
                sender is Button { DataContext: ViewModels.RepositoryTagItem tag })
                vm.MoveUp(tag);

            e.Handled = true;
        }

        private void OnMoveDown(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.EditRepositoryTags vm &&
                sender is Button { DataContext: ViewModels.RepositoryTagItem tag })
                vm.MoveDown(tag);

            e.Handled = true;
        }

        private void OnRemoveTag(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.EditRepositoryTags vm &&
                sender is Button { DataContext: ViewModels.RepositoryTagItem tag })
                vm.RemoveTag(tag);

            e.Handled = true;
        }

        private void OnDone(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.EditRepositoryTags vm)
                vm.ApplyChanges();

            Close();
            e.Handled = true;
        }
    }
}

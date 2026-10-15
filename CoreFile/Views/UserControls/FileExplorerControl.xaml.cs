using CoreFile.Models;
using CoreFile.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CoreFile.Views.UserControls;

public partial class FileExplorerControl : UserControl
{
    private Point _startPoint;

    public FileExplorerControl()
    {
        InitializeComponent();
    }

    private void FilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is ListView lv)
        {
            vm.SelectedItems = lv.SelectedItems.Cast<FileItem>().ToList();
        }
    }

    private async void FilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not ListViewItem { Content: FileItem item })
            return;

        e.Handled = true;
        await vm.OpenItemCommand.ExecuteAsync(item);
    }

    private void FileItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
    }

    private void FileItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && DataContext is MainViewModel vm)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _startPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var targets = vm.SelectedItems.Count > 0
                    ? vm.SelectedItems.Select(x => x.FullPath).ToArray()
                    : (vm.SelectedItem != null ? new[] { vm.SelectedItem.FullPath } : Array.Empty<string>());

                if (targets.Length > 0)
                {
                    var data = new DataObject(DataFormats.FileDrop, targets);
                    DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy | DragDropEffects.Move);
                }
            }
        }
    }

    private void FileItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem item)
        {
            item.IsSelected = true;
        }
    }

    private void FileListView_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void FileListView_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) || DataContext is not MainViewModel vm)
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        string targetDirectory = vm.CurrentTab?.CurrentPath ?? string.Empty;

        if (e.OriginalSource is DependencyObject depObj)
        {
            var itemContainer = ItemsControl.ContainerFromElement(ItemsListView, depObj) as ListViewItem;
            if (itemContainer?.Content is FileItem targetItem && targetItem.IsDirectory)
            {
                targetDirectory = targetItem.FullPath;
            }
        }

        if (string.IsNullOrWhiteSpace(targetDirectory)) return;

        e.Handled = true;
        await vm.HandleDropFilesCommand.ExecuteAsync(Tuple.Create(files, targetDirectory));
    }
}
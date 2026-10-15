using CoreFile.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CoreFile.Views.UserControls;

public partial class SidebarControl : UserControl
{
    public SidebarControl()
    {
        InitializeComponent();
    }

    private void DriveButton_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void DriveButton_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (sender is FrameworkElement { DataContext: Models.FileItem item } && item.IsDirectory)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                e.Handled = true;
                await vm.HandleDropFilesCommand.ExecuteAsync(Tuple.Create(files, item.FullPath));
            }
        }
    }

    private async void RecycleBin_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;

        e.Handled = true;
        await Task.Run(() =>
        {
            foreach (var f in files)
                Services.RecycleBinService.SendToRecycleBin(f);
        });

        await vm.RefreshCommand.ExecuteAsync(null);
        vm.StatusText = $"{files.Length} item(s) moved to Recycle Bin.";
    }
}
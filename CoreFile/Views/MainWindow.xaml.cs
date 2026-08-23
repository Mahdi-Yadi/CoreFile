using CoreFile.Models;
using CoreFile.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace CoreFile.Views;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private async void FilesList_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.SelectedItem is null)
            return;

        await viewModel.OpenItemCommand.ExecuteAsync(viewModel.SelectedItem);
    }

    private void FilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is ListView listView)
        {
            // اصلاح تایپ به FileItem و ارسال لیست انتخاب‌شده‌ها
            vm.SelectedItems = listView.SelectedItems.Cast<FileItem>().ToList();
        }
    }
}
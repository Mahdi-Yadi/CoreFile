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

    // این هندلر حالا از طریق EventSetter روی هر ListViewItem وصل می‌شود (نه روی کل ListView)،
    // پس دابل‌کلیک روی سرستون‌ها یا فضای خالی دیگر آیتم انتخاب‌شده را باز نمی‌کند.
    private async void FilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (sender is not ListViewItem { Content: FileItem item })
            return;

        e.Handled = true;

        try
        {
            await viewModel.OpenItemCommand.ExecuteAsync(item);
        }
        catch (Exception ex)
        {
            // مثلاً UnauthorizedAccessException روی پوشه‌های سیستمی؛ بدون این try/catch برنامه کرش می‌کند.
            viewModel.StatusText = $"Can't open '{item.Name}': {ex.Message}";
        }
    }

    // راست‌کلیک روی یک آیتم، اگر انتخاب نشده باشد فقط همان را انتخاب می‌کند
    // تا Copy / Cut / Delete منوی راست‌کلیک روی آیتم درست عمل کنند.
    private void FileItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListViewItem { IsSelected: false } item)
            return;

        if (ItemsControl.ItemsControlFromItemContainer(item) is ListView listView)
        {
            listView.SelectedItems.Clear();
            item.IsSelected = true;
        }
    }

    private void FilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is ListView listView)
        {
            vm.SelectedItems = listView.SelectedItems.Cast<FileItem>().ToList();
        }
    }
}

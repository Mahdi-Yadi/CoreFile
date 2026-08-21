using CoreFile.Models;
using CoreFile.ViewModels;
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

        await viewModel.OpenItemCommand.ExecuteAsync(
            viewModel.SelectedItem);
    }
}
using CoreFile.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace CoreFile.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void LeftPane_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsDualPaneEnabled)
            vm.SetActivePaneCommand.Execute("Left");
    }

    private void RightPane_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsDualPaneEnabled)
            vm.SetActivePaneCommand.Execute("Right");
    }
}
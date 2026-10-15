using CoreFile.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CoreFile.Views.UserControls;

public partial class ToolbarControl : UserControl
{
    public ToolbarControl()
    {
        InitializeComponent();
    }

    private void AddressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.StartEditAddressBarCommand.Execute(null);
            AddressTextBox.Focus();
            AddressTextBox.SelectAll();
        }
    }

    private void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (e.Key == Key.Enter)
            vm.SubmitAddressBarCommand.Execute(null);
        else if (e.Key == Key.Escape)
            vm.CancelEditAddressBarCommand.Execute(null);
    }

    private void AddressTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.CancelEditAddressBarCommand.Execute(null);
    }

    private void CloseTerminalPopup_Click(object sender, RoutedEventArgs e) => TerminalMenuBtn.IsChecked = false;
    private void CloseGroupPopup_Click(object sender, RoutedEventArgs e) => GroupByBtn.IsChecked = false;
}
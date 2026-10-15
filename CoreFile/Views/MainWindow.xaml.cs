using CoreFile.Models;
using CoreFile.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CoreFile.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;

        // بازیابی وضعیت تم ذخیره‌شده هنگام اجرا
        LoadSavedTheme(viewModel);
    }

    private void LoadSavedTheme(MainViewModel viewModel)
    {
        // ۱. خواندن وضعیت ذخیره‌شده از Settings
        bool isDark = Properties.Settings.Default.IsDarkMode;

        // ۲. اعمال وضعیت به ViewModel و منابع WPF
        viewModel.IsDarkMode = isDark;
        ApplyTheme(viewModel, isDark);

        // ۳. ذخیره‌سازی و اعمال تغییرات هنگام تغییر IsDarkMode در برنامه
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsDarkMode))
            {
                bool currentDarkState = viewModel.IsDarkMode;
                Properties.Settings.Default.IsDarkMode = currentDarkState;
                Properties.Settings.Default.Save();

                ApplyTheme(viewModel, currentDarkState);
            }
        };
    }

    private void ApplyTheme(MainViewModel viewModel, bool isDark)
    {
        // فراخوانی متد تغییر رنگ در ViewModel جهت اعمال SolidColorBrushها در Resources
        viewModel.ToggleThemeCommand.Execute(isDark ? "Dark" : "Light");

        // در صورتی که فایل‌های XAML مستقل تم هم داشته باشید:
        string themeUri = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        try
        {
            var dict = new ResourceDictionary { Source = new Uri(themeUri, UriKind.RelativeOrAbsolute) };
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }
        catch
        {
            // در صورت عدم وجود فایل‌های خارجی، ToggleTheme رنگ‌ها را به صورت دستی مدیریت کرده است
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            if (MaximizeIcon != null) MaximizeIcon.Text = "\uE922";
        }
        else
        {
            WindowState = WindowState.Maximized;
            if (MaximizeIcon != null) MaximizeIcon.Text = "\uE923";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void FilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.SelectedItem is null)
            return;

        // اجرا و باز کردن پوشه یا فایل انتخابی
        await viewModel.OpenItemCommand.ExecuteAsync(viewModel.SelectedItem);
    }

    private void FilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not ListView listView)
            return;

        // به‌روزرسانی لیست عناصر انتخاب‌شده جهت کپی، برش و حذف چندتایی
        viewModel.SelectedItems = listView.SelectedItems.Cast<FileItem>().ToList();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
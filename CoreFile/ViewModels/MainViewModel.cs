using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoreFile.Models;
using CoreFile.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace CoreFile.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FileSystemService _fileSystemService;

    private readonly Stack<string> _backHistory = new();

    private readonly Stack<string> _forwardHistory = new();

    [ObservableProperty]
    private string currentPath = string.Empty;

    [ObservableProperty]
    private FileItem? selectedItem;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusText = "Ready";

    public ObservableCollection<FileItem> Drives { get; } = [];

    public ObservableCollection<FileItem> Items { get; } = [];


    public MainViewModel()
    {
        _fileSystemService = new FileSystemService();

        LoadDrives();
    }


    private void LoadDrives()
    {
        Drives.Clear();

        foreach (var drive in _fileSystemService.GetDrives())
        {
            Drives.Add(drive);
        }

        StatusText = $"{Drives.Count} Drives";
    }


    [RelayCommand]
    private async Task OpenDriveAsync(FileItem? drive)
    {
        if (drive is null)
            return;

        await NavigateToAsync(
            drive.FullPath,
            addToHistory: true);
    }


    [RelayCommand]
    private async Task OpenItemAsync(FileItem? item)
    {
        if (item is null)
            return;

        if (item.IsDirectory)
        {
            await NavigateToAsync(
                item.FullPath,
                addToHistory: true);

            return;
        }

        OpenFile(item.FullPath);
    }


    private async Task NavigateToAsync(
        string path,
        bool addToHistory)
    {
        if (!Directory.Exists(path))
            return;

        if (string.Equals(
                CurrentPath,
                path,
                StringComparison.OrdinalIgnoreCase))
        {
            await RefreshAsync();
            return;
        }

        if (addToHistory &&
            !string.IsNullOrWhiteSpace(CurrentPath))
        {
            _backHistory.Push(CurrentPath);
        }

        // وقتی مسیر جدیدی انتخاب می‌کنیم
        // Forward باید پاک شود.
        if (addToHistory)
        {
            _forwardHistory.Clear();
        }

        await LoadDirectoryAsync(path);
    }


    private async Task LoadDirectoryAsync(string path)
    {
        try
        {
            IsLoading = true;

            StatusText = "Loading...";

            var items = await Task.Run(() =>
                _fileSystemService
                    .GetItems(path)
                    .ToList());

            Items.Clear();

            foreach (var item in items)
            {
                Items.Add(item);
            }

            CurrentPath = path;

            StatusText = $"{Items.Count} Items";
        }
        finally
        {
            IsLoading = false;
        }
    }


    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (_backHistory.Count == 0)
            return;

        var previousPath = _backHistory.Pop();

        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            _forwardHistory.Push(CurrentPath);
        }

        await LoadDirectoryAsync(previousPath);
    }


    [RelayCommand]
    private async Task GoForwardAsync()
    {
        if (_forwardHistory.Count == 0)
            return;

        var nextPath = _forwardHistory.Pop();

        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            _backHistory.Push(CurrentPath);
        }

        await LoadDirectoryAsync(nextPath);
    }


    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
        {
            LoadDrives();
            return;
        }

        await LoadDirectoryAsync(CurrentPath);
    }


    private static void OpenFile(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch
        {
            // در مراحل بعدی NotificationService اضافه می‌کنیم.
        }
    }
}
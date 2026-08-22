using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoreFile.Models;
using CoreFile.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

    // پراپرتی جدید جهت ذخیره آیتم‌های انتخابی چندتایی
    [ObservableProperty]
    private List<FileItem> selectedItems = [];

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

    private FileSystemWatcher? _watcher;

    private void SetupWatcher(string path)
    {
        _watcher?.Dispose();

        if (!Directory.Exists(path)) return;

        _watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += (s, e) => System.Windows.Application.Current.Dispatcher.InvokeAsync(RefreshAsync);
        _watcher.Deleted += (s, e) => System.Windows.Application.Current.Dispatcher.InvokeAsync(RefreshAsync);
        _watcher.Renamed += (s, e) => System.Windows.Application.Current.Dispatcher.InvokeAsync(RefreshAsync);
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

        await NavigateToAsync(drive.FullPath, addToHistory: true);
    }

    [RelayCommand]
    private async Task OpenItemAsync(FileItem? item)
    {
        if (item is null)
            return;

        if (item.IsDirectory)
        {
            await NavigateToAsync(item.FullPath, addToHistory: true);
            return;
        }

        OpenFile(item.FullPath);
    }

    private async Task NavigateToAsync(string path, bool addToHistory)
    {
        if (!Directory.Exists(path))
            return;

        if (string.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase))
        {
            await RefreshAsync();
            return;
        }

        if (addToHistory && !string.IsNullOrWhiteSpace(CurrentPath))
        {
            _backHistory.Push(CurrentPath);
        }

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
            SetupWatcher(path);

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
            // NotificationService
        }
    }

    [ObservableProperty]
    private double operationProgress;

    [ObservableProperty]
    private bool isOperationRunning;

    private ClipboardState? _clipboard;
    private CancellationTokenSource? _cts;
    private readonly FileOperationService _fileOperationService = new();

    // پشتیبانی از کپی چندتایی
    [RelayCommand]
    private void Copy()
    {
        var targets = SelectedItems.Count > 0 ? SelectedItems : (SelectedItem != null ? [SelectedItem] : new List<FileItem>());
        if (targets.Count == 0) return;

        var paths = targets.Select(x => x.FullPath).ToArray();

        _clipboard = new ClipboardState(paths.ToList(), OperationType.Copy);

        var data = new DataObject(DataFormats.FileDrop, paths);
        Clipboard.SetDataObject(data, true);

        StatusText = $"Copied {paths.Length} item(s) to clipboard.";
    }

    // پشتیبانی از Cut چندتایی
    [RelayCommand]
    private void Cut()
    {
        var targets = SelectedItems.Count > 0 ? SelectedItems : (SelectedItem != null ? [SelectedItem] : new List<FileItem>());
        if (targets.Count == 0) return;

        var paths = targets.Select(x => x.FullPath).ToArray();

        _clipboard = new ClipboardState(paths.ToList(), OperationType.Cut);

        var data = new DataObject(DataFormats.FileDrop, paths);
        byte[] moveEffect = [2, 0, 0, 0]; // 2 = PreferredDropEffect: Move
        var memoryStream = new MemoryStream(moveEffect);
        data.SetData("Preferred DropEffect", memoryStream);

        Clipboard.SetDataObject(data, true);

        StatusText = $"Cut {paths.Length} item(s) to clipboard.";
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;

        List<string> sourcePaths = [];
        OperationType operation = OperationType.Copy;

        if (Clipboard.ContainsFileDropList())
        {
            var fileList = Clipboard.GetFileDropList();
            foreach (string? file in fileList)
            {
                if (!string.IsNullOrEmpty(file))
                    sourcePaths.Add(file);
            }

            if (Clipboard.GetData("Preferred DropEffect") is MemoryStream stream)
            {
                byte[] bytes = stream.ToArray();
                if (bytes.Length > 0 && bytes[0] == 2)
                {
                    operation = OperationType.Cut;
                }
            }
        }
        else if (_clipboard is not null)
        {
            sourcePaths = _clipboard.SourcePaths;
            operation = _clipboard.Type;
        }

        if (sourcePaths.Count == 0) return;

        _cts = new CancellationTokenSource();
        IsOperationRunning = true;

        try
        {
            var progress = new Progress<double>(p => OperationProgress = p);
            await _fileOperationService.CopyOrMoveAsync(sourcePaths, CurrentPath, operation, progress, _cts.Token);

            if (operation == OperationType.Cut)
            {
                _clipboard = null;
                Clipboard.Clear();
            }

            await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationRunning = false;
            OperationProgress = 0;
        }
    }

    // پشتیبانی از حذف چندتایی
    [RelayCommand]
    private async Task DeleteAsync()
    {
        var targets = SelectedItems.Count > 0 ? SelectedItems : (SelectedItem != null ? [SelectedItem] : new List<FileItem>());
        if (targets.Count == 0) return;

        int successCount = 0;
        await Task.Run(() =>
        {
            foreach (var item in targets.ToList())
            {
                if (RecycleBinService.SendToRecycleBin(item.FullPath))
                {
                    successCount++;
                }
            }
        });

        if (successCount > 0)
        {
            await RefreshAsync();
            StatusText = $"{successCount} item(s) moved to Recycle Bin.";
        }
        else
        {
            StatusText = "Error deleting selected item(s).";
        }
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;

        string newFolderPath = Path.Combine(CurrentPath, "New Folder");
        int count = 1;

        while (Directory.Exists(newFolderPath))
        {
            newFolderPath = Path.Combine(CurrentPath, $"New Folder ({count++})");
        }

        Directory.CreateDirectory(newFolderPath);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RenameAsync(Tuple<FileItem, string>? param)
    {
        if (param is null) return;
        var (item, newName) = param;

        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

        string? parent = Path.GetDirectoryName(item.FullPath);
        if (parent is null) return;

        string newPath = item.IsDirectory
            ? Path.Combine(parent, newName)
            : Path.Combine(parent, newName + item.Extension);

        try
        {
            if (item.IsDirectory)
                Directory.Move(item.FullPath, newPath);
            else
                File.Move(item.FullPath, newPath);

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Error renaming: {ex.Message}";
        }
    }

    [ObservableProperty]
    private ImageSource? previewImage;

    [ObservableProperty]
    private string? previewText;

    [ObservableProperty]
    private bool isPreviewVisible;

    [ObservableProperty]
    private bool isImagePreview;

    [ObservableProperty]
    private bool isTextPreview;

    [ObservableProperty]
    private bool isMediaPreview;

    partial void OnSelectedItemChanged(FileItem? value)
    {
        _ = LoadPreviewAsync(value);
    }

    private async Task LoadPreviewAsync(FileItem? item)
    {
        PreviewImage = null;
        PreviewText = null;
        IsImagePreview = false;
        IsTextPreview = false;
        IsMediaPreview = false;

        if (item is null || item.IsDirectory)
        {
            IsPreviewVisible = false;
            return;
        }

        IsPreviewVisible = true;
        string ext = item.Extension.ToLowerInvariant();

        string[] imageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];
        if (imageExtensions.Contains(ext))
        {
            IsImagePreview = true;
            PreviewImage = await Task.Run(() => LoadBitmapImage(item.FullPath));
            return;
        }

        string[] textExtensions = [".txt", ".json", ".xml", ".cs", ".log", ".md", ".css", ".js"];
        if (textExtensions.Contains(ext))
        {
            IsTextPreview = true;
            PreviewText = await Task.Run(() =>
            {
                try
                {
                    var fileInfo = new FileInfo(item.FullPath);
                    if (fileInfo.Length > 100 * 1024)
                    {
                        using var reader = new StreamReader(item.FullPath);
                        char[] buffer = new char[1500];
                        int read = reader.ReadBlock(buffer, 0, buffer.Length);
                        return new string(buffer, 0, read) + "\n\n... [ادامه متن]";
                    }
                    return File.ReadAllText(item.FullPath);
                }
                catch (Exception ex)
                {
                    return $"خطا در خواندن فایل:\n{ex.Message}";
                }
            });
            return;
        }

        string[] mediaExtensions = [".mp4", ".mkv", ".avi", ".mov", ".mp3", ".wav"];
        if (mediaExtensions.Contains(ext))
        {
            IsMediaPreview = true;
        }
    }

    private static BitmapImage? LoadBitmapImage(string filePath)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 800;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    [ObservableProperty]
    private bool isDarkMode;

    [RelayCommand]
    private void ToggleTheme(string theme)
    {
        IsDarkMode = theme == "Dark";

        var resources = System.Windows.Application.Current.Resources;

        if (IsDarkMode)
        {
            resources["BgMain"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
            resources["BgPanel"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526"));
            resources["BgSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30"));
            resources["BgPreview"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
            resources["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"));
            resources["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F1F1"));
            resources["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"));
        }
        else
        {
            resources["BgMain"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA"));
            resources["BgPanel"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            resources["BgSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F3"));
            resources["BgPreview"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9F9F9"));
            resources["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5"));
            resources["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222222"));
            resources["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
        }
    }

    [RelayCommand]
    private void CancelOperation()
    {
        _cts?.Cancel();
        StatusText = "Cancelling operation...";
    }


    // اضافه کردن این فیلدها جهت مدیریت مرتب‌سازی
    private string _currentSortColumn = string.Empty;
    private bool _isSortAscending = true;

    [RelayCommand]
    private void Sort(string columnName)
    {
        if (Items.Count == 0) return;

        if (_currentSortColumn == columnName)
        {
            _isSortAscending = !_isSortAscending;
        }
        else
        {
            _currentSortColumn = columnName;
            _isSortAscending = true;
        }

        Func<FileItem, object?> keySelector = columnName switch
        {
            "Name" => x => x.Name,
            "Type" => x => x.Type,
            "Size" => x => x.IsDirectory ? -1 : x.SizeBytes, // پوشه‌ها همیشه بالا یا ثابت بمانند
            "CreatedDate" => x => x.CreatedDate,
            "ModifiedDate" => x => x.ModifiedDate,
            _ => x => x.Name
        };

        var sorted = _isSortAscending
            ? Items.OrderBy(keySelector).ToList()
            : Items.OrderByDescending(keySelector).ToList();

        Items.Clear();
        foreach (var item in sorted)
        {
            Items.Add(item);
        }
    }

}
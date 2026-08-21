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

        // به دلیل اجرا روی Background Thread باید به Main UI Thread منتقل شود
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

    [ObservableProperty]
    private double operationProgress;

    [ObservableProperty]
    private bool isOperationRunning;

    private ClipboardState? _clipboard;
    private CancellationTokenSource? _cts;
    private readonly FileOperationService _fileOperationService = new();

    [RelayCommand]
    private void Copy(FileItem? item)
    {
        if (item is null) return;

        // ۱. ذخیره در حافظه داخلی برنامه
        _clipboard = new ClipboardState([item.FullPath], OperationType.Copy);

        // ۲. ارسال به کلیپ‌بورد ویندوز برای Paste شدن روی Desktop و سایر برنامه‌ها
        var data = new DataObject(DataFormats.FileDrop, new[] { item.FullPath });
        Clipboard.SetDataObject(data, true);

        StatusText = $"Copied '{item.Name}' to clipboard.";
    }

    [RelayCommand]
    private void Cut(FileItem? item)
    {
        if (item is null) return;

        // ۱. ذخیره در حافظه داخلی برنامه
        _clipboard = new ClipboardState([item.FullPath], OperationType.Cut);

        // ۲. ارسال به کلیپ‌بورد ویندوز با پرچم (Flag) مربوط به Cut/Move
        var data = new DataObject(DataFormats.FileDrop, new[] { item.FullPath });

        // مشخص کردن حالت Cut برای ویندوز
        byte[] moveEffect = [2, 0, 0, 0]; // 2 = PreferredDropEffect: Move
        var memoryStream = new MemoryStream(moveEffect);
        data.SetData("Preferred DropEffect", memoryStream);

        Clipboard.SetDataObject(data, true);

        StatusText = $"Cut '{item.Name}' to clipboard.";
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;

        List<string> sourcePaths = [];
        OperationType operation = OperationType.Copy;

        // ۱. ابتدا بررسی کلیپ‌بورد سیستم‌عامل (ویندوز)
        if (Clipboard.ContainsFileDropList())
        {
            var fileList = Clipboard.GetFileDropList();
            foreach (string? file in fileList)
            {
                if (!string.IsNullOrEmpty(file))
                    sourcePaths.Add(file);
            }

            // بررسی اینکه آیا عملیات Cut بوده یا Copy
            if (Clipboard.GetData("Preferred DropEffect") is MemoryStream stream)
            {
                byte[] bytes = stream.ToArray();
                if (bytes.Length > 0 && bytes[0] == 2) // 2 یعنی Move/Cut
                {
                    operation = OperationType.Cut;
                }
            }
        }
        // ۲. اگر کلیپ‌بورد ویندوز خالی بود، از حافظه داخلی استفاده کن
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
                Clipboard.Clear(); // پاکسازی کلیپ‌بورد بعد از Cut
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

    [RelayCommand]
    private async Task DeleteAsync(FileItem? item)
    {
        if (item is null) return;

        // استفاده از Recycle Bin به جای File.Delete
        bool success = await Task.Run(() => RecycleBinService.SendToRecycleBin(item.FullPath));

        if (success)
        {
            await RefreshAsync();
        }
        else
        {
            StatusText = $"خطا در انتقال {item.Name} به سطل زباله";
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
    private async Task RenameAsync(FileItem? item)
    {
        if (item is null) return;

        // در گام بعدی می‌توانید یک Dialog UI برای دریافت نام جدید طراحی کنید
        // نمونه منطق تغییر نام:
        string? parent = Path.GetDirectoryName(item.FullPath);
        if (parent is null) return;

        string newName = "NewName"; // دریافت نام از UI Dialog
        string newPath = Path.Combine(parent, newName + item.Extension);

        if (item.IsDirectory)
            Directory.Move(item.FullPath, newPath);
        else
            File.Move(item.FullPath, newPath);

        await RefreshAsync();
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

        // ۱. تصاویر
        string[] imageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];
        if (imageExtensions.Contains(ext))
        {
            IsImagePreview = true;
            PreviewImage = await Task.Run(() => LoadBitmapImage(item.FullPath));
            return;
        }

        // ۲. فایل‌های متنی
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

        // ۳. فایل‌های ویدئویی و صوتی (نمایش کارت مشخصات)
        string[] mediaExtensions = [".mp4", ".mkv", ".avi", ".mov", ".mp3", ".wav"];
        if (mediaExtensions.Contains(ext))
        {
            IsMediaPreview = true;
        }
    }

    // لود ایمن تصویر بدون قفل کردن فایل روی دیسک
    private static BitmapImage? LoadBitmapImage(string filePath)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // عدم Lock شدن فایل
            bitmap.DecodePixelWidth = 800; // کاهش سایز جهت بهینه‌سازی حافظه
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
}
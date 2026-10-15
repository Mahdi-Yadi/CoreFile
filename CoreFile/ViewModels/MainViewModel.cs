using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoreFile.Models;
using CoreFile.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CoreFile.ViewModels;

public enum ViewMode
{
    Details,
    List,
    SmallIcons,
    LargeIcons
}

public partial class MainViewModel : ObservableObject
{

    [ObservableProperty]
    private bool isArchivePreview;
    private CancellationTokenSource? _folderSizeCts;
    public ObservableCollection<ArchiveEntryModel> ArchiveEntries { get; } = [];
    public ObservableCollection<FileItem> QuickAccessItems { get; } = [];
    private readonly FileSystemService _fileSystemService;
    private readonly FileOperationService _fileOperationService = new();
    private readonly UndoRedoService _undoRedoService = new();

    private FileSystemWatcher? _watcher;
    private ClipboardState? _clipboard;

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _watcherDebounceCts;
    private CancellationTokenSource? _operationCts;

    [ObservableProperty]
    private bool isDualPaneEnabled;

    [ObservableProperty]
    private bool isRightPaneActive;

    [ObservableProperty]
    private TabItemModel? secondaryTab;

    // پنل فعال فعلی که فرامین نوار ابزار روی آن اعمال می‌شود
    public TabItemModel? ActiveTab => (IsDualPaneEnabled && IsRightPaneActive && SecondaryTab != null)
        ? SecondaryTab
        : CurrentTab;

    private string _currentSortColumn = string.Empty;
    private bool _isSortAscending = true;

    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = [];
    public ObservableCollection<TabItemModel> Tabs { get; } = [];
    public ObservableCollection<FileItem> Drives { get; } = [];

    [ObservableProperty]
    private bool isAddressBarEditing;

    [ObservableProperty]
    private string editablePath = string.Empty;

    [ObservableProperty]
    private TabItemModel? currentTab;

    [ObservableProperty]
    private ICollectionView? filteredItems;

    [ObservableProperty]
    private FileItem? selectedItem;

    [ObservableProperty]
    private List<FileItem> selectedItems = [];

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool isDeepSearchEnabled;

    [ObservableProperty]
    private bool isSearching;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private double operationProgress;

    [ObservableProperty]
    private bool isOperationRunning;

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

    [ObservableProperty]
    private bool isDarkMode;

    [ObservableProperty]
    private ViewMode currentViewMode = ViewMode.Details;

    [ObservableProperty]
    private ImageSource? recycleBinIcon;

    public MainViewModel()
    {
        _fileSystemService = new FileSystemService();

        var settings = AppSettingsService.Load();
        IsDarkMode = settings.IsDarkMode;
        SetTheme(IsDarkMode);

        LoadQuickAccess();
        LoadRecycleBinInfo();

        // بارگذاری همزمان درایوها در سایدبار و تب جاری
        _ = LoadDrivesAsync();
        _ = InitializeTabsAsync();
    }

    public async Task InitializeTabsAsync()
    {
        Tabs.Clear();
        var initialTab = new TabItemModel
        {
            Title = "This PC",
            CurrentPath = string.Empty,
            IsSelected = true
        };

        Tabs.Add(initialTab);
        SelectTab(initialTab);

        // بارگذاری خودکار درایوهای سیستم داخل نمای وسط صفحه
        await NavigateToAsync(string.Empty, addToHistory: false);
    }

    [RelayCommand]
    public void SelectTab(TabItemModel? tab)
    {
        if (tab == null) return;

        foreach (var t in Tabs)
        {
            t.IsSelected = (t == tab);
        }

        CurrentTab = tab;
    }

    [RelayCommand]
    private async Task AddNewTabAsync()
    {
        var newTab = new TabItemModel
        {
            Title = "This PC",
            CurrentPath = string.Empty,
            IsSelected = true
        };

        foreach (var tab in Tabs)
        {
            tab.IsSelected = false;
        }

        Tabs.Add(newTab);
        SelectTab(newTab);

        await LoadDrivesAsync();
    }

    [RelayCommand]
    private async Task CloseTabAsync(TabItemModel? tab)
    {
        if (tab == null || Tabs.Count <= 1) return;

        int index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        _folderSizeCts?.Cancel();
        if (CurrentTab == tab)
        {
            var nextActiveTab = Tabs[Math.Max(0, index - 1)];
            SelectTab(nextActiveTab);

            if (!string.IsNullOrEmpty(nextActiveTab.CurrentPath))
            {
                await LoadDirectoryForTabAsync(nextActiveTab, nextActiveTab.CurrentPath);
            }
            else
            {
                await NavigateToAsync(string.Empty, addToHistory: false);
            }
        }
    }

    partial void OnCurrentTabChanged(TabItemModel? value)
    {
        if (value == null) return;

        SetupWatcher(value.CurrentPath);

        FilteredItems = CollectionViewSource.GetDefaultView(value.Items);
        FilteredItems.Filter = FilterFileItem;

        if (!string.IsNullOrEmpty(CurrentGroupBy) && CurrentGroupBy != "None")
        {
            SetGroupBy(CurrentGroupBy);
        }

        SelectedItem = value.SelectedItem;
        StatusText = $"{value.Items.Count} Items";

        UpdateBreadcrumbs(value.CurrentPath);

        _ = LoadPreviewAsync(value.SelectedItem);
    }

    [RelayCommand]
    private async Task OpenDriveAsync(FileItem? drive)
    {
        if (drive is null) return;
        await NavigateToAsync(drive.FullPath, addToHistory: true);
    }

    [RelayCommand]
    private async Task OpenItemAsync(FileItem? item)
    {
        if (item is null) return;

        if (item.IsDirectory)
        {
            await NavigateToAsync(item.FullPath, addToHistory: true);
            return;
        }

        OpenFile(item.FullPath);
    }

    public async Task NavigateToAsync(string path, bool addToHistory)
    {
        if (CurrentTab == null) return;
        _folderSizeCts?.Cancel();
        // هندل کردن ناوبری به This PC (مسیر خالی)
        if (string.IsNullOrWhiteSpace(path))
        {
            if (addToHistory && !string.IsNullOrWhiteSpace(CurrentTab.CurrentPath))
            {
                CurrentTab.BackHistory.Push(CurrentTab.CurrentPath);
                CurrentTab.ForwardHistory.Clear();
            }

            CurrentTab.CurrentPath = string.Empty;
            CurrentTab.Title = "This PC";
            CurrentTab.Items.Clear();

            var drives = await Task.Run(() => _fileSystemService.GetDrives().ToList());
            foreach (var d in drives)
            {
                CurrentTab.Items.Add(d);
            }

            SetupWatcher(string.Empty);
            FilteredItems?.Refresh();
            StatusText = $"{drives.Count} Drives";
            UpdateBreadcrumbs(string.Empty);
            return;
        }

        if (!Directory.Exists(path)) return;

        if (addToHistory && !string.IsNullOrWhiteSpace(CurrentTab.CurrentPath))
        {
            CurrentTab.BackHistory.Push(CurrentTab.CurrentPath);
            CurrentTab.ForwardHistory.Clear();
        }

        await LoadDirectoryForTabAsync(CurrentTab, path);
    }

    private async Task LoadDirectoryForTabAsync(TabItemModel targetTab, string path)
    {
        try
        {
            targetTab.IsLoading = true;
            IsLoading = true;
            StatusText = "Loading...";

            var items = await Task.Run(() => _fileSystemService.GetItems(path).ToList());

            targetTab.Items.Clear();
            foreach (var item in items)
            {
                targetTab.Items.Add(item);
            }

            targetTab.CurrentPath = path;
            targetTab.UpdateTitle();

            if (CurrentTab == targetTab)
            {
                SetupWatcher(path);
                FilteredItems?.Refresh();
                StatusText = $"{targetTab.Items.Count} Items";
                UpdateBreadcrumbs(path);
            }

            _ = LoadThumbnailsBackgroundAsync(items);
            _ = CalculateFolderSizesAsync(items);
        }
        catch (UnauthorizedAccessException)
        {
            StatusText = "Access denied.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            targetTab.IsLoading = false;
            IsLoading = false;
        }
    }

    private async Task LoadThumbnailsBackgroundAsync(IEnumerable<FileItem> items)
    {
        await Task.Run(() =>
        {
            string[] previewableExts = [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif", ".ico", ".mp4", ".mkv", ".pdf"];

            Parallel.ForEach(items, new ParallelOptions { MaxDegreeOfParallelism = 4 }, item =>
            {
                if (item.IsDirectory) return;

                string ext = item.Extension.ToLowerInvariant();
                if (previewableExts.Contains(ext))
                {
                    var thumb = ThumbnailService.GetThumbnail(item.FullPath, 128);
                    if (thumb != null)
                    {
                        Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            item.Thumbnail = thumb;
                        });
                    }
                }
            });
        });
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (CurrentTab == null || CurrentTab.BackHistory.Count == 0) return;

        var prev = CurrentTab.BackHistory.Pop();
        if (!string.IsNullOrWhiteSpace(CurrentTab.CurrentPath))
        {
            CurrentTab.ForwardHistory.Push(CurrentTab.CurrentPath);
        }

        await NavigateToAsync(prev, addToHistory: false);
    }

    [RelayCommand]
    private async Task GoForwardAsync()
    {
        if (CurrentTab == null || CurrentTab.ForwardHistory.Count == 0) return;

        var next = CurrentTab.ForwardHistory.Pop();
        if (!string.IsNullOrWhiteSpace(CurrentTab.CurrentPath))
        {
            CurrentTab.BackHistory.Push(CurrentTab.CurrentPath);
        }

        await NavigateToAsync(next, addToHistory: false);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (CurrentTab == null) return;

        if (string.IsNullOrWhiteSpace(CurrentTab.CurrentPath))
        {
            await NavigateToAsync(string.Empty, addToHistory: false);
            return;
        }

        await LoadDirectoryForTabAsync(CurrentTab, CurrentTab.CurrentPath);
    }

    private async Task LoadDrivesAsync()
    {
        try
        {
            var drives = await Task.Run(() => _fileSystemService.GetDrives().ToList());
            Drives.Clear();
            foreach (var drive in drives)
            {
                Drives.Add(drive);
            }
            StatusText = $"{Drives.Count} Drives";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading drives: {ex.Message}";
        }
    }

    private static void OpenFile(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void SetupWatcher(string path)
    {
        try
        {
            _watcher?.Dispose();
            _watcher = null;

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

            _watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
                InternalBufferSize = 65536
            };

            _watcher.Created += OnFileSystemChanged;
            _watcher.Deleted += OnFileSystemChanged;
            _watcher.Renamed += OnFileSystemChanged;
        }
        catch { }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        _watcherDebounceCts?.Cancel();
        _watcherDebounceCts?.Dispose();
        _watcherDebounceCts = new CancellationTokenSource();
        var token = _watcherDebounceCts.Token;

        Task.Delay(350, token).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                Application.Current?.Dispatcher.InvokeAsync(RefreshAsync);
            }
        }, TaskScheduler.Default);
    }

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

    [RelayCommand]
    private void Cut()
    {
        var targets = SelectedItems.Count > 0 ? SelectedItems : (SelectedItem != null ? [SelectedItem] : new List<FileItem>());
        if (targets.Count == 0) return;

        var paths = targets.Select(x => x.FullPath).ToArray();
        _clipboard = new ClipboardState(paths.ToList(), OperationType.Cut);

        var data = new DataObject(DataFormats.FileDrop, paths);
        byte[] moveEffect = [2, 0, 0, 0];
        data.SetData("Preferred DropEffect", new MemoryStream(moveEffect));

        Clipboard.SetDataObject(data, true);
        StatusText = $"Cut {paths.Length} item(s) to clipboard.";
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        if (CurrentTab == null || string.IsNullOrEmpty(CurrentTab.CurrentPath)) return;

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

        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();

        IsOperationRunning = true;

        try
        {
            var progress = new Progress<double>(p => OperationProgress = p);
            await _fileOperationService.CopyOrMoveAsync(
                sourcePaths,
                CurrentTab.CurrentPath,
                operation,
                progress,
                _operationCts.Token,
                PromptConflictResolutionAsync);

            // ۱. ثبت در سیستم Undo
            var transferredItems = sourcePaths.Select(src => (
                Source: src,
                Destination: Path.Combine(CurrentTab.CurrentPath, Path.GetFileName(src))
            )).ToList();
            _undoRedoService.Record(new TransferAction(transferredItems, operation));

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

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var targets = SelectedItems.Count > 0 ? SelectedItems : (SelectedItem != null ? [SelectedItem] : new List<FileItem>());
        if (targets.Count == 0) return;

        var deletedPaths = new List<string>();

        await Task.Run(() =>
        {
            foreach (var item in targets.ToList())
            {
                if (RecycleBinService.SendToRecycleBin(item.FullPath))
                {
                    deletedPaths.Add(item.FullPath);
                }
            }
        });

        if (deletedPaths.Count > 0)
        {
            // ثبت در سیستم Undo برای بازگردانی با Ctrl + Z
            _undoRedoService.Record(new DeleteAction(deletedPaths));

            await RefreshAsync();
            StatusText = $"{deletedPaths.Count} item(s) moved to Recycle Bin.";
        }
        else
        {
            StatusText = "Error deleting selected item(s).";
        }
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        if (CurrentTab == null || string.IsNullOrEmpty(CurrentTab.CurrentPath)) return;

        string newFolderPath = Path.Combine(CurrentTab.CurrentPath, "New folder");
        int count = 1;

        while (Directory.Exists(newFolderPath))
        {
            newFolderPath = Path.Combine(CurrentTab.CurrentPath, $"New folder ({count++})");
        }

        try
        {
            Directory.CreateDirectory(newFolderPath);

            // ۲. ثبت ساخت پوشه در سیستم Undo
            _undoRedoService.Record(new CreateFolderAction(newFolderPath));

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Cannot create folder: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RenameAsync(Tuple<FileItem, string>? param)
    {
        if (param is null) return;
        var (item, newName) = param;

        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            StatusText = "File name contains invalid characters.";
            return;
        }

        string? parent = Path.GetDirectoryName(item.FullPath);
        if (parent is null) return;

        string newPath = item.IsDirectory
            ? Path.Combine(parent, newName)
            : Path.Combine(parent, newName.EndsWith(item.Extension, StringComparison.OrdinalIgnoreCase) ? newName : newName + item.Extension);

        try
        {
            if (item.IsDirectory)
                Directory.Move(item.FullPath, newPath);
            else
                File.Move(item.FullPath, newPath);

            // ۳. ثبت تغییر نام در سیستم Undo
            _undoRedoService.Record(new RenameAction(item.FullPath, newPath));

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Error renaming: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelOperation()
    {
        _operationCts?.Cancel();
        StatusText = "Cancelling operation...";
    }

    partial void OnSelectedItemChanged(FileItem? value)
    {
        if (CurrentTab != null)
        {
            CurrentTab.SelectedItem = value;
        }
        _ = LoadPreviewAsync(value);
    }

    private async Task LoadPreviewAsync(FileItem? item)
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;

        PreviewImage = null;
        PreviewText = null;
        IsImagePreview = false;
        IsTextPreview = false;
        IsMediaPreview = false;
        IsArchivePreview = false;
        ArchiveEntries.Clear();

        if (item is null || item.IsDirectory || !File.Exists(item.FullPath))
        {
            IsPreviewVisible = false;
            return;
        }

        IsPreviewVisible = true;
        string ext = item.Extension.ToLowerInvariant();

        // ۱. پیش‌نمایش فایل‌های فشرده (ZIP)
        if (ext == ".zip")
        {
            IsArchivePreview = true;
            var entries = await Task.Run(() =>
            {
                var list = new List<ArchiveEntryModel>();
                try
                {
                    using var archive = System.IO.Compression.ZipFile.OpenRead(item.FullPath);
                    foreach (var entry in archive.Entries.Take(100)) // جهت سرعت، تا ۱۰۰ آیتم اول
                    {
                        bool isDir = string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/');
                        list.Add(new ArchiveEntryModel
                        {
                            Name = isDir ? entry.FullName.TrimEnd('/') : entry.Name,
                            IsDirectory = isDir,
                            SizeText = isDir ? "<DIR>" : FormatSize(entry.Length)
                        });
                    }
                }
                catch (Exception ex)
                {
                    list.Add(new ArchiveEntryModel { Name = $"Error reading ZIP: {ex.Message}", SizeText = "" });
                }
                return list;
            }, token);

            if (!token.IsCancellationRequested)
            {
                foreach (var e in entries)
                {
                    ArchiveEntries.Add(e);
                }
            }
            return;
        }

        // ۲. پیش‌نمایش اسناد تصویری و PDF (نمایش صفحه اول PDF از کش سیستم)
        string[] imageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".ico", ".pdf"];
        if (imageExtensions.Contains(ext))
        {
            IsImagePreview = true;
            var bitmap = await Task.Run(() =>
            {
                if (ext == ".pdf")
                {
                    // استفاده از تامبنیل صفحه اول ویندوز برای PDF
                    return ThumbnailService.GetThumbnail(item.FullPath, 500);
                }
                return LoadBitmapImage(item.FullPath);
            }, token);

            if (!token.IsCancellationRequested)
            {
                PreviewImage = bitmap;
            }
            return;
        }

        // ۳. پیش‌نمایش اسناد متنی، اداری و کد
        string[] textExtensions = [
            ".txt", ".json", ".xml", ".cs", ".log", ".md", ".css", ".js", ".html",
        ".xaml", ".config", ".csv", ".tsv", ".ini", ".bat", ".ps1", ".sql",
        ".yaml", ".yml", ".env", ".gitignore", ".editorconfig"
        ];

        if (textExtensions.Contains(ext))
        {
            IsTextPreview = true;
            var text = await Task.Run(() =>
            {
                try
                {
                    var fileInfo = new FileInfo(item.FullPath);
                    if (fileInfo.Length > 200 * 1024)
                    {
                        using var fs = new FileStream(item.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(fs);
                        char[] buffer = new char[2500];
                        int read = reader.ReadBlock(buffer, 0, buffer.Length);
                        return new string(buffer, 0, read) + "\n\n... [ادامه متن به دلیل حجم بالا نمایش داده نشد]";
                    }
                    return File.ReadAllText(item.FullPath);
                }
                catch (Exception ex)
                {
                    return $"خطا در خواندن فایل:\n{ex.Message}";
                }
            }, token);

            if (!token.IsCancellationRequested)
            {
                PreviewText = text;
            }
            return;
        }

        // ۴. فایل‌های صوتی و ویدیویی
        string[] mediaExtensions = [".mp4", ".mkv", ".avi", ".mov", ".mp3", ".wav", ".flac"];
        if (mediaExtensions.Contains(ext))
        {
            IsMediaPreview = true;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024d * 1024):F1} MB";
        return $"{bytes / (1024d * 1024 * 1024):F1} GB";
    }

    private static BitmapImage? LoadBitmapImage(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
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

    [RelayCommand]
    private void Sort(string columnName)
    {
        if (CurrentTab == null || CurrentTab.Items.Count == 0) return;

        if (_currentSortColumn == columnName)
        {
            _isSortAscending = !_isSortAscending;
        }
        else
        {
            _currentSortColumn = columnName;
            _isSortAscending = true;
        }

        IEnumerable<FileItem> sorted = columnName switch
        {
            "Name" => _isSortAscending ? CurrentTab.Items.OrderBy(x => x.Name) : CurrentTab.Items.OrderByDescending(x => x.Name),
            "Type" => _isSortAscending ? CurrentTab.Items.OrderBy(x => x.Type) : CurrentTab.Items.OrderByDescending(x => x.Type),
            "Size" => _isSortAscending ? CurrentTab.Items.OrderBy(x => x.IsDirectory ? -1 : x.SizeBytes) : CurrentTab.Items.OrderByDescending(x => x.IsDirectory ? -1 : x.SizeBytes),
            "CreatedDate" => _isSortAscending ? CurrentTab.Items.OrderBy(x => x.CreatedDate) : CurrentTab.Items.OrderByDescending(x => x.CreatedDate),
            "ModifiedDate" => _isSortAscending ? CurrentTab.Items.OrderBy(x => x.ModifiedDate) : CurrentTab.Items.OrderByDescending(x => x.ModifiedDate),
            _ => CurrentTab.Items.OrderBy(x => x.Name)
        };

        var list = sorted.ToList();
        CurrentTab.Items.Clear();
        foreach (var item in list)
        {
            CurrentTab.Items.Add(item);
        }
    }

    private bool FilterFileItem(object obj)
    {
        if (obj is not FileItem item) return false;
        if (string.IsNullOrWhiteSpace(SearchQuery)) return true;

        return item.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
               item.Extension.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (IsDeepSearchEnabled)
        {
            _ = TriggerDeepSearchAsync(value);
        }
        else
        {
            FilteredItems?.Refresh();
            UpdateStatusWithFilterCount();
        }
    }

    partial void OnIsDeepSearchEnabledChanged(bool value)
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            OnSearchQueryChanged(SearchQuery);
        }
    }

    private async Task TriggerDeepSearchAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        if (string.IsNullOrWhiteSpace(query) || CurrentTab == null || string.IsNullOrEmpty(CurrentTab.CurrentPath))
        {
            await RefreshAsync();
            return;
        }

        try
        {
            await Task.Delay(300, token);

            IsSearching = true;
            StatusText = "Searching...";

            var results = await Task.Run(() => SearchDirectoryRecursive(CurrentTab.CurrentPath, query, token), token);

            if (!token.IsCancellationRequested)
            {
                CurrentTab.Items.Clear();
                foreach (var item in results)
                {
                    CurrentTab.Items.Add(item);
                }
                StatusText = $"Found {CurrentTab.Items.Count} items for '{query}'";
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsSearching = false;
            }
        }
    }

    private static List<FileItem> SearchDirectoryRecursive(string path, string query, CancellationToken token)
    {
        var matchedItems = new List<FileItem>();
        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };

            var dirInfo = new DirectoryInfo(path);
            foreach (var file in dirInfo.EnumerateFileSystemInfos($"*{query}*", options))
            {
                if (token.IsCancellationRequested) break;

                bool isDir = (file.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                matchedItems.Add(new FileItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = isDir,
                    Extension = isDir ? "" : file.Extension,
                    CreatedDate = file.CreationTime,
                    ModifiedDate = file.LastWriteTime,
                    SizeBytes = isDir ? 0 : ((FileInfo)file).Length
                });

                if (matchedItems.Count >= 1000) break;
            }
        }
        catch { }

        return matchedItems;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    private void UpdateStatusWithFilterCount()
    {
        if (FilteredItems == null || CurrentTab == null) return;
        int count = FilteredItems.Cast<object>().Count();
        StatusText = string.IsNullOrWhiteSpace(SearchQuery)
            ? $"{CurrentTab.Items.Count} Items"
            : $"{count} of {CurrentTab.Items.Count} Items (Filtered)";
    }

    [RelayCommand]
    private void ChangeViewMode(string mode)
    {
        if (Enum.TryParse<ViewMode>(mode, out var result))
        {
            CurrentViewMode = result;
        }
    }

    [RelayCommand]
    private void ToggleTheme(string theme)
    {
        IsDarkMode = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);
        SetTheme(IsDarkMode);

        AppSettingsService.Save(new AppSettings { IsDarkMode = IsDarkMode });
    }

    public void SetTheme(bool isDark)
    {
        try
        {
            var themeUri = isDark
                ? new Uri("Themes/ColorsDark.xaml", UriKind.Relative)
                : new Uri("Themes/ColorsLight.xaml", UriKind.Relative);

            var newThemeDict = new ResourceDictionary { Source = themeUri };
            var appResources = Application.Current.Resources;

            for (int i = 0; i < appResources.MergedDictionaries.Count; i++)
            {
                var dict = appResources.MergedDictionaries[i];
                if (dict.Source != null && dict.Source.OriginalString.Contains("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    appResources.MergedDictionaries[i] = newThemeDict;
                    return;
                }
            }

            appResources.MergedDictionaries.Add(newThemeDict);
        }
        catch (Exception ex)
        {
            StatusText = $"Theme error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task HandleDropFilesAsync(Tuple<string[], string>? dropData)
    {
        if (dropData == null || dropData.Item1.Length == 0) return;

        var (sourcePaths, targetDirectory) = dropData;

        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
            return;

        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();

        IsOperationRunning = true;
        StatusText = $"Copying {sourcePaths.Length} item(s)...";

        try
        {
            var progress = new Progress<double>(p => OperationProgress = p);
            await _fileOperationService.CopyOrMoveAsync(
                sourcePaths.ToList(),
                targetDirectory,
                OperationType.Copy,
                progress,
                _operationCts.Token,
                PromptConflictResolutionAsync);

            await RefreshAsync();
            StatusText = $"Successfully copied {sourcePaths.Length} item(s).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Drop error: {ex.Message}";
        }
        finally
        {
            IsOperationRunning = false;
            OperationProgress = 0;
        }
    }

    private void UpdateBreadcrumbs(string path)
    {
        Breadcrumbs.Clear();
        EditablePath = path;

        if (string.IsNullOrWhiteSpace(path))
        {
            Breadcrumbs.Add(new BreadcrumbItem { Name = "This PC", FullPath = string.Empty });
            return;
        }

        var directory = new DirectoryInfo(path);
        var stack = new Stack<BreadcrumbItem>();

        DirectoryInfo? current = directory;
        while (current != null)
        {
            string name = string.IsNullOrEmpty(current.Parent?.FullName)
                ? current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : current.Name;

            stack.Push(new BreadcrumbItem
            {
                Name = name,
                FullPath = current.FullName
            });

            current = current.Parent;
        }

        Breadcrumbs.Add(new BreadcrumbItem { Name = "This PC", FullPath = string.Empty });

        while (stack.Count > 0)
        {
            Breadcrumbs.Add(stack.Pop());
        }
    }

    [RelayCommand]
    private async Task NavigateToBreadcrumbAsync(BreadcrumbItem? item)
    {
        if (item == null) return;
        await NavigateToAsync(item.FullPath, addToHistory: true);
    }

    [RelayCommand]
    private void StartEditAddressBar()
    {
        EditablePath = CurrentTab?.CurrentPath ?? string.Empty;
        IsAddressBarEditing = true;
    }

    [RelayCommand]
    private async Task SubmitAddressBarAsync()
    {
        IsAddressBarEditing = false;

        if (CurrentTab == null) return;

        string target = EditablePath.Trim();

        if (string.IsNullOrWhiteSpace(target) || string.Equals(target, "This PC", StringComparison.OrdinalIgnoreCase))
        {
            await NavigateToAsync(string.Empty, addToHistory: true);
            return;
        }

        if (Directory.Exists(target))
        {
            await NavigateToAsync(target, addToHistory: true);
        }
        else
        {
            StatusText = $"Path does not exist: {target}";
            EditablePath = CurrentTab.CurrentPath;
        }
    }

    [RelayCommand]
    private void CancelEditAddressBar()
    {
        IsAddressBarEditing = false;
        EditablePath = CurrentTab?.CurrentPath ?? string.Empty;
    }

    private async Task<ConflictResolutionResult> PromptConflictResolutionAsync(ConflictFileInfo info)
    {
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new CoreFile.Views.FileConflictDialog(info)
            {
                Owner = Application.Current.MainWindow
            };
            dialog.ShowDialog();
            return dialog.Result;
        });
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (!_undoRedoService.CanUndo)
        {
            StatusText = "Nothing to undo.";
            return;
        }

        try
        {
            IsLoading = true;
            var desc = await _undoRedoService.UndoAsync();
            await RefreshAsync();
            StatusText = $"Undid: {desc}";
        }
        catch (Exception ex)
        {
            StatusText = $"Undo error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RedoAsync()
    {
        if (!_undoRedoService.CanRedo)
        {
            StatusText = "Nothing to redo.";
            return;
        }

        try
        {
            IsLoading = true;
            var desc = await _undoRedoService.RedoAsync();
            await RefreshAsync();
            StatusText = $"Redid: {desc}";
        }
        catch (Exception ex)
        {
            StatusText = $"Redo error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadQuickAccess()
    {
        QuickAccessItems.Clear();

        // ۱. پوشه‌های پیش‌فرض استاندارد کاربر
        var specialFolders = new (Environment.SpecialFolder Folder, string IconGlyph)[]
        {
        (Environment.SpecialFolder.Desktop, "\uE8B7"),
        (Environment.SpecialFolder.MyDocuments, "\uE8B7"),
        (Environment.SpecialFolder.UserProfile, "\uE8B7"),
        };

        // اضافه کردن مسیر Downloads
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string downloadsPath = Path.Combine(userProfile, "Downloads");

        var defaultPaths = new List<string>
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        downloadsPath,
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
    };

        // ۲. بارگذاری پوشه‌های پین‌شده از تنظیمات
        var settings = AppSettingsService.Load();
        foreach (var pinned in settings.PinnedFolders)
        {
            if (!defaultPaths.Contains(pinned, StringComparer.OrdinalIgnoreCase) && Directory.Exists(pinned))
            {
                defaultPaths.Add(pinned);
            }
        }

        foreach (var path in defaultPaths)
        {
            if (!Directory.Exists(path)) continue;

            QuickAccessItems.Add(new FileItem
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                Type = "QuickAccess",
                IsDirectory = true,
                Icon = IconService.GetIcon(path, isDirectory: true)
            });
        }
    }

    [RelayCommand]
    public void PinToQuickAccess(FileItem? item)
    {
        if (item == null || !item.IsDirectory) return;

        var settings = AppSettingsService.Load();
        if (!settings.PinnedFolders.Contains(item.FullPath, StringComparer.OrdinalIgnoreCase))
        {
            settings.PinnedFolders.Add(item.FullPath);
            AppSettingsService.Save(settings);
            LoadQuickAccess();
            StatusText = $"Pinned '{item.Name}' to Quick Access.";
        }
    }

    [RelayCommand]
    public void UnpinFromQuickAccess(FileItem? item)
    {
        if (item == null) return;

        var settings = AppSettingsService.Load();
        if (settings.PinnedFolders.RemoveAll(p => string.Equals(p, item.FullPath, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            AppSettingsService.Save(settings);
            LoadQuickAccess();
            StatusText = $"Unpinned '{item.Name}' from Quick Access.";
        }
    }

    [RelayCommand]
    public void OpenTerminal(string? terminalType = null)
    {
        // دریافت مسیر فعلی؛ اگر در ریشه بود یا فولدری انتخاب شده بود
        string targetPath = CurrentTab?.CurrentPath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
        {
            // در صورت حضور در This PC مسیر دسکتاپ یا پوشه پروفایل کاربر باز می‌شود
            targetPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = targetPath,
                UseShellExecute = true
            };

            switch (terminalType?.ToLowerInvariant())
            {
                case "cmd":
                    startInfo.FileName = "cmd.exe";
                    break;

                case "powershell":
                    startInfo.FileName = "powershell.exe";
                    startInfo.Arguments = "-NoExit";
                    break;

                case "wt": // Windows Terminal
                    startInfo.FileName = "wt.exe";
                    startInfo.Arguments = $"-d \"{targetPath}\"";
                    break;

                default:
                    // در حالت پیش‌فرض: اولویت با Windows Terminal، در غیر این صورت PowerShell
                    if (IsWindowsTerminalAvailable())
                    {
                        startInfo.FileName = "wt.exe";
                        startInfo.Arguments = $"-d \"{targetPath}\"";
                    }
                    else
                    {
                        startInfo.FileName = "powershell.exe";
                        startInfo.Arguments = "-NoExit";
                    }
                    break;
            }

            Process.Start(startInfo);
            StatusText = $"Terminal opened in '{Path.GetFileName(targetPath)}'";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open terminal: {ex.Message}";
        }
    }

    private static bool IsWindowsTerminalAvailable()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var wtPath = Path.Combine(localAppData, @"Microsoft\WindowsApps\wt.exe");
            return File.Exists(wtPath);
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    public async Task ToggleDualPaneAsync()
    {
        IsDualPaneEnabled = !IsDualPaneEnabled;

        if (IsDualPaneEnabled)
        {
            // در صورت باز شدن برای اولین بار، مسیر تب اول یا This PC درون پنل دوم بارگذاری شود
            if (SecondaryTab == null)
            {
                SecondaryTab = new TabItemModel
                {
                    Title = "This PC",
                    CurrentPath = string.Empty,
                    IsSelected = true
                };

                await LoadDrivesForSecondaryPaneAsync();
            }

            StatusText = "Dual Pane View enabled.";
        }
        else
        {
            IsRightPaneActive = false;
            StatusText = "Dual Pane View disabled.";
        }

        OnPropertyChanged(nameof(ActiveTab));
    }

    [RelayCommand]
    public void SetActivePane(string pane)
    {
        IsRightPaneActive = string.Equals(pane, "Right", StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(ActiveTab));

        var active = ActiveTab;
        if (active != null)
        {
            StatusText = $"{active.Items.Count} Items ({pane} Pane)";
            UpdateBreadcrumbs(active.CurrentPath);
            _ = LoadPreviewAsync(active.SelectedItem);
        }
    }

    private async Task LoadDrivesForSecondaryPaneAsync()
    {
        if (SecondaryTab == null) return;
        try
        {
            var drives = await Task.Run(() => _fileSystemService.GetDrives().ToList());
            SecondaryTab.Items.Clear();
            foreach (var d in drives)
            {
                SecondaryTab.Items.Add(d);
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task OpenSecondaryItemAsync(FileItem? item)
    {
        if (item == null || SecondaryTab == null) return;

        if (item.IsDirectory)
        {
            await LoadDirectoryForTabAsync(SecondaryTab, item.FullPath);
            return;
        }

        OpenFile(item.FullPath);
    }

    [RelayCommand]
    public async Task OpenBatchRenameAsync()
    {
        var targets = SelectedItems.Count > 1
            ? SelectedItems
            : (SelectedItem != null ? [SelectedItem] : new List<FileItem>());

        if (targets.Count == 0)
        {
            StatusText = "Select at least one file or folder to rename.";
            return;
        }

        var dialog = new CoreFile.Views.BatchRenameDialog(targets.ToList())
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.RenamedPairs.Count > 0)
        {
            int success = 0;
            var executed = new List<(string OldPath, string NewPath)>();

            await Task.Run(() =>
            {
                foreach (var (oldPath, newPath) in dialog.RenamedPairs)
                {
                    try
                    {
                        if (File.Exists(oldPath))
                        {
                            File.Move(oldPath, newPath);
                            executed.Add((oldPath, newPath));
                            success++;
                        }
                        else if (Directory.Exists(oldPath))
                        {
                            Directory.Move(oldPath, newPath);
                            executed.Add((oldPath, newPath));
                            success++;
                        }
                    }
                    catch { }
                }
            });

            if (executed.Count > 0)
            {
                // ثبت در سیستم Undo
                _undoRedoService.Record(new BatchRenameAction(executed));
                await RefreshAsync();
                StatusText = $"Batch renamed {success} item(s).";
            }
        }
    }

    // بارگذاری آیکون واقعی سطل زباله ویندوز
    private void LoadRecycleBinInfo()
    {
        try
        {
            // مسیر GUID اختصاصی سطل بازیافت در ویندوز
            string recycleBinPath = @"shell:RecycleBinFolder";
            RecycleBinIcon = IconService.GetIcon(recycleBinPath, isDirectory: true);
        }
        catch { }
    }

    [RelayCommand]
    public void OpenRecycleBin()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "shell:RecycleBinFolder",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Cannot open Recycle Bin: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CompressSelectedAsync()
    {
        var currentTab = ActiveTab;
        if (currentTab == null || string.IsNullOrWhiteSpace(currentTab.CurrentPath)) return;

        var targets = SelectedItems.Count > 0
            ? SelectedItems.Select(x => x.FullPath).ToList()
            : (SelectedItem != null ? [SelectedItem.FullPath] : new List<string>());

        if (targets.Count == 0)
        {
            StatusText = "Select items to compress.";
            return;
        }

        IsLoading = true;
        StatusText = "Compressing items...";

        try
        {
            string createdZip = await ZipArchiveService.CompressAsync(targets, currentTab.CurrentPath);
            if (!string.IsNullOrEmpty(createdZip))
            {
                // ثبت در سیستم Undo
                _undoRedoService.Record(new CreateFolderAction(createdZip)); // از منطق حذف بازیافت برای پاک کردن فایل جدید در Undo استفاده می‌شود
                await RefreshAsync();
                StatusText = $"Created archive: {Path.GetFileName(createdZip)}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Compression failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ExtractSelectedAsync()
    {
        var currentTab = ActiveTab;
        if (currentTab == null || string.IsNullOrWhiteSpace(currentTab.CurrentPath)) return;

        var target = SelectedItem;
        if (target == null || target.IsDirectory || !target.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "Select a .zip file to extract.";
            return;
        }

        IsLoading = true;
        StatusText = "Extracting archive...";

        try
        {
            string extractedPath = await ZipArchiveService.ExtractAsync(target.FullPath, currentTab.CurrentPath);
            if (!string.IsNullOrEmpty(extractedPath))
            {
                _undoRedoService.Record(new CreateFolderAction(extractedPath));
                await RefreshAsync();
                StatusText = $"Extracted to: {Path.GetFileName(extractedPath)}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Extraction failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [ObservableProperty]
    private string currentGroupBy = "None";

    [RelayCommand]
    public void SetGroupBy(string groupBy)
    {
        CurrentGroupBy = groupBy;

        if (FilteredItems == null) return;

        // پاک کردن گروه‌بندی قبلی
        FilteredItems.GroupDescriptions.Clear();

        switch (groupBy)
        {
            case "Type":
                FilteredItems.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FileItem.GroupByType)));
                break;
            case "Date":
                FilteredItems.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FileItem.GroupByDate)));
                break;
            case "Size":
                FilteredItems.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FileItem.GroupBySize)));
                break;
            case "Name":
                FilteredItems.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FileItem.GroupByName)));
                break;
            default: // None
                break;
        }

        StatusText = groupBy == "None" ? "Grouping cleared." : $"Grouped by {groupBy}.";
    }

    private async Task CalculateFolderSizesAsync(IEnumerable<FileItem> items)
    {
        _folderSizeCts?.Cancel();
        _folderSizeCts?.Dispose();
        _folderSizeCts = new CancellationTokenSource();
        var token = _folderSizeCts.Token;

        var folders = items.Where(x => x.IsDirectory && !x.IsDrive).ToList();
        if (folders.Count == 0) return;

        // پیمایش ترتیبی پوشه‌ها برای جلوگیری از مصرف بی‌رویه دیسک
        await Task.Run(async () =>
        {
            foreach (var folder in folders)
            {
                if (token.IsCancellationRequested) break;

                try
                {
                    var (bytes, count) = await _fileSystemService.CalculateDirectorySizeAsync(folder.FullPath, token);

                    if (!token.IsCancellationRequested)
                    {
                        Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            folder.SizeBytes = bytes;
                            folder.SizeText = bytes == 0 ? "Empty" : $"{FormatSize(bytes)} ({count} files)";
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch { }
            }
        }, token);
    }

}
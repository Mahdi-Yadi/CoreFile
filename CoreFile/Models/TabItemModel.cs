using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;

namespace CoreFile.Models;
public partial class TabItemModel : ObservableObject
{
    [ObservableProperty]
    private string title = "This PC";

    [ObservableProperty]
    private string currentPath = string.Empty;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private FileItem? selectedItem;

    [ObservableProperty]
    private bool isLoading;

    // لیست فایل‌های مختص این تب
    public ObservableCollection<FileItem> Items { get; } = [];

    // تاریخچه‌های ناوبری مستقل برای این تب
    public Stack<string> BackHistory { get; } = new();
    public Stack<string> ForwardHistory { get; } = new();

    public void UpdateTitle()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
        {
            Title = "This PC";
            return;
        }

        var dirName = Path.GetFileName(CurrentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        Title = string.IsNullOrEmpty(dirName) ? CurrentPath : dirName;
    }
}
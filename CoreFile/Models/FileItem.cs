using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
namespace CoreFile.Models;
public partial class FileItem : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string fullPath = string.Empty;

    [ObservableProperty]
    private string extension = string.Empty;

    [ObservableProperty]
    private string type = string.Empty;

    [ObservableProperty]
    private string sizeText = string.Empty;

    [ObservableProperty]
    private long sizeBytes;

    [ObservableProperty]
    private DateTime createdDate;

    [ObservableProperty]
    private DateTime modifiedDate;

    [ObservableProperty]
    private bool isDirectory;

    [ObservableProperty]
    private ImageSource? icon;

    [ObservableProperty]
    private ImageSource? thumbnail;

    [ObservableProperty]
    private bool isLoadingThumbnail;

    [ObservableProperty]
    private bool isDrive;

    [ObservableProperty]
    private double driveUsedPercentage;

    [ObservableProperty]
    private string driveUsageText = string.Empty;

    [ObservableProperty]
    private bool isDriveNearFull; // وقتی بیش از ۹۰٪ پر است

    // اضافه کردن این بخش به انتهای کلاس FileItem
    public string GroupByType => IsDirectory ? "Folders (پوشه‌ها)" : $"{Type} Files";

    public string GroupByDate
    {
        get
        {
            var diff = DateTime.Now.Date - ModifiedDate.Date;
            if (diff.TotalDays == 0) return "Today (امروز)";
            if (diff.TotalDays == 1) return "Yesterday (دیروز)";
            if (diff.TotalDays <= 7) return "Earlier this week (این هفته)";
            if (diff.TotalDays <= 30) return "Earlier this month (این ماه)";
            if (ModifiedDate.Year == DateTime.Now.Year) return "Earlier this year (امسال)";
            return "A long time ago (قدیمی‌تر)";
        }
    }

    public string GroupBySize
    {
        get
        {
            if (IsDirectory) return "Folders (پوشه‌ها)";
            if (SizeBytes < 16 * 1024) return "Tiny (کمتر از ۱۶ کیلوبایت)";
            if (SizeBytes < 1024 * 1024) return "Small (۱۶ کیلوبایت تا ۱ مگابایت)";
            if (SizeBytes < 128 * 1024 * 1024) return "Medium (۱ تا ۱۲۸ مگابایت)";
            if (SizeBytes < 1024L * 1024 * 1024) return "Large (۱۲۸ مگابایت تا ۱ گیگابایت)";
            return "Huge (بزرگ‌تر از ۱ گیگابایت)";
        }
    }

    public string GroupByName
    {
        get
        {
            if (string.IsNullOrEmpty(Name)) return "#";
            char first = char.ToUpperInvariant(Name[0]);
            if (first >= 'A' && first <= 'H') return "A - H";
            if (first >= 'I' && first <= 'P') return "I - P";
            if (first >= 'Q' && first <= 'Z') return "Q - Z";
            return "# (Numbers & Others)";
        }
    }

}